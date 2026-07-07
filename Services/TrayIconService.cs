// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// System tray icon. Backed by libappindicator3 / libayatana-appindicator3
/// (StatusNotifierItem) on modern desktops — GNOME with the AppIndicator
/// extension, KDE, Cinnamon, MATE, XFCE all show it. Apps create one per
/// process and call <see cref="Show"/>.
///
/// Property changes apply immediately. Disposing or calling <see cref="Hide"/>
/// removes the icon. <see cref="MenuItems"/> is mutable; replacing entries and
/// calling <see cref="UpdateMenu"/> rebuilds the menu.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    public string Id { get; }
    public string Title { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to a PNG/SVG icon, or a freedesktop icon name (e.g.
    /// "application-default-icon") that the backend resolves via the current
    /// GTK icon theme.
    /// </summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>Menu items shown when the user activates the icon.</summary>
    public List<TrayMenuItem> MenuItems { get; } = new();

    /// <summary>
    /// Fires on left-click when the backend exposes that signal. The
    /// AppIndicator backend never raises this: StatusNotifierItem left-click
    /// opens the menu by design and libappindicator exposes no activation
    /// signal. The event exists for future backends (e.g. an XEmbed fallback).
    /// </summary>
    public event EventHandler? Activated;

    // Not called by the AppIndicator backend (see Activated). Kept for future
    // backends that do surface an activation signal.
    internal void RaiseActivated()
    {
        if (LinuxDispatcher.IsMainThread)
            Activated?.Invoke(this, EventArgs.Empty);
        else
            LinuxDispatcher.Main?.Dispatch(() => Activated?.Invoke(this, EventArgs.Empty));
    }

    private readonly ITrayBackend _backend;
    private bool _shown;
    private bool _disposed;

    public TrayIcon(string id)
    {
        Id = id;
        _backend = TrayIconService.Backend;
    }

    public void Show()
    {
        if (_shown || _disposed) return;
        _backend.Create(this);
        _shown = true;
    }

    public void Hide()
    {
        if (!_shown) return;
        _backend.Remove(this);
        _shown = false;
    }

    /// <summary>Push current property values into the platform icon.</summary>
    public void Update()
    {
        if (!_shown) return;
        _backend.Update(this);
    }

    /// <summary>Re-build the menu from <see cref="MenuItems"/>.</summary>
    public void UpdateMenu()
    {
        if (!_shown) return;
        _backend.UpdateMenu(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hide();
    }
}

/// <summary>
/// Backend strategy used by <see cref="TrayIcon"/>. Probed once at startup;
/// apps don't deal with this directly.
/// </summary>
internal interface ITrayBackend
{
    bool IsAvailable { get; }
    void Create(TrayIcon icon);
    void Update(TrayIcon icon);
    void UpdateMenu(TrayIcon icon);
    void Remove(TrayIcon icon);
}

/// <summary>
/// Factory + backend probe. Order of preference:
///   1. libayatana-appindicator3.so.1 (Ubuntu/Debian + most modern installs)
///   2. libappindicator3.so.1         (Fedora, older distros)
///   3. NullTrayBackend                (no tray available — Show/Hide no-op)
/// </summary>
public static class TrayIconService
{
    private static ITrayBackend? s_backend;
    private static readonly Lock s_lock = new();

    internal static ITrayBackend Backend
    {
        get
        {
            if (s_backend != null) return s_backend;
            lock (s_lock)
            {
                if (s_backend != null) return s_backend;
                s_backend = ProbeBackend();
                return s_backend;
            }
        }
    }

    private static ITrayBackend ProbeBackend()
    {
        foreach (var lib in new[] { "libayatana-appindicator3.so.1", "libappindicator3.so.1" })
        {
            if (NativeLibrary.TryLoad(lib, out var handle))
            {
                try { return new AppIndicatorBackend(handle, lib); }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("TrayIconService", $"AppIndicator backend init failed ({lib}): {ex.Message}");
                    NativeLibrary.Free(handle);
                }
            }
        }

        DiagnosticLog.Warn("TrayIconService", "No supported tray backend found — TrayIcon will be a no-op");
        return new NullTrayBackend();
    }

    /// <summary>True when a real tray backend is available on this desktop.</summary>
    public static bool IsAvailable => Backend.IsAvailable;
}

internal sealed class NullTrayBackend : ITrayBackend
{
    public bool IsAvailable => false;
    public void Create(TrayIcon icon) { }
    public void Update(TrayIcon icon) { }
    public void UpdateMenu(TrayIcon icon) { }
    public void Remove(TrayIcon icon) { }
}

/// <summary>
/// libappindicator3 / libayatana-appindicator3 binding. Both ABIs are
/// identical — ayatana is a hard fork that kept the C function names. We
/// dlopen one or the other and resolve symbols by name.
///
/// On the wire the indicator becomes a <c>StatusNotifierItem</c> on the
/// session bus, registered with <c>org.kde.StatusNotifierWatcher</c>. The
/// library handles GVariant marshalling, watcher registration, and the menu
/// (DBusMenu) protocol for us; we just call setter functions.
/// </summary>
internal sealed class AppIndicatorBackend : ITrayBackend
{
    private const string LibGtk3 = "libgtk-3.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";

    private readonly IntPtr _libHandle;
    private readonly Dictionary<TrayIcon, IntPtr> _indicators = new();

    private readonly NewDelegate _new;
    private readonly SetStatusDelegate _setStatus;
    private readonly SetTwoStringsDelegate _setIcon;
    private readonly SetStringDelegate _setTitle;
    private readonly SetTwoStringsDelegate _setLabel;
    private readonly SetMenuDelegate _setMenu;

    public bool IsAvailable => true;

    // libappindicator delegate shapes — UTF-8 ANSI is what the C library
    // expects (it stores them as plain const char*).
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr NewDelegate(string id, string iconName, int category);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetStatusDelegate(IntPtr self, int status);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate void SetStringDelegate(IntPtr self, string value);
    // app_indicator_set_icon_full(self, icon_name, icon_desc) and
    // app_indicator_set_label(self, label, guide) both take a second string
    // (nullable in C).
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate void SetTwoStringsDelegate(IntPtr self, string value, string? second);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetMenuDelegate(IntPtr self, IntPtr menu);

    // app_indicator_category enum
    private const int CategoryApplicationStatus = 0;
    // app_indicator_status enum
    private const int StatusActive = 1;
    private const int StatusPassive = 0;

    [DllImport(LibGtk3)] private static extern IntPtr gtk_menu_new();
    [DllImport(LibGtk3, CharSet = CharSet.Ansi)] private static extern IntPtr gtk_menu_item_new_with_label(string label);
    [DllImport(LibGtk3)] private static extern IntPtr gtk_separator_menu_item_new();
    [DllImport(LibGtk3)] private static extern void gtk_widget_show(IntPtr widget);
    [DllImport(LibGtk3)] private static extern void gtk_widget_set_sensitive(IntPtr widget, [MarshalAs(UnmanagedType.Bool)] bool sensitive);
    [DllImport(LibGtk3)] private static extern void gtk_menu_shell_append(IntPtr menu, IntPtr child);

    [DllImport(LibGObject, CharSet = CharSet.Ansi, EntryPoint = "g_signal_connect_data")]
    private static extern ulong g_signal_connect_data(IntPtr instance, string detailedSignal, IntPtr cHandler, IntPtr data, IntPtr destroyData, int connectFlags);
    [DllImport(LibGObject)] private static extern void g_object_unref(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GtkActivateDelegate(IntPtr widget, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GClosureNotifyDelegate(IntPtr data, IntPtr closure);

    // One static activate handler + one destroy-notify shared by every menu
    // item. The per-item Action travels as user_data (a GCHandle), and GTK
    // calls the notify when it destroys the closure — i.e. when the menu item
    // itself dies, however long a popped-open old menu keeps it alive. That
    // makes GTK the owner of the handle's lifetime, so we never free one that
    // a live widget can still call through. Rooted in static fields so the
    // function pointers stay valid for the process lifetime.
    private static readonly GtkActivateDelegate s_menuItemActivate = OnMenuItemActivate;
    private static readonly GClosureNotifyDelegate s_menuItemDestroyed = OnMenuItemClosureDestroyed;
    private static readonly IntPtr s_menuItemActivatePtr = Marshal.GetFunctionPointerForDelegate(s_menuItemActivate);
    private static readonly IntPtr s_menuItemDestroyedPtr = Marshal.GetFunctionPointerForDelegate(s_menuItemDestroyed);

    private static void OnMenuItemActivate(IntPtr widget, IntPtr userData)
    {
        if (GCHandle.FromIntPtr(userData).Target is not Action action) return;
        try
        {
            if (LinuxDispatcher.IsMainThread) action();
            else LinuxDispatcher.Main?.Dispatch(action);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("TrayIconService", $"Menu item handler threw: {ex.Message}");
        }
    }

    private static void OnMenuItemClosureDestroyed(IntPtr data, IntPtr closure)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (handle.IsAllocated) handle.Free();
    }

    // GTK and libappindicator are not thread-safe; everything below must run
    // on the GLib main loop. Callers may be on any thread (e.g. after an
    // await that resumed on the pool), so marshal here rather than trusting
    // them. Before the dispatcher exists the app is single-threaded startup
    // code, so inline is safe.
    private static void RunOnMain(Action action)
    {
        if (LinuxDispatcher.IsMainThread || LinuxDispatcher.Main is not { } main) action();
        else main.Dispatch(action);
    }

    public AppIndicatorBackend(IntPtr libHandle, string libName)
    {
        _libHandle = libHandle;
        _new = Marshal.GetDelegateForFunctionPointer<NewDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_new"));
        _setStatus = Marshal.GetDelegateForFunctionPointer<SetStatusDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_status"));
        _setIcon = Marshal.GetDelegateForFunctionPointer<SetTwoStringsDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_icon_full"));
        _setTitle = Marshal.GetDelegateForFunctionPointer<SetStringDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_title"));
        _setLabel = Marshal.GetDelegateForFunctionPointer<SetTwoStringsDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_label"));
        _setMenu = Marshal.GetDelegateForFunctionPointer<SetMenuDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_menu"));

        DiagnosticLog.Debug("TrayIconService", $"AppIndicator backend ready ({libName})");
    }

    public void Create(TrayIcon icon) => RunOnMain(() =>
    {
        if (_indicators.ContainsKey(icon)) return;

        var native = _new(icon.Id, icon.IconPath ?? string.Empty, CategoryApplicationStatus);
        if (native == IntPtr.Zero)
        {
            DiagnosticLog.Error("TrayIconService", $"app_indicator_new returned NULL for {icon.Id}");
            return;
        }
        _indicators[icon] = native;

        // libappindicator requires a menu to be set before most
        // StatusNotifierWatcher hosts will draw the icon. Build an initial one
        // and apply property values.
        BuildMenu(icon, native);
        UpdateCore(icon, native);
        _setStatus(native, StatusActive);
    });

    public void Update(TrayIcon icon) => RunOnMain(() =>
    {
        if (_indicators.TryGetValue(icon, out var native))
            UpdateCore(icon, native);
    });

    private void UpdateCore(TrayIcon icon, IntPtr native)
    {
        if (!string.IsNullOrEmpty(icon.IconPath))
            _setIcon(native, icon.IconPath, icon.Title.Length > 0 ? icon.Title : icon.Id);
        if (!string.IsNullOrEmpty(icon.Title))
            _setTitle(native, icon.Title);
        // Tooltip → "label" on the indicator (text drawn alongside the icon on
        // desktops that show one). The guide is used for width reservation;
        // the label itself is a fine guide.
        if (!string.IsNullOrEmpty(icon.Tooltip))
            _setLabel(native, icon.Tooltip, icon.Tooltip);
    }

    public void UpdateMenu(TrayIcon icon) => RunOnMain(() =>
    {
        if (!_indicators.TryGetValue(icon, out var native)) return;
        BuildMenu(icon, native);
    });

    private void BuildMenu(TrayIcon icon, IntPtr indicator)
    {
        // The previous GtkMenu is unref'd by set_menu below; its items'
        // closures are destroyed with it (or once a popped-open copy closes),
        // and each destruction frees that item's action GCHandle via
        // s_menuItemDestroyed.
        var menu = gtk_menu_new();
        foreach (var item in icon.MenuItems)
        {
            IntPtr widget;
            if (item.IsSeparator)
            {
                widget = gtk_separator_menu_item_new();
            }
            else
            {
                widget = gtk_menu_item_new_with_label(item.Text ?? string.Empty);
                gtk_widget_set_sensitive(widget, item.IsEnabled);

                if (item.Action != null)
                {
                    var handle = GCHandle.Alloc(item.Action);
                    g_signal_connect_data(widget, "activate", s_menuItemActivatePtr,
                        GCHandle.ToIntPtr(handle), s_menuItemDestroyedPtr, 0);
                }
            }
            gtk_widget_show(widget);
            gtk_menu_shell_append(menu, widget);
        }
        gtk_widget_show(menu);
        _setMenu(indicator, menu);
    }

    public void Remove(TrayIcon icon) => RunOnMain(() =>
    {
        if (!_indicators.TryGetValue(icon, out var native)) return;
        _setStatus(native, StatusPassive);
        _indicators.Remove(icon);
        // Drop the ref app_indicator_new gave us. The indicator releases its
        // menu ref in dispose; menu-item closure destruction then frees the
        // action GCHandles via s_menuItemDestroyed.
        g_object_unref(native);
    });
}
