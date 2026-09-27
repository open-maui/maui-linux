// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>What happened when a service tried its portal.</summary>
internal enum PortalOutcome
{
    /// <summary>No portal frontend or interface (or it failed before a request existed): take the fallback.</summary>
    Unavailable,

    /// <summary>The portal completed the interaction.</summary>
    Completed,

    /// <summary>The user dismissed it: do not fall back to another dialog.</summary>
    Cancelled,

    /// <summary>The portal answered "other" (denied, backend error): take the fallback.</summary>
    Failed,
}

internal static class PortalOutcomeExtensions
{
    /// <summary>Unavailable and Failed mean "try the non-portal path"; Completed and Cancelled are final.</summary>
    public static bool ShouldFallBack(this PortalOutcome outcome)
        => outcome is PortalOutcome.Unavailable or PortalOutcome.Failed;

    public static PortalOutcome ToOutcome(this PortalResponse response) => response.Code switch
    {
        PortalResponseCode.Success => PortalOutcome.Completed,
        PortalResponseCode.Cancelled => PortalOutcome.Cancelled,
        _ => PortalOutcome.Failed,
    };
}

/// <summary>The "parent_window" identifier portals use to parent their dialogs.</summary>
internal static class PortalParentWindow
{
    /// <summary>
    /// "x11:XID" for an X11 main window. Wayland needs an xdg-foreign export
    /// handle, which is not wired yet, so it (and headless) is "" (unparented).
    /// </summary>
    public static string Current
    {
        get
        {
            try
            {
                var handle = (LinuxApplication.Current?.MainWindow as IX11Surface)?.Handle ?? IntPtr.Zero;
                return ForX11(handle.ToInt64());
            }
            catch
            {
                return "";
            }
        }
    }

    internal static string ForX11(long xid)
        => xid == 0 ? "" : "x11:" + xid.ToString("x", CultureInfo.InvariantCulture);
}

/// <summary>Result of a FileChooser call.</summary>
internal readonly record struct PortalFileChooserResult(PortalOutcome Outcome, IReadOnlyList<string> Paths);

/// <summary>FileChooser.OpenFile / SaveFile.</summary>
internal sealed class PortalFileChooser
{
    private readonly IDesktopPortal _portal;

    public PortalFileChooser(IDesktopPortal portal) => _portal = portal;

    public Task<PortalFileChooserResult> OpenAsync(PortalFileChooserRequest request, string parentWindow, CancellationToken cancellationToken = default)
        => RunAsync(save: false, request, parentWindow, cancellationToken);

    public Task<PortalFileChooserResult> SaveAsync(PortalFileChooserRequest request, string parentWindow, CancellationToken cancellationToken = default)
        => RunAsync(save: true, request, parentWindow, cancellationToken);

    private async Task<PortalFileChooserResult> RunAsync(bool save, PortalFileChooserRequest request, string parentWindow, CancellationToken cancellationToken)
    {
        var version = await _portal.GetVersionAsync(PortalInterfaces.FileChooser, cancellationToken).ConfigureAwait(false);
        if (version == 0)
            return new(PortalOutcome.Unavailable, Array.Empty<string>());
        // "directory" arrived in FileChooser version 3.
        if (request.Directory && version < 3)
            return new(PortalOutcome.Unavailable, Array.Empty<string>());

        try
        {
            var response = save
                ? await _portal.FileChooserSaveFileAsync(parentWindow, request.Title, PortalOptions.FileChooserSave(request), cancellationToken).ConfigureAwait(false)
                : await _portal.FileChooserOpenFileAsync(parentWindow, request.Title, PortalOptions.FileChooserOpen(request), cancellationToken).ConfigureAwait(false);

            var outcome = response.ToOutcome();
            var paths = outcome == PortalOutcome.Completed
                ? PortalOptions.UrisToLocalPaths(response.GetStrings("uris"))
                : new List<string>();
            return new(outcome, paths);
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("PortalFileChooser", $"FileChooser unavailable: {ex.Message}");
            return new(PortalOutcome.Unavailable, Array.Empty<string>());
        }
    }
}

/// <summary>OpenURI: open a URI, a file (by descriptor) or a file's folder.</summary>
internal sealed class PortalLauncher
{
    /// <summary>Upper bound for an open request; an app chooser may be on screen.</summary>
    internal static TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    private readonly IDesktopPortal _portal;

    public PortalLauncher(IDesktopPortal portal) => _portal = portal;

