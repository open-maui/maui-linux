using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux;

public class LinuxApplication : IDisposable
{
	private static int _invalidateCount;

	private static int _requestRedrawCount;

	private static int _drawCount;

	private static int _gtkThreadId;

	private static DateTime _lastCounterReset = DateTime.Now;

	private X11Window? _mainWindow;

	private GtkHostWindow? _gtkWindow;

	private SkiaRenderingEngine? _renderingEngine;

	private SkiaView? _rootView;

	private SkiaView? _focusedView;

	private SkiaView? _hoveredView;

	private SkiaView? _capturedView;

	private bool _disposed;

	private bool _useGtk;

	private static bool _isRedrawing;

	private static int _loopCounter = 0;

	public static LinuxApplication? Current { get; private set; }

	public static bool IsGtkMode => Current?._useGtk ?? false;

	public X11Window? MainWindow => _mainWindow;

	public SkiaRenderingEngine? RenderingEngine => _renderingEngine;

	public SkiaView? RootView
	{
		get
		{
			return _rootView;
		}
		set
		{
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			_rootView = value;
			if (_rootView != null && _mainWindow != null)
			{
				_rootView.Arrange(new SKRect(0f, 0f, (float)_mainWindow.Width, (float)_mainWindow.Height));
			}
		}
	}

	public SkiaView? FocusedView
	{
		get
		{
			return _focusedView;
		}
		set
		{
			if (_focusedView != value)
			{
				if (_focusedView != null)
				{
					_focusedView.IsFocused = false;
				}
				_focusedView = value;
				if (_focusedView != null)
				{
					_focusedView.IsFocused = true;
				}
			}
		}
	}

	public static void LogInvalidate(string source)
	{
		int currentManagedThreadId = Environment.CurrentManagedThreadId;
		Interlocked.Increment(ref _invalidateCount);
		if (currentManagedThreadId != _gtkThreadId && _gtkThreadId != 0)
		{
			Console.WriteLine($"[DIAG] ⚠\ufe0f Invalidate from WRONG THREAD! GTK={_gtkThreadId}, Current={currentManagedThreadId}, Source={source}");
		}
	}

	public static void LogRequestRedraw()
	{
		int currentManagedThreadId = Environment.CurrentManagedThreadId;
		Interlocked.Increment(ref _requestRedrawCount);
		if (currentManagedThreadId != _gtkThreadId && _gtkThreadId != 0)
		{
			Console.WriteLine($"[DIAG] ⚠\ufe0f RequestRedraw from WRONG THREAD! GTK={_gtkThreadId}, Current={currentManagedThreadId}");
		}
	}

	public static void LogDraw()
	{
		Interlocked.Increment(ref _drawCount);
	}

	private static void StartHeartbeat()
	{
		_gtkThreadId = Environment.CurrentManagedThreadId;
		Console.WriteLine($"[DIAG] GTK thread ID: {_gtkThreadId}");
		GLibNative.TimeoutAdd(250u, delegate
		{
			DateTime now = DateTime.Now;
			if ((now - _lastCounterReset).TotalSeconds >= 1.0)
			{
				int value = Interlocked.Exchange(ref _invalidateCount, 0);
				int value2 = Interlocked.Exchange(ref _requestRedrawCount, 0);
				int value3 = Interlocked.Exchange(ref _drawCount, 0);
				Console.WriteLine($"[DIAG] ❤\ufe0f Heartbeat | Invalidate={value}/s, RequestRedraw={value2}/s, Draw={value3}/s");
				_lastCounterReset = now;
			}
			return true;
		});
	}

	public LinuxApplication()
	{
		Current = this;
		LinuxDialogService.SetInvalidateCallback(delegate
		{
			_renderingEngine?.InvalidateAll();
		});
	}

	public static void Run(MauiApp app, string[] args)
	{
		Run(app, args, null);
	}

