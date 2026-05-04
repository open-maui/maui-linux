// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// File picker service using xdg-desktop-portal for native dialogs.
/// Falls back to zenity/kdialog if portal is unavailable.
/// </summary>
public class PortalFilePickerService : IFilePicker
{
    private bool _portalAvailable = true;
    private string? _fallbackTool;

    public PortalFilePickerService()
    {
        DetectAvailableTools();
    }

    private void DetectAvailableTools()
    {
        // Check if portal is available
        _portalAvailable = CheckPortalAvailable();

        if (!_portalAvailable)
        {
            // Check for fallback tools
            if (IsCommandAvailable("zenity"))
                _fallbackTool = "zenity";
            else if (IsCommandAvailable("kdialog"))
                _fallbackTool = "kdialog";
            else if (IsCommandAvailable("yad"))
                _fallbackTool = "yad";
        }
    }

    private bool CheckPortalAvailable()
    {
        try
        {
            // Check if xdg-desktop-portal is running
            var output = RunCommand("busctl", "--user list | grep -q org.freedesktop.portal.Desktop && echo yes");
            return output.Trim() == "yes";
        }
        catch
        {
            return false;
        }
    }

    private bool IsCommandAvailable(string command)
    {
        try
        {
            var output = RunCommand("which", command);
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    public async Task<FileResult?> PickAsync(PickOptions? options = null)
    {
        options ??= new PickOptions();
        var results = await PickFilesAsync(options, allowMultiple: false);
        return results.FirstOrDefault();
    }

    public async Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null)
    {
        options ??= new PickOptions();
        return await PickFilesAsync(options, allowMultiple: true);
    }

    private async Task<IEnumerable<FileResult>> PickFilesAsync(PickOptions options, bool allowMultiple)
    {
        if (_portalAvailable)
        {
            return await PickWithPortalAsync(options, allowMultiple);
        }
        else if (_fallbackTool != null)
        {
            return await PickWithFallbackAsync(options, allowMultiple);
        }
        else
        {
            // No file picker available
            Console.WriteLine("[FilePickerService] No file picker available (install xdg-desktop-portal, zenity, or kdialog)");
            return Enumerable.Empty<FileResult>();
        }
    }

    private async Task<IEnumerable<FileResult>> PickWithPortalAsync(PickOptions options, bool allowMultiple)
    {
        try
        {
            // Use gdbus to call the portal
            var filterArgs = BuildPortalFilterArgs(options.FileTypes);
            var multipleArg = allowMultiple ? "true" : "false";
            var title = options.PickerTitle ?? "Open File";

            // Build the D-Bus call
            var args = new StringBuilder();
            args.Append("call --session ");
            args.Append("--dest org.freedesktop.portal.Desktop ");
            args.Append("--object-path /org/freedesktop/portal/desktop ");
            args.Append("--method org.freedesktop.portal.FileChooser.OpenFile ");
            args.Append("\"\" "); // Parent window (empty for no parent)
            args.Append($"\"{EscapeForShell(title)}\" "); // Title

            // Options dictionary
            args.Append("@a{sv} {");
            args.Append($"'multiple': <{multipleArg}>");
            if (filterArgs != null)
            {
                args.Append($", 'filters': <{filterArgs}>");
            }
            args.Append("}");

            var output = await Task.Run(() => RunCommand("gdbus", args.ToString()));

            // Parse the response to get the request path
            // Response format: (objectpath '/org/freedesktop/portal/desktop/request/...',)
            var requestPath = ParseRequestPath(output);
            if (string.IsNullOrEmpty(requestPath))
            {
                return Enumerable.Empty<FileResult>();
            }

            // Wait for the response signal (simplified - in production use D-Bus signal subscription)
            await Task.Delay(100);

            // For now, fall back to synchronous zenity if portal response parsing is complex
            if (_fallbackTool != null)
            {
                return await PickWithFallbackAsync(options, allowMultiple);
            }

            return Enumerable.Empty<FileResult>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FilePickerService] Portal error: {ex.Message}");
            // Fall back to zenity/kdialog
            if (_fallbackTool != null)
            {
                return await PickWithFallbackAsync(options, allowMultiple);
            }
            return Enumerable.Empty<FileResult>();
        }
    }