    public async Task<PortalOutcome> OpenUriAsync(string uri, bool ask = false, CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.OpenUri, cancellationToken).ConfigureAwait(false) == 0)
            return PortalOutcome.Unavailable;

        // Local files go through OpenFile (by descriptor) so a sandboxed app
        // can hand over files the host cannot see by path.
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile && File.Exists(parsed.LocalPath))
            return await OpenFileAsync(parsed.LocalPath, ask, cancellationToken).ConfigureAwait(false);

        return await RunAsync(ct => _portal.OpenUriAsync(PortalParentWindow.Current, uri, PortalOptions.OpenUri(ask), ct), cancellationToken).ConfigureAwait(false);
    }

    public async Task<PortalOutcome> OpenFileAsync(string path, bool ask = false, CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.OpenUri, cancellationToken).ConfigureAwait(false) == 0)
            return PortalOutcome.Unavailable;

        if (Directory.Exists(path))
        {
            var dirUri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            return await RunAsync(ct => _portal.OpenUriAsync(PortalParentWindow.Current, dirUri, PortalOptions.OpenUri(ask), ct), cancellationToken).ConfigureAwait(false);
        }

        Microsoft.Win32.SafeHandles.SafeFileHandle handle;
        try
        {
            handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("PortalLauncher", $"Cannot open '{path}' for OpenFile: {ex.Message}");
            return PortalOutcome.Unavailable;
        }

        using (handle)
            return await RunAsync(ct => _portal.OpenFileAsync(PortalParentWindow.Current, handle, PortalOptions.OpenUri(ask), ct), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>OpenDirectory (OpenURI v3+): the file manager with the file selected.</summary>
    public async Task<PortalOutcome> OpenContainingFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.OpenUri, cancellationToken).ConfigureAwait(false) < 3)
            return PortalOutcome.Unavailable;

        Microsoft.Win32.SafeHandles.SafeFileHandle handle;
        try
        {
            handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);
        }
        catch
        {
            return PortalOutcome.Unavailable;
        }

        using (handle)
            return await RunAsync(ct => _portal.OpenDirectoryAsync(PortalParentWindow.Current, handle, PortalOptions.OpenUri(), ct), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PortalOutcome> RunAsync(Func<CancellationToken, Task<PortalResponse>> call, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            var response = await call(timeout.Token).ConfigureAwait(false);
            return response.ToOutcome();
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("PortalLauncher", $"OpenURI unavailable: {ex.Message}");
            return PortalOutcome.Unavailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Debug("PortalLauncher", "OpenURI request timed out");
            return PortalOutcome.Failed;
        }
    }
}

/// <summary>Screenshot.Screenshot, returning the URI of the saved image.</summary>
internal sealed class PortalScreenshot
{
    private readonly IDesktopPortal _portal;

    public PortalScreenshot(IDesktopPortal portal) => _portal = portal;

