using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services;

public class IBusInputMethodService : IInputMethodService, IDisposable
{
	private delegate void IBusCommitTextCallback(IntPtr context, IntPtr text, IntPtr userData);

	private delegate void IBusUpdatePreeditTextCallback(IntPtr context, IntPtr text, uint cursorPos, bool visible, IntPtr userData);

	private delegate void IBusShowPreeditTextCallback(IntPtr context, IntPtr userData);

	private delegate void IBusHidePreeditTextCallback(IntPtr context, IntPtr userData);

	private IntPtr _bus;

	private IntPtr _context;

	private IInputContext? _currentContext;

	private string _preEditText = string.Empty;

	private int _preEditCursorPosition;

	private bool _isActive;

	private bool _disposed;

	private IBusCommitTextCallback? _commitCallback;

	private IBusUpdatePreeditTextCallback? _preeditCallback;

	private IBusShowPreeditTextCallback? _showPreeditCallback;

	private IBusHidePreeditTextCallback? _hidePreeditCallback;

	private const uint IBUS_CAP_PREEDIT_TEXT = 1u;

	private const uint IBUS_CAP_FOCUS = 8u;

	private const uint IBUS_CAP_SURROUNDING_TEXT = 32u;

	private const uint IBUS_SHIFT_MASK = 1u;

	private const uint IBUS_LOCK_MASK = 2u;

	private const uint IBUS_CONTROL_MASK = 4u;

	private const uint IBUS_MOD1_MASK = 8u;

	private const uint IBUS_SUPER_MASK = 67108864u;

	private const uint IBUS_RELEASE_MASK = 1073741824u;

	private const uint IBUS_ATTR_TYPE_UNDERLINE = 1u;

	private const uint IBUS_ATTR_TYPE_FOREGROUND = 2u;

	private const uint IBUS_ATTR_TYPE_BACKGROUND = 3u;

	public bool IsActive => _isActive;

	public string PreEditText => _preEditText;

	public int PreEditCursorPosition => _preEditCursorPosition;

	public event EventHandler<TextCommittedEventArgs>? TextCommitted;

	public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;

	public event EventHandler? PreEditEnded;

	public void Initialize(IntPtr windowHandle)
	{
		try
		{
			ibus_init();
			_bus = ibus_bus_new();
			if (_bus == IntPtr.Zero)
			{
				Console.WriteLine("IBusInputMethodService: Failed to connect to IBus");
				return;
			}
			if (!ibus_bus_is_connected(_bus))
			{
				Console.WriteLine("IBusInputMethodService: IBus not connected");
				return;
			}
			_context = ibus_bus_create_input_context(_bus, "maui-linux");
			if (_context == IntPtr.Zero)
			{
				Console.WriteLine("IBusInputMethodService: Failed to create input context");
				return;
			}
			uint capabilities = 41u;
			ibus_input_context_set_capabilities(_context, capabilities);
			ConnectSignals();
			Console.WriteLine("IBusInputMethodService: Initialized successfully");
		}
		catch (Exception ex)
		{
			Console.WriteLine("IBusInputMethodService: Initialization failed - " + ex.Message);
		}
	}

	private void ConnectSignals()
	{
		if (_context != IntPtr.Zero)
		{
			_commitCallback = OnCommitText;
			_preeditCallback = OnUpdatePreeditText;
			_showPreeditCallback = OnShowPreeditText;
			_hidePreeditCallback = OnHidePreeditText;
			g_signal_connect(_context, "commit-text", Marshal.GetFunctionPointerForDelegate(_commitCallback), IntPtr.Zero);
			g_signal_connect(_context, "update-preedit-text", Marshal.GetFunctionPointerForDelegate(_preeditCallback), IntPtr.Zero);
			g_signal_connect(_context, "show-preedit-text", Marshal.GetFunctionPointerForDelegate(_showPreeditCallback), IntPtr.Zero);
			g_signal_connect(_context, "hide-preedit-text", Marshal.GetFunctionPointerForDelegate(_hidePreeditCallback), IntPtr.Zero);
		}
	}

