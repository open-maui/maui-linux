using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Maui.Platform.Linux.Services;

public class X11InputMethodService : IInputMethodService, IDisposable
{
	private delegate int XIMProc(IntPtr xic, IntPtr clientData, IntPtr callData);

	private struct XPoint
	{
		public short x;

		public short y;
	}

	private struct XKeyEvent
	{
		public int type;

		public ulong serial;

		public bool send_event;

		public IntPtr display;

		public IntPtr window;

		public IntPtr root;

		public IntPtr subwindow;

		public ulong time;

		public int x;

		public int y;

		public int x_root;

		public int y_root;

		public uint state;

		public uint keycode;

		public bool same_screen;
	}

	private IntPtr _display;

	private IntPtr _window;

	private IntPtr _xim;

	private IntPtr _xic;

	private IInputContext? _currentContext;

	private string _preEditText = string.Empty;

	private int _preEditCursorPosition;

	private bool _isActive;

	private bool _disposed;

	private XIMProc? _preeditStartCallback;

	private XIMProc? _preeditDoneCallback;

	private XIMProc? _preeditDrawCallback;

	private XIMProc? _preeditCaretCallback;

	private XIMProc? _commitCallback;

	private const int KeyPress = 2;

	private const int KeyRelease = 3;

	private const uint ShiftMask = 1u;

	private const uint LockMask = 2u;

	private const uint ControlMask = 4u;

	private const uint Mod1Mask = 8u;

	private const uint Mod2Mask = 16u;

	private const uint Mod4Mask = 64u;

	private const long XIMPreeditNothing = 8L;

	private const long XIMPreeditCallbacks = 2L;

	private const long XIMStatusNothing = 1024L;

	private static readonly IntPtr XNClientWindow = Marshal.StringToHGlobalAnsi("clientWindow");

	private static readonly IntPtr XNFocusWindow = Marshal.StringToHGlobalAnsi("focusWindow");

	private static readonly IntPtr XNInputStyle = Marshal.StringToHGlobalAnsi("inputStyle");

	private static readonly IntPtr XNPreeditAttributes = Marshal.StringToHGlobalAnsi("preeditAttributes");

	private static readonly IntPtr XNSpotLocation = Marshal.StringToHGlobalAnsi("spotLocation");

	public bool IsActive => _isActive;

	public string PreEditText => _preEditText;

	public int PreEditCursorPosition => _preEditCursorPosition;

	public event EventHandler<TextCommittedEventArgs>? TextCommitted;

	public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;

	public event EventHandler? PreEditEnded;

