// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Tmds.DBus;

namespace Microsoft.Maui.Platform.Linux.Services.AtSpi;

/// <summary>
/// The AT-SPI2 bridge: what GTK's atk-bridge and Qt's QSpiAccessibleBridge are for their
/// toolkits. It connects to the accessibility bus (its address from the session bus's
/// <c>org.a11y.Bus</c>), exports the application root at <c>/org/a11y/atspi/accessible/root</c>
/// and embeds it in the desktop through the registry's <c>Socket.Embed</c>, so screen readers
/// (Orca) and other assistive technology find the app. Every window is a frame under the root,
/// and every view of its tree an accessible object below it, exported when a client first
/// reaches it (as a child, a parent, the object at a point or the focus) and answering the
/// Accessible, Component, Action, Value and Text interfaces from the view's
/// <see cref="IAccessible"/> model, which is read on the UI thread. Focus, state, name,
/// children and window activation changes are emitted as AT-SPI events.
/// </summary>
/// <remarks>
/// It starts when assistive technology is running (<c>org.a11y.Status</c>'s IsEnabled or
/// ScreenReaderEnabled, followed while the app runs, as Qt does), or always with
/// <c>OPENMAUI_A11Y=1</c>; <c>OPENMAUI_A11Y=0</c> turns it off.
/// </remarks>
internal sealed partial class AtSpiBridge
{
    internal const string RootPath = "/org/a11y/atspi/accessible/root";
    internal const string NullPath = "/org/a11y/atspi/null";
    private const string CachePath = "/org/a11y/atspi/cache";
    private const string ObjectPathPrefix = "/org/a11y/atspi/accessible/";

    private static int s_starting;
    private static AtSpiBridge? s_instance;

    /// <summary>The running bridge, or null when no assistive technology asked for one.</summary>
    internal static AtSpiBridge? Instance => Volatile.Read(ref s_instance);

    private readonly Connection _connection;
    private readonly string _busName;
    private readonly AtSpiRoot _root;
    private readonly ConditionalWeakTable<IAccessible, AtSpiNode> _nodes = new();
    private readonly Dictionary<ObjectPath, AtSpiNode> _byPath = new();
    private readonly List<AtSpiNode> _pending = new();
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _registering = new(1, 1);
    private readonly ConditionalWeakTable<WindowContext, WindowAccessible> _windows = new();
    private long _nextId;
    private int _createdSinceSweep;

    private AtSpiBridge(Connection connection, string busName, string appName)
    {
        _connection = connection;
        _busName = busName;
        _root = new AtSpiRoot(this, appName);
    }

    /// <summary>The application root's own reference.</summary>
    internal (string, ObjectPath) RootReference => (_busName, new ObjectPath(RootPath));

    /// <summary>The null reference (no object).</summary>
    internal (string, ObjectPath) NullReference => (_busName, new ObjectPath(NullPath));

    /// <summary>The desktop the registry embedded the app in (the root's parent).</summary>
    internal (string, ObjectPath) Desktop { get; private set; } = ("org.a11y.atspi.Registry", new ObjectPath(RootPath));

    // ---- Start ----------------------------------------------------------------------------

    /// <summary>
    /// Starts the bridge in the background when assistive technology is running, and later when
    /// it starts. Never throws: without an accessibility bus the app runs without the bridge.
    /// </summary>
    internal static void StartWhenRequested(string appName)
    {
        var setting = Environment.GetEnvironmentVariable("OPENMAUI_A11Y");
        if (setting == "0")
            return;
        if (Interlocked.Exchange(ref s_starting, 1) == 1)
            return;
        bool forced = setting == "1";
        _ = Task.Run(async () =>
        {
            try
            {
                var session = await Portal.SessionBus.GetAsync().ConfigureAwait(false);
                var bus = session.Connection.CreateProxy<IA11yBus>("org.a11y.Bus", new ObjectPath("/org/a11y/bus"));
                var status = session.Connection.CreateProxy<IA11yStatus>("org.a11y.Bus", new ObjectPath("/org/a11y/bus"));
                if (!forced && !await IsRequestedAsync(status).ConfigureAwait(false))
                {
                    // Qt's way: start when a screen reader (or another tool) turns accessibility on.
                    IDisposable? watch = null;
                    watch = await status.WatchPropertiesAsync(changes =>
                    {
                        if (changes.Changed.Any(c => c.Key is "IsEnabled" or "ScreenReaderEnabled" && c.Value is true))
                        {
                            watch?.Dispose();
                            _ = Task.Run(() => ConnectAsync(bus, appName));
                        }
                    }).ConfigureAwait(false);
                    return;
                }
                await ConnectAsync(bus, appName).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("AtSpi", $"No accessibility bus: {ex.GetBaseException().Message}");
            }
        });
    }

