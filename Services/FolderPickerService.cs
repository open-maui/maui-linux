// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux folder picker utility using zenity or kdialog.
/// This is a standalone service as MAUI core does not define IFolderPicker.
/// </summary>
public class FolderPickerService
{
    public async Task<string?> PickFolderAsync(string? initialDirectory = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try zenity first (GNOME)
            var result = await TryZenityFolderPicker(initialDirectory, cancellationToken);
            if (result != null)
            {
                return result;
            }

            // Fall back to kdialog (KDE)
            result = await TryKdialogFolderPicker(initialDirectory, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryZenityFolderPicker(string? initialDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var args = "--file-selection --directory";
            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                args += $" --filename=\"{initialDirectory}/\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var path = output.Trim();
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryKdialogFolderPicker(string? initialDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var args = "--getexistingdirectory";
            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                args += $" \"{initialDirectory}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "kdialog",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var path = output.Trim();
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
