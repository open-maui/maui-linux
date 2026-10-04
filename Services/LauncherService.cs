// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux launcher service for opening URLs and files. Inside a sandbox (or
/// with OPENMAUI_PORTALS=prefer) it goes through the xdg-desktop-portal
/// OpenURI interface (files by descriptor via OpenFile); otherwise, and when
/// the portal is unavailable, through xdg-open.
/// </summary>
public class LauncherService : ILauncher
{
    private readonly IDesktopPortal _portal;

    public LauncherService()
        : this(DesktopPortal.Current)
    {
    }

    internal LauncherService(IDesktopPortal portal)
    {
        _portal = portal;
    }

    /// <summary>
    /// True when the desktop has an application for the URI: a <c>file:</c> URI whose file or
    /// folder exists, or a scheme with a registered handler (x-scheme-handler/&lt;scheme&gt; in the
    /// XDG MIME associations xdg-open uses; inside a sandbox the portal's chooser handles any
    /// scheme). An unknown scheme is false, as on the other platforms.
    /// </summary>
    public Task<bool> CanOpenAsync(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        return Task.FromResult(CanOpen(uri));
    }

    internal static bool CanOpen(Uri uri)
    {
        if (!uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.Scheme))
            return false;
        if (uri.IsFile)
            return File.Exists(uri.LocalPath) || Directory.Exists(uri.LocalPath);
        return SchemeHandlers.HasHandler(uri.Scheme);
    }

    public Task<bool> OpenAsync(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        // AbsoluteUri keeps the escaped form (ToString() would unescape spaces
        // and break the argument xdg-open receives).
        var target = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
        if (DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred) && uri.IsAbsoluteUri)
            return OpenWithPortalAsync(launcher => launcher.OpenUriAsync(target), target);
        return Task.Run(() => ExternalProcess.TryStart(BuildStartInfo(target)));
    }

    public Task<bool> OpenAsync(OpenFileRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (request.File == null)
            throw new ArgumentNullException(nameof(request.File));

        var filePath = request.File.FullPath;
        if (DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred))
            return OpenWithPortalAsync(launcher => launcher.OpenFileAsync(filePath), filePath);
        return Task.Run(() => ExternalProcess.TryStart(BuildStartInfo(filePath)));
    }

    /// <summary>Portal first; xdg-open when the portal is unavailable or fails. A user cancel is final.</summary>
    private async Task<bool> OpenWithPortalAsync(Func<PortalLauncher, Task<PortalOutcome>> portalCall, string xdgOpenTarget)
    {
        var outcome = await portalCall(new PortalLauncher(_portal)).ConfigureAwait(false);
        return ResolveLaunch(outcome, () => ExternalProcess.TryStart(BuildStartInfo(xdgOpenTarget)));
    }

    /// <summary>Completed is true, Cancelled false, otherwise the fallback decides.</summary>
    internal static bool ResolveLaunch(PortalOutcome outcome, Func<bool> fallback) => outcome switch
    {
        PortalOutcome.Completed => true,
        PortalOutcome.Cancelled => false,
        _ => fallback(),
    };

    /// <summary>Opens the URI when <see cref="CanOpenAsync"/> says it can; false otherwise (nothing is launched).</summary>
    public Task<bool> TryOpenAsync(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        return CanOpen(uri) ? OpenAsync(uri) : Task.FromResult(false);
    }

    /// <summary>
    /// xdg-open invocation for a URI or file path. The target is passed as a
    /// single argument (ArgumentList), so spaces and quotes in file names are
    /// forwarded verbatim rather than shell-quoted.
    /// </summary>
    internal static ProcessStartInfo BuildStartInfo(string target)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(target);
        return psi;
    }
}
