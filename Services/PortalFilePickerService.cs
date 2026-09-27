// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services.Portal;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// File picker service using xdg-desktop-portal (FileChooser over native
/// D-Bus) for native dialogs. Falls back to zenity/kdialog/yad when the
/// portal is unavailable or answers with an error; a cancelled portal dialog
/// is final (no second dialog).
/// </summary>
public class PortalFilePickerService : IFilePicker
{
    private readonly IDesktopPortal _portal;
    private bool? _portalAvailable;
    private string? _fallbackTool;
    private bool _fallbackDetected;

    public PortalFilePickerService()
        : this(DesktopPortal.Current)
    {
    }

    internal PortalFilePickerService(IDesktopPortal portal)
    {
        _portal = portal;
    }

    private string? FallbackTool
    {
        get
        {
            if (!_fallbackDetected)
            {
                _fallbackDetected = true;
                if (IsCommandAvailable("zenity"))
                    _fallbackTool = "zenity";
                else if (IsCommandAvailable("kdialog"))
                    _fallbackTool = "kdialog";
                else if (IsCommandAvailable("yad"))
                    _fallbackTool = "yad";
            }
            return _fallbackTool;
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
        if (_portalAvailable != false && DesktopPortal.ShouldTry(PortalUse.Always))
        {
            var portal = await PickWithPortalAsync(options, allowMultiple).ConfigureAwait(false);
            if (!portal.Outcome.ShouldFallBack())
            {
                _portalAvailable = true;
                return portal.Paths.Select(p => new FileResult(p)).ToList();
            }
            if (portal.Outcome == PortalOutcome.Unavailable)
                _portalAvailable = false;
        }

        if (FallbackTool != null)
            return await PickWithFallbackAsync(options, allowMultiple);

        DiagnosticLog.Warn("PortalFilePickerService", "No file picker available (install xdg-desktop-portal, zenity, or kdialog)");
        return Enumerable.Empty<FileResult>();
    }

    /// <summary>FileChooser request for a MAUI pick: title, multiple, one "Files" filter.</summary>
    internal static PortalFileChooserRequest BuildPortalRequest(PickOptions options, bool allowMultiple, string? currentFolder = null)
    {
        var filter = PortalFileFilter.FromExtensions("Files", GetExtensionsFromFileType(options.FileTypes));
        return new PortalFileChooserRequest
        {
            Title = options.PickerTitle ?? "Open File",
            Multiple = allowMultiple,
            Filters = filter == null ? Array.Empty<PortalFileFilter>() : new[] { filter },
            CurrentFolder = currentFolder,
        };
    }

    private Task<PortalFileChooserResult> PickWithPortalAsync(PickOptions options, bool allowMultiple)
        => new PortalFileChooser(_portal).OpenAsync(BuildPortalRequest(options, allowMultiple), PortalParentWindow.Current);

    /// <summary>
    /// Save dialog through FileChooser.SaveFile. Returns the chosen path, or
    /// null when the user cancelled or no portal is available (MAUI has no
    /// save-picker contract, so there is no subprocess fallback here).
    /// </summary>
    internal async Task<string?> PickSaveFileAsync(string title, string? suggestedName, string? currentFolder, IReadOnlyCollection<string>? extensions, CancellationToken cancellationToken = default)
    {
        if (!DesktopPortal.ShouldTry(PortalUse.Always))
            return null;
        var filter = extensions == null ? null : PortalFileFilter.FromExtensions("Files", extensions);
        var result = await new PortalFileChooser(_portal).SaveAsync(new PortalFileChooserRequest
        {
            Title = title,
            CurrentName = suggestedName,
            CurrentFolder = currentFolder,
            Filters = filter == null ? Array.Empty<PortalFileFilter>() : new[] { filter },
        }, PortalParentWindow.Current, cancellationToken).ConfigureAwait(false);
        return result.Outcome == PortalOutcome.Completed ? result.Paths.FirstOrDefault() : null;
    }

    private async Task<IEnumerable<FileResult>> PickWithFallbackAsync(PickOptions options, bool allowMultiple)
    {
        return FallbackTool switch
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
    /// GVariant text literal for the FileChooser "filters" option (gdbus form).
    /// The native path uses <see cref="PortalFileFilter"/>; this stays for
    /// diagnostics and existing callers.
    /// </summary>
    /// <remarks>
    /// GVariant literal for the portal FileChooser "filters" option:
    /// a(sa(us)) with one "Files" entry holding glob patterns (type 0).
    /// </remarks>
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
    /// Legacy gdbus-reply parser (kept for existing callers).
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
            DiagnosticLog.Error("PortalFilePickerService", $"Command error: {ex.Message}", ex);
            return "";
        }
    }
}