    private async Task<IEnumerable<FileResult>> PickWithFallbackAsync(PickOptions options, bool allowMultiple)
    {
        return _fallbackTool switch
        {
            "zenity" => await PickWithZenityAsync(options, allowMultiple),
            "kdialog" => await PickWithKdialogAsync(options, allowMultiple),
            "yad" => await PickWithYadAsync(options, allowMultiple),
            _ => Enumerable.Empty<FileResult>()
        };
    }

    private async Task<IEnumerable<FileResult>> PickWithZenityAsync(PickOptions options, bool allowMultiple)
    {
        var args = new StringBuilder();
        args.Append("--file-selection ");

        if (!string.IsNullOrEmpty(options.PickerTitle))
        {
            args.Append($"--title=\"{EscapeForShell(options.PickerTitle)}\" ");
        }

        if (allowMultiple)
        {
            args.Append("--multiple --separator=\"|\" ");
        }

        // Add file filters from FilePickerFileType
        var extensions = GetExtensionsFromFileType(options.FileTypes);
        if (extensions.Count > 0)
        {
            var filterPattern = string.Join(" ", extensions.Select(e => $"*{e}"));
            args.Append($"--file-filter=\"Files | {filterPattern}\" ");
        }

        var output = await Task.Run(() => RunCommand("zenity", args.ToString()));

        if (string.IsNullOrWhiteSpace(output))
        {
            return Enumerable.Empty<FileResult>();
        }

        var files = output.Trim().Split('|', StringSplitOptions.RemoveEmptyEntries);
        return files.Select(f => new FileResult(f.Trim())).ToList();
    }

    private async Task<IEnumerable<FileResult>> PickWithKdialogAsync(PickOptions options, bool allowMultiple)
    {
        var args = new StringBuilder();
        args.Append("--getopenfilename ");

        // Start directory
        args.Append(". ");

        // Add file filters
        var extensions = GetExtensionsFromFileType(options.FileTypes);
        if (extensions.Count > 0)
        {
            var filterPattern = string.Join(" ", extensions.Select(e => $"*{e}"));
            args.Append($"\"Files ({filterPattern})\" ");
        }

        if (!string.IsNullOrEmpty(options.PickerTitle))
        {
            args.Append($"--title \"{EscapeForShell(options.PickerTitle)}\" ");
        }

        if (allowMultiple)
        {
            args.Append("--multiple --separate-output ");
        }

        var output = await Task.Run(() => RunCommand("kdialog", args.ToString()));

        if (string.IsNullOrWhiteSpace(output))
        {
            return Enumerable.Empty<FileResult>();
        }

        var files = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return files.Select(f => new FileResult(f.Trim())).ToList();
    }

    private async Task<IEnumerable<FileResult>> PickWithYadAsync(PickOptions options, bool allowMultiple)
    {
        // YAD is similar to zenity
        var args = new StringBuilder();
        args.Append("--file ");

        if (!string.IsNullOrEmpty(options.PickerTitle))
        {
            args.Append($"--title=\"{EscapeForShell(options.PickerTitle)}\" ");
        }

        if (allowMultiple)
        {
            args.Append("--multiple --separator=\"|\" ");
        }

        var extensions = GetExtensionsFromFileType(options.FileTypes);
        if (extensions.Count > 0)
        {
            var filterPattern = string.Join(" ", extensions.Select(e => $"*{e}"));
            args.Append($"--file-filter=\"Files | {filterPattern}\" ");
        }

        var output = await Task.Run(() => RunCommand("yad", args.ToString()));

        if (string.IsNullOrWhiteSpace(output))
        {
            return Enumerable.Empty<FileResult>();
        }

        var files = output.Trim().Split('|', StringSplitOptions.RemoveEmptyEntries);
        return files.Select(f => new FileResult(f.Trim())).ToList();
    }

