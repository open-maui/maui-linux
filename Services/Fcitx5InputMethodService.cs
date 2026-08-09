// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Fcitx5 Input Method service over the session bus.
///
/// Talks to Fcitx5's D-Bus frontend (<c>org.fcitx.Fcitx5</c>) with typed
/// <see cref="Tmds.DBus"/> proxies: <c>org.fcitx.Fcitx.InputMethod1</c> creates
/// an input context and <c>org.fcitx.Fcitx.InputContext1</c> carries method
/// calls (FocusIn/FocusOut, ProcessKeyEvent, SetCursorRect, Reset, Destroy) and
/// the composition signals (<c>CommitString</c>, <c>UpdateFormattedPreedit</c>,
/// <c>ForwardKey</c>). This replaces the earlier <c>gdbus</c>/<c>dbus-monitor</c>
/// subprocess implementation — there is no <c>Process.Start</c> in this path
/// anymore.
///
/// Signals arrive on the D-Bus connection's own thread; every callback is
/// marshalled to the GLib main thread via <see cref="LinuxDispatcher"/> before
/// touching service state or raising events, matching the rest of the platform.
/// </summary>
public class Fcitx5InputMethodService : IInputMethodService, IDisposable
{
    private const string Fcitx5Service = "org.fcitx.Fcitx5";
    private const string InputMethodPath = "/org/freedesktop/portal/inputmethod";

    private IInputContext? _currentContext;
    private string _preEditText = string.Empty;
    private int _preEditCursorPosition;
    private bool _isActive;
    private bool _disposed;

    private Connection? _connection;
    private IFcitxInputContext1? _inputContext;
    private readonly List<IDisposable> _signalWatchers = new();

    public bool IsActive => _isActive;
    public string PreEditText => _preEditText;
    public int PreEditCursorPosition => _preEditCursorPosition;

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;
    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;
    public event EventHandler? PreEditEnded;

    public void Initialize(nint windowHandle)
    {
        try
        {
            // The IInputMethodService contract is synchronous and callers expect
            // the context to be usable once Initialize returns, so bridge the
            // async connect/handshake with a bounded wait. Run it off the calling
            // thread to avoid any interaction with the installed
            // LinuxSynchronizationContext; Tmds.DBus continuations run on the
            // thread pool regardless.
            if (!Task.Run(InitializeAsync).Wait(TimeSpan.FromSeconds(2)))
                DiagnosticLog.Error("Fcitx5InputMethodService", "Initialization timed out connecting to Fcitx5");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Fcitx5InputMethodService", $"Initialization failed - {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address))
        {
            DiagnosticLog.Error("Fcitx5InputMethodService", "No session bus address; Fcitx5 unavailable");
            return;
        }

        var connection = new Connection(address);
        await connection.ConnectAsync();
        _connection = connection;

        // Create an input context. Fcitx5's portal frontend takes a(ss) of
        // client hints and returns (object-path, uuid). We only need the path.
        var inputMethod = connection.CreateProxy<IFcitxInputMethod1>(Fcitx5Service, InputMethodPath);
        var (icPath, _) = await inputMethod.CreateInputContextAsync(new[] { ("program", "maui-linux") });

        var inputContext = connection.CreateProxy<IFcitxInputContext1>(Fcitx5Service, icPath);
        _inputContext = inputContext;

        // Subscribe to the composition signals. Handlers marshal onto the main
        // thread before doing anything observable.
        _signalWatchers.Add(await inputContext.WatchCommitStringAsync(OnCommitString, OnSignalError));
        _signalWatchers.Add(await inputContext.WatchUpdateFormattedPreeditAsync(OnUpdateFormattedPreedit, OnSignalError));
        _signalWatchers.Add(await inputContext.WatchForwardKeyAsync(OnForwardKey, OnSignalError));

        DiagnosticLog.Debug("Fcitx5InputMethodService", $"Created context at {icPath}");
    }

