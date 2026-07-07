// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Interop;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// freedesktop System Tray Protocol (XEmbed) fallback for X11 sessions whose
/// desktop has no StatusNotifierItem host (bare window managers, trayer,
/// stalonetray, older panels, ...). We dock a small child window into the
/// tray manager's socket and paint the icon ourselves with SkiaSharp.
///
/// Threading: a dedicated background thread owns a private Display connection
/// and performs every Xlib call for it. Public ITrayBackend methods (callable
/// from any thread) post closures to a command queue that the thread drains
/// between XNextEvent polls. TrayIcon callbacks (Activated, menu actions) are
/// marshalled to the main thread via LinuxDispatcher.
///
/// Availability is probed once, when TrayIconService first selects a backend:
/// if no manager owns _NET_SYSTEM_TRAY_S{screen} at that moment the probe
/// fails and the null backend wins. Once selected, though, the backend keeps
/// listening for MANAGER announcements on the root window, so a tray that
/// restarts later (panel crash/upgrade) gets our icons re-docked.
///
/// Icon sources: <see cref="TrayIcon.IconPath"/> may be a file path (any
/// SkiaSharp-decodable raster format) or a freedesktop icon name resolved
/// against hicolor theme dirs and pixmaps — PNG only, because SkiaSharp
/// cannot rasterize SVG; SVG-only themed icons log a warning and stay blank.
/// </summary>
internal sealed class XEmbedTrayBackend : ITrayBackend
{
    private const string Tag = "XEmbedTray";
    private const int DefaultIconSize = 22;

    // System Tray Protocol opcode (data.l[1] of _NET_SYSTEM_TRAY_OPCODE)
    private const long SystemTrayRequestDock = 0;

    // _XEMBED_INFO flags
    private const long XembedMapped = 1;

    private const int PropModeReplace = 0;
    private const int AllocNone = 0;
    private const long VisualIdMask = 0x1;

    // XCreateWindow value-mask bits
    private const ulong CWBackPixel = 1UL << 1;
    private const ulong CWBorderPixel = 1UL << 3;
    private const ulong CWEventMask = 1UL << 11;
    private const ulong CWColormap = 1UL << 13;

    /// <summary>Per-icon state. Touched only on the event thread.</summary>
    private sealed class IconState
    {
        public IconState(TrayIcon owner) => Owner = owner;

        public TrayIcon Owner { get; }
        public IntPtr Window;
        public IntPtr Gc;
        public IntPtr Colormap;
        public IntPtr Visual;
        public int Depth;
        public int Width = DefaultIconSize;
        public int Height = DefaultIconSize;
        public SKBitmap? Source;
        public string? SourcePath;
    }

    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly AutoResetEvent _wake = new(false);
    private volatile bool _dead;

    // Everything below is owned by the event thread.
    private IntPtr _display;
    private int _screen;
    private IntPtr _root;
    private IntPtr _selectionAtom;
    private IntPtr _opcodeAtom;
    private IntPtr _xembedInfoAtom;
    private IntPtr _managerAtom;
    private IntPtr _trayVisualAtom;
    private IntPtr _manager;
    private readonly Dictionary<TrayIcon, IconState> _icons = new();

    public bool IsAvailable => true;

    /// <summary>
    /// Returns a backend when an X11 tray manager is reachable, else null.
    /// Uses a short-lived probe connection; the event thread opens its own.
    /// </summary>
    public static XEmbedTrayBackend? TryCreate()
    {
        // Pure Wayland without XWayland: no DISPLAY, no XEmbed tray possible.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            return null;

        var display = X11.XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
            return null;

        try
        {
            var screen = X11.XDefaultScreen(display);
            var selection = X11.XInternAtom(display, $"_NET_SYSTEM_TRAY_S{screen}", false);
            if (X11.XGetSelectionOwner(display, selection) == IntPtr.Zero)
            {
                DiagnosticLog.Debug(Tag, "No _NET_SYSTEM_TRAY selection owner — XEmbed tray unavailable");
                return null;
            }
        }
        finally
        {
            X11.XCloseDisplay(display);
        }

        return new XEmbedTrayBackend();
    }

