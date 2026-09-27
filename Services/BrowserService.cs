// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux browser implementation: xdg-open, or the xdg-desktop-portal OpenURI
/// interface inside a sandbox (or with OPENMAUI_PORTALS=prefer), falling back
/// to xdg-open when the portal is unavailable.
/// </summary>
public class BrowserService : IBrowser
{
    private readonly IDesktopPortal _portal;

    public BrowserService()
        : this(DesktopPortal.Current)
    {
    }

    internal BrowserService(IDesktopPortal portal)
    {
        _portal = portal;
    }

    public async Task<bool> OpenAsync(string uri)
    {
        return await OpenAsync(new Uri(uri), BrowserLaunchMode.SystemPreferred);
    }

    public async Task<bool> OpenAsync(string uri, BrowserLaunchMode launchMode)
    {
        return await OpenAsync(new Uri(uri), launchMode);
    }

    public async Task<bool> OpenAsync(Uri uri)
    {
        return await OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }

    public async Task<bool> OpenAsync(Uri uri, BrowserLaunchMode launchMode)
    {
        return await OpenAsync(uri, new BrowserLaunchOptions { LaunchMode = launchMode });
    }

    public async Task<bool> OpenAsync(Uri uri, BrowserLaunchOptions options)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        if (DesktopPortal.ShouldTry(PortalUse.SandboxedOrPreferred) && uri.IsAbsoluteUri)
        {
            var outcome = await new PortalLauncher(_portal).OpenUriAsync(uri.AbsoluteUri).ConfigureAwait(false);
            if (!outcome.ShouldFallBack())
                return outcome == PortalOutcome.Completed;
        }

        try
        {
            var exitCode = await ExternalProcess.RunAsync(BuildStartInfo(uri));
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// xdg-open invocation for the URI (absolute form, passed as one argument)
    /// so the user's default browser is respected.
    /// </summary>
    internal static ProcessStartInfo BuildStartInfo(Uri uri)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(uri.AbsoluteUri);
        return startInfo;
    }
}
