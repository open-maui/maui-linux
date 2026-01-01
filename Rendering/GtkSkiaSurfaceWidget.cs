using System;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Native;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

public sealed class GtkSkiaSurfaceWidget : IDisposable
{
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool DrawCallback(IntPtr widget, IntPtr cairoContext, IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool ConfigureCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool ButtonEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool MotionEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool KeyEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool ScrollEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);

	private struct GdkEventButton
	{
		public int type;

		public IntPtr window;

		public sbyte send_event;

		public uint time;

		public double x;

		public double y;

		public IntPtr axes;

		public uint state;

		public uint button;
	}

	private struct GdkEventMotion
	{
		public int type;

		public IntPtr window;

		public sbyte send_event;

		public uint time;

		public double x;

		public double y;
	}

	private struct GdkEventKey
	{
		public int type;

		public IntPtr window;

		public sbyte send_event;

		public uint time;

		public uint state;

		public uint keyval;

		public int length;

		public IntPtr str;

		public ushort hardware_keycode;
	}

	private struct GdkEventScroll
	{
		public int type;

		public IntPtr window;

		public sbyte send_event;

		public uint time;

		public double x;

		public double y;

		public uint state;

		public int direction;

		public IntPtr device;

		public double x_root;

		public double y_root;

		public double delta_x;

		public double delta_y;
	}

	private IntPtr _widget;

	private SKImageInfo _imageInfo;

	private SKBitmap? _bitmap;

	private SKCanvas? _canvas;

	private IntPtr _cairoSurface;

	private readonly DrawCallback _drawCallback;

	private readonly ConfigureCallback _configureCallback;

	private ulong _drawSignalId;

	private ulong _configureSignalId;

	private bool _isTransparent;

	private readonly ButtonEventCallback _buttonPressCallback;

	private readonly ButtonEventCallback _buttonReleaseCallback;

	private readonly MotionEventCallback _motionCallback;

	private readonly KeyEventCallback _keyPressCallback;

	private readonly KeyEventCallback _keyReleaseCallback;

	private readonly ScrollEventCallback _scrollCallback;

	public IntPtr Widget => _widget;

	public SKCanvas? Canvas => _canvas;

	public SKImageInfo ImageInfo => _imageInfo;

	public int Width => ((SKImageInfo)(ref _imageInfo)).Width;

	public int Height => ((SKImageInfo)(ref _imageInfo)).Height;

	public bool IsTransparent => _isTransparent;

	public event EventHandler? DrawRequested;

	public event EventHandler<(int Width, int Height)>? Resized;

	public event EventHandler<(double X, double Y, int Button)>? PointerPressed;

	public event EventHandler<(double X, double Y, int Button)>? PointerReleased;

	public event EventHandler<(double X, double Y)>? PointerMoved;

	public event EventHandler<(uint KeyVal, uint KeyCode, uint State)>? KeyPressed;

	public event EventHandler<(uint KeyVal, uint KeyCode, uint State)>? KeyReleased;

	public event EventHandler<(double X, double Y, double DeltaX, double DeltaY)>? Scrolled;

	public event EventHandler<string>? TextInput;

	public GtkSkiaSurfaceWidget(int width, int height)
	{
		_widget = GtkNative.gtk_drawing_area_new();
		if (_widget == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to create GTK drawing area");
		}
		GtkNative.gtk_widget_set_size_request(_widget, width, height);
		GtkNative.gtk_widget_add_events(_widget, 10551046);
		GtkNative.gtk_widget_set_can_focus(_widget, canFocus: true);
		CreateBuffer(width, height);
		_drawCallback = OnDraw;
		_configureCallback = OnConfigure;
		_buttonPressCallback = OnButtonPress;
		_buttonReleaseCallback = OnButtonRelease;
		_motionCallback = OnMotion;
		_keyPressCallback = OnKeyPress;
		_keyReleaseCallback = OnKeyRelease;
		_scrollCallback = OnScroll;
		_drawSignalId = GtkNative.g_signal_connect_data(_widget, "draw", Marshal.GetFunctionPointerForDelegate(_drawCallback), IntPtr.Zero, IntPtr.Zero, 0);
		_configureSignalId = GtkNative.g_signal_connect_data(_widget, "configure-event", Marshal.GetFunctionPointerForDelegate(_configureCallback), IntPtr.Zero, IntPtr.Zero, 0);
		GtkNative.g_signal_connect_data(_widget, "button-press-event", Marshal.GetFunctionPointerForDelegate(_buttonPressCallback), IntPtr.Zero, IntPtr.Zero, 0);
		GtkNative.g_signal_connect_data(_widget, "button-release-event", Marshal.GetFunctionPointerForDelegate(_buttonReleaseCallback), IntPtr.Zero, IntPtr.Zero, 0);
		GtkNative.g_signal_connect_data(_widget, "motion-notify-event", Marshal.GetFunctionPointerForDelegate(_motionCallback), IntPtr.Zero, IntPtr.Zero, 0);
		GtkNative.g_signal_connect_data(_widget, "key-press-event", Marshal.GetFunctionPointerForDelegate(_keyPressCallback), IntPtr.Zero, IntPtr.Zero, 0);
		GtkNative.g_signal_connect_data(_widget, "key-release-event", Marshal.GetFunctionPointerForDelegate(_keyReleaseCallback), IntPtr.Zero, IntPtr.Zero, 0);
		GtkNative.g_signal_connect_data(_widget, "scroll-event", Marshal.GetFunctionPointerForDelegate(_scrollCallback), IntPtr.Zero, IntPtr.Zero, 0);
		Console.WriteLine($"[GtkSkiaSurfaceWidget] Created with size {width}x{height}");
	}

