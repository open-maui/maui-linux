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
            DiagnosticLog.Warn("PortalFilePickerService", "No file picker available (install xdg-desktop-portal, zenity, or kdialog)");
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
            DiagnosticLog.Error("PortalFilePickerService", $"Portal error: {ex.Message}");
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
            args.Append($"--file-filter=\"{BuildZenityFilter("Files", extensions)}\" ");
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
            args.Append($"--file-filter=\"{BuildZenityFilter("Files", extensions)}\" ");
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
    /// Extracts file extensions from a MAUI FilePickerFileType. The portable
    /// Essentials build resolves <see cref="FilePickerFileType.Value"/> through
    /// DeviceInfo.Platform, which is only "Linux" once EssentialsPatches has
    /// run; when that lookup fails the platform dictionary is read directly
    /// (Linux entry first, otherwise every platform's extension-style entries).
    /// </summary>
    internal static List<string> GetExtensionsFromFileType(FilePickerFileType? fileType)
    {
        if (fileType == null) return new List<string>();

        IEnumerable<string>? raw = null;
        try
        {
            raw = fileType.Value;
        }
        catch
        {
            raw = ReadPlatformDictionary(fileType);
        }

        return NormalizeExtensions(raw);
    }

    private static IEnumerable<string>? ReadPlatformDictionary(FilePickerFileType fileType)
    {
        try
        {
            // FieldInfo.GetValue would run FilePickerFileType's static constructor,
            // which throws in the portable build (its Images/Videos/... statics
            // call NotImplementedInReferenceAssembly stubs). UnsafeAccessor reads
            // the instance field directly without that initialisation.
            var dict = GetPlatformFileTypes(fileType);
            if (dict == null)
                return null;

            if (dict.TryGetValue(Microsoft.Maui.Devices.DevicePlatform.Create("Linux"), out var linux))
                return linux;

            return dict.Values.SelectMany(v => v ?? Array.Empty<string>()).ToList();
        }
        catch
        {
            return null;
        }
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "fileTypes")]
    private static extern ref IDictionary<Microsoft.Maui.Devices.DevicePlatform, IEnumerable<string>>? GetPlatformFileTypes(FilePickerFileType fileType);

    /// <summary>
    /// Turns the mixed entries a FilePickerFileType carries ("png", ".png",
    /// "*.png", "image/png", "public.png") into distinct dotted extensions.
    /// MIME types and UTIs have no direct glob form and are dropped.
    /// </summary>
    internal static List<string> NormalizeExtensions(IEnumerable<string>? raw)
    {
        var extensions = new List<string>();
        if (raw == null) return extensions;

        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var ext = entry.Trim();
            if (ext.Contains('/')) continue;            // MIME type
            if (ext.StartsWith("public.", StringComparison.OrdinalIgnoreCase)) continue; // Apple UTI
            if (ext.StartsWith("*")) ext = ext.Substring(1);
            if (ext.Length == 0 || ext == ".") continue;
            if (ext.Contains('*') || ext.Contains('?')) continue;
            var normalized = ext.StartsWith(".") ? ext : $".{ext}";
            if (!extensions.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                extensions.Add(normalized);
        }

        return extensions;
    }

    /// <summary>zenity/yad --file-filter value: "Name | *.a *.b".</summary>
    internal static string BuildZenityFilter(string name, IEnumerable<string> extensions)
        => $"{name} | {string.Join(" ", extensions.Select(e => $"*{e}"))}";

    /// <summary>
    /// GVariant literal for the portal FileChooser "filters" option:
    /// a(sa(us)) with one "Files" entry holding glob patterns (type 0).
    /// </summary>
    internal static string? BuildPortalFilterArgs(FilePickerFileType? fileType)
        => BuildPortalFilterArgs(GetExtensionsFromFileType(fileType));

    internal static string? BuildPortalFilterArgs(IReadOnlyCollection<string> extensions)
    {
        if (extensions.Count == 0)
            return null;

        var patterns = string.Join(", ", extensions.Select(e => $"(uint32 0, '*{e}')"));
        return $"[('Files', [{patterns}])]";
    }

    /// <summary>
    /// Pulls the request object path out of a gdbus reply such as
    /// "(objectpath '/org/freedesktop/portal/desktop/request/1_0/t',)".
    /// </summary>
    internal static string? ParseRequestPath(string output)
    {
        if (string.IsNullOrEmpty(output)) return null;
        var start = output.IndexOf("'/", StringComparison.Ordinal);
        if (start < 0) return null;
        var end = output.IndexOf('\'', start + 1);
        if (end <= start) return null;
        return output.Substring(start + 1, end - start - 1);
    }

    internal static string EscapeForShell(string input)
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
            DiagnosticLog.Error("PortalFilePickerService", $"Command error: {ex.Message}");
            return "";
        }
    }
}