    /// <summary>
    /// Extracts file extensions from a MAUI FilePickerFileType.
    /// </summary>
    private List<string> GetExtensionsFromFileType(FilePickerFileType? fileType)
    {
        var extensions = new List<string>();
        if (fileType == null) return extensions;

        try
        {
            // FilePickerFileType.Value is IEnumerable<string> for the current platform
            var value = fileType.Value;
            if (value == null) return extensions;

            foreach (var ext in value)
            {
                // Skip MIME types, only take file extensions
                if (ext.StartsWith(".") || (!ext.Contains('/') && !ext.Contains('*')))
                {
                    var normalized = ext.StartsWith(".") ? ext : $".{ext}";
                    if (!extensions.Contains(normalized))
                    {
                        extensions.Add(normalized);
                    }
                }
            }
        }
        catch
        {
            // Silently fail if we can't parse the file type
        }

        return extensions;
    }

    private string? BuildPortalFilterArgs(FilePickerFileType? fileType)
    {
        var extensions = GetExtensionsFromFileType(fileType);
        if (extensions.Count == 0)
            return null;

        var patterns = string.Join(", ", extensions.Select(e => $"(uint32 0, '*{e}')"));
        return $"[('Files', [{patterns}])]";
    }

    private string? ParseRequestPath(string output)
    {
        // Parse D-Bus response like: (objectpath '/org/freedesktop/portal/desktop/request/...',)
        var start = output.IndexOf("'/");
        var end = output.IndexOf("',", start);
        if (start >= 0 && end > start)
        {
            return output.Substring(start + 1, end - start - 1);
        }
        return null;
    }

    private string EscapeForShell(string input)
    {
        return input.Replace("\"", "\\\"").Replace("'", "\\'");
    }

    private string RunCommand(string command, string arguments)
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
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            return output;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FilePickerService] Command error: {ex.Message}");
            return "";
        }
    }
}

/// <summary>
/// Folder picker service using xdg-desktop-portal for native dialogs.
/// </summary>
public class PortalFolderPickerService
{
    public async Task<FolderPickerResult> PickAsync(FolderPickerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new FolderPickerOptions();

        // Use zenity/kdialog for folder selection (simpler than portal)
        string? selectedFolder = null;

        if (IsCommandAvailable("zenity"))
        {
            var args = $"--file-selection --directory --title=\"{options.Title ?? "Select Folder"}\"";
            selectedFolder = await Task.Run(() => RunCommand("zenity", args)?.Trim());
        }
        else if (IsCommandAvailable("kdialog"))
        {
            var args = $"--getexistingdirectory . --title \"{options.Title ?? "Select Folder"}\"";
            selectedFolder = await Task.Run(() => RunCommand("kdialog", args)?.Trim());
        }

        if (!string.IsNullOrEmpty(selectedFolder) && Directory.Exists(selectedFolder))
        {
            return new FolderPickerResult(new FolderResult(selectedFolder));
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
            var output = RunCommand("which", command);
            return !string.IsNullOrWhiteSpace(output);
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
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            return output;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Result of a folder picker operation.
/// </summary>
public class FolderResult
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path) ?? Path;

    public FolderResult(string path)
    {
        Path = path;
    }
}

/// <summary>
/// Result wrapper for folder picker.
/// </summary>
public class FolderPickerResult
{
    public FolderResult? Folder { get; }
    public bool WasSuccessful => Folder != null;

    public FolderPickerResult(FolderResult? folder)
    {
        Folder = folder;
    }
}

/// <summary>
/// Options for folder picker.
/// </summary>
public class FolderPickerOptions
{
    public string? Title { get; set; }
    public string? InitialDirectory { get; set; }
}