    private static async Task<bool> IsRequestedAsync(IA11yStatus status)
    {
        try
        {
            var all = await status.GetAllAsync().ConfigureAwait(false);
            return all.IsEnabled || all.ScreenReaderEnabled;
        }
        catch (DBusException)
        {
            return false;
        }
    }

    private static async Task ConnectAsync(IA11yBus bus, string appName)
    {
        if (Instance != null)
            return;
        try
        {
            var address = await bus.GetAddressAsync().ConfigureAwait(false);
            var bridge = await AttachAsync(address, appName).ConfigureAwait(false);
            var socket = bridge._connection.CreateProxy<IAtSpiSocket>("org.a11y.atspi.Registry", new ObjectPath(RootPath));
            bridge.Desktop = (await socket.EmbedAsync(bridge.RootReference).ConfigureAwait(false)).Item1;
            DiagnosticLog.Info("AtSpi", $"Exported to the accessibility bus as {bridge._busName}");
        }
        catch (Exception ex)
        {
            Volatile.Write(ref s_instance, null);
            DiagnosticLog.Warn("AtSpi", $"Connecting to the accessibility bus failed: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>
    /// Connects to the bus at <paramref name="address"/>, exports the root and the cache and makes
    /// this the running bridge (without embedding it in a desktop: tests attach to a private bus).
    /// </summary>
    internal static async Task<AtSpiBridge> AttachAsync(string address, string appName)
    {
        var connection = new Connection(address);
        var info = await connection.ConnectAsync().ConfigureAwait(false);
        var bridge = new AtSpiBridge(connection, info.LocalName, appName);
        await connection.RegisterObjectAsync(bridge._root).ConfigureAwait(false);
        await connection.RegisterObjectAsync(new AtSpiCache()).ConfigureAwait(false);
        Volatile.Write(ref s_instance, bridge);
        return bridge;
    }

    /// <summary>Stops the running bridge and closes its connection.</summary>
    internal static void Detach()
    {
        var bridge = Interlocked.Exchange(ref s_instance, null);
        bridge?._connection.Dispose();
    }

    /// <summary>The bridge's unique name on its bus.</summary>
    internal string BusName => _busName;

    // ---- The UI thread --------------------------------------------------------------------

    /// <summary>Runs <paramref name="read"/> on the UI thread (the view tree is not thread-safe).</summary>
    internal static Task<T> OnUi<T>(Func<T> read)
    {
        var dispatcher = LinuxDispatcher.Main;
        // Without a running event loop nothing runs what is dispatched (tests): read here.
        if (dispatcher == null || LinuxDispatcher.IsMainThread || !LinuxApplication.IsEventLoopRunning)
        {
            try
            {
                return Task.FromResult(read());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.Dispatch(() =>
            {
                try
                {
                    done.TrySetResult(read());
                }
                catch (Exception ex)
                {
                    done.TrySetException(ex);
                }
            }))
            done.TrySetException(new InvalidOperationException("The UI thread is not running"));
        return done.Task;
    }

    // ---- Objects --------------------------------------------------------------------------

    /// <summary>
    /// The reference to <paramref name="accessible"/> (null: the null reference), creating its
    /// exported object if it has none; the object is registered on the bus before the reply that
    /// carries the reference is sent (<see cref="FlushAsync"/>).
    /// </summary>
    internal (string, ObjectPath) Ref(IAccessible? accessible) =>
        accessible == null ? NullReference : (_busName, NodeOf(accessible).ObjectPath);

    internal AtSpiNode NodeOf(IAccessible accessible)
    {
        lock (_gate)
        {
            if (_nodes.TryGetValue(accessible, out var node))
                return node;
            var path = new ObjectPath(ObjectPathPrefix + (++_nextId).ToString(CultureInfo.InvariantCulture));
            node = new AtSpiNode(this, accessible, path);
            _nodes.Add(accessible, node);
            _byPath[path] = node;
            _pending.Add(node);
            _createdSinceSweep++;
            return node;
        }
    }

    /// <summary>Registers the objects created since the last flush, and drops those whose views are gone.</summary>
    internal async Task FlushAsync()
    {
        List<AtSpiNode> pending;
        List<AtSpiNode>? dead = null;
        lock (_gate)
        {
            if (_pending.Count == 0 && _createdSinceSweep < 512)
                return;
            pending = new List<AtSpiNode>(_pending);
            _pending.Clear();
            if (_createdSinceSweep >= 512)
            {
                _createdSinceSweep = 0;
                dead = _byPath.Values.Where(n => !n.IsAlive).ToList();
                foreach (var node in dead)
                    _byPath.Remove(node.ObjectPath);
            }
        }
        await _registering.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var node in pending)
            {
                try
                {
                    await _connection.RegisterObjectAsync(node).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // Already registered.
                }
            }
            if (dead is { Count: > 0 })
                _connection.UnregisterObjects(dead.Select(n => n.ObjectPath));
        }
        finally
        {
            _registering.Release();
        }
    }

    /// <summary>The windows, in the application's order, as accessible frames.</summary>
    internal IReadOnlyList<IAccessible> Windows()
    {
        var app = LinuxApplication.Current;
        if (app == null)
            return Array.Empty<IAccessible>();
        var windows = new List<IAccessible>();
        foreach (var context in app.WindowContexts)
        {
            // A native window, or GTK mode's GTK window (whose context has no native window).
            if (context.DisplayWindow == null && context.GtkWindow == null)
                continue;
            windows.Add(WindowOf(context));
        }
        return windows;
    }

    internal WindowAccessible WindowOf(WindowContext context) => _windows.GetValue(context, c => new WindowAccessible(c));

    /// <summary>The window a root view belongs to (the parent of a window's root view).</summary>
    internal IAccessible? WindowOfRoot(IAccessible accessible)
    {
        var app = LinuxApplication.Current;
        if (app == null)
            return null;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, accessible))
                return WindowOf(context);
        }
        return null;
    }

    /// <summary>An object's parent: its view parent, its window for a root view, else the application.</summary>
    internal (string, ObjectPath) ParentOf(IAccessible accessible)
    {
        if (accessible is WindowAccessible)
            return RootReference;
        if (accessible.Parent is { } parent)
            return Ref(parent);
        return WindowOfRoot(accessible) is { } window ? Ref(window) : RootReference;
    }

    // ---- Events ---------------------------------------------------------------------------

    /// <summary>Emits once the objects the event refers to are on the bus.</summary>
    private void Emit(Action emit)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await FlushAsync().ConfigureAwait(false);
                emit();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug("AtSpi", $"Emitting an event failed: {ex.GetBaseException().Message}");
            }
        });
    }

    private static readonly IDictionary<string, object> NoProperties = new Dictionary<string, object>();

    /// <summary>The focus moved to <paramref name="accessible"/> (null: nothing in the app has it).</summary>
    internal void FocusChanged(IAccessible? previous, IAccessible? accessible)
    {
        var old = previous != null ? NodeOf(previous) : null;
        var node = accessible != null ? NodeOf(accessible) : null;
        Emit(() =>
        {
            old?.RaiseStateChanged("focused", false);
            if (node != null)
            {
                node.RaiseFocus();
                node.RaiseStateChanged("focused", true);
            }
        });
    }

    internal void StateChanged(IAccessible accessible, string state, bool value)
    {
        var node = NodeOf(accessible);
        Emit(() => node.RaiseStateChanged(state, value));
    }

    internal void NameChanged(IAccessible accessible)
    {
        var node = NodeOf(accessible);
        Emit(() => node.RaisePropertyChange("accessible-name", accessible.AccessibleName));
    }

    /// <summary>A range control's value changed (object:property-change:accessible-value).</summary>
    internal void ValueChanged(IAccessible accessible)
    {
        var node = NodeOf(accessible);
        Emit(() => node.RaisePropertyChange("accessible-value", accessible.Value ?? 0.0));
    }

    internal void DescriptionChanged(IAccessible accessible)
    {
        var node = NodeOf(accessible);
        Emit(() => node.RaisePropertyChange("accessible-description", accessible.AccessibleDescription));
    }

    private readonly HashSet<IAccessible> _childrenChanged = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// An object's children changed. Changes are gathered for 50 ms (building a page adds many
    /// children) and each object then tells clients once, so they read its children again.
    /// </summary>
    internal void ChildrenChanged(IAccessible accessible)
    {
        bool schedule;
        lock (_childrenChanged)
        {
            schedule = _childrenChanged.Count == 0;
            _childrenChanged.Add(accessible);
        }
        if (!schedule)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(50).ConfigureAwait(false);
            IAccessible[] changed;
            lock (_childrenChanged)
            {
                changed = _childrenChanged.ToArray();
                _childrenChanged.Clear();
            }
            // Only objects a client has already reached: the others are read fresh when reached.
            var nodes = new List<AtSpiNode>();
            lock (_gate)
            {
                foreach (var a in changed)
                {
                    if (_nodes.TryGetValue(a, out var node))
                        nodes.Add(node);
                }
            }
            foreach (var node in nodes)
                node.RaiseChildrenChanged("add", -1, NullReference);
        });
    }

    /// <summary>A window was activated or deactivated (it gained or lost the keyboard).</summary>
    internal void WindowActivated(WindowContext context, bool active)
    {
        var window = WindowOf(context);
        window.IsActive = active;
        var node = NodeOf(window);
        Emit(() =>
        {
            if (active)
                node.RaiseActivate(window.AccessibleName);
            else
                node.RaiseDeactivate(window.AccessibleName);
            node.RaiseStateChanged("active", active);
        });
        if (active && context.FocusedView is IAccessible focused)
            FocusChanged(null, focused);
    }

    /// <summary>A window was added to or removed from the application.</summary>
    internal void WindowsChanged(WindowContext context, bool added)
    {
        var window = WindowOf(context);
        var node = NodeOf(window);
        var reference = (_busName, node.ObjectPath);
        Emit(() =>
        {
            _root.RaiseChildrenChanged(added ? "add" : "remove", -1, reference);
            if (added)
                node.RaiseCreate(window.AccessibleName);
            else
                node.RaiseDestroy(window.AccessibleName);
        });
    }

    internal void TextChanged(IAccessible accessible, bool inserted, int start, string text)
    {
        var node = NodeOf(accessible);
        Emit(() => node.RaiseTextChanged(inserted ? "insert" : "delete", start, text));
    }

    internal void CaretMoved(IAccessible accessible, int offset)
    {
        var node = NodeOf(accessible);
        Emit(() => node.RaiseCaretMoved(offset));
    }

    internal void Announce(IAccessible? source, string text, bool assertive)
    {
        // org.a11y.atspi.Event.Object:Announcement (AT-SPI 2.46): detail1 is the politeness
        // (1 polite, 2 assertive). It goes out on the focused object, else the first window.
        var accessible = source ?? Windows().FirstOrDefault();
        if (accessible == null)
            return;
        var node = NodeOf(accessible);
        Emit(() => node.RaiseAnnouncement(text, assertive ? 2 : 1));
    }

    // ---- Roles and states -----------------------------------------------------------------

    internal static uint AtSpiRole(AccessibleRole role, IAccessible accessible) => role switch
    {
        AccessibleRole.Window => 69,
        AccessibleRole.Application => 75,
        AccessibleRole.Panel => 39,
        AccessibleRole.Frame => 23,
        AccessibleRole.Button => 43,
        AccessibleRole.CheckBox => 7,
        AccessibleRole.RadioButton => 44,
        AccessibleRole.ComboBox => 11,
        AccessibleRole.Entry => accessible.States.HasFlag(AccessibleStates.MultiLine) ? 61u : 79u,
        AccessibleRole.Label => 29,
        AccessibleRole.List => 31,
        AccessibleRole.ListItem => 32,
        AccessibleRole.Menu => 33,
        AccessibleRole.MenuBar => 34,
        AccessibleRole.MenuItem => 35,
        AccessibleRole.ScrollBar => 48,
        AccessibleRole.Slider => 51,
        AccessibleRole.SpinButton => 52,
        AccessibleRole.StatusBar => 54,
        AccessibleRole.Tab => 37,
        AccessibleRole.TabPanel => 39,
        AccessibleRole.Text => 61,
        AccessibleRole.ToggleButton => 62,
        AccessibleRole.ToolBar => 63,
        AccessibleRole.ToolTip => 64,
        AccessibleRole.Tree => 65,
        AccessibleRole.TreeItem => 91,
        AccessibleRole.Image => 27,
        AccessibleRole.ProgressBar => 42,
        AccessibleRole.Separator => 50,
        AccessibleRole.Link => 88,
        AccessibleRole.Table => 55,
        AccessibleRole.TableCell => 56,
        AccessibleRole.TableRow => 90,
        AccessibleRole.TableColumnHeader => 57,
        AccessibleRole.TableRowHeader => 58,
        AccessibleRole.PageTab => 37,
        AccessibleRole.PageTabList => 38,
        AccessibleRole.Dialog => 16,
        AccessibleRole.Alert => 2,
        AccessibleRole.Filler => 20,
        AccessibleRole.Icon => 26,
        AccessibleRole.Canvas => 6,
        AccessibleRole.Heading => 83,
        AccessibleRole.PasswordText => 40,
        AccessibleRole.Switch => 130,
        AccessibleRole.ScrollPane => 49,
        AccessibleRole.Page => 84,
        AccessibleRole.DateEditor => 12,
        _ => 67, // ATSPI_ROLE_UNKNOWN
    };

    /// <summary>The AT-SPI role name (AtspiRole's nick, spaces for dashes), which clients show as the role.</summary>
    internal static string RoleName(uint role) => role switch
    {
        2 => "alert", 6 => "canvas", 7 => "check box", 11 => "combo box", 12 => "date editor", 16 => "dialog",
        20 => "filler", 23 => "frame", 26 => "icon", 27 => "image", 29 => "label", 31 => "list", 32 => "list item",
        33 => "menu", 34 => "menu bar", 35 => "menu item", 37 => "page tab", 38 => "page tab list", 39 => "panel",
        40 => "password text", 42 => "progress bar", 43 => "push button", 44 => "radio button", 48 => "scroll bar",
        49 => "scroll pane", 50 => "separator", 51 => "slider", 52 => "spin button", 54 => "status bar", 55 => "table",
        56 => "table cell", 57 => "table column header", 58 => "table row header", 61 => "text", 62 => "toggle button",
        63 => "tool bar", 64 => "tool tip", 65 => "tree", 69 => "window", 75 => "application", 79 => "entry",
        83 => "heading", 84 => "page", 88 => "link", 90 => "table row", 91 => "tree item", 130 => "switch",
        _ => "unknown",
    };

    /// <summary>
    /// The AT-SPI state set (two 32-bit words, bit n for AtspiStateType n). Each
    /// <see cref="AccessibleStates"/> flag k is state k + 1, except ReadOnly (43); enabled
    /// objects are also sensitive, and visible ones in a visible tree are showing.
    /// </summary>
    internal static uint[] StateSet(AccessibleStates states, bool showing)
    {
        ulong set = 0;
        for (int bit = 0; bit < 40; bit++)
        {
            if (((long)states & (1L << bit)) != 0)
                set |= 1UL << (bit + 1);
        }
        if (states.HasFlag(AccessibleStates.ReadOnly))
            set |= 1UL << 43;
        if (states.HasFlag(AccessibleStates.Enabled))
            set |= 1UL << 24; // sensitive
        if (showing)
            set |= 1UL << 25;
        else
            set &= ~(1UL << 25);
        return [(uint)(set & 0xFFFFFFFF), (uint)(set >> 32)];
    }

    /// <summary>Whether a view and every view above it are visible.</summary>
    internal static bool IsShowing(IAccessible accessible)
    {
        if (accessible is WindowAccessible window)
            return window.IsShown;
        for (IAccessible? current = accessible; current != null; current = current.Parent)
        {
            if (!current.States.HasFlag(AccessibleStates.Visible))
                return false;
        }
        return true;
    }

    // ---- Coordinates ----------------------------------------------------------------------

    /// <summary>The window an object is in: its frame's, or the one whose root view it is under.</summary>
    internal static WindowContext? ContextOf(IAccessible accessible)
    {
        if (accessible is WindowAccessible window)
            return window.Context;
        var top = accessible;
        while (top.Parent is { } parent)
            top = parent;
        var app = LinuxApplication.Current;
        if (app == null)
            return null;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, top))
                return context;
        }
        return null;
    }

    /// <summary>
    /// How view-tree positions (the logical space views are laid out in) become the coordinates a
    /// client asked for about <paramref name="accessible"/>: coordinates = tree position x Scale
    /// + (X, Y). A window that knows where it is on the screen (X11) speaks the X server's
    /// physical pixels, as X11 screen readers and their pointer do: Scale is the window's, and
    /// screen coordinates add its origin. A Wayland client is never told where its window is,
    /// so there every coordinate type is the window's own, in logical pixels (as GTK 4 reports).
    /// Window coordinates count from the window's top-left, above a title bar the app draws
    /// itself; parent coordinates from the parent view (a root view's parent is its frame).
    /// </summary>
    internal static (double X, double Y, double Scale) CoordinateMap(IAccessible accessible, uint coordType)
    {
        var context = ContextOf(accessible);
        double inset = context?.CsdPointerInsetLogical ?? 0;
        bool positioned = TryScreenOrigin(context, out double ox, out double oy, out double scale);
        if (!positioned)
            scale = 1;
        switch (coordType)
        {
            case 0 when positioned:
                return (ox, oy + inset * scale, scale);
            case 2 when accessible is not WindowAccessible && accessible.Parent is { } parent:
                var p = parent.Bounds;
                return (-p.X * scale, -p.Y * scale, scale);
            default:
                return (0, inset * scale, scale);
        }
    }

    /// <summary>A frame's map: its bounds are the window's own (no title-bar offset).</summary>
    internal static (double X, double Y, double Scale) FrameMap(WindowAccessible window, uint coordType)
    {
        if (!TryScreenOrigin(window.Context, out double ox, out double oy, out double scale))
            return (0, 0, 1);
        return coordType == 0 ? (ox, oy, scale) : (0, 0, scale);
    }

    /// <summary>The window's origin on the screen in physical pixels, and its scale, when the backend knows it (X11).</summary>
    private static bool TryScreenOrigin(WindowContext? context, out double x, out double y, out double scale)
    {
        x = y = 0;
        scale = 1;
        return context != null && context.TryGetScreenPosition(out x, out y, out scale);
    }

    internal static string Locale => CultureInfo.CurrentUICulture.Name.Replace('-', '_') is { Length: > 0 } name ? name : "C";
}
