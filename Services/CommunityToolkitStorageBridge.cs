// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// CommunityToolkit.Maui's <c>FolderPicker</c> and <c>FileSaver</c> on Linux.
/// The toolkit's platform-neutral build, which Linux apps get, implements
/// neither: every call returned an unsuccessful result, so "pick a folder" and
/// "save as" did nothing at all. When the toolkit is part of the app, its
/// defaults are replaced (through the toolkit's own <c>SetDefault</c>) with
/// implementations on the desktop portal, falling back to zenity/kdialog.
/// No compile-time reference: the toolkit is optional.
/// </summary>
internal static class CommunityToolkitStorageBridge
{
    private const string Assembly = "CommunityToolkit.Maui.Core";

    internal static void Install()
    {
        TryInstall("CommunityToolkit.Maui.Storage.FolderPicker", "CommunityToolkit.Maui.Storage.IFolderPicker", typeof(ToolkitFolderPicker));
        TryInstall("CommunityToolkit.Maui.Storage.FileSaver", "CommunityToolkit.Maui.Storage.IFileSaver", typeof(ToolkitFileSaver));
    }

    private static void TryInstall(string holderName, string interfaceName, Type proxyType)
    {
        try
        {
            var holder = Type.GetType($"{holderName}, {Assembly}");
            var contract = Type.GetType($"{interfaceName}, {Assembly}");
            if (holder == null || contract == null)
                return; // the toolkit is not part of the app

            var setDefault = holder.GetMethod("SetDefault", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { contract }, null);
            if (setDefault == null)
            {
                DiagnosticLog.Warn("CommunityToolkit", $"{holderName}.SetDefault not found in this toolkit release; its Linux implementation is not installed.");
                return;
            }
            setDefault.Invoke(null, new[] { DispatchProxy.Create(contract, proxyType) });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CommunityToolkit", $"Installing the Linux {holderName} failed", ex);
        }
    }

    /// <summary>Runs <paramref name="work"/> and returns it as the method's <c>Task&lt;TResult&gt;</c>.</summary>
    internal static object AsTypedTask(Type taskType, Func<Task<object>> work)
    {
        var resultType = taskType.GetGenericArguments()[0];
        return typeof(CommunityToolkitStorageBridge)
            .GetMethod(nameof(Cast), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(resultType)
            .Invoke(null, new object[] { work() })!;
    }

    private static async Task<T> Cast<T>(Task<object> task) => (T)await task.ConfigureAwait(true);
}

/// <summary>IFolderPicker: PickAsync([initialPath,] cancellationToken).</summary>
internal class ToolkitFolderPicker : DispatchProxy
{
    /// <summary>The toolkit's FolderPickerResult for <paramref name="path"/>, or a cancelled one.</summary>
    internal static object ResultFor(Type resultType, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return Activator.CreateInstance(resultType, null, new OperationCanceledException("No folder was picked."))!;
        var name = Path.GetFileName(path.TrimEnd('/'));
        // The Folder record's type comes from the result's own constructor: it
        // is not in the Storage namespace (Core.Primitives in 15.x).
        var folderType = resultType.GetConstructors()
            .Select(c => c.GetParameters())
            .First(p => p.Length == 2 && p[1].ParameterType == typeof(Exception))[0].ParameterType;
        var folder = Activator.CreateInstance(folderType, path, name)!;
        return Activator.CreateInstance(resultType, folder, null)!;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        args ??= Array.Empty<object?>();
        var initial = args.Length >= 2 ? args[0] as string : null;
        var token = args.OfType<CancellationToken>().FirstOrDefault();

        return CommunityToolkitStorageBridge.AsTypedTask(method.ReturnType, async () =>
        {
            var resultType = method.ReturnType.GetGenericArguments()[0];
            try
            {
                var picked = await new PortalFolderPickerService()
                    .PickAsync(new FolderPickerOptions { InitialDirectory = initial }, token).ConfigureAwait(true);
                return ResultFor(resultType, picked.Folder?.Path);
            }
            catch (Exception ex)
            {
                return Activator.CreateInstance(resultType, null, ex)!;
            }
        });
    }
}

/// <summary>IFileSaver: SaveAsync([initialPath,] fileName, stream, [progress,] cancellationToken).</summary>
internal class ToolkitFileSaver : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        args ??= Array.Empty<object?>();
        var strings = args.OfType<string>().ToArray();
        var initial = strings.Length >= 2 ? strings[0] : null;
        var fileName = strings.Length >= 1 ? strings[^1] : "file";
        var stream = args.OfType<Stream>().First();
        var progress = args.OfType<IProgress<double>>().FirstOrDefault();
        var token = args.OfType<CancellationToken>().FirstOrDefault();

        return CommunityToolkitStorageBridge.AsTypedTask(method.ReturnType, async () =>
        {
            var resultType = method.ReturnType.GetGenericArguments()[0];
            try
            {
                var path = await PickSavePathAsync(initial, fileName, token).ConfigureAwait(true);
                if (string.IsNullOrEmpty(path))
                    return Activator.CreateInstance(resultType, null, new OperationCanceledException("No file was chosen."))!;

                await WriteAsync(stream, path, progress, token).ConfigureAwait(true);
                return Activator.CreateInstance(resultType, path, null)!;
            }
            catch (Exception ex)
            {
                return Activator.CreateInstance(resultType, null, ex)!;
            }
        });
    }

    /// <summary>The desktop's Save dialog (the portal, else zenity or kdialog); null when cancelled or none is available.</summary>
    internal static async Task<string?> PickSavePathAsync(string? initial, string fileName, CancellationToken token)
    {
        var folder = Directory.Exists(initial) ? initial : null;
        var viaPortal = await new PortalFilePickerService()
            .PickSaveFileAsync("Save", fileName, folder, null, token).ConfigureAwait(true);
        if (viaPortal != null)
            return viaPortal;

        // No portal: the desktop's dialog tool, as the folder and file pickers do.
        var suggested = Path.Combine(folder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fileName);
        var (tool, arguments) = ToolOnPath("zenity") ? ("zenity", $"--file-selection --save --confirm-overwrite --filename=\"{suggested}\"")
            : ToolOnPath("kdialog") ? ("kdialog", $"--getsavefilename \"{suggested}\"")
            : (null, null);
        if (tool == null)
            return null;

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tool, arguments!)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });
        if (process == null)
            return null;
        var output = await process.StandardOutput.ReadToEndAsync(token).ConfigureAwait(true);
        await process.WaitForExitAsync(token).ConfigureAwait(true);
        var path = output.Trim();
        return process.ExitCode == 0 && path.Length > 0 ? path : null;
    }

    private static bool ToolOnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':').Any(dir => File.Exists(Path.Combine(dir, tool)));

    private static async Task WriteAsync(Stream source, string path, IProgress<double>? progress, CancellationToken token)
    {
        if (source.CanSeek)
            source.Seek(0, SeekOrigin.Begin);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write);
        var buffer = new byte[81920];
        long total = 0;
        long length = source.CanSeek ? source.Length : 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            total += read;
            if (length > 0)
                progress?.Report((double)total / length);
        }
        progress?.Report(1.0);
    }
}
