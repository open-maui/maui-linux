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

    public Task<bool> CanOpenAsync(Uri uri)
    {
        // On Linux, we can generally open any URI using xdg-open
        return Task.FromResult(true);
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
        if (request?.File == null)
            return Task.FromResult(false);

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

    public Task<bool> TryOpenAsync(Uri uri)
    {
        return OpenAsync(uri);
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