	public void Initialize(IntPtr windowHandle)
	{
		_window = windowHandle;
		_display = XOpenDisplay(IntPtr.Zero);
		if (_display == IntPtr.Zero)
		{
			Console.WriteLine("X11InputMethodService: Failed to open display");
			return;
		}
		if (XSetLocaleModifiers("") == IntPtr.Zero)
		{
			XSetLocaleModifiers("@im=none");
		}
		_xim = XOpenIM(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
		if (_xim == IntPtr.Zero)
		{
			Console.WriteLine("X11InputMethodService: No input method available, trying IBus...");
			TryIBusFallback();
		}
		else
		{
			CreateInputContext();
		}
	}

	private void CreateInputContext()
	{
		if (_xim != IntPtr.Zero && _window != IntPtr.Zero)
		{
			IntPtr intPtr = CreatePreeditAttributes();
			_xic = XCreateIC(_xim, XNClientWindow, _window, XNFocusWindow, _window, XNInputStyle, 1026L, XNPreeditAttributes, intPtr, IntPtr.Zero);
			if (intPtr != IntPtr.Zero)
			{
				XFree(intPtr);
			}
			if (_xic == IntPtr.Zero)
			{
				_xic = XCreateICSimple(_xim, XNClientWindow, _window, XNFocusWindow, _window, XNInputStyle, 1032L, IntPtr.Zero);
			}
			if (_xic != IntPtr.Zero)
			{
				Console.WriteLine("X11InputMethodService: Input context created successfully");
			}
		}
	}

	private IntPtr CreatePreeditAttributes()
	{
		_preeditStartCallback = PreeditStartCallback;
		_preeditDoneCallback = PreeditDoneCallback;
		_preeditDrawCallback = PreeditDrawCallback;
		_preeditCaretCallback = PreeditCaretCallback;
		return IntPtr.Zero;
	}

	private int PreeditStartCallback(IntPtr xic, IntPtr clientData, IntPtr callData)
	{
		_isActive = true;
		_preEditText = string.Empty;
		_preEditCursorPosition = 0;
		return -1;
	}

	private int PreeditDoneCallback(IntPtr xic, IntPtr clientData, IntPtr callData)
	{
		_isActive = false;
		_preEditText = string.Empty;
		_preEditCursorPosition = 0;
		this.PreEditEnded?.Invoke(this, EventArgs.Empty);
		_currentContext?.OnPreEditEnded();
		return 0;
	}

	private int PreeditDrawCallback(IntPtr xic, IntPtr clientData, IntPtr callData)
	{
		this.PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition));
		_currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
		return 0;
	}

	private int PreeditCaretCallback(IntPtr xic, IntPtr clientData, IntPtr callData)
	{
		return 0;
	}

	private void TryIBusFallback()
	{
		Console.WriteLine("X11InputMethodService: IBus fallback not yet implemented");
	}

	public void SetFocus(IInputContext? context)
	{
		_currentContext = context;
		if (_xic != IntPtr.Zero)
		{
			if (context != null)
			{
				XSetICFocus(_xic);
			}
			else
			{
				XUnsetICFocus(_xic);
			}
		}
	}

	public void SetCursorLocation(int x, int y, int width, int height)
	{
		if (_xic != IntPtr.Zero)
		{
			XPoint value = new XPoint
			{
				x = (short)x,
				y = (short)y
			};
			IntPtr intPtr = XVaCreateNestedList(0, XNSpotLocation, ref value, IntPtr.Zero);
			if (intPtr != IntPtr.Zero)
			{
				XSetICValues(_xic, XNPreeditAttributes, intPtr, IntPtr.Zero);
				XFree(intPtr);
			}
		}
	}

	public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
	{
		if (_xic == IntPtr.Zero)
		{
			return false;
		}
		XKeyEvent xevent = new XKeyEvent
		{
			type = (isKeyDown ? 2 : 3),
			display = _display,
			window = _window,
			state = ConvertModifiers(modifiers),
			keycode = keyCode
		};
		if (XFilterEvent(ref xevent, _window))
		{
			return true;
		}
		if (isKeyDown)
		{
			byte[] array = new byte[64];
			IntPtr keySym = IntPtr.Zero;
			IntPtr status = IntPtr.Zero;
			int num = Xutf8LookupString(_xic, ref xevent, array, array.Length, ref keySym, ref status);
			if (num > 0)
			{
				string text = Encoding.UTF8.GetString(array, 0, num);
				OnTextCommit(text);
				return true;
			}
		}
		return false;
	}

	private void OnTextCommit(string text)
	{
		_preEditText = string.Empty;
		_preEditCursorPosition = 0;
		this.TextCommitted?.Invoke(this, new TextCommittedEventArgs(text));
		_currentContext?.OnTextCommitted(text);
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
			num |= 0x40;
		}
		if (modifiers.HasFlag(KeyModifiers.CapsLock))
		{
			num |= 2;
		}
		if (modifiers.HasFlag(KeyModifiers.NumLock))
		{
			num |= 0x10;
		}
		return num;
	}

	public void Reset()
	{
		if (_xic != IntPtr.Zero)
		{
			XmbResetIC(_xic);
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
			if (_xic != IntPtr.Zero)
			{
				XDestroyIC(_xic);
				_xic = IntPtr.Zero;
			}
			if (_xim != IntPtr.Zero)
			{
				XCloseIM(_xim);
				_xim = IntPtr.Zero;
			}
		}
	}

	[DllImport("libX11.so.6")]
	private static extern IntPtr XOpenDisplay(IntPtr display);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XSetLocaleModifiers(string modifiers);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XOpenIM(IntPtr display, IntPtr db, IntPtr res_name, IntPtr res_class);

	[DllImport("libX11.so.6")]
	private static extern void XCloseIM(IntPtr xim);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XCreateIC(IntPtr xim, IntPtr name1, IntPtr value1, IntPtr name2, IntPtr value2, IntPtr name3, long value3, IntPtr name4, IntPtr value4, IntPtr terminator);

	[DllImport("libX11.so.6", EntryPoint = "XCreateIC")]
	private static extern IntPtr XCreateICSimple(IntPtr xim, IntPtr name1, IntPtr value1, IntPtr name2, IntPtr value2, IntPtr name3, long value3, IntPtr terminator);

	[DllImport("libX11.so.6")]
	private static extern void XDestroyIC(IntPtr xic);

	[DllImport("libX11.so.6")]
	private static extern void XSetICFocus(IntPtr xic);

	[DllImport("libX11.so.6")]
	private static extern void XUnsetICFocus(IntPtr xic);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XSetICValues(IntPtr xic, IntPtr name, IntPtr value, IntPtr terminator);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XVaCreateNestedList(int unused, IntPtr name, ref XPoint value, IntPtr terminator);

	[DllImport("libX11.so.6")]
	private static extern bool XFilterEvent(ref XKeyEvent xevent, IntPtr window);

	[DllImport("libX11.so.6")]
	private static extern int Xutf8LookupString(IntPtr xic, ref XKeyEvent xevent, byte[] buffer, int bytes, ref IntPtr keySym, ref IntPtr status);

	[DllImport("libX11.so.6")]
	private static extern IntPtr XmbResetIC(IntPtr xic);

	[DllImport("libX11.so.6")]
	private static extern void XFree(IntPtr ptr);
}