    // Run an action on the GLib main thread. Signal callbacks land on the D-Bus
    // reader thread; Dispatch queues them onto the main loop (GLibNative.IdleAdd)
    // from any background thread. If the dispatcher doesn't exist yet we're in
    // single-threaded startup and inline is safe.
    private static void Post(Action action)
    {
        if (LinuxDispatcher.IsMainThread || LinuxDispatcher.Main is not { } main) action();
        else main.Dispatch(action);
    }

    private static void OnSignalError(Exception ex)
        => DiagnosticLog.Debug("Fcitx5InputMethodService", $"Signal watch error - {ex.Message}");

    private void OnCommitString(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        Post(() =>
        {
            if (_disposed) return;
            _preEditText = string.Empty;
            _preEditCursorPosition = 0;
            _isActive = false;

            TextCommitted?.Invoke(this, new TextCommittedEventArgs(text));
            _currentContext?.OnTextCommitted(text);
        });
    }

    private void OnUpdateFormattedPreedit(((string Text, int Format)[] Segments, int Cursor) preedit)
    {
        // UpdateFormattedPreedit carries the composition as a list of formatted
        // segments; the visible pre-edit is their concatenation. (The per-segment
        // format flags select underline/highlight styling — not surfaced here, to
        // preserve the previous behavior which only tracked the plain string.)
        var text = string.Concat(preedit.Segments.Select(s => s.Text));
        Post(() =>
        {
            if (_disposed) return;
            _preEditText = text;
            _isActive = !string.IsNullOrEmpty(text);

            PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition, new List<PreEditAttribute>()));
            _currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
        });
    }

    private void OnForwardKey((uint Keyval, uint State, bool IsRelease) key)
    {
        // Fcitx5 forwards keys it chose not to consume back to the client. The
        // previous subprocess implementation ignored these entirely; we bind the
        // signal (so nothing is silently dropped at the transport level) but keep
        // the same observable behavior for now.
        // TODO: route forwarded keys back into the app's key-input pipeline
        // (there is no IInputContext hook for raw key injection yet).
        DiagnosticLog.Debug("Fcitx5InputMethodService", $"ForwardKey ignored (keyval={key.Keyval}, release={key.IsRelease})");
    }

    public void SetFocus(IInputContext? context)
    {
        _currentContext = context;

        var ic = _inputContext;
        if (ic == null) return;

        if (context != null)
            FireAndForget(ic.FocusInAsync(), "FocusIn");
        else
            FireAndForget(ic.FocusOutAsync(), "FocusOut");
    }

    public void SetCursorLocation(int x, int y, int width, int height)
    {
        var ic = _inputContext;
        if (ic == null) return;
        FireAndForget(ic.SetCursorRectAsync(x, y, width, height), "SetCursorRect");
    }

    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
    {
        var ic = _inputContext;
        if (ic == null) return false;

        uint state = ConvertModifiers(modifiers);
        try
        {
            // The contract is synchronous: the caller needs to know whether the
            // IME consumed the key to decide on fallback handling. Bound the wait
            // so a stalled IME can't freeze input; treat a timeout as "not
            // handled". (isRelease is the release flag — true on key up.)
            var task = ic.ProcessKeyEventAsync(keyCode, keyCode, state, !isKeyDown, 0);
            return task.Wait(TimeSpan.FromMilliseconds(200)) && task.Result;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("Fcitx5InputMethodService", $"ProcessKeyEvent failed - {ex.Message}");
            return false;
        }
    }

    private uint ConvertModifiers(KeyModifiers modifiers)
    {
        uint state = 0;
        if (modifiers.HasFlag(KeyModifiers.Shift)) state |= 1;
        if (modifiers.HasFlag(KeyModifiers.CapsLock)) state |= 2;
        if (modifiers.HasFlag(KeyModifiers.Control)) state |= 4;
        if (modifiers.HasFlag(KeyModifiers.Alt)) state |= 8;
        if (modifiers.HasFlag(KeyModifiers.Super)) state |= 64;
        return state;
    }

    public void Reset()
    {
        var ic = _inputContext;
        if (ic != null)
            FireAndForget(ic.ResetAsync(), "Reset");

        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        _isActive = false;

        PreEditEnded?.Invoke(this, EventArgs.Empty);
        _currentContext?.OnPreEditEnded();
    }

    public void Shutdown()
    {
        Dispose();
    }

    private static void FireAndForget(Task task, string op)
    {
        task.ContinueWith(
            t => DiagnosticLog.Debug("Fcitx5InputMethodService", $"{op} failed - {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var watcher in _signalWatchers)
        {
            try { watcher.Dispose(); }
            catch (Exception ex) { DiagnosticLog.Debug("Fcitx5InputMethodService", $"Signal watcher cleanup failed - {ex.Message}"); }
        }
        _signalWatchers.Clear();

        try
        {
            // Best-effort Destroy before tearing the connection down.
            if (_inputContext != null)
                _inputContext.DestroyAsync().Wait(TimeSpan.FromMilliseconds(200));
        }
        catch (Exception ex) { DiagnosticLog.Debug("Fcitx5InputMethodService", $"Destroy failed - {ex.Message}"); }

        _inputContext = null;
        _connection?.Dispose();
        _connection = null;
    }

    /// <summary>
    /// Checks whether Fcitx5 is reachable on the session bus. Returns false
    /// (never throws) when there's no session bus or Fcitx5 isn't running, so
    /// the factory can fall back to another IME.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            var task = Task.Run(IsAvailableAsync);
            return task.Wait(TimeSpan.FromSeconds(1)) && task.Result;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsAvailableAsync()
    {
        var address = Address.Session;
        if (string.IsNullOrEmpty(address)) return false;

        using var connection = new Connection(address);
        await connection.ConnectAsync();
        var services = await connection.ListServicesAsync();
        return services.Contains(Fcitx5Service);
    }
}

// --- Fcitx5 D-Bus proxy interfaces -----------------------------------------
// Typed Tmds.DBus proxies for the two Fcitx5 frontend interfaces we use. Method
// and signal shapes mirror Fcitx5's introspection XML.

/// <summary>org.fcitx.Fcitx.InputMethod1 — factory for input contexts.</summary>
[DBusInterface("org.fcitx.Fcitx.InputMethod1")]
internal interface IFcitxInputMethod1 : IDBusObject
{
    // CreateInputContext(in a(ss) args, out o path, out ay uuid)
    Task<(ObjectPath path, byte[] uuid)> CreateInputContextAsync((string, string)[] args);
}

/// <summary>org.fcitx.Fcitx.InputContext1 — a single input context.</summary>
[DBusInterface("org.fcitx.Fcitx.InputContext1")]
internal interface IFcitxInputContext1 : IDBusObject
{
    Task FocusInAsync();
    Task FocusOutAsync();
    Task ResetAsync();
    Task DestroyAsync();
    Task SetCursorRectAsync(int X, int Y, int W, int H);

    // ProcessKeyEvent(in u keyval, u keycode, u state, b isRelease, u time, out b handled)
    Task<bool> ProcessKeyEventAsync(uint Keyval, uint Keycode, uint State, bool IsRelease, uint Time);

    // CommitString(s str)
    Task<IDisposable> WatchCommitStringAsync(Action<string> handler, Action<Exception>? onError = null);

    // UpdateFormattedPreedit(a(si) str, i cursorpos)
    Task<IDisposable> WatchUpdateFormattedPreeditAsync(Action<((string, int)[] str, int cursorpos)> handler, Action<Exception>? onError = null);

    // ForwardKey(u keyval, u state, b isRelease)
    Task<IDisposable> WatchForwardKeyAsync(Action<(uint keyval, uint state, bool isRelease)> handler, Action<Exception>? onError = null);
}