    private XEmbedTrayBackend()
    {
        s_instance = this;
        InstallErrorHandler();

        _thread = new Thread(EventLoop) { IsBackground = true, Name = "XEmbedTray" };
        _thread.Start();
    }

    #region X error handler

    // The stock Xlib error handler terminates the process. A tray manager can
    // die between any of our requests (panel restart), turning a benign
    // BadWindow/BadDrawable into an app crash — so we swallow errors on our
    // display and forward everything else to whatever handler was installed
    // before us (logging when there was none, which beats exiting).
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XErrorHandlerDelegate(IntPtr display, ref XErrorEvent error);

    // Rooted for the process lifetime — Xlib keeps the raw function pointer.
    private static readonly XErrorHandlerDelegate s_errorHandler = OnXError;
    private static IntPtr s_previousErrorHandler;
    private static volatile XEmbedTrayBackend? s_instance;
    private static bool s_errorHandlerInstalled;

    private static void InstallErrorHandler()
    {
        if (s_errorHandlerInstalled) return;
        s_errorHandlerInstalled = true;
        s_previousErrorHandler = X11.XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(s_errorHandler));
    }

    private static int OnXError(IntPtr display, ref XErrorEvent error)
    {
        var self = s_instance;
        if (self != null && display == self._display)
        {
            DiagnosticLog.Warn(Tag, $"Ignoring X error {error.ErrorCode} (request {error.RequestCode}) on tray display — tray manager likely restarting");
            return 0;
        }

        if (s_previousErrorHandler != IntPtr.Zero)
            return Marshal.GetDelegateForFunctionPointer<XErrorHandlerDelegate>(s_previousErrorHandler)(display, ref error);

        DiagnosticLog.Error(Tag, $"X error {error.ErrorCode} (request {error.RequestCode}, resource 0x{error.ResourceId:x})");
        return 0;
    }

    #endregion

    #region ITrayBackend (any thread → command queue)

    public void Create(TrayIcon icon) => Post(() =>
    {
        if (_icons.ContainsKey(icon)) return;
        var state = new IconState(icon);
        _icons[icon] = state;
        EnsureManager();
        Dock(state);
    });

    public void Update(TrayIcon icon) => Post(() =>
    {
        if (!_icons.TryGetValue(icon, out var state)) return;
        // Force a re-decode: the path may be unchanged but point at new pixels.
        state.Source?.Dispose();
        state.Source = null;
        state.SourcePath = null;
        ApplyTooltip(state);
        Render(state);
    });

    // The menu is rebuilt from TrayIcon.MenuItems at popup time, so there is
    // nothing to sync here.
    public void UpdateMenu(TrayIcon icon) { }

    public void Remove(TrayIcon icon) => Post(() =>
    {
        if (!_icons.Remove(icon, out var state)) return;
        Undock(state, destroyWindow: true);
        state.Source?.Dispose();
        state.Source = null;
        X11.XFlush(_display);
    });

    // XEmbed can genuinely remove an icon window at runtime, so hide and
    // permanent teardown are the same operation; Create re-docks a fresh window.
    public void Destroy(TrayIcon icon) => Remove(icon);

    private void Post(Action action)
    {
        if (_dead) return;
        _commands.Enqueue(action);
        _wake.Set();
    }

    #endregion

    #region Event thread

    private void EventLoop()
    {
        _display = X11.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
        {
            DiagnosticLog.Error(Tag, "XOpenDisplay failed on event thread — tray backend inert");
            _dead = true;
            _commands.Clear();
            return;
        }

        _screen = X11.XDefaultScreen(_display);
        _root = X11.XRootWindow(_display, _screen);
        _selectionAtom = X11.XInternAtom(_display, $"_NET_SYSTEM_TRAY_S{_screen}", false);
        _opcodeAtom = X11.XInternAtom(_display, "_NET_SYSTEM_TRAY_OPCODE", false);
        _xembedInfoAtom = X11.XInternAtom(_display, "_XEMBED_INFO", false);
        _managerAtom = X11.XInternAtom(_display, "MANAGER", false);
        _trayVisualAtom = X11.XInternAtom(_display, "_NET_SYSTEM_TRAY_VISUAL", false);

        // MANAGER announcements arrive as ClientMessages on the root window
        // under StructureNotifyMask — this is what lets us re-dock after a
        // panel restart.
        X11.XSelectInput(_display, _root, XEventMask.StructureNotifyMask);
        EnsureManager();
        X11.XFlush(_display);

        while (true)
        {
            while (_commands.TryDequeue(out var command))
            {
                try { command(); }
                catch (Exception ex) { DiagnosticLog.Error(Tag, $"Tray command failed: {ex.Message}"); }
            }

            bool sawEvent = false;
            while (X11.XPending(_display) > 0)
            {
                X11.XNextEvent(_display, out var ev);
                sawEvent = true;
                try { HandleEvent(ref ev); }
                catch (Exception ex) { DiagnosticLog.Error(Tag, $"Tray event handling failed: {ex.Message}"); }
            }

            if (!sawEvent && _commands.IsEmpty)
            {
                // Idle: with no icons docked block until a command arrives;
                // otherwise poll at the cadence GlobalHotkeyService uses.
                if (_icons.Count == 0) _wake.WaitOne();
                else _wake.WaitOne(10);
            }
        }
    }

    private void HandleEvent(ref XEvent ev)
    {
        switch (ev.Type)
        {
            case XEventType.Expose:
                if (ev.ExposeEvent.Count == 0 && FindByWindow(ev.ExposeEvent.Window) is { } exposed)
                    Render(exposed);
                break;

            case XEventType.ConfigureNotify:
                if (FindByWindow(ev.ConfigureEvent.Window) is { } resized &&
                    ev.ConfigureEvent.Width > 0 && ev.ConfigureEvent.Height > 0 &&
                    (resized.Width != ev.ConfigureEvent.Width || resized.Height != ev.ConfigureEvent.Height))
                {
                    resized.Width = ev.ConfigureEvent.Width;
                    resized.Height = ev.ConfigureEvent.Height;
                    Render(resized);
                }
                break;

            case XEventType.ButtonPress:
                if (FindByWindow(ev.ButtonEvent.Window) is { } clicked)
                    OnButtonPress(clicked, ev.ButtonEvent.Button);
                break;

            case XEventType.DestroyNotify:
                if (ev.DestroyWindowEvent.Window == _manager)
                    OnManagerLost();
                else if (FindByWindow(ev.DestroyWindowEvent.Window) is { } gone)
                    Undock(gone, destroyWindow: false); // dying tray already destroyed it
                break;

            case XEventType.ClientMessage:
                if (ev.ClientMessageEvent.MessageType == _managerAtom &&
                    (IntPtr)ev.ClientMessageEvent.Data.L1 == _selectionAtom)
                    OnManagerAppeared((IntPtr)ev.ClientMessageEvent.Data.L2);
                break;
        }
    }

    private IconState? FindByWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return null;
        foreach (var state in _icons.Values)
        {
            if (state.Window == window) return state;
        }
        return null;
    }

    #endregion

    #region Docking / manager lifecycle (event thread)

    private void EnsureManager()
    {
        if (_manager != IntPtr.Zero) return;
        _manager = X11.XGetSelectionOwner(_display, _selectionAtom);
        if (_manager != IntPtr.Zero)
        {
            // StructureNotify on the manager gets us DestroyNotify when it dies.
            X11.XSelectInput(_display, _manager, XEventMask.StructureNotifyMask);
        }
    }

    private void OnManagerAppeared(IntPtr manager)
    {
        _manager = manager;
        if (_manager == IntPtr.Zero)
        {
            EnsureManager();
            if (_manager == IntPtr.Zero) return;
        }
        else
        {
            X11.XSelectInput(_display, _manager, XEventMask.StructureNotifyMask);
        }

        DiagnosticLog.Debug(Tag, "Tray manager appeared — docking icons");
        foreach (var state in _icons.Values)
        {
            if (state.Window == IntPtr.Zero)
                Dock(state);
        }
        X11.XFlush(_display);
    }

    private void OnManagerLost()
    {
        DiagnosticLog.Debug(Tag, "Tray manager destroyed — going dormant until a new one announces itself");
        _manager = IntPtr.Zero;
        // Destroy any icon windows the dying tray reparented back to the root
        // rather than destroying; leftovers would float on the desktop. Ones
        // it did destroy just BadWindow, which our error handler swallows.
        foreach (var state in _icons.Values)
            Undock(state, destroyWindow: true);
        X11.XFlush(_display);
    }

    private void Dock(IconState state)
    {
        if (_manager == IntPtr.Zero)
        {
            DiagnosticLog.Debug(Tag, $"No tray manager for '{state.Owner.Id}' — dormant until MANAGER announcement");
            return;
        }

        ChooseVisual(state);

        // BackPixel/BorderPixel must be set explicitly: a depth-32 window
        // otherwise BadMatches on defaults inherited from a depth-24 parent.
        var attributes = new XSetWindowAttributes
        {
            BackgroundPixel = 0,
            BorderPixel = 0,
            EventMask = XEventMask.ExposureMask | XEventMask.ButtonPressMask | XEventMask.StructureNotifyMask,
            Colormap = state.Colormap,
        };
        ulong valueMask = CWBackPixel | CWBorderPixel | CWEventMask;
        if (state.Colormap != IntPtr.Zero)
            valueMask |= CWColormap;

        state.Window = X11.XCreateWindow(
            _display, _root, 0, 0, (uint)state.Width, (uint)state.Height, 0,
            state.Depth, XWindowClass.InputOutput, state.Visual, valueMask, ref attributes);
        if (state.Window == IntPtr.Zero)
        {
            DiagnosticLog.Error(Tag, $"XCreateWindow failed for tray icon '{state.Owner.Id}'");
            return;
        }

        state.Gc = X11.XCreateGC(_display, state.Window, 0, IntPtr.Zero);
        ApplyTooltip(state);

        // XEmbed handshake: version 0, XEMBED_MAPPED — the tray maps us after
        // embedding, so we never XMapWindow ourselves.
        unsafe
        {
            // Format-32 property data is passed as native longs.
            var info = stackalloc long[2] { 0, XembedMapped };
            X11.XChangeProperty(_display, state.Window, _xembedInfoAtom, _xembedInfoAtom,
                32, PropModeReplace, (IntPtr)info, 2);
        }

        var ev = new XEvent();
        ev.ClientMessageEvent.Type = XEventType.ClientMessage;
        ev.ClientMessageEvent.Window = _manager;
        ev.ClientMessageEvent.MessageType = _opcodeAtom;
        ev.ClientMessageEvent.Format = 32;
        ev.ClientMessageEvent.Data.L0 = 0; // CurrentTime
        ev.ClientMessageEvent.Data.L1 = SystemTrayRequestDock;
        ev.ClientMessageEvent.Data.L2 = state.Window.ToInt64();
        X11.XSendEvent(_display, _manager, false, 0, ref ev);
        X11.XFlush(_display);

        DiagnosticLog.Debug(Tag, $"Requested dock for tray icon '{state.Owner.Id}' (window 0x{state.Window:x})");
    }

    private void Undock(IconState state, bool destroyWindow)
    {
        if (state.Gc != IntPtr.Zero)
        {
            X11.XFreeGC(_display, state.Gc);
            state.Gc = IntPtr.Zero;
        }
        if (destroyWindow && state.Window != IntPtr.Zero)
            X11.XDestroyWindow(_display, state.Window);
        state.Window = IntPtr.Zero;
        if (state.Colormap != IntPtr.Zero)
        {
            X11.XFreeColormap(_display, state.Colormap);
            state.Colormap = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Uses the visual the tray advertises via _NET_SYSTEM_TRAY_VISUAL (ARGB
    /// on composited trays → real transparency). Per spec, when the property
    /// is absent the icon must use the default visual; transparent pixels
    /// then flatten to black — acceptable for a legacy-tray fallback.
    /// </summary>
    private void ChooseVisual(IconState state)
    {
        state.Visual = X11.XDefaultVisual(_display, _screen);
        state.Depth = X11.XDefaultDepth(_display, _screen);
        state.Colormap = IntPtr.Zero;

        var visualId = ReadTrayVisualId();
        if (visualId == 0) return;

        var template = new XVisualInfo { VisualId = visualId };
        var infoPtr = X11.XGetVisualInfo(_display, VisualIdMask, ref template, out var count);
        if (infoPtr == IntPtr.Zero) return;
        try
        {
            if (count <= 0) return;
            var info = Marshal.PtrToStructure<XVisualInfo>(infoPtr);
            if (info.Visual == state.Visual) return; // default visual, nothing to set up
            state.Visual = info.Visual;
            state.Depth = info.Depth;
            state.Colormap = X11.XCreateColormap(_display, _root, info.Visual, AllocNone);
        }
        finally
        {
            X11.XFree(infoPtr);
        }
    }

    private ulong ReadTrayVisualId()
    {
        if (X11.XGetWindowProperty(_display, _manager, _trayVisualAtom, 0, 1, false, IntPtr.Zero,
                out _, out var format, out var nitems, out _, out var prop) != 0)
            return 0;
        if (prop == IntPtr.Zero) return 0;
        try
        {
            if (format != 32 || nitems == IntPtr.Zero) return 0;
            return (ulong)Marshal.ReadInt64(prop);
        }
        finally
        {
            X11.XFree(prop);
        }
    }

    private void ApplyTooltip(IconState state)
    {
        if (state.Window == IntPtr.Zero) return;
        // Not standardized, but common XEmbed trays (tint2, trayer, ...) show
        // the icon window's WM_NAME as the tooltip.
        var name = state.Owner.Tooltip.Length > 0 ? state.Owner.Tooltip
            : state.Owner.Title.Length > 0 ? state.Owner.Title
            : state.Owner.Id;
        X11.XStoreName(_display, state.Window, name);
    }

    #endregion

    #region Rendering (event thread)

    private static readonly SKSamplingOptions s_sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private void Render(IconState state)
    {
        if (state.Window == IntPtr.Zero) return;
        EnsureSource(state);

        int width = state.Width, height = state.Height;
        using var frame = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(frame))
        {
            canvas.Clear(SKColors.Transparent);
            if (state.Source is { } source && source.Width > 0 && source.Height > 0)
            {
                // Aspect-fit, centered.
                float scale = Math.Min((float)width / source.Width, (float)height / source.Height);
                float w = source.Width * scale, h = source.Height * scale;
                var dest = SKRect.Create((width - w) / 2f, (height - h) / 2f, w, h);
                // DrawBitmap has no SKSamplingOptions overload in SkiaSharp 3.x.
                using var sourceImage = SKImage.FromBitmap(source);
                canvas.DrawImage(sourceImage, dest, s_sampling);
            }
        }

        // XDestroyImage frees the buffer, so it must be unmanaged and copied.
        int stride = frame.RowBytes;
        int size = stride * height;
        var pixels = Marshal.AllocHGlobal(size);
        unsafe
        {
            Buffer.MemoryCopy((void*)frame.GetPixels(), (void*)pixels, size, size);
        }

        var image = X11.XCreateImage(
            _display, state.Visual, (uint)state.Depth, X11.ZPixmap, 0,
            pixels, (uint)width, (uint)height, 32, stride);
        if (image == IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pixels);
            DiagnosticLog.Error(Tag, "XCreateImage failed for tray icon");
            return;
        }

        X11.XPutImage(_display, state.Window, state.Gc, image, 0, 0, 0, 0, (uint)width, (uint)height);
        X11.XDestroyImage(image);
        X11.XFlush(_display);
    }

    private void EnsureSource(IconState state)
    {
        var iconPath = state.Owner.IconPath ?? string.Empty;
        // SourcePath is also set on failed decodes so a broken icon doesn't
        // re-hit the disk on every Expose; Update() clears it to force a retry.
        if (state.SourcePath == iconPath) return;

        state.Source?.Dispose();
        state.Source = null;
        state.SourcePath = iconPath;
        if (iconPath.Length == 0) return;

        var file = ResolveIconFile(iconPath);
        if (file == null)
        {
            DiagnosticLog.Warn(Tag, $"Tray icon '{iconPath}' not found (paths and PNG theme icons are supported; SVG-only theme icons are not)");
            return;
        }

        state.Source = SKBitmap.Decode(file);
        if (state.Source == null)
            DiagnosticLog.Warn(Tag, $"Failed to decode tray icon '{file}'");
    }

    private static readonly string[] s_iconSizes = { "48x48", "32x32", "24x24", "22x22", "16x16" };
    private static readonly string[] s_iconCategories = { "apps", "status", "actions", "devices" };

    private static string? ResolveIconFile(string iconPath)
    {
        if (File.Exists(iconPath)) return iconPath;
        if (iconPath.Contains('/')) return null;

        foreach (var dataDir in IconDataDirs())
        {
            foreach (var size in s_iconSizes)
            {
                foreach (var category in s_iconCategories)
                {
                    var candidate = Path.Combine(dataDir, "icons", "hicolor", size, category, iconPath + ".png");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            var pixmap = Path.Combine(dataDir, "pixmaps", iconPath + ".png");
            if (File.Exists(pixmap)) return pixmap;
        }
        return null;
    }

    private static IEnumerable<string> IconDataDirs()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            yield return Path.Combine(home, ".local", "share");

        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (string.IsNullOrEmpty(dataDirs))
            dataDirs = "/usr/local/share:/usr/share";
        foreach (var dir in dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries))
            yield return dir;
    }

    #endregion

    #region Clicks / menu

    // GtkContextMenuService needs a live GTK; LinuxApplication normally
    // gtk_init_checks at startup, so a loadable libgtk-3 is the best proxy we
    // have. Without it, clicks fall back to raising Activated.
    private static readonly bool s_gtkAvailable = NativeLibrary.TryLoad("libgtk-3.so.0", out _);

    private void OnButtonPress(IconState state, uint button)
    {
        var icon = state.Owner;
        switch (button)
        {
            case 1:
                icon.RaiseActivated();
                break;

            case 3:
                if (s_gtkAvailable && LinuxDispatcher.Main is { } main)
                {
                    // MenuItems is read on the main thread, where apps mutate it.
                    main.Dispatch(() =>
                    {
                        if (icon.MenuItems.Count == 0)
                        {
                            icon.RaiseActivated();
                            return;
                        }

                        var items = new List<GtkMenuItem>(icon.MenuItems.Count);
                        foreach (var item in icon.MenuItems)
                        {
                            items.Add(item.IsSeparator
                                ? GtkMenuItem.Separator
                                : new GtkMenuItem(item.Text ?? string.Empty, item.Action, item.IsEnabled));
                        }
                        GtkContextMenuService.ShowContextMenu(items);
                    });
                }
                else
                {
                    icon.RaiseActivated();
                }
                break;
        }
    }

    #endregion
}
