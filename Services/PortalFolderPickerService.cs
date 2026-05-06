// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

public class PortalFolderPickerService
{
    public async Task<FolderPickerResult> PickAsync(FolderPickerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new FolderPickerOptions();

        string? result = null;

        if (IsCommandAvailable("zenity"))
        {
            var args = $"--file-selection --directory --title=\"{options.Title ?? "Select Folder"}\"";
            result = await Task.Run(() => RunCommand("zenity", args)?.Trim(), cancellationToken);
        }
        else if (IsCommandAvailable("kdialog"))
        {
            var args = $"--getexistingdirectory . --title \"{options.Title ?? "Select Folder"}\"";
            result = await Task.Run(() => RunCommand("kdialog", args)?.Trim(), cancellationToken);
        }

        if (!string.IsNullOrEmpty(result) && Directory.Exists(result))
        {
            return new FolderPickerResult(new FolderResult(result));
        }

        return new FolderPickerResult(null);
    }

    public async Task<FolderPickerResult> PickAsync(CancellationToken cancellationToken = default)
    {
        return await PickAsync(null, cancellationToken);
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(RunCommand("which", command));
        }
        catch
        {
            return false;
        }
    }

    private string? RunCommand(string command, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var result = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            return result;
        }
        catch
        {
            return null;
        }
    }
}
