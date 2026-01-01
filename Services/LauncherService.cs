// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux launcher service for opening URLs and files.
/// </summary>
public class LauncherService : ILauncher
{
    public Task<bool> CanOpenAsync(Uri uri)
    {
        // On Linux, we can generally open any URI using xdg-open
        return Task.FromResult(true);
    }

    public Task<bool> OpenAsync(Uri uri)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = uri.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                // Don't wait for the process to exit - xdg-open may spawn another process
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public Task<bool> OpenAsync(OpenFileRequest request)
    {
        if (request.File == null)
            return Task.FromResult(false);

        return Task.Run(() =>
        {
            try
            {
                var filePath = request.File.FullPath;

                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                return process != null;
            }
            catch
            {
                return false;
            }
        });
    }

    public Task<bool> TryOpenAsync(Uri uri)
    {
        return OpenAsync(uri);
    }
}
