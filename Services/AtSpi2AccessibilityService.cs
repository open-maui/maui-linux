// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// AT-SPI2 accessibility service implementation.
/// Provides screen reader support through the AT-SPI2 D-Bus interface.
/// </summary>
public class AtSpi2AccessibilityService : IAccessibilityService, IDisposable
{
    private nint _connection;
    private nint _registry;
    private bool _isEnabled;
    private bool _disposed;
    private IAccessible? _focusedAccessible;
    private readonly ConcurrentDictionary<string, IAccessible> _registeredObjects = new();
    private readonly string _applicationName;
    private nint _applicationAccessible;

    public bool IsEnabled => _isEnabled;

    public AtSpi2AccessibilityService(string applicationName = "MAUI Application")
    {
        _applicationName = applicationName;
    }

    public void Initialize()
    {
        try
        {
            // Initialize AT-SPI2
            int result = atspi_init();
            if (result != 0)
            {
                Console.WriteLine("AtSpi2AccessibilityService: Failed to initialize AT-SPI2");
                return;
            }

            // Check if accessibility is enabled
            _isEnabled = CheckAccessibilityEnabled();

            if (_isEnabled)
            {
                // Get the desktop (root accessible)
                _registry = atspi_get_desktop(0);

                // Register our application
                RegisterApplication();

                Console.WriteLine("AtSpi2AccessibilityService: Initialized successfully");
            }
            else
            {
                Console.WriteLine("AtSpi2AccessibilityService: Accessibility is not enabled");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AtSpi2AccessibilityService: Initialization failed - {ex.Message}");
        }
    }

    private bool CheckAccessibilityEnabled()
    {
        // Check if AT-SPI2 registry is available
        try
        {
            nint desktop = atspi_get_desktop(0);
            if (desktop != IntPtr.Zero)
            {
                g_object_unref(desktop);
                return true;
            }
        }
        catch
        {
            // AT-SPI2 not available
        }

        // Also check the gsettings key
        var enabled = Environment.GetEnvironmentVariable("GTK_A11Y");
        return enabled?.ToLowerInvariant() != "none";
    }

    private void RegisterApplication()
    {
        // In a full implementation, we would create an AtspiApplication object
        // and register it with the AT-SPI2 registry. For now, we set up the basics.

        // Set application name
        atspi_set_main_context(IntPtr.Zero);
    }

    public void Register(IAccessible accessible)
    {
        if (accessible == null) return;

        _registeredObjects.TryAdd(accessible.AccessibleId, accessible);

        // In a full implementation, we would create an AtspiAccessible object
        // and register it with AT-SPI2
    }

    public void Unregister(IAccessible accessible)
    {
        if (accessible == null) return;

        _registeredObjects.TryRemove(accessible.AccessibleId, out _);

        // Clean up AT-SPI2 resources for this accessible
    }

    public void NotifyFocusChanged(IAccessible? accessible)
    {
        _focusedAccessible = accessible;

        if (!_isEnabled || accessible == null) return;

        // Emit focus event through AT-SPI2
        EmitEvent("focus:", accessible);
    }

    public void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property)
    {
        if (!_isEnabled || accessible == null) return;

        string eventName = property switch
        {
            AccessibleProperty.Name => "object:property-change:accessible-name",
            AccessibleProperty.Description => "object:property-change:accessible-description",
            AccessibleProperty.Role => "object:property-change:accessible-role",
            AccessibleProperty.Value => "object:property-change:accessible-value",
            AccessibleProperty.Parent => "object:property-change:accessible-parent",
            AccessibleProperty.Children => "object:children-changed",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(eventName))
        {
            EmitEvent(eventName, accessible);
        }
    }

