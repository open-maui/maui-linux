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
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        // AbsoluteUri keeps the escaped form (ToString() would unescape spaces
        // and break the argument xdg-open receives).
        var target = uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;
        return Task.Run(() => ExternalProcess.TryStart(BuildStartInfo(target)));
    }

    public Task<bool> OpenAsync(OpenFileRequest request)
    {
        if (request?.File == null)
            return Task.FromResult(false);

        var filePath = request.File.FullPath;
        return Task.Run(() => ExternalProcess.TryStart(BuildStartInfo(filePath)));
    }

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