    /// <summary>
    /// A desktop screenshot through the portal (the compositor/DE takes it,
    /// possibly after asking the user). Returns the image URI (usually a
    /// file:// under the user's pictures folder) or null. The Essentials
    /// Screenshot service keeps capturing the app's own window; this is for
    /// whole-desktop capture only.
    /// </summary>
    public async Task<string?> TakeAsync(bool interactive, CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.Screenshot, cancellationToken).ConfigureAwait(false) == 0)
            return null;
        try
        {
            var response = await _portal.ScreenshotAsync(PortalParentWindow.Current, PortalOptions.Screenshot(interactive), cancellationToken).ConfigureAwait(false);
            return response.IsSuccess ? response.GetString("uri") : null;
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("PortalScreenshot", $"Screenshot unavailable: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Secret.RetrieveSecret: a per-application secret kept by the desktop's
/// keyring (meaningful for sandboxed apps; each gets its own), turned into a
/// 256-bit storage key with HKDF.
/// </summary>
internal sealed class PortalSecretKey
{
    internal const string KeyInfo = "openmaui-secure-storage-v2";
    internal static TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    private readonly IDesktopPortal _portal;

    public PortalSecretKey(IDesktopPortal portal) => _portal = portal;

    /// <summary>The raw secret bytes, or null when the portal cannot provide one.</summary>
    public async Task<byte[]?> RetrieveSecretAsync(CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.Secret, cancellationToken).ConfigureAwait(false) == 0)
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        // Read concurrently: the backend writes before it answers, and EOF only
        // arrives once every write end (ours included) is closed.
        var read = ReadToEndAsync(pipe, timeout.Token);
        try
        {
            PortalResponse response;
            try
            {
                response = await _portal.RetrieveSecretAsync(pipe.ClientSafePipeHandle, new Dictionary<string, object>(), timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                pipe.DisposeLocalCopyOfClientHandle();
            }

            if (!response.IsSuccess)
                return null;

            var secret = await read.ConfigureAwait(false);
            return secret.Length > 0 ? secret : null;
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("PortalSecretKey", $"Secret portal unavailable: {ex.Message}");
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Debug("PortalSecretKey", "RetrieveSecret timed out");
            return null;
        }
        catch (IOException ex)
        {
            DiagnosticLog.Debug("PortalSecretKey", $"Reading the secret failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>The 32-byte storage key derived from the portal secret, or null.</summary>
    public async Task<byte[]?> DeriveStorageKeyAsync(CancellationToken cancellationToken = default)
    {
        var secret = await RetrieveSecretAsync(cancellationToken).ConfigureAwait(false);
        return secret == null ? null : DeriveKey(secret);
    }

    internal static byte[] DeriveKey(byte[] secret)
        => HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, salt: null, info: System.Text.Encoding.UTF8.GetBytes(KeyInfo));

    private static async Task<byte[]> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}

/// <summary>
/// Screen-saver / idle inhibition through Inhibit.Inhibit. Calls are
/// serialised so quick on/off toggles resolve in order; the handle is closed
/// to lift the inhibition.
/// </summary>
internal sealed class PortalIdleInhibitor
{
    private readonly IDesktopPortal _portal;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IAsyncDisposable? _handle;

    public PortalIdleInhibitor(IDesktopPortal portal) => _portal = portal;

    public bool IsInhibiting => Volatile.Read(ref _handle) != null;

    /// <summary>
    /// Applies the requested state. Returns false when the portal could not
    /// inhibit (caller falls back); releasing never needs a fallback when
    /// the portal held the inhibition.
    /// </summary>
    public async Task<bool> SetAsync(bool inhibit, string reason, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (inhibit)
            {
                if (_handle != null)
                    return true;
                if (await _portal.GetVersionAsync(PortalInterfaces.Inhibit, cancellationToken).ConfigureAwait(false) == 0)
                    return false;
                try
                {
                    _handle = await _portal.InhibitAsync(PortalParentWindow.Current, PortalInhibitFlags.Idle, PortalOptions.Inhibit(reason), cancellationToken).ConfigureAwait(false);
                    return true;
                }
                catch (PortalUnavailableException ex)
                {
                    DiagnosticLog.Debug("PortalIdleInhibitor", $"Inhibit unavailable: {ex.Message}");
                    return false;
                }
            }

            var handle = _handle;
            _handle = null;
            if (handle == null)
                return false;
            await handle.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>Result of <see cref="PortalBackground.RequestAsync"/>.</summary>
internal readonly record struct PortalBackgroundResult(PortalOutcome Outcome, bool Background, bool Autostart);

/// <summary>Background.RequestBackground.</summary>
internal sealed class PortalBackground
{
    private readonly IDesktopPortal _portal;

    public PortalBackground(IDesktopPortal portal) => _portal = portal;

    public async Task<PortalBackgroundResult> RequestAsync(string? reason, bool autostart, IReadOnlyList<string>? commandLine, bool dbusActivatable, CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.Background, cancellationToken).ConfigureAwait(false) == 0)
            return new(PortalOutcome.Unavailable, false, false);
        try
        {
            var response = await _portal.RequestBackgroundAsync(
                PortalParentWindow.Current,
                PortalOptions.Background(reason, autostart, commandLine, dbusActivatable),
                cancellationToken).ConfigureAwait(false);
            return new(response.ToOutcome(), response.GetBoolean("background") ?? false, response.GetBoolean("autostart") ?? false);
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("PortalBackground", $"Background unavailable: {ex.Message}");
            return new(PortalOutcome.Unavailable, false, false);
        }
    }
}

/// <summary>Location: one fix through a short-lived session.</summary>
internal sealed class PortalGeolocation
{
    private readonly IDesktopPortal _portal;

    public PortalGeolocation(IDesktopPortal portal) => _portal = portal;

    public async Task<PortalLocationFix?> GetFixAsync(uint accuracy, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (await _portal.GetVersionAsync(PortalInterfaces.Location, cancellationToken).ConfigureAwait(false) == 0)
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            var raw = await _portal.GetLocationAsync(PortalOptions.LocationSession(accuracy, PortalRequestPath.NewToken("location")), cts.Token).ConfigureAwait(false);
            return PortalLocationFix.Parse(raw);
        }
        catch (PortalUnavailableException ex)
        {
            DiagnosticLog.Debug("PortalGeolocation", $"Location unavailable: {ex.Message}");
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Debug("PortalGeolocation", "No location before the timeout");
            return null;
        }
    }
}

/// <summary>
/// Runs a portal task from synchronous code (constructors, property
/// getters) with a bounded wait, off any synchronization context.
/// </summary>
internal static class PortalSync
{
    public static bool TryRun<T>(Func<CancellationToken, Task<T>> work, TimeSpan timeout, out T? result)
    {
        result = default;
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var task = Task.Run(() => work(cts.Token));
            if (!task.Wait(timeout))
                return false;
            result = task.Result;
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("PortalSync", $"Portal call failed: {ex.GetBaseException().Message}");
            return false;
        }
    }
}
