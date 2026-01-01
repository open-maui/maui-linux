using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

public class AtSpi2AccessibilityService : IAccessibilityService, IDisposable
{
	private IntPtr _connection;

	private IntPtr _registry;

	private bool _isEnabled;

	private bool _disposed;

	private IAccessible? _focusedAccessible;

	private readonly ConcurrentDictionary<string, IAccessible> _registeredObjects = new ConcurrentDictionary<string, IAccessible>();

	private readonly string _applicationName;

	private IntPtr _applicationAccessible;

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

	public bool IsEnabled => _isEnabled;

	public AtSpi2AccessibilityService(string applicationName = "MAUI Application")
	{
		_applicationName = applicationName;
	}

	public void Initialize()
	{
		try
		{
			if (atspi_init() != 0)
			{
				Console.WriteLine("AtSpi2AccessibilityService: Failed to initialize AT-SPI2");
				return;
			}
			_isEnabled = CheckAccessibilityEnabled();
			if (_isEnabled)
			{
				_registry = atspi_get_desktop(0);
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
			Console.WriteLine("AtSpi2AccessibilityService: Initialization failed - " + ex.Message);
		}
	}

	private bool CheckAccessibilityEnabled()
	{
		try
		{
			IntPtr intPtr = atspi_get_desktop(0);
			if (intPtr != IntPtr.Zero)
			{
				g_object_unref(intPtr);
				return true;
			}
		}
		catch
		{
		}
		return Environment.GetEnvironmentVariable("GTK_A11Y")?.ToLowerInvariant() != "none";
	}

	private void RegisterApplication()
	{
		atspi_set_main_context(IntPtr.Zero);
	}

	public void Register(IAccessible accessible)
	{
		if (accessible != null)
		{
			_registeredObjects.TryAdd(accessible.AccessibleId, accessible);
		}
	}

	public void Unregister(IAccessible accessible)
	{
		if (accessible != null)
		{
			_registeredObjects.TryRemove(accessible.AccessibleId, out IAccessible _);
		}
	}

	public void NotifyFocusChanged(IAccessible? accessible)
	{
		_focusedAccessible = accessible;
		if (_isEnabled && accessible != null)
		{
			EmitEvent("focus:", accessible);
		}
	}

	public void NotifyPropertyChanged(IAccessible accessible, AccessibleProperty property)
	{
		if (_isEnabled && accessible != null)
		{
			string text = property switch
			{
				AccessibleProperty.Name => "object:property-change:accessible-name", 
				AccessibleProperty.Description => "object:property-change:accessible-description", 
				AccessibleProperty.Role => "object:property-change:accessible-role", 
				AccessibleProperty.Value => "object:property-change:accessible-value", 
				AccessibleProperty.Parent => "object:property-change:accessible-parent", 
				AccessibleProperty.Children => "object:children-changed", 
				_ => string.Empty, 
			};
			if (!string.IsNullOrEmpty(text))
			{
				EmitEvent(text, accessible);
			}
		}
	}

	public void NotifyStateChanged(IAccessible accessible, AccessibleState state, bool value)
	{
		if (_isEnabled && accessible != null)
		{
			string text = state.ToString().ToLowerInvariant();
			string eventName = "object:state-changed:" + text;
			EmitEvent(eventName, accessible, value ? 1 : 0);
		}
	}

	public void Announce(string text, AnnouncementPriority priority = AnnouncementPriority.Polite)
	{
		if (!_isEnabled || string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			Console.WriteLine($"[Accessibility Announcement ({priority})]: {text}");
		}
		catch (Exception ex)
		{
			Console.WriteLine("AtSpi2AccessibilityService: Announcement failed - " + ex.Message);
		}
	}

	private void EmitEvent(string eventName, IAccessible accessible, int detail1 = 0, int detail2 = 0)
	{
		Console.WriteLine($"[AT-SPI2 Event] {eventName}: {accessible.AccessibleName} ({accessible.Role})");
	}

	public static int GetAtSpiRole(AccessibleRole role)
	{
		return role switch
		{
			AccessibleRole.Unknown => 0, 
			AccessibleRole.Window => 22, 
			AccessibleRole.Application => 75, 
			AccessibleRole.Panel => 25, 
			AccessibleRole.Frame => 11, 
			AccessibleRole.Button => 31, 
			AccessibleRole.CheckBox => 4, 
			AccessibleRole.RadioButton => 33, 
			AccessibleRole.ComboBox => 6, 
			AccessibleRole.Entry => 24, 
			AccessibleRole.Label => 16, 
			AccessibleRole.List => 17, 
			AccessibleRole.ListItem => 18, 
			AccessibleRole.Menu => 19, 
			AccessibleRole.MenuBar => 20, 
			AccessibleRole.MenuItem => 21, 
			AccessibleRole.ScrollBar => 40, 
			AccessibleRole.Slider => 43, 
			AccessibleRole.SpinButton => 44, 
			AccessibleRole.StatusBar => 46, 
			AccessibleRole.Tab => 26, 
			AccessibleRole.TabPanel => 27, 
			AccessibleRole.Text => 49, 
			AccessibleRole.ToggleButton => 51, 
			AccessibleRole.ToolBar => 52, 
			AccessibleRole.ToolTip => 53, 
			AccessibleRole.Tree => 54, 
			AccessibleRole.TreeItem => 55, 
			AccessibleRole.Image => 14, 
			AccessibleRole.ProgressBar => 30, 
			AccessibleRole.Separator => 42, 
			AccessibleRole.Link => 83, 
			AccessibleRole.Table => 47, 
			AccessibleRole.TableCell => 48, 
			AccessibleRole.TableRow => 89, 
			AccessibleRole.TableColumnHeader => 36, 
			AccessibleRole.TableRowHeader => 37, 
			AccessibleRole.PageTab => 26, 
			AccessibleRole.PageTabList => 27, 
			AccessibleRole.Dialog => 8, 
			AccessibleRole.Alert => 2, 
			AccessibleRole.Filler => 10, 
			AccessibleRole.Icon => 13, 
			AccessibleRole.Canvas => 3, 
			_ => 0, 
		};
	}

	public static (uint Low, uint High) GetAtSpiStates(AccessibleStates states)
	{
		uint num = 0u;
		uint num2 = 0u;
		if (states.HasFlag(AccessibleStates.Active))
		{
			num |= 1;
		}
		if (states.HasFlag(AccessibleStates.Armed))
		{
			num |= 2;
		}
		if (states.HasFlag(AccessibleStates.Busy))
		{
			num |= 4;
		}
		if (states.HasFlag(AccessibleStates.Checked))
		{
			num |= 8;
		}
		if (states.HasFlag(AccessibleStates.Collapsed))
		{
			num |= 0x10;
		}
		if (states.HasFlag(AccessibleStates.Defunct))
		{
			num |= 0x20;
		}
		if (states.HasFlag(AccessibleStates.Editable))
		{
			num |= 0x40;
		}
		if (states.HasFlag(AccessibleStates.Enabled))
		{
			num |= 0x80;
		}
		if (states.HasFlag(AccessibleStates.Expandable))
		{
			num |= 0x100;
		}
		if (states.HasFlag(AccessibleStates.Expanded))
		{
			num |= 0x200;
		}
		if (states.HasFlag(AccessibleStates.Focusable))
		{
			num |= 0x400;
		}
		if (states.HasFlag(AccessibleStates.Focused))
		{
			num |= 0x800;
		}
		if (states.HasFlag(AccessibleStates.Horizontal))
		{
			num |= 0x2000;
		}
		if (states.HasFlag(AccessibleStates.Iconified))
		{
			num |= 0x4000;
		}
		if (states.HasFlag(AccessibleStates.Modal))
		{
			num |= 0x8000;
		}
		if (states.HasFlag(AccessibleStates.MultiLine))
		{
			num |= 0x10000;
		}
		if (states.HasFlag(AccessibleStates.MultiSelectable))
		{
			num |= 0x20000;
		}
		if (states.HasFlag(AccessibleStates.Opaque))
		{
			num |= 0x40000;
		}
		if (states.HasFlag(AccessibleStates.Pressed))
		{
			num |= 0x80000;
		}
		if (states.HasFlag(AccessibleStates.Resizable))
		{
			num |= 0x100000;
		}
		if (states.HasFlag(AccessibleStates.Selectable))
		{
			num |= 0x200000;
		}
		if (states.HasFlag(AccessibleStates.Selected))
		{
			num |= 0x400000;
		}
		if (states.HasFlag(AccessibleStates.Sensitive))
		{
			num |= 0x800000;
		}
		if (states.HasFlag(AccessibleStates.Showing))
		{
			num |= 0x1000000;
		}
		if (states.HasFlag(AccessibleStates.SingleLine))
		{
			num |= 0x2000000;
		}
		if (states.HasFlag(AccessibleStates.Stale))
		{
			num |= 0x4000000;
		}
		if (states.HasFlag(AccessibleStates.Transient))
		{
			num |= 0x8000000;
		}
		if (states.HasFlag(AccessibleStates.Vertical))
		{
			num |= 0x10000000;
		}
		if (states.HasFlag(AccessibleStates.Visible))
		{
			num |= 0x20000000;
		}
		if (states.HasFlag(AccessibleStates.ManagesDescendants))
		{
			num |= 0x40000000;
		}
		if (states.HasFlag(AccessibleStates.Indeterminate))
		{
			num |= 0x80000000u;
		}
		if (states.HasFlag(AccessibleStates.Required))
		{
			num2 |= 1;
		}
		if (states.HasFlag(AccessibleStates.Truncated))
		{
			num2 |= 2;
		}
		if (states.HasFlag(AccessibleStates.Animated))
		{
			num2 |= 4;
		}
		if (states.HasFlag(AccessibleStates.InvalidEntry))
		{
			num2 |= 8;
		}
		if (states.HasFlag(AccessibleStates.SupportsAutocompletion))
		{
			num2 |= 0x10;
		}
		if (states.HasFlag(AccessibleStates.SelectableText))
		{
			num2 |= 0x20;
		}
		if (states.HasFlag(AccessibleStates.IsDefault))
		{
			num2 |= 0x40;
		}
		if (states.HasFlag(AccessibleStates.Visited))
		{
			num2 |= 0x80;
		}
		if (states.HasFlag(AccessibleStates.ReadOnly))
		{
			num2 |= 0x400;
		}
		return (Low: num, High: num2);
	}

	public void Shutdown()
	{
		Dispose();
	}

	public void Dispose()
	{
		if (!_disposed)
		{
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
			atspi_exit();
		}
	}

	[DllImport("libatspi.so.0")]
	private static extern int atspi_init();

	[DllImport("libatspi.so.0")]
	private static extern int atspi_exit();

	[DllImport("libatspi.so.0")]
	private static extern IntPtr atspi_get_desktop(int i);

	[DllImport("libatspi.so.0")]
	private static extern void atspi_set_main_context(IntPtr context);

	[DllImport("libgobject-2.0.so.0")]
	private static extern void g_object_unref(IntPtr obj);
}
