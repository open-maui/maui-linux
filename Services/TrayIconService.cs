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

    /// <summary>Fires on left-click when the backend exposes that signal.</summary>
    public event EventHandler? Activated;

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
    // Keep GCHandles for click delegates so the GC doesn't collect them while
    // GTK still holds a function-pointer reference.
    private readonly Dictionary<IntPtr, List<GCHandle>> _delegateHandles = new();

    private readonly NewDelegate _new;
    private readonly SetStatusDelegate _setStatus;
    private readonly SetStringDelegate _setIcon;
    private readonly SetStringDelegate _setTitle;
    private readonly SetStringDelegate _setLabel;
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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GtkActivateDelegate(IntPtr widget, IntPtr userData);

    public AppIndicatorBackend(IntPtr libHandle, string libName)
    {
        _libHandle = libHandle;
        _new = Marshal.GetDelegateForFunctionPointer<NewDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_new"));
        _setStatus = Marshal.GetDelegateForFunctionPointer<SetStatusDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_status"));
        _setIcon = Marshal.GetDelegateForFunctionPointer<SetStringDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_icon_full"));
        _setTitle = Marshal.GetDelegateForFunctionPointer<SetStringDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_title"));
        _setLabel = Marshal.GetDelegateForFunctionPointer<SetStringDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_label"));
        _setMenu = Marshal.GetDelegateForFunctionPointer<SetMenuDelegate>(NativeLibrary.GetExport(libHandle, "app_indicator_set_menu"));

        DiagnosticLog.Debug("TrayIconService", $"AppIndicator backend ready ({libName})");
    }

    public void Create(TrayIcon icon)
    {
        if (_indicators.ContainsKey(icon)) return;

        var native = _new(icon.Id, icon.IconPath ?? string.Empty, CategoryApplicationStatus);
        if (native == IntPtr.Zero)
        {
            DiagnosticLog.Error("TrayIconService", $"app_indicator_new returned NULL for {icon.Id}");
            return;
        }
        _indicators[icon] = native;
        _delegateHandles[native] = new List<GCHandle>();

        // libappindicator requires a menu to be set before most
        // StatusNotifierWatcher hosts will draw the icon. Build an initial one
        // and apply property values.
        BuildMenu(icon, native);
        Update(icon);
        _setStatus(native, StatusActive);
    }

    public void Update(TrayIcon icon)
    {
        if (!_indicators.TryGetValue(icon, out var native)) return;

        if (!string.IsNullOrEmpty(icon.IconPath))
            _setIcon(native, icon.IconPath);
        if (!string.IsNullOrEmpty(icon.Title))
            _setTitle(native, icon.Title);
        // Tooltip → "label" on the indicator (text drawn alongside the icon on
        // desktops that show one).
        if (!string.IsNullOrEmpty(icon.Tooltip))
            _setLabel(native, icon.Tooltip);
    }

    public void UpdateMenu(TrayIcon icon)
    {
        if (!_indicators.TryGetValue(icon, out var native)) return;
        BuildMenu(icon, native);
    }

    private void BuildMenu(TrayIcon icon, IntPtr indicator)
    {
        // Free previously-pinned delegate handles for this indicator. The old
        // GtkMenu is unref'd when we set_menu the new one — libappindicator
        // takes the ref.
        if (_delegateHandles.TryGetValue(indicator, out var oldHandles))
        {
            foreach (var h in oldHandles) if (h.IsAllocated) h.Free();
            oldHandles.Clear();
        }

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
                    var capturedAction = item.Action;
                    GtkActivateDelegate handler = (w, _) =>
                    {
                        try
                        {
                            if (LinuxDispatcher.IsMainThread) capturedAction();
                            else LinuxDispatcher.Main?.Dispatch(capturedAction);
                        }
                        catch (Exception ex)
                        {
                            DiagnosticLog.Error("TrayIconService", $"Menu item handler threw: {ex.Message}");
                        }
                    };
                    var ptr = Marshal.GetFunctionPointerForDelegate(handler);
                    var pinned = GCHandle.Alloc(handler);
                    _delegateHandles[indicator].Add(pinned);
                    g_signal_connect_data(widget, "activate", ptr, IntPtr.Zero, IntPtr.Zero, 0);
                }
            }
            gtk_widget_show(widget);
            gtk_menu_shell_append(menu, widget);
        }
        gtk_widget_show(menu);
        _setMenu(indicator, menu);
    }

    public void Remove(TrayIcon icon)
    {
        if (!_indicators.TryGetValue(icon, out var native)) return;
        _setStatus(native, StatusPassive);
        // app_indicator gets g_object_unref'd implicitly when we drop our last
        // reference. Releasing it explicitly is tricky because AppIndicator
        // owns a g_object_ref on its menu; the safer path is to let the GTK
        // main loop reap it.
        _indicators.Remove(icon);
        if (_delegateHandles.Remove(native, out var handles))
            foreach (var h in handles) if (h.IsAllocated) h.Free();
    }
}