	private void OnCommitText(IntPtr context, IntPtr text, IntPtr userData)
	{
		if (text != IntPtr.Zero)
		{
			string iBusTextString = GetIBusTextString(text);
			if (!string.IsNullOrEmpty(iBusTextString))
			{
				_preEditText = string.Empty;
				_preEditCursorPosition = 0;
				_isActive = false;
				this.TextCommitted?.Invoke(this, new TextCommittedEventArgs(iBusTextString));
				_currentContext?.OnTextCommitted(iBusTextString);
			}
		}
	}

	private void OnUpdatePreeditText(IntPtr context, IntPtr text, uint cursorPos, bool visible, IntPtr userData)
	{
		if (!visible)
		{
			OnHidePreeditText(context, userData);
			return;
		}
		_isActive = true;
		_preEditText = ((text != IntPtr.Zero) ? GetIBusTextString(text) : string.Empty);
		_preEditCursorPosition = (int)cursorPos;
		List<PreEditAttribute> preeditAttributes = GetPreeditAttributes(text);
		this.PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition, preeditAttributes));
		_currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
	}

	private void OnShowPreeditText(IntPtr context, IntPtr userData)
	{
		_isActive = true;
	}

	private void OnHidePreeditText(IntPtr context, IntPtr userData)
	{
		_isActive = false;
		_preEditText = string.Empty;
		_preEditCursorPosition = 0;
		this.PreEditEnded?.Invoke(this, EventArgs.Empty);
		_currentContext?.OnPreEditEnded();
	}

	private string GetIBusTextString(IntPtr ibusText)
	{
		if (ibusText == IntPtr.Zero)
		{
			return string.Empty;
		}
		IntPtr intPtr = ibus_text_get_text(ibusText);
		if (intPtr == IntPtr.Zero)
		{
			return string.Empty;
		}
		return Marshal.PtrToStringUTF8(intPtr) ?? string.Empty;
	}

	private List<PreEditAttribute> GetPreeditAttributes(IntPtr ibusText)
	{
		List<PreEditAttribute> list = new List<PreEditAttribute>();
		if (ibusText == IntPtr.Zero)
		{
			return list;
		}
		IntPtr intPtr = ibus_text_get_attributes(ibusText);
		if (intPtr == IntPtr.Zero)
		{
			return list;
		}
		uint num = ibus_attr_list_size(intPtr);
		for (uint num2 = 0u; num2 < num; num2++)
		{
			IntPtr intPtr2 = ibus_attr_list_get(intPtr, num2);
			if (intPtr2 != IntPtr.Zero)
			{
				uint ibusType = ibus_attribute_get_attr_type(intPtr2);
				uint num3 = ibus_attribute_get_start_index(intPtr2);
				uint num4 = ibus_attribute_get_end_index(intPtr2);
				list.Add(new PreEditAttribute
				{
					Start = (int)num3,
					Length = (int)(num4 - num3),
					Type = ConvertAttributeType(ibusType)
				});
			}
		}
		return list;
	}

	private PreEditAttributeType ConvertAttributeType(uint ibusType)
	{
		return ibusType switch
		{
			1u => PreEditAttributeType.Underline, 
			2u => PreEditAttributeType.Highlighted, 
			3u => PreEditAttributeType.Reverse, 
			_ => PreEditAttributeType.None, 
		};
	}

	public void SetFocus(IInputContext? context)
	{
		_currentContext = context;
		if (_context != IntPtr.Zero)
		{
			if (context != null)
			{
				ibus_input_context_focus_in(_context);
			}
			else
			{
				ibus_input_context_focus_out(_context);
			}
		}
	}

	public void SetCursorLocation(int x, int y, int width, int height)
	{
		if (_context != IntPtr.Zero)
		{
			ibus_input_context_set_cursor_location(_context, x, y, width, height);
		}
	}

	public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
	{
		if (_context == IntPtr.Zero)
		{
			return false;
		}
		uint num = ConvertModifiers(modifiers);
		if (!isKeyDown)
		{
			num |= 0x40000000;
		}
		return ibus_input_context_process_key_event(_context, keyCode, keyCode, num);
	}

	private uint ConvertModifiers(KeyModifiers modifiers)
	{
		uint num = 0u;
		if (modifiers.HasFlag(KeyModifiers.Shift))
		{
			num |= 1;
		}
		if (modifiers.HasFlag(KeyModifiers.Control))
		{
			num |= 4;
		}
		if (modifiers.HasFlag(KeyModifiers.Alt))
		{
			num |= 8;
		}
		if (modifiers.HasFlag(KeyModifiers.Super))
		{
			num |= 0x4000000;
		}
		if (modifiers.HasFlag(KeyModifiers.CapsLock))
		{
			num |= 2;
		}
		return num;
	}

	public void Reset()
	{
		if (_context != IntPtr.Zero)
		{
			ibus_input_context_reset(_context);
		}
		_preEditText = string.Empty;
		_preEditCursorPosition = 0;
		_isActive = false;
		this.PreEditEnded?.Invoke(this, EventArgs.Empty);
		_currentContext?.OnPreEditEnded();
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
			if (_context != IntPtr.Zero)
			{
				ibus_input_context_focus_out(_context);
				g_object_unref(_context);
				_context = IntPtr.Zero;
			}
			if (_bus != IntPtr.Zero)
			{
				g_object_unref(_bus);
				_bus = IntPtr.Zero;
			}
		}
	}

	[DllImport("libibus-1.0.so.5")]
	private static extern void ibus_init();

	[DllImport("libibus-1.0.so.5")]
	private static extern IntPtr ibus_bus_new();

	[DllImport("libibus-1.0.so.5")]
	private static extern bool ibus_bus_is_connected(IntPtr bus);

	[DllImport("libibus-1.0.so.5")]
	private static extern IntPtr ibus_bus_create_input_context(IntPtr bus, string clientName);

	[DllImport("libibus-1.0.so.5")]
	private static extern void ibus_input_context_set_capabilities(IntPtr context, uint capabilities);

	[DllImport("libibus-1.0.so.5")]
	private static extern void ibus_input_context_focus_in(IntPtr context);

	[DllImport("libibus-1.0.so.5")]
	private static extern void ibus_input_context_focus_out(IntPtr context);

	[DllImport("libibus-1.0.so.5")]
	private static extern void ibus_input_context_reset(IntPtr context);

	[DllImport("libibus-1.0.so.5")]
	private static extern void ibus_input_context_set_cursor_location(IntPtr context, int x, int y, int w, int h);

	[DllImport("libibus-1.0.so.5")]
	private static extern bool ibus_input_context_process_key_event(IntPtr context, uint keyval, uint keycode, uint state);

	[DllImport("libibus-1.0.so.5")]
	private static extern IntPtr ibus_text_get_text(IntPtr text);

	[DllImport("libibus-1.0.so.5")]
	private static extern IntPtr ibus_text_get_attributes(IntPtr text);

	[DllImport("libibus-1.0.so.5")]
	private static extern uint ibus_attr_list_size(IntPtr attrList);

	[DllImport("libibus-1.0.so.5")]
	private static extern IntPtr ibus_attr_list_get(IntPtr attrList, uint index);

	[DllImport("libibus-1.0.so.5")]
	private static extern uint ibus_attribute_get_attr_type(IntPtr attr);

	[DllImport("libibus-1.0.so.5")]
	private static extern uint ibus_attribute_get_start_index(IntPtr attr);

	[DllImport("libibus-1.0.so.5")]
	private static extern uint ibus_attribute_get_end_index(IntPtr attr);

	[DllImport("libgobject-2.0.so.0")]
	private static extern void g_object_unref(IntPtr obj);

	[DllImport("libgobject-2.0.so.0")]
	private static extern ulong g_signal_connect(IntPtr instance, string signal, IntPtr handler, IntPtr data);
}
