// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

public class PortalFolderPickerService
{
    public async Task<FolderPickerResult> PickAsync(FolderPickerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new FolderPickerOptions();
        var title = options.Title ?? "Select Folder";

        // The desktop portal first: the desktop's own folder dialog (KDE, GNOME),
        // and the only one inside a sandbox. Cancel is final; an unavailable or
        // failed portal falls through to zenity/kdialog.
        if (DesktopPortal.ShouldTry(PortalUse.Always))
        {
            var portal = await new PortalFileChooser(DesktopPortal.Current).OpenAsync(new PortalFileChooserRequest
            {
                Title = title,
                Directory = true,
                CurrentFolder = Directory.Exists(options.InitialDirectory) ? options.InitialDirectory : null,
            }, PortalParentWindow.Current, cancellationToken).ConfigureAwait(false);

            DiagnosticLog.Info("FolderPicker", $"Portal: {portal.Outcome}, paths: [{string.Join(", ", portal.Paths)}]");
            if (portal.Outcome == PortalOutcome.Completed && portal.Paths.FirstOrDefault() is { } picked && Directory.Exists(picked))
                return new FolderPickerResult(new FolderResult(picked));
            if (!portal.Outcome.ShouldFallBack())
                return new FolderPickerResult(null);
        }

        string? result = null;
        var start = Directory.Exists(options.InitialDirectory) ? options.InitialDirectory! : ".";

        if (IsCommandAvailable("zenity"))
        {
            var args = $"--file-selection --directory --title=\"{title}\" --filename=\"{start.TrimEnd('/')}/\"";
            result = await Task.Run(() => RunCommand("zenity", args)?.Trim(), cancellationToken);
        }
        else if (IsCommandAvailable("kdialog"))
        {
            var args = $"--getexistingdirectory \"{start}\" --title \"{title}\"";
            result = await Task.Run(() => RunCommand("kdialog", args)?.Trim(), cancellationToken);
        }

        DiagnosticLog.Info("FolderPicker", $"Dialog tool result: '{result}'");
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
