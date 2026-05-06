// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux browser implementation using xdg-open.
/// </summary>
public class BrowserService : IBrowser
{
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

        try
        {
            var uriString = uri.AbsoluteUri;

            // Use xdg-open which respects user's default browser
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{uriString}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