	private void CreateBuffer(int width, int height)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		width = Math.Max(1, width);
		height = Math.Max(1, height);
		SKCanvas? canvas = _canvas;
		if (canvas != null)
		{
			((SKNativeObject)canvas).Dispose();
		}
		SKBitmap? bitmap = _bitmap;
		if (bitmap != null)
		{
			((SKNativeObject)bitmap).Dispose();
		}
		if (_cairoSurface != IntPtr.Zero)
		{
			CairoNative.cairo_surface_destroy(_cairoSurface);
			_cairoSurface = IntPtr.Zero;
		}
		_imageInfo = new SKImageInfo(width, height, (SKColorType)6, (SKAlphaType)2);
		_bitmap = new SKBitmap(_imageInfo);
		_canvas = new SKCanvas(_bitmap);
		IntPtr pixels = _bitmap.GetPixels();
		_cairoSurface = CairoNative.cairo_image_surface_create_for_data(pixels, CairoNative.cairo_format_t.CAIRO_FORMAT_ARGB32, ((SKImageInfo)(ref _imageInfo)).Width, ((SKImageInfo)(ref _imageInfo)).Height, ((SKImageInfo)(ref _imageInfo)).RowBytes);
		Console.WriteLine($"[GtkSkiaSurfaceWidget] Created buffer {width}x{height}, stride={((SKImageInfo)(ref _imageInfo)).RowBytes}");
	}

	public void Resize(int width, int height)
	{
		if (width != ((SKImageInfo)(ref _imageInfo)).Width || height != ((SKImageInfo)(ref _imageInfo)).Height)
		{
			CreateBuffer(width, height);
			this.Resized?.Invoke(this, (width, height));
		}
	}

	public void RenderFrame(Action<SKCanvas, SKImageInfo> render)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (_canvas != null && _bitmap != null)
		{
			render(_canvas, _imageInfo);
			_canvas.Flush();
			CairoNative.cairo_surface_flush(_cairoSurface);
			CairoNative.cairo_surface_mark_dirty(_cairoSurface);
			GtkNative.gtk_widget_queue_draw(_widget);
		}
	}

	public void Invalidate()
	{
		GtkNative.gtk_widget_queue_draw(_widget);
	}

	public void SetTransparent(bool transparent)
	{
		_isTransparent = transparent;
	}

	private bool OnDraw(IntPtr widget, IntPtr cairoContext, IntPtr userData)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (_cairoSurface == IntPtr.Zero || cairoContext == IntPtr.Zero)
		{
			return false;
		}
		if (_isTransparent)
		{
			SKCanvas? canvas = _canvas;
			if (canvas != null)
			{
				canvas.Clear(SKColors.Transparent);
			}
		}
		this.DrawRequested?.Invoke(this, EventArgs.Empty);
		SKCanvas? canvas2 = _canvas;
		if (canvas2 != null)
		{
			canvas2.Flush();
		}
		CairoNative.cairo_surface_flush(_cairoSurface);
		CairoNative.cairo_surface_mark_dirty(_cairoSurface);
		CairoNative.cairo_set_source_surface(cairoContext, _cairoSurface, 0.0, 0.0);
		CairoNative.cairo_paint(cairoContext);
		return true;
	}

	private bool OnConfigure(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		GtkNative.gtk_widget_get_allocation(widget, out var allocation);
		if (allocation.Width > 0 && allocation.Height > 0 && (allocation.Width != ((SKImageInfo)(ref _imageInfo)).Width || allocation.Height != ((SKImageInfo)(ref _imageInfo)).Height))
		{
			Resize(allocation.Width, allocation.Height);
		}
		return false;
	}

	private bool OnButtonPress(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		GtkNative.gtk_widget_grab_focus(_widget);
		var (num, num2, num3) = ParseButtonEvent(eventData);
		Console.WriteLine($"[GtkSkiaSurfaceWidget] ButtonPress at ({num}, {num2}), button={num3}");
		this.PointerPressed?.Invoke(this, (num, num2, num3));
		return true;
	}

	private bool OnButtonRelease(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		var (item, item2, item3) = ParseButtonEvent(eventData);
		this.PointerReleased?.Invoke(this, (item, item2, item3));
		return true;
	}

	private bool OnMotion(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		var (item, item2) = ParseMotionEvent(eventData);
		this.PointerMoved?.Invoke(this, (item, item2));
		return true;
	}

	public void RaisePointerPressed(double x, double y, int button)
	{
		Console.WriteLine($"[GtkSkiaSurfaceWidget] RaisePointerPressed at ({x}, {y}), button={button}");
		this.PointerPressed?.Invoke(this, (x, y, button));
	}

	public void RaisePointerReleased(double x, double y, int button)
	{
		this.PointerReleased?.Invoke(this, (x, y, button));
	}

	public void RaisePointerMoved(double x, double y)
	{
		this.PointerMoved?.Invoke(this, (x, y));
	}

	private bool OnKeyPress(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		var (num, item, item2) = ParseKeyEvent(eventData);
		this.KeyPressed?.Invoke(this, (num, item, item2));
		uint num2 = GdkNative.gdk_keyval_to_unicode(num);
		if (num2 != 0 && num2 < 65536)
		{
			char c = (char)num2;
			if (!char.IsControl(c) || c == '\r' || c == '\n' || c == '\t')
			{
				string text = c.ToString();
				Console.WriteLine($"[GtkSkiaSurfaceWidget] TextInput: '{text}' (keyval={num}, unicode={num2})");
				this.TextInput?.Invoke(this, text);
			}
		}
		return true;
	}

	private bool OnKeyRelease(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		var (item, item2, item3) = ParseKeyEvent(eventData);
		this.KeyReleased?.Invoke(this, (item, item2, item3));
		return true;
	}

	private bool OnScroll(IntPtr widget, IntPtr eventData, IntPtr userData)
	{
		var (item, item2, item3, item4) = ParseScrollEvent(eventData);
		this.Scrolled?.Invoke(this, (item, item2, item3, item4));
		return true;
	}

	private static (double x, double y, int button) ParseButtonEvent(IntPtr eventData)
	{
		GdkEventButton gdkEventButton = Marshal.PtrToStructure<GdkEventButton>(eventData);
		return (x: gdkEventButton.x, y: gdkEventButton.y, button: (int)gdkEventButton.button);
	}

	private static (double x, double y) ParseMotionEvent(IntPtr eventData)
	{
		GdkEventMotion gdkEventMotion = Marshal.PtrToStructure<GdkEventMotion>(eventData);
		return (x: gdkEventMotion.x, y: gdkEventMotion.y);
	}

	private static (uint keyval, uint keycode, uint state) ParseKeyEvent(IntPtr eventData)
	{
		GdkEventKey gdkEventKey = Marshal.PtrToStructure<GdkEventKey>(eventData);
		return (keyval: gdkEventKey.keyval, keycode: gdkEventKey.hardware_keycode, state: gdkEventKey.state);
	}

	private static (double x, double y, double deltaX, double deltaY) ParseScrollEvent(IntPtr eventData)
	{
		GdkEventScroll gdkEventScroll = Marshal.PtrToStructure<GdkEventScroll>(eventData);
		double item = 0.0;
		double item2 = 0.0;
		if (gdkEventScroll.direction == 4)
		{
			item = gdkEventScroll.delta_x;
			item2 = gdkEventScroll.delta_y;
		}
		else
		{
			switch (gdkEventScroll.direction)
			{
			case 0:
				item2 = -1.0;
				break;
			case 1:
				item2 = 1.0;
				break;
			case 2:
				item = -1.0;
				break;
			case 3:
				item = 1.0;
				break;
			}
		}
		return (x: gdkEventScroll.x, y: gdkEventScroll.y, deltaX: item, deltaY: item2);
	}

	public void GrabFocus()
	{
		GtkNative.gtk_widget_grab_focus(_widget);
	}

	public void Dispose()
	{
		SKCanvas? canvas = _canvas;
		if (canvas != null)
		{
			((SKNativeObject)canvas).Dispose();
		}
		_canvas = null;
		SKBitmap? bitmap = _bitmap;
		if (bitmap != null)
		{
			((SKNativeObject)bitmap).Dispose();
		}
		_bitmap = null;
		if (_cairoSurface != IntPtr.Zero)
		{
			CairoNative.cairo_surface_destroy(_cairoSurface);
			_cairoSurface = IntPtr.Zero;
		}
	}
}
