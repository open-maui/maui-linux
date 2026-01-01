using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Input;
using Microsoft.Maui.Platform.Linux.Interop;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform.Linux.Window;

public class X11Window : IDisposable
{
	private IntPtr _display;

	private IntPtr _window;

	private IntPtr _wmDeleteMessage;

	private int _screen;

	private bool _disposed;

	private bool _isRunning;

	private int _width;

	private int _height;

	private IntPtr _arrowCursor;

	private IntPtr _handCursor;

	private IntPtr _textCursor;

	private IntPtr _currentCursor;

	private CursorType _currentCursorType;

	private static int _eventCounter;

	public IntPtr Display => _display;

	public IntPtr Handle => _window;

	public int Width => _width;

	public int Height => _height;

	public bool IsRunning => _isRunning;

	public event EventHandler<KeyEventArgs>? KeyDown;

	public event EventHandler<KeyEventArgs>? KeyUp;

	public event EventHandler<TextInputEventArgs>? TextInput;

	public event EventHandler<PointerEventArgs>? PointerMoved;

	public event EventHandler<PointerEventArgs>? PointerPressed;

	public event EventHandler<PointerEventArgs>? PointerReleased;

	public event EventHandler<ScrollEventArgs>? Scroll;

	public event EventHandler? Exposed;

	public event EventHandler<(int Width, int Height)>? Resized;

	public event EventHandler? CloseRequested;

	public event EventHandler? FocusGained;

	public event EventHandler? FocusLost;

	public X11Window(string title, int width, int height)
	{
		_width = width;
		_height = height;
		_display = X11.XOpenDisplay(IntPtr.Zero);
		if (_display == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to open X11 display. Is X11 running?");
		}
		_screen = X11.XDefaultScreen(_display);
		IntPtr parent = X11.XRootWindow(_display, _screen);
		_window = X11.XCreateSimpleWindow(_display, parent, 0, 0, (uint)width, (uint)height, 0u, 0uL, 16777215uL);
		if (_window == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to create X11 window");
		}
		X11.XStoreName(_display, _window, title);
		long num = 2261119L;
		Console.WriteLine($"[X11Window] Setting event mask: {num} (0x{num:X})");
		X11.XSelectInput(_display, _window, num);
		_wmDeleteMessage = X11.XInternAtom(_display, "WM_DELETE_WINDOW", onlyIfExists: false);
		_arrowCursor = X11.XCreateFontCursor(_display, 68u);
		_handCursor = X11.XCreateFontCursor(_display, 60u);
		_textCursor = X11.XCreateFontCursor(_display, 152u);
		_currentCursor = _arrowCursor;
	}

	public void SetCursor(CursorType cursorType)
	{
		if (_currentCursorType != cursorType)
		{
			_currentCursorType = cursorType;
			IntPtr intPtr = cursorType switch
			{
				CursorType.Hand => _handCursor, 
				CursorType.Text => _textCursor, 
				_ => _arrowCursor, 
			};
			if (intPtr != _currentCursor)
			{
				_currentCursor = intPtr;
				X11.XDefineCursor(_display, _window, _currentCursor);
				X11.XFlush(_display);
			}
		}
	}

	public void Show()
	{
		X11.XMapWindow(_display, _window);
		X11.XFlush(_display);
		_isRunning = true;
	}

	public void Hide()
	{
		X11.XUnmapWindow(_display, _window);
		X11.XFlush(_display);
	}

	public void SetTitle(string title)
	{
		X11.XStoreName(_display, _window, title);
	}

	public void Resize(int width, int height)
	{
		X11.XResizeWindow(_display, _window, (uint)width, (uint)height);
		X11.XFlush(_display);
	}