    public void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value)
    {
        if (!_isEnabled || accessible == null) return;

        string stateName = state.ToString().ToLowerInvariant();
        string eventName = $"object:state-changed:{stateName}";

        EmitEvent(eventName, accessible, value ? 1 : 0);
    }

    public void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite)
    {
        if (!_isEnabled || string.IsNullOrEmpty(text)) return;

        // Use AT-SPI2 live region to announce text
        // Priority maps to: Polite = ATSPI_LIVE_POLITE, Assertive = ATSPI_LIVE_ASSERTIVE

        try
        {
            // In AT-SPI2, announcements are typically done through live regions
            // or by emitting "object:announcement" events

            // For now, use a simpler approach with the event system
            Console.WriteLine($"[Accessibility Announcement ({priority})]: {text}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AtSpi2AccessibilityService: Announcement failed - {ex.Message}");
        }
    }

    private void EmitEvent(string eventName, IAccessible accessible, int detail1 = 0, int detail2 = 0)
    {
        // In a full implementation, we would emit the event through D-Bus
        // using the org.a11y.atspi.Event interface

        // For now, log the event for debugging
        Console.WriteLine($"[AT-SPI2 Event] {eventName}: {accessible.AccessibleName} ({accessible.Role})");
    }

    /// <summary>
    /// Gets the AT-SPI2 role value for the given accessible role.
    /// </summary>
    public static int GetAtSpiRole(AccessibleRole role)
    {
        return role switch
        {
            AccessibleRole.Unknown => ATSPI_ROLE_UNKNOWN,
            AccessibleRole.Window => ATSPI_ROLE_WINDOW,
            AccessibleRole.Application => ATSPI_ROLE_APPLICATION,
            AccessibleRole.Panel => ATSPI_ROLE_PANEL,
            AccessibleRole.Frame => ATSPI_ROLE_FRAME,
            AccessibleRole.Button => ATSPI_ROLE_PUSH_BUTTON,
            AccessibleRole.CheckBox => ATSPI_ROLE_CHECK_BOX,
            AccessibleRole.RadioButton => ATSPI_ROLE_RADIO_BUTTON,
            AccessibleRole.ComboBox => ATSPI_ROLE_COMBO_BOX,
            AccessibleRole.Entry => ATSPI_ROLE_ENTRY,
            AccessibleRole.Label => ATSPI_ROLE_LABEL,
            AccessibleRole.List => ATSPI_ROLE_LIST,
            AccessibleRole.ListItem => ATSPI_ROLE_LIST_ITEM,
            AccessibleRole.Menu => ATSPI_ROLE_MENU,
            AccessibleRole.MenuBar => ATSPI_ROLE_MENU_BAR,
            AccessibleRole.MenuItem => ATSPI_ROLE_MENU_ITEM,
            AccessibleRole.ScrollBar => ATSPI_ROLE_SCROLL_BAR,
            AccessibleRole.Slider => ATSPI_ROLE_SLIDER,
            AccessibleRole.SpinButton => ATSPI_ROLE_SPIN_BUTTON,
            AccessibleRole.StatusBar => ATSPI_ROLE_STATUS_BAR,
            AccessibleRole.Tab => ATSPI_ROLE_PAGE_TAB,
            AccessibleRole.TabPanel => ATSPI_ROLE_PAGE_TAB_LIST,
            AccessibleRole.Text => ATSPI_ROLE_TEXT,
            AccessibleRole.ToggleButton => ATSPI_ROLE_TOGGLE_BUTTON,
            AccessibleRole.ToolBar => ATSPI_ROLE_TOOL_BAR,
            AccessibleRole.ToolTip => ATSPI_ROLE_TOOL_TIP,
            AccessibleRole.Tree => ATSPI_ROLE_TREE,
            AccessibleRole.TreeItem => ATSPI_ROLE_TREE_ITEM,
            AccessibleRole.Image => ATSPI_ROLE_IMAGE,
            AccessibleRole.ProgressBar => ATSPI_ROLE_PROGRESS_BAR,
            AccessibleRole.Separator => ATSPI_ROLE_SEPARATOR,
            AccessibleRole.Link => ATSPI_ROLE_LINK,
            AccessibleRole.Table => ATSPI_ROLE_TABLE,
            AccessibleRole.TableCell => ATSPI_ROLE_TABLE_CELL,
            AccessibleRole.TableRow => ATSPI_ROLE_TABLE_ROW,
            AccessibleRole.TableColumnHeader => ATSPI_ROLE_TABLE_COLUMN_HEADER,
            AccessibleRole.TableRowHeader => ATSPI_ROLE_TABLE_ROW_HEADER,
            AccessibleRole.PageTab => ATSPI_ROLE_PAGE_TAB,
            AccessibleRole.PageTabList => ATSPI_ROLE_PAGE_TAB_LIST,
            AccessibleRole.Dialog => ATSPI_ROLE_DIALOG,
            AccessibleRole.Alert => ATSPI_ROLE_ALERT,
            AccessibleRole.Filler => ATSPI_ROLE_FILLER,
            AccessibleRole.Icon => ATSPI_ROLE_ICON,
            AccessibleRole.Canvas => ATSPI_ROLE_CANVAS,
            _ => ATSPI_ROLE_UNKNOWN
        };
    }

    /// <summary>
    /// Converts accessible states to AT-SPI2 state set.
    /// </summary>
    public static (uint Low, uint High) GetAtSpiStates(AccessibleStates states)
    {
        uint low = 0;
        uint high = 0;

        if (states.HasFlag(AccessibleStates.Active)) low |= 1 << 0;
        if (states.HasFlag(AccessibleStates.Armed)) low |= 1 << 1;
        if (states.HasFlag(AccessibleStates.Busy)) low |= 1 << 2;
        if (states.HasFlag(AccessibleStates.Checked)) low |= 1 << 3;
        if (states.HasFlag(AccessibleStates.Collapsed)) low |= 1 << 4;
        if (states.HasFlag(AccessibleStates.Defunct)) low |= 1 << 5;
        if (states.HasFlag(AccessibleStates.Editable)) low |= 1 << 6;
        if (states.HasFlag(AccessibleStates.Enabled)) low |= 1 << 7;
        if (states.HasFlag(AccessibleStates.Expandable)) low |= 1 << 8;
        if (states.HasFlag(AccessibleStates.Expanded)) low |= 1 << 9;
        if (states.HasFlag(AccessibleStates.Focusable)) low |= 1 << 10;
        if (states.HasFlag(AccessibleStates.Focused)) low |= 1 << 11;
        if (states.HasFlag(AccessibleStates.Horizontal)) low |= 1 << 13;
        if (states.HasFlag(AccessibleStates.Iconified)) low |= 1 << 14;
        if (states.HasFlag(AccessibleStates.Modal)) low |= 1 << 15;
        if (states.HasFlag(AccessibleStates.MultiLine)) low |= 1 << 16;
        if (states.HasFlag(AccessibleStates.MultiSelectable)) low |= 1 << 17;
        if (states.HasFlag(AccessibleStates.Opaque)) low |= 1 << 18;
        if (states.HasFlag(AccessibleStates.Pressed)) low |= 1 << 19;
        if (states.HasFlag(AccessibleStates.Resizable)) low |= 1 << 20;
        if (states.HasFlag(AccessibleStates.Selectable)) low |= 1 << 21;
        if (states.HasFlag(AccessibleStates.Selected)) low |= 1 << 22;
        if (states.HasFlag(AccessibleStates.Sensitive)) low |= 1 << 23;
        if (states.HasFlag(AccessibleStates.Showing)) low |= 1 << 24;
        if (states.HasFlag(AccessibleStates.SingleLine)) low |= 1 << 25;
        if (states.HasFlag(AccessibleStates.Stale)) low |= 1 << 26;
        if (states.HasFlag(AccessibleStates.Transient)) low |= 1 << 27;
        if (states.HasFlag(AccessibleStates.Vertical)) low |= 1 << 28;
        if (states.HasFlag(AccessibleStates.Visible)) low |= 1 << 29;
        if (states.HasFlag(AccessibleStates.ManagesDescendants)) low |= 1 << 30;
        if (states.HasFlag(AccessibleStates.Indeterminate)) low |= 1u << 31;

        // High bits (states 32+)
        if (states.HasFlag(AccessibleStates.Required)) high |= 1 << 0;
        if (states.HasFlag(AccessibleStates.Truncated)) high |= 1 << 1;
        if (states.HasFlag(AccessibleStates.Animated)) high |= 1 << 2;
        if (states.HasFlag(AccessibleStates.InvalidEntry)) high |= 1 << 3;
        if (states.HasFlag(AccessibleStates.SupportsAutocompletion)) high |= 1 << 4;
        if (states.HasFlag(AccessibleStates.SelectableText)) high |= 1 << 5;
        if (states.HasFlag(AccessibleStates.IsDefault)) high |= 1 << 6;
        if (states.HasFlag(AccessibleStates.Visited)) high |= 1 << 7;
        if (states.HasFlag(AccessibleStates.ReadOnly)) high |= 1 << 10;

        return (low, high);
    }

    public void Shutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _registeredObjects.Clear();

        if (_applicationAccessible != IntPtr.Zero)
        {
            g_object_unref(_applicationAccessible);
            _applicationAccessible = IntPtr.Zero;
        }

        if (_registry != IntPtr.Zero)
        {
            g_object_unref(_registry);
            _registry = IntPtr.Zero;
        }

        // Exit AT-SPI2
        atspi_exit();
    }

    #region AT-SPI2 Role Constants

    private const int ATSPI_ROLE_UNKNOWN = 0;
    private const int ATSPI_ROLE_WINDOW = 22;
    private const int ATSPI_ROLE_APPLICATION = 75;
    private const int ATSPI_ROLE_PANEL = 25;
    private const int ATSPI_ROLE_FRAME = 11;
    private const int ATSPI_ROLE_PUSH_BUTTON = 31;
    private const int ATSPI_ROLE_CHECK_BOX = 4;
    private const int ATSPI_ROLE_RADIO_BUTTON = 33;
    private const int ATSPI_ROLE_COMBO_BOX = 6;
    private const int ATSPI_ROLE_ENTRY = 24;
    private const int ATSPI_ROLE_LABEL = 16;
    private const int ATSPI_ROLE_LIST = 17;
    private const int ATSPI_ROLE_LIST_ITEM = 18;
    private const int ATSPI_ROLE_MENU = 19;
    private const int ATSPI_ROLE_MENU_BAR = 20;
    private const int ATSPI_ROLE_MENU_ITEM = 21;
    private const int ATSPI_ROLE_SCROLL_BAR = 40;
    private const int ATSPI_ROLE_SLIDER = 43;
    private const int ATSPI_ROLE_SPIN_BUTTON = 44;
    private const int ATSPI_ROLE_STATUS_BAR = 46;
    private const int ATSPI_ROLE_PAGE_TAB = 26;
    private const int ATSPI_ROLE_PAGE_TAB_LIST = 27;
    private const int ATSPI_ROLE_TEXT = 49;
    private const int ATSPI_ROLE_TOGGLE_BUTTON = 51;
    private const int ATSPI_ROLE_TOOL_BAR = 52;
    private const int ATSPI_ROLE_TOOL_TIP = 53;
    private const int ATSPI_ROLE_TREE = 54;
    private const int ATSPI_ROLE_TREE_ITEM = 55;
    private const int ATSPI_ROLE_IMAGE = 14;
    private const int ATSPI_ROLE_PROGRESS_BAR = 30;
    private const int ATSPI_ROLE_SEPARATOR = 42;
    private const int ATSPI_ROLE_LINK = 83;
    private const int ATSPI_ROLE_TABLE = 47;
    private const int ATSPI_ROLE_TABLE_CELL = 48;
    private const int ATSPI_ROLE_TABLE_ROW = 89;
    private const int ATSPI_ROLE_TABLE_COLUMN_HEADER = 36;
    private const int ATSPI_ROLE_TABLE_ROW_HEADER = 37;
    private const int ATSPI_ROLE_DIALOG = 8;
    private const int ATSPI_ROLE_ALERT = 2;
    private const int ATSPI_ROLE_FILLER = 10;
    private const int ATSPI_ROLE_ICON = 13;
    private const int ATSPI_ROLE_CANVAS = 3;

    #endregion

    #region AT-SPI2 Interop

    [DllImport("libatspi.so.0")]
    private static extern int atspi_init();

    [DllImport("libatspi.so.0")]
    private static extern int atspi_exit();

    [DllImport("libatspi.so.0")]
    private static extern nint atspi_get_desktop(int i);

    [DllImport("libatspi.so.0")]
    private static extern void atspi_set_main_context(nint context);

    [DllImport("libgobject-2.0.so.0")]
    private static extern void g_object_unref(nint obj);

    #endregion
}

/// <summary>
/// Factory for creating accessibility service instances.
/// </summary>
public static class AccessibilityServiceFactory
{
    private static IAccessibilityService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton accessibility service instance.
    /// </summary>
    public static IAccessibilityService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= CreateService();
                }
            }
            return _instance;
        }
    }

    private static IAccessibilityService CreateService()
    {
        try
        {
            var service = new AtSpi2AccessibilityService();
            service.Initialize();
            return service;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AccessibilityServiceFactory: Failed to create AT-SPI2 service - {ex.Message}");
            return new NullAccessibilityService();
        }
    }

    /// <summary>
    /// Resets the singleton instance.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _instance?.Shutdown();
            _instance = null;
        }
    }
}

/// <summary>
/// Null implementation of accessibility service.
/// </summary>
public class NullAccessibilityService : IAccessibilityService
{
    public bool IsEnabled => false;

    public void Initialize() { }
    public void Register(IAccessible accessible) { }
    public void Unregister(IAccessible accessible) { }
    public void NotifyFocusChanged(IAccessible? accessible) { }
    public void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property) { }
    public void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value) { }
    public void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite) { }
    public void Shutdown() { }
}