	public static void Run(MauiApp app, string[] args, Action<LinuxApplicationOptions>? configure)
	{
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Expected O, but got Unknown
		Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Initialize();
		DispatcherProvider.SetCurrent((IDispatcherProvider)(object)LinuxDispatcherProvider.Instance);
		Console.WriteLine("[LinuxApplication] Dispatcher initialized");
		LinuxApplicationOptions linuxApplicationOptions = app.Services.GetService<LinuxApplicationOptions>() ?? new LinuxApplicationOptions();
		configure?.Invoke(linuxApplicationOptions);
		ParseCommandLineOptions(args, linuxApplicationOptions);
		LinuxApplication linuxApp = new LinuxApplication();
		try
		{
			linuxApp.Initialize(linuxApplicationOptions);
			LinuxMauiContext mauiContext = new LinuxMauiContext(app.Services, linuxApp);
			IApplication service = app.Services.GetService<IApplication>();
			SkiaView skiaView = null;
			Application mauiApplication = (Application)(object)((service is Application) ? service : null);
			if (mauiApplication != null)
			{
				PropertyInfo property = typeof(Application).GetProperty("Current");
				if (property != null && property.CanWrite)
				{
					property.SetValue(null, mauiApplication);
				}
				((BindableObject)mauiApplication).PropertyChanged += delegate(object? s, PropertyChangedEventArgs e)
				{
					//IL_0030: Unknown result type (might be due to invalid IL or missing references)
					if (e.PropertyName == "UserAppTheme")
					{
						Console.WriteLine($"[LinuxApplication] Theme changed to: {mauiApplication.UserAppTheme}");
						LinuxViewRenderer.CurrentSkiaShell?.RefreshTheme();
						linuxApp._renderingEngine?.InvalidateAll();
					}
				};
				if (mauiApplication.MainPage != null)
				{
					Page mainPage = mauiApplication.MainPage;
					List<Window> list = typeof(Application).GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(mauiApplication) as List<Window>;
					if (list != null && list.Count == 0)
					{
						Window val = new Window(mainPage);
						list.Add(val);
						((Element)val).Parent = (Element)(object)mauiApplication;
					}
					else if (list != null && list.Count > 0 && list[0].Page == null)
					{
						list[0].Page = mainPage;
					}
					skiaView = new LinuxViewRenderer((IMauiContext)(object)mauiContext).RenderPage(mainPage);
					string text = "OpenMaui App";
					NavigationPage val2 = (NavigationPage)(object)((mainPage is NavigationPage) ? mainPage : null);
					if (val2 != null)
					{
						text = ((Page)val2).Title ?? text;
					}
					else
					{
						Shell val3 = (Shell)(object)((mainPage is Shell) ? mainPage : null);
						text = ((val3 == null) ? (mainPage.Title ?? text) : (((Page)val3).Title ?? text));
					}
					linuxApp.SetWindowTitle(text);
				}
			}
			if (skiaView == null)
			{
				skiaView = LinuxProgramHost.CreateDemoView();
			}
			linuxApp.RootView = skiaView;
			linuxApp.Run();
		}
		finally
		{
			if (linuxApp != null)
			{
				((IDisposable)linuxApp).Dispose();
			}
		}
	}

