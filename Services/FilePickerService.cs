// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux file picker implementation using zenity or kdialog.
/// </summary>
public class FilePickerService : IFilePicker
{
    private enum DialogTool
    {
        None,
        Zenity,
        Kdialog
    }

    private static DialogTool? _availableTool;

    private static DialogTool GetAvailableTool()
    {
        if (_availableTool.HasValue)
            return _availableTool.Value;

        // Check for zenity first (GNOME/GTK)
        if (IsToolAvailable("zenity"))
        {
            _availableTool = DialogTool.Zenity;
            return DialogTool.Zenity;
        }

        // Check for kdialog (KDE)
        if (IsToolAvailable("kdialog"))
        {
            _availableTool = DialogTool.Kdialog;
            return DialogTool.Kdialog;
        }

        _availableTool = DialogTool.None;
        return DialogTool.None;
    }

    private static bool IsToolAvailable(string tool)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = tool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(1000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public Task<FileResult?> PickAsync(PickOptions? options = null)
    {
        return PickInternalAsync(options, false);
    }

    public Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null)
    {
        return PickMultipleInternalAsync(options);
    }

    private async Task<FileResult?> PickInternalAsync(PickOptions? options, bool multiple)
    {
        var results = await PickMultipleInternalAsync(options, multiple);
        return results.FirstOrDefault();
    }

    private Task<IEnumerable<FileResult>> PickMultipleInternalAsync(PickOptions? options, bool multiple = true)
    {
        return Task.Run<IEnumerable<FileResult>>(() =>
        {
            var tool = GetAvailableTool();
            if (tool == DialogTool.None)
            {
                // Fall back to console path input
                Console.WriteLine("No file dialog available. Please enter file path:");
                var path = Console.ReadLine();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return new[] { new LinuxFileResult(path) };
                }
                return Array.Empty<FileResult>();
            }

            string arguments;
            if (tool == DialogTool.Zenity)
            {
                arguments = BuildZenityArguments(options, multiple);
            }
            else
            {
                arguments = BuildKdialogArguments(options, multiple);
            }

            var psi = new ProcessStartInfo
            {
                FileName = tool == DialogTool.Zenity ? "zenity" : "kdialog",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                    return Array.Empty<FileResult>();

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                    return Array.Empty<FileResult>();

                // Parse output (paths separated by | for zenity, newlines for kdialog)
                var separator = tool == DialogTool.Zenity ? '|' : '\n';
                var paths = output.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                return paths
                    .Where(File.Exists)
                    .Select(p => (FileResult)new LinuxFileResult(p))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<FileResult>();
            }
        });
    }

    private string BuildZenityArguments(PickOptions? options, bool multiple)
    {
        var sb = new StringBuilder("--file-selection");

        if (multiple)
            sb.Append(" --multiple --separator='|'");

        if (!string.IsNullOrEmpty(options?.PickerTitle))
            sb.Append($" --title=\"{EscapeArgument(options.PickerTitle)}\"");

        if (options?.FileTypes != null)
        {
            foreach (var ext in options.FileTypes.Value)
            {
                var extension = ext.StartsWith(".") ? ext : $".{ext}";
                sb.Append($" --file-filter='*{extension}'");
            }
        }

        return sb.ToString();
    }

    private string BuildKdialogArguments(PickOptions? options, bool multiple)
    {
        var sb = new StringBuilder("--getopenfilename");

        if (multiple)
            sb.Insert(0, "--multiple ");

        sb.Append(" .");

        if (options?.FileTypes != null)
        {
            var extensions = string.Join(" ", options.FileTypes.Value.Select(e =>
                e.StartsWith(".") ? $"*{e}" : $"*.{e}"));
            if (!string.IsNullOrEmpty(extensions))
            {
                sb.Append($" \"{extensions}\"");
            }
        }

        if (!string.IsNullOrEmpty(options?.PickerTitle))
            sb.Append($" --title \"{EscapeArgument(options.PickerTitle)}\"");

        return sb.ToString();
    }

    private static string EscapeArgument(string arg)
    {
        return arg.Replace("\"", "\\\"").Replace("'", "\\'");
    }
}

/// <summary>
/// Linux-specific FileResult implementation.
/// </summary>
internal class LinuxFileResult : FileResult
{
    public LinuxFileResult(string fullPath) : base(fullPath)
    {
    }
}