	public unsafe void SetIcon(string iconPath)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
		{
			Console.WriteLine("[X11Window] Icon file not found: " + iconPath);
			return;
		}
		Console.WriteLine("[X11Window] SetIcon called: " + iconPath);
		try
		{
			SKBitmap val = null;
			if (iconPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("[X11Window] Loading SVG icon");
				SKSvg val2 = new SKSvg();
				try
				{
					val2.Load(iconPath);
					if (val2.Picture != null)
					{
						SKRect cullRect = val2.Picture.CullRect;
						float num = 48f / Math.Max(((SKRect)(ref cullRect)).Width, ((SKRect)(ref cullRect)).Height);
						int num2 = (int)(((SKRect)(ref cullRect)).Width * num);
						int num3 = (int)(((SKRect)(ref cullRect)).Height * num);
						val = new SKBitmap(num2, num3, false);
						SKCanvas val3 = new SKCanvas(val);
						try
						{
							val3.Clear(SKColors.Transparent);
							val3.Scale(num);
							val3.DrawPicture(val2.Picture, (SKPaint)null);
						}
						finally
						{
							((IDisposable)val3)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			else
			{
				Console.WriteLine("[X11Window] Loading raster icon");
				val = SKBitmap.Decode(iconPath);
			}
			if (val == null)
			{
				Console.WriteLine("[X11Window] Failed to load icon: " + iconPath);
				return;
			}
			Console.WriteLine($"[X11Window] Loaded bitmap: {val.Width}x{val.Height}");
			int num4 = 64;
			if (val.Width != num4 || val.Height != num4)
			{
				SKBitmap val4 = new SKBitmap(num4, num4, false);
				val.ScalePixels(val4, (SKFilterQuality)3);
				((SKNativeObject)val).Dispose();
				val = val4;
			}
			int width = val.Width;
			int height = val.Height;
			int num5 = 2 + width * height;
			uint[] array = new uint[num5];
			array[0] = (uint)width;
			array[1] = (uint)height;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					SKColor pixel = val.GetPixel(j, i);
					array[2 + i * width + j] = (uint)((((SKColor)(ref pixel)).Alpha << 24) | (((SKColor)(ref pixel)).Red << 16) | (((SKColor)(ref pixel)).Green << 8) | ((SKColor)(ref pixel)).Blue);
				}
			}
			((SKNativeObject)val).Dispose();
			IntPtr property = X11.XInternAtom(_display, "_NET_WM_ICON", onlyIfExists: false);
			IntPtr type = X11.XInternAtom(_display, "CARDINAL", onlyIfExists: false);
			fixed (uint* data = array)
			{
				X11.XChangeProperty(_display, _window, property, type, 32, 0, (nint)data, num5);
			}
			X11.XFlush(_display);
			Console.WriteLine($"[X11Window] Set window icon: {width}x{height}");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[X11Window] Failed to set icon: " + ex.Message);
		}
	}

	public void ProcessEvents()
	{
		int num = X11.XPending(_display);
		if (num > 0)
		{
			if (_eventCounter % 100 == 0)
			{
				Console.WriteLine($"[X11Window] ProcessEvents: {num} pending events");
			}
			_eventCounter++;
			while (X11.XPending(_display) > 0)
			{
				Console.WriteLine("[X11Window] About to call XNextEvent");
				Console.Out.Flush();
				X11.XNextEvent(_display, out var eventReturn);
				Console.WriteLine($"[X11Window] XNextEvent returned, type={eventReturn.Type}");
				Console.Out.Flush();
				HandleEvent(ref eventReturn);
			}
		}
	}

	public void Run()
	{
		_isRunning = true;
		while (_isRunning)
		{
			X11.XNextEvent(_display, out var eventReturn);
			HandleEvent(ref eventReturn);
		}
	}

	public void Stop()
	{
		_isRunning = false;
	}

	private void HandleEvent(ref XEvent xEvent)
	{
		Console.WriteLine($"[X11Window] Event: type={xEvent.Type}");
		switch (xEvent.Type)
		{
		case 2:
			Console.WriteLine("[X11Window] KeyPress event");
			HandleKeyPress(ref xEvent.KeyEvent);
			break;
		case 3:
			HandleKeyRelease(ref xEvent.KeyEvent);
			break;
		case 4:
			Console.WriteLine($"[X11Window] ButtonPress event at ({xEvent.ButtonEvent.X}, {xEvent.ButtonEvent.Y}) button={xEvent.ButtonEvent.Button}");
			HandleButtonPress(ref xEvent.ButtonEvent);
			break;
		case 5:
			HandleButtonRelease(ref xEvent.ButtonEvent);
			break;
		case 6:
			HandleMotion(ref xEvent.MotionEvent);
			break;
		case 12:
			if (xEvent.ExposeEvent.Count == 0)
			{
				this.Exposed?.Invoke(this, EventArgs.Empty);
			}
			break;
		case 22:
			HandleConfigure(ref xEvent.ConfigureEvent);
			break;
		case 9:
			this.FocusGained?.Invoke(this, EventArgs.Empty);
			break;
		case 10:
			this.FocusLost?.Invoke(this, EventArgs.Empty);
			break;
		case 33:
			if (xEvent.ClientMessageEvent.Data.L0 == (long)_wmDeleteMessage)
			{
				this.CloseRequested?.Invoke(this, EventArgs.Empty);
				_isRunning = false;
			}
			break;
		}
	}

	private void HandleKeyPress(ref XKeyEvent keyEvent)
	{
		ulong keysym = KeyMapping.GetKeysym(_display, keyEvent.Keycode, (keyEvent.State & 1) != 0);
		Key key = KeyMapping.FromKeysym(keysym);
		KeyModifiers modifiers = KeyMapping.GetModifiers(keyEvent.State);
		this.KeyDown?.Invoke(this, new KeyEventArgs(key, modifiers));
		bool flag = (keyEvent.State & 4) != 0;
		bool flag2 = (keyEvent.State & 8) != 0;
		if (keysym >= 32 && keysym <= 126 && !flag && !flag2)
		{
			this.TextInput?.Invoke(this, new TextInputEventArgs(((char)keysym).ToString()));
		}
	}

	private void HandleKeyRelease(ref XKeyEvent keyEvent)
	{
		Key key = KeyMapping.FromKeysym(KeyMapping.GetKeysym(_display, keyEvent.Keycode, (keyEvent.State & 1) != 0));
		KeyModifiers modifiers = KeyMapping.GetModifiers(keyEvent.State);
		this.KeyUp?.Invoke(this, new KeyEventArgs(key, modifiers));
	}

	private void HandleButtonPress(ref XButtonEvent buttonEvent)
	{
		Console.WriteLine($"[X11Window] HandleButtonPress: button={buttonEvent.Button}, pos=({buttonEvent.X}, {buttonEvent.Y}), hasHandler={this.PointerPressed != null}");
		if (buttonEvent.Button == 4)
		{
			this.Scroll?.Invoke(this, new ScrollEventArgs(buttonEvent.X, buttonEvent.Y, 0f, -1f));
			return;
		}
		if (buttonEvent.Button == 5)
		{
			this.Scroll?.Invoke(this, new ScrollEventArgs(buttonEvent.X, buttonEvent.Y, 0f, 1f));
			return;
		}
		PointerButton pointerButton = MapButton(buttonEvent.Button);
		Console.WriteLine($"[X11Window] Invoking PointerPressed with button={pointerButton}");
		this.PointerPressed?.Invoke(this, new PointerEventArgs(buttonEvent.X, buttonEvent.Y, pointerButton));
	}

	private void HandleButtonRelease(ref XButtonEvent buttonEvent)
	{
		Console.WriteLine($"[X11Window] HandleButtonRelease: button={buttonEvent.Button}, pos=({buttonEvent.X}, {buttonEvent.Y})");
		if (buttonEvent.Button != 4 && buttonEvent.Button != 5)
		{
			PointerButton button = MapButton(buttonEvent.Button);
			Console.WriteLine($"[X11Window] Invoking PointerReleased, hasHandler={this.PointerReleased != null}");
			this.PointerReleased?.Invoke(this, new PointerEventArgs(buttonEvent.X, buttonEvent.Y, button));
		}
	}

	private void HandleMotion(ref XMotionEvent motionEvent)
	{
		this.PointerMoved?.Invoke(this, new PointerEventArgs(motionEvent.X, motionEvent.Y));
	}

	private void HandleConfigure(ref XConfigureEvent configureEvent)
	{
		if (configureEvent.Width != _width || configureEvent.Height != _height)
		{
			_width = configureEvent.Width;
			_height = configureEvent.Height;
			this.Resized?.Invoke(this, (_width, _height));
		}
	}

	private static PointerButton MapButton(uint button)
	{
		return button switch
		{
			1u => PointerButton.Left, 
			2u => PointerButton.Middle, 
			3u => PointerButton.Right, 
			8u => PointerButton.XButton1, 
			9u => PointerButton.XButton2, 
			_ => PointerButton.None, 
		};
	}

	public int GetFileDescriptor()
	{
		return X11.XConnectionNumber(_display);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (_arrowCursor != IntPtr.Zero)
			{
				X11.XFreeCursor(_display, _arrowCursor);
				_arrowCursor = IntPtr.Zero;
			}
			if (_handCursor != IntPtr.Zero)
			{
				X11.XFreeCursor(_display, _handCursor);
				_handCursor = IntPtr.Zero;
			}
			if (_textCursor != IntPtr.Zero)
			{
				X11.XFreeCursor(_display, _textCursor);
				_textCursor = IntPtr.Zero;
			}
			if (_window != IntPtr.Zero)
			{
				X11.XDestroyWindow(_display, _window);
				_window = IntPtr.Zero;
			}
			if (_display != IntPtr.Zero)
			{
				X11.XCloseDisplay(_display);
				_display = IntPtr.Zero;
			}
			_disposed = true;
		}
	}

	public unsafe void DrawPixels(IntPtr pixels, int width, int height, int stride)
	{
		if (_display == IntPtr.Zero || _window == IntPtr.Zero)
		{
			return;
		}
		IntPtr gc = X11.XDefaultGC(_display, _screen);
		IntPtr visual = X11.XDefaultVisual(_display, _screen);
		int depth = X11.XDefaultDepth(_display, _screen);
		int num = height * stride;
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Buffer.MemoryCopy((void*)pixels, (void*)intPtr, num, num);
			IntPtr intPtr2 = X11.XCreateImage(_display, visual, (uint)depth, 2, 0, intPtr, (uint)width, (uint)height, 32, stride);
			if (intPtr2 != IntPtr.Zero)
			{
				X11.XPutImage(_display, _window, gc, intPtr2, 0, 0, 0, 0, (uint)width, (uint)height);
				X11.XDestroyImage(intPtr2);
			}
			else
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}
		catch
		{
			Marshal.FreeHGlobal(intPtr);
			throw;
		}
		X11.XFlush(_display);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	~X11Window()
	{
		Dispose(disposing: false);
	}
}