	private static void ParseCommandLineOptions(string[] args, LinuxApplicationOptions options)
	{
		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i].ToLowerInvariant())
			{
			case "--title":
				if (i + 1 < args.Length)
				{
					options.Title = args[++i];
				}
				break;
			case "--width":
			{
				if (i + 1 < args.Length && int.TryParse(args[i + 1], out var result2))
				{
					options.Width = result2;
					i++;
				}
				break;
			}
			case "--height":
			{
				if (i + 1 < args.Length && int.TryParse(args[i + 1], out var result))
				{
					options.Height = result;
					i++;
				}
				break;
			}
			}
		}
	}

	public void Initialize(LinuxApplicationOptions options)
	{
		_useGtk = options.UseGtk;
		if (_useGtk)
		{
			InitializeGtk(options);
		}
		else
		{
			InitializeX11(options);
		}
		RegisterServices();
	}

	private void InitializeX11(LinuxApplicationOptions options)
	{
		_mainWindow = new X11Window(options.Title ?? "MAUI Application", options.Width, options.Height);
		SkiaWebView.SetMainWindow(_mainWindow.Display, _mainWindow.Handle);
		string text = ResolveIconPath(options.IconPath);
		if (!string.IsNullOrEmpty(text))
		{
			_mainWindow.SetIcon(text);
		}
		_renderingEngine = new SkiaRenderingEngine(_mainWindow);
		_mainWindow.Resized += OnWindowResized;
		_mainWindow.Exposed += OnWindowExposed;
		_mainWindow.KeyDown += OnKeyDown;
		_mainWindow.KeyUp += OnKeyUp;
		_mainWindow.TextInput += OnTextInput;
		_mainWindow.PointerMoved += OnPointerMoved;
		_mainWindow.PointerPressed += OnPointerPressed;
		_mainWindow.PointerReleased += OnPointerReleased;
		_mainWindow.Scroll += OnScroll;
		_mainWindow.CloseRequested += OnCloseRequested;
	}

	private void InitializeGtk(LinuxApplicationOptions options)
	{
		_gtkWindow = GtkHostService.Instance.GetOrCreateHostWindow(options.Title ?? "MAUI Application", options.Width, options.Height);
		string text = ResolveIconPath(options.IconPath);
		if (!string.IsNullOrEmpty(text))
		{
			GtkHostService.Instance.SetWindowIcon(text);
		}
		if (_gtkWindow.SkiaSurface != null)
		{
			_gtkWindow.SkiaSurface.DrawRequested += OnGtkDrawRequested;
			_gtkWindow.SkiaSurface.PointerPressed += OnGtkPointerPressed;
			_gtkWindow.SkiaSurface.PointerReleased += OnGtkPointerReleased;
			_gtkWindow.SkiaSurface.PointerMoved += OnGtkPointerMoved;
			_gtkWindow.SkiaSurface.KeyPressed += OnGtkKeyPressed;
			_gtkWindow.SkiaSurface.KeyReleased += OnGtkKeyReleased;
			_gtkWindow.SkiaSurface.Scrolled += OnGtkScrolled;
			_gtkWindow.SkiaSurface.TextInput += OnGtkTextInput;
		}
		_gtkWindow.Resized += OnGtkResized;
	}

	private void RegisterServices()
	{
	}

	private static string? ResolveIconPath(string? explicitPath)
	{
		if (!string.IsNullOrEmpty(explicitPath))
		{
			if (Path.IsPathRooted(explicitPath))
			{
				if (!File.Exists(explicitPath))
				{
					return null;
				}
				return explicitPath;
			}
			string text = Path.Combine(AppContext.BaseDirectory, explicitPath);
			if (!File.Exists(text))
			{
				return null;
			}
			return text;
		}
		string baseDirectory = AppContext.BaseDirectory;
		string text2 = Path.Combine(baseDirectory, "appicon.meta");
		if (File.Exists(text2))
		{
			string text3 = MauiIconGenerator.GenerateIcon(text2);
			if (!string.IsNullOrEmpty(text3) && File.Exists(text3))
			{
				return text3;
			}
		}
		string text4 = Path.Combine(baseDirectory, "appicon.png");
		if (File.Exists(text4))
		{
			return text4;
		}
		string text5 = Path.Combine(baseDirectory, "appicon.svg");
		if (File.Exists(text5))
		{
			return text5;
		}
		return null;
	}

	public void SetWindowTitle(string title)
	{
		_mainWindow?.SetTitle(title);
	}

	public static void RequestRedraw()
	{
		LogRequestRedraw();
		if (_isRedrawing)
		{
			return;
		}
		_isRedrawing = true;
		try
		{
			LinuxApplication? current = Current;
			if (current != null && current._useGtk)
			{
				Current._gtkWindow?.RequestRedraw();
			}
			else
			{
				Current?._renderingEngine?.InvalidateAll();
			}
		}
		finally
		{
			_isRedrawing = false;
		}
	}

	public void Run()
	{
		if (_useGtk)
		{
			RunGtk();
		}
		else
		{
			RunX11();
		}
	}

	private void RunX11()
	{
		if (_mainWindow == null)
		{
			throw new InvalidOperationException("Application not initialized");
		}
		_mainWindow.Show();
		Render();
		Console.WriteLine("[LinuxApplication] Starting event loop");
		while (_mainWindow.IsRunning)
		{
			_loopCounter++;
			if (_loopCounter % 1000 == 0)
			{
				Console.WriteLine($"[LinuxApplication] Loop iteration {_loopCounter}");
			}
			_mainWindow.ProcessEvents();
			SkiaWebView.ProcessGtkEvents();
			UpdateAnimations();
			Render();
			Thread.Sleep(1);
		}
		Console.WriteLine("[LinuxApplication] Event loop ended");
	}

	private void RunGtk()
	{
		if (_gtkWindow == null)
		{
			throw new InvalidOperationException("Application not initialized");
		}
		StartHeartbeat();
		PerformGtkLayout(_gtkWindow.Width, _gtkWindow.Height);
		_gtkWindow.RequestRedraw();
		_gtkWindow.Run();
		GtkHostService.Instance.Shutdown();
	}

	private void PerformGtkLayout(int width, int height)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (_rootView != null)
		{
			_rootView.Measure(new SKSize((float)width, (float)height));
			_rootView.Arrange(new SKRect(0f, 0f, (float)width, (float)height));
		}
	}

	private void UpdateAnimations()
	{
		if (_focusedView is SkiaEntry skiaEntry)
		{
			skiaEntry.UpdateCursorBlink();
		}
	}

	private void Render()
	{
		if (_renderingEngine != null && _rootView != null)
		{
			_renderingEngine.Render(_rootView);
		}
	}

	private void OnWindowResized(object? sender, (int Width, int Height) size)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (_rootView != null)
		{
			SKSize availableSize = default(SKSize);
			((SKSize)(ref availableSize))._002Ector((float)size.Width, (float)size.Height);
			_rootView.Measure(availableSize);
			_rootView.Arrange(new SKRect(0f, 0f, (float)size.Width, (float)size.Height));
		}
		_renderingEngine?.InvalidateAll();
	}

	private void OnWindowExposed(object? sender, EventArgs e)
	{
		Render();
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.TopDialog?.OnKeyDown(e);
		}
		else if (_focusedView != null)
		{
			_focusedView.OnKeyDown(e);
		}
	}

	private void OnKeyUp(object? sender, KeyEventArgs e)
	{
		if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.TopDialog?.OnKeyUp(e);
		}
		else if (_focusedView != null)
		{
			_focusedView.OnKeyUp(e);
		}
	}

	private void OnTextInput(object? sender, TextInputEventArgs e)
	{
		if (_focusedView != null)
		{
			_focusedView.OnTextInput(e);
		}
	}

	private void OnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (LinuxDialogService.HasContextMenu)
		{
			LinuxDialogService.ActiveContextMenu?.OnPointerMoved(e);
		}
		else if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.TopDialog?.OnPointerMoved(e);
		}
		else
		{
			if (_rootView == null)
			{
				return;
			}
			if (_capturedView != null)
			{
				_capturedView.OnPointerMoved(e);
				return;
			}
			SkiaView skiaView = SkiaView.GetPopupOwnerAt(e.X, e.Y) ?? _rootView.HitTest(e.X, e.Y);
			if (skiaView != _hoveredView)
			{
				_hoveredView?.OnPointerExited(e);
				_hoveredView = skiaView;
				_hoveredView?.OnPointerEntered(e);
				CursorType cursor = skiaView?.CursorType ?? CursorType.Arrow;
				_mainWindow?.SetCursor(cursor);
			}
			skiaView?.OnPointerMoved(e);
		}
	}

	private void OnPointerPressed(object? sender, PointerEventArgs e)
	{
		Console.WriteLine($"[LinuxApplication] OnPointerPressed at ({e.X}, {e.Y})");
		if (LinuxDialogService.HasContextMenu)
		{
			LinuxDialogService.ActiveContextMenu?.OnPointerPressed(e);
		}
		else if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.TopDialog?.OnPointerPressed(e);
		}
		else
		{
			if (_rootView == null)
			{
				return;
			}
			SkiaView skiaView = SkiaView.GetPopupOwnerAt(e.X, e.Y) ?? _rootView.HitTest(e.X, e.Y);
			Console.WriteLine("[LinuxApplication] HitView: " + (((object)skiaView)?.GetType().Name ?? "null") + ", rootView: " + ((object)_rootView).GetType().Name);
			if (skiaView != null)
			{
				_capturedView = skiaView;
				if (skiaView.IsFocusable)
				{
					FocusedView = skiaView;
				}
				Console.WriteLine("[LinuxApplication] Calling OnPointerPressed on " + ((object)skiaView).GetType().Name);
				skiaView.OnPointerPressed(e);
			}
			else
			{
				if (SkiaView.HasActivePopup && _focusedView != null)
				{
					_focusedView.OnFocusLost();
				}
				FocusedView = null;
			}
		}
	}

	private void OnPointerReleased(object? sender, PointerEventArgs e)
	{
		Console.WriteLine($"[LinuxApplication] OnPointerReleased at ({e.X}, {e.Y}), capturedView={((object)_capturedView)?.GetType().Name ?? "null"}");
		if (LinuxDialogService.HasActiveDialog)
		{
			LinuxDialogService.TopDialog?.OnPointerReleased(e);
		}
		else if (_rootView != null)
		{
			if (_capturedView != null)
			{
				_capturedView.OnPointerReleased(e);
				_capturedView = null;
			}
			else
			{
				(SkiaView.GetPopupOwnerAt(e.X, e.Y) ?? _rootView.HitTest(e.X, e.Y))?.OnPointerReleased(e);
			}
		}
	}

	private void OnScroll(object? sender, ScrollEventArgs e)
	{
		Console.WriteLine($"[LinuxApplication] OnScroll - X={e.X}, Y={e.Y}, DeltaX={e.DeltaX}, DeltaY={e.DeltaY}");
		if (_rootView == null)
		{
			return;
		}
		SkiaView skiaView = _rootView.HitTest(e.X, e.Y);
		Console.WriteLine("[LinuxApplication] HitView: " + (((object)skiaView)?.GetType().Name ?? "null"));
		for (SkiaView skiaView2 = skiaView; skiaView2 != null; skiaView2 = skiaView2.Parent)
		{
			Console.WriteLine("[LinuxApplication] Bubbling to: " + ((object)skiaView2).GetType().Name);
			if (skiaView2 is SkiaScrollView skiaScrollView)
			{
				skiaScrollView.OnScroll(e);
				break;
			}
			skiaView2.OnScroll(e);
			if (e.Handled)
			{
				break;
			}
		}
	}

	private void OnCloseRequested(object? sender, EventArgs e)
	{
		_mainWindow?.Stop();
	}

	private void OnGtkDrawRequested(object? sender, EventArgs e)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		Console.WriteLine("[DIAG] >>> OnGtkDrawRequested ENTER");
		LogDraw();
		GtkSkiaSurfaceWidget gtkSkiaSurfaceWidget = _gtkWindow?.SkiaSurface;
		if (gtkSkiaSurfaceWidget?.Canvas != null && _rootView != null)
		{
			Application current = Application.Current;
			SKColor val = (SKColor)((current != null && (int)current.UserAppTheme == 2) ? new SKColor((byte)32, (byte)33, (byte)36) : SKColors.White);
			gtkSkiaSurfaceWidget.Canvas.Clear(val);
			Console.WriteLine("[DIAG] Drawing rootView...");
			_rootView.Draw(gtkSkiaSurfaceWidget.Canvas);
			Console.WriteLine("[DIAG] Drawing dialogs...");
			SKRect bounds = default(SKRect);
			((SKRect)(ref bounds))._002Ector(0f, 0f, (float)gtkSkiaSurfaceWidget.Width, (float)gtkSkiaSurfaceWidget.Height);
			LinuxDialogService.DrawDialogs(gtkSkiaSurfaceWidget.Canvas, bounds);
			Console.WriteLine("[DIAG] <<< OnGtkDrawRequested EXIT");
		}
	}

	private void OnGtkResized(object? sender, (int Width, int Height) size)
	{
		PerformGtkLayout(size.Width, size.Height);
		_gtkWindow?.RequestRedraw();
	}

	private void OnGtkPointerPressed(object? sender, (double X, double Y, int Button) e)
	{
		string value = ((e.Button == 1) ? "Left" : ((e.Button == 2) ? "Middle" : ((e.Button == 3) ? "Right" : $"Unknown({e.Button})")));
		Console.WriteLine($"[LinuxApplication.GTK] PointerPressed at ({e.X:F1}, {e.Y:F1}), Button={e.Button} ({value})");
		if (LinuxDialogService.HasContextMenu)
		{
			PointerButton button = ((e.Button == 1) ? PointerButton.Left : ((e.Button == 2) ? PointerButton.Middle : PointerButton.Right));
			PointerEventArgs e2 = new PointerEventArgs((float)e.X, (float)e.Y, button);
			LinuxDialogService.ActiveContextMenu?.OnPointerPressed(e2);
			_gtkWindow?.RequestRedraw();
			return;
		}
		if (_rootView == null)
		{
			Console.WriteLine("[LinuxApplication.GTK] _rootView is null!");
			return;
		}
		SkiaView skiaView = _rootView.HitTest((float)e.X, (float)e.Y);
		Console.WriteLine("[LinuxApplication.GTK] HitView: " + (((object)skiaView)?.GetType().Name ?? "null"));
		if (skiaView != null)
		{
			if (skiaView.IsFocusable && _focusedView != skiaView)
			{
				_focusedView?.OnFocusLost();
				_focusedView = skiaView;
				_focusedView.OnFocusGained();
			}
			_capturedView = skiaView;
			PointerButton button2 = ((e.Button == 1) ? PointerButton.Left : ((e.Button == 2) ? PointerButton.Middle : PointerButton.Right));
			PointerEventArgs e3 = new PointerEventArgs((float)e.X, (float)e.Y, button2);
			Console.WriteLine("[DIAG] >>> Before OnPointerPressed");
			skiaView.OnPointerPressed(e3);
			Console.WriteLine("[DIAG] <<< After OnPointerPressed, calling RequestRedraw");
			_gtkWindow?.RequestRedraw();
			Console.WriteLine("[DIAG] <<< After RequestRedraw, returning from handler");
		}
	}

	private void OnGtkPointerReleased(object? sender, (double X, double Y, int Button) e)
	{
		Console.WriteLine("[DIAG] >>> OnGtkPointerReleased ENTER");
		if (_rootView == null)
		{
			return;
		}
		if (_capturedView != null)
		{
			PointerButton button = ((e.Button == 1) ? PointerButton.Left : ((e.Button == 2) ? PointerButton.Middle : PointerButton.Right));
			PointerEventArgs e2 = new PointerEventArgs((float)e.X, (float)e.Y, button);
			Console.WriteLine("[DIAG] Calling OnPointerReleased on " + ((object)_capturedView).GetType().Name);
			_capturedView.OnPointerReleased(e2);
			Console.WriteLine("[DIAG] OnPointerReleased returned");
			_capturedView = null;
			_gtkWindow?.RequestRedraw();
			Console.WriteLine("[DIAG] <<< OnGtkPointerReleased EXIT (captured path)");
		}
		else
		{
			SkiaView skiaView = _rootView.HitTest((float)e.X, (float)e.Y);
			if (skiaView != null)
			{
				PointerButton button2 = ((e.Button == 1) ? PointerButton.Left : ((e.Button == 2) ? PointerButton.Middle : PointerButton.Right));
				PointerEventArgs e3 = new PointerEventArgs((float)e.X, (float)e.Y, button2);
				skiaView.OnPointerReleased(e3);
				_gtkWindow?.RequestRedraw();
			}
		}
	}

	private void OnGtkPointerMoved(object? sender, (double X, double Y) e)
	{
		if (LinuxDialogService.HasContextMenu)
		{
			PointerEventArgs e2 = new PointerEventArgs((float)e.X, (float)e.Y);
			LinuxDialogService.ActiveContextMenu?.OnPointerMoved(e2);
			_gtkWindow?.RequestRedraw();
		}
		else
		{
			if (_rootView == null)
			{
				return;
			}
			if (_capturedView != null)
			{
				PointerEventArgs e3 = new PointerEventArgs((float)e.X, (float)e.Y);
				_capturedView.OnPointerMoved(e3);
				_gtkWindow?.RequestRedraw();
				return;
			}
			SkiaView skiaView = _rootView.HitTest((float)e.X, (float)e.Y);
			if (skiaView != _hoveredView)
			{
				PointerEventArgs e4 = new PointerEventArgs((float)e.X, (float)e.Y);
				_hoveredView?.OnPointerExited(e4);
				_hoveredView = skiaView;
				_hoveredView?.OnPointerEntered(e4);
				_gtkWindow?.RequestRedraw();
			}
			if (skiaView != null)
			{
				PointerEventArgs e5 = new PointerEventArgs((float)e.X, (float)e.Y);
				skiaView.OnPointerMoved(e5);
			}
		}
	}

	private void OnGtkKeyPressed(object? sender, (uint KeyVal, uint KeyCode, uint State) e)
	{
		if (_focusedView != null)
		{
			Key key = ConvertGdkKey(e.KeyVal);
			KeyModifiers modifiers = ConvertGdkModifiers(e.State);
			KeyEventArgs e2 = new KeyEventArgs(key, modifiers);
			_focusedView.OnKeyDown(e2);
			_gtkWindow?.RequestRedraw();
		}
	}

	private void OnGtkKeyReleased(object? sender, (uint KeyVal, uint KeyCode, uint State) e)
	{
		if (_focusedView != null)
		{
			Key key = ConvertGdkKey(e.KeyVal);
			KeyModifiers modifiers = ConvertGdkModifiers(e.State);
			KeyEventArgs e2 = new KeyEventArgs(key, modifiers);
			_focusedView.OnKeyUp(e2);
			_gtkWindow?.RequestRedraw();
		}
	}

	private void OnGtkScrolled(object? sender, (double X, double Y, double DeltaX, double DeltaY) e)
	{
		if (_rootView == null)
		{
			return;
		}
		for (SkiaView skiaView = _rootView.HitTest((float)e.X, (float)e.Y); skiaView != null; skiaView = skiaView.Parent)
		{
			if (skiaView is SkiaScrollView skiaScrollView)
			{
				ScrollEventArgs e2 = new ScrollEventArgs((float)e.X, (float)e.Y, (float)e.DeltaX, (float)e.DeltaY);
				skiaScrollView.OnScroll(e2);
				_gtkWindow?.RequestRedraw();
				break;
			}
		}
	}

	private void OnGtkTextInput(object? sender, string text)
	{
		if (_focusedView != null)
		{
			TextInputEventArgs e = new TextInputEventArgs(text);
			_focusedView.OnTextInput(e);
			_gtkWindow?.RequestRedraw();
		}
	}

	private static Key ConvertGdkKey(uint keyval)
	{
		switch (keyval)
		{
		case 65288u:
			return Key.Backspace;
		case 65289u:
			return Key.Tab;
		case 65293u:
			return Key.Enter;
		case 65307u:
			return Key.Escape;
		case 65360u:
			return Key.Home;
		case 65361u:
			return Key.Left;
		case 65362u:
			return Key.Up;
		case 65363u:
			return Key.Right;
		case 65364u:
			return Key.Down;
		case 65365u:
			return Key.PageUp;
		case 65366u:
			return Key.PageDown;
		case 65367u:
			return Key.End;
		case 65535u:
			return Key.Delete;
		case 32u:
		case 33u:
		case 34u:
		case 35u:
		case 36u:
		case 37u:
		case 38u:
		case 39u:
		case 40u:
		case 41u:
		case 42u:
		case 43u:
		case 44u:
		case 45u:
		case 46u:
		case 47u:
		case 48u:
		case 49u:
		case 50u:
		case 51u:
		case 52u:
		case 53u:
		case 54u:
		case 55u:
		case 56u:
		case 57u:
		case 58u:
		case 59u:
		case 60u:
		case 61u:
		case 62u:
		case 63u:
		case 64u:
		case 65u:
		case 66u:
		case 67u:
		case 68u:
		case 69u:
		case 70u:
		case 71u:
		case 72u:
		case 73u:
		case 74u:
		case 75u:
		case 76u:
		case 77u:
		case 78u:
		case 79u:
		case 80u:
		case 81u:
		case 82u:
		case 83u:
		case 84u:
		case 85u:
		case 86u:
		case 87u:
		case 88u:
		case 89u:
		case 90u:
		case 91u:
		case 92u:
		case 93u:
		case 94u:
		case 95u:
		case 96u:
		case 97u:
		case 98u:
		case 99u:
		case 100u:
		case 101u:
		case 102u:
		case 103u:
		case 104u:
		case 105u:
		case 106u:
		case 107u:
		case 108u:
		case 109u:
		case 110u:
		case 111u:
		case 112u:
		case 113u:
		case 114u:
		case 115u:
		case 116u:
		case 117u:
		case 118u:
		case 119u:
		case 120u:
		case 121u:
		case 122u:
		case 123u:
		case 124u:
		case 125u:
		case 126u:
			return (Key)keyval;
		default:
			return Key.Unknown;
		}
	}

	private static KeyModifiers ConvertGdkModifiers(uint state)
	{
		KeyModifiers keyModifiers = KeyModifiers.None;
		if ((state & 1) != 0)
		{
			keyModifiers |= KeyModifiers.Shift;
		}
		if ((state & 4) != 0)
		{
			keyModifiers |= KeyModifiers.Control;
		}
		if ((state & 8) != 0)
		{
			keyModifiers |= KeyModifiers.Alt;
		}
		return keyModifiers;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_renderingEngine?.Dispose();
			_mainWindow?.Dispose();
			if (Current == this)
			{
				Current = null;
			}
			_disposed = true;
		}
	}
}
