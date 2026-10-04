// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.TestUtils.DeviceTests.Runners;
using SkiaSharp;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// A window of a <see cref="LinuxApplication"/> without a display: the
	/// startup path of an OpenMaui app (LinuxApplication.Lifecycle.cs) minus the
	/// native toplevel. The MAUI window is adopted by a fresh primary
	/// <see cref="WindowContext"/> (which attaches the Linux WindowHandler and
	/// presents modal pages), its page is rendered by
	/// <see cref="LinuxViewRenderer.RenderPage"/> as the app's first page is,
	/// IWindow.Created is raised as the bootstrap raises it, and frames run on the
	/// main thread every 16 ms: animations tick (LinuxTicker), the tree and its
	/// modal layers are measured and arranged at the window size (800x600, the
	/// default window size) and drawn into a raster surface, as
	/// SkiaRenderingEngine.Render does for a native window. Loaded therefore
	/// comes at the first frame, as in an app.
	///
	/// Must be used on the main thread (TestDispatcher). One window is open at a
	/// time, as on MAUI's device runners (ControlsHandlerTestBase serializes
	/// window tests).
	/// </summary>
	public sealed class HeadlessWindowHost
	{
		public const int WindowWidth = 800;
		public const int WindowHeight = 600;
		const int MaxLayoutPasses = 3;

		static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

		static readonly List<HeadlessWindowHost> s_open = new();
		static IDispatcherTimer? s_frameTimer;
		static MethodInfo? s_pumpTicker;
		static FieldInfo? s_layoutRequests;

		readonly SkiaView? _bareRoot;
		bool _closed;

		HeadlessWindowHost(LinuxApplication app, WindowContext context, IWindow? window, IMauiContext mauiContext, SkiaView? bareRoot)
		{
			App = app;
			Context = context;
			Window = window;
			MauiContext = mauiContext;
			_bareRoot = bareRoot;
		}

		public LinuxApplication App { get; }

		public WindowContext Context { get; }

		/// <summary>The MAUI window; null for a bare platform view hosted without one.</summary>
		public IWindow? Window { get; }

		/// <summary>The window-scoped context the window's views are realized with.</summary>
		public IMauiContext MauiContext { get; }

		public SkiaView? Root => Context.RootView;

		/// <summary>The host whose window is open (the last opened), if any.</summary>
		public static HeadlessWindowHost? Current => s_open.Count > 0 ? s_open[s_open.Count - 1] : null;

		/// <summary>The LinuxApplication of the test process (created once, as an app has one).</summary>
		public static LinuxApplication Application =>
			LinuxApplication.Current ?? new LinuxApplication();

		static void AssertMainThread()
		{
			if (!LinuxDispatcher.IsMainThread)
				throw new InvalidOperationException("HeadlessWindowHost must be used on the main thread (InvokeOnMainThreadAsync).");
		}

		/// <summary>
		/// Opens <paramref name="window"/> the way LinuxApplication's startup opens
		/// the app's first window. <paramref name="mauiContext"/> is the test's
		/// context; the window gets a window-scoped LinuxMauiContext over the same
		/// services (MAUI's MakeWindowScope; the Linux bootstrap does the same).
		/// </summary>
		public static HeadlessWindowHost Open(IWindow window, IMauiContext mauiContext)
		{
			AssertMainThread();
			var app = Application;
			var services = mauiContext.Services;
			var scope = services.CreateScope();
			var windowContext = new LinuxMauiContext(scope.ServiceProvider, app);

			// What the bootstrap sets before the first window: the app's context,
			// IPlatformApplication.Current and its root services.
			SetInternal(app, "MauiContext", windowContext);
			SetInternal(app, "RootServices", services);
			IPlatformApplication.Current = app;

			if (window is Controls.Window cw && cw.Parent is Controls.Application mauiApp)
			{
				// The bootstrap pins the theme from the desktop; tests pin Light so
				// colours do not depend on the machine running them.
				Controls.Application.Current = mauiApp;
				if (mauiApp.UserAppTheme == AppTheme.Unspecified)
					mauiApp.UserAppTheme = AppTheme.Light;
			}

			var context = NewPrimaryContext(app);
			var host = new HeadlessWindowHost(app, context, window, windowContext, null);
			s_open.Add(host);
			try
			{
				context.MauiWindow = window;
				// A native window reports its size when the context adopts the MAUI window
				// (WindowContext.ReportFrame); this window has no native toplevel, so it
				// reports the size its frames are laid out at.
				window.FrameChanged(new Rect(0, 0, WindowWidth, WindowHeight));

				SkiaView? root = null;
				if (window is Controls.Window w && w.Page is Page page)
				{
					var renderer = new LinuxViewRenderer(windowContext);
					root = renderer.RenderPage(page);
				}
				if (root is null)
					throw new XunitException($"OpenMaui rendered no view for the page of {window} ({(window as Controls.Window)?.Page?.GetType().Name ?? "no page"}).");
				context.RootView = root;

				// IWindow.Created (and Application.OnStart), as for the startup window.
				Invoke(context, "NotifyCreated", true);

				host.RunFrame();
				EnsureFramePump();
				return host;
			}
			catch
			{
				host.Close();
				throw;
			}
		}

		/// <summary>
		/// Hosts a platform view that has no MAUI window to go in (a bare Skia view
		/// or one whose virtual view is parented to something other than a window):
		/// the root of a window context, laid out at its desired size every frame.
		/// </summary>
		public static HeadlessWindowHost OpenBare(SkiaView root, IMauiContext? mauiContext)
		{
			AssertMainThread();
			var app = Application;
			var context = NewPrimaryContext(app);
			var host = new HeadlessWindowHost(app, context, null, mauiContext!, root);
			s_open.Add(host);
			context.RootView = root;
			host.RunFrame();
			EnsureFramePump();
			return host;
		}

		static WindowContext NewPrimaryContext(LinuxApplication app)
		{
			// A fresh context per window: a context latches the MAUI lifecycle events
			// it has sent (Created, Activated, Destroying), as a native window does.
			// The previous test's context must be gone so this one is the primary.
			var contexts = WindowContexts(app);
			foreach (var stale in contexts.ToList())
			{
				if (s_open.Any(h => h.Context == stale))
					continue;
				contexts.Remove(stale);
				try { stale.Dispose(); } catch { }
			}
			var method = typeof(LinuxApplication).GetMethod("AttachWindowContext", Instance)
				?? throw new MissingMethodException("LinuxApplication.AttachWindowContext");
			return (WindowContext)method.Invoke(app, new object?[] { null, null, false })!;
		}

		static List<WindowContext> WindowContexts(LinuxApplication app) =>
			(List<WindowContext>)(typeof(LinuxApplication).GetField("_windowContexts", Instance)
				?? throw new MissingFieldException("LinuxApplication._windowContexts")).GetValue(app)!;

		/// <summary>
		/// Closes the window: Destroying (unless the test already raised it), the
		/// window handler disconnected, the context removed, as closing the
		/// native window does (LinuxApplication.ReapClosedContexts).
		/// </summary>
		public void Close()
		{
			if (_closed)
				return;
			_closed = true;
			try
			{
				if (Window is Controls.Window w && !w.IsDestroyed)
					Invoke(Context, "NotifyDestroying");
			}
			finally
			{
				// As LinuxApplication.ReapClosedContexts: the context is disposed (its
				// tree unloaded) and dropped, then the window handler disconnected.
				s_open.Remove(this);
				WindowContexts(App).Remove(Context);
				try { Context.Dispose(); } catch (Exception ex) { Console.Error.WriteLine($"[HeadlessWindowHost] dispose failed: {ex}"); }
				try
				{
					(Window as IElement)?.Handler?.DisconnectHandler();
				}
				catch { }
			}
		}

		// ---------------- frames ----------------

		/// <summary>A capture of a view in an open window first renders a frame (pending layout applied).</summary>
		[System.Runtime.CompilerServices.ModuleInitializer]
		internal static void InstallCaptureHook() =>
			AssertionExtensions.BeforeCapture = view => HostOf(view)?.RunFrame();

		static void EnsureFramePump()
		{
			if (s_frameTimer is not null)
				return;
			s_frameTimer = TestDispatcher.Current.CreateTimer();
			s_frameTimer.Interval = TimeSpan.FromMilliseconds(16);
			s_frameTimer.IsRepeating = true;
			s_frameTimer.Tick += (_, _) => Frame();
			s_frameTimer.Start();
		}

		static void Frame()
		{
			PumpTicker();
			foreach (var host in s_open.ToArray())
			{
				try
				{
					host.RunFrame();
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"[HeadlessWindowHost] frame failed: {ex}");
				}
			}
		}

		/// <summary>LinuxApplication's run loop pumps the animation tickers once per iteration.</summary>
		static void PumpTicker()
		{
			s_pumpTicker ??= typeof(LinuxApplication).Assembly
				.GetType("Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker")?
				.GetMethod("PumpAll", Static);
			s_pumpTicker?.Invoke(null, null);
		}

		static int LayoutRequestCount
		{
			get
			{
				s_layoutRequests ??= typeof(SkiaView).GetField("LayoutRequestCount", Static);
				return s_layoutRequests is null ? 0 : (int)s_layoutRequests.GetValue(null)!;
			}
		}

		/// <summary>
		/// One frame, as SkiaRenderingEngine.Render does it: layout until no view
		/// asks for another pass (bounded), then draw the tree, its modal layers
		/// and the popup overlays.
		/// </summary>
		public void RunFrame()
		{
			var root = Context.RootView;
			if (root is null || _closed)
				return;

			Size available;
			if (_bareRoot is not null)
			{
				var desired = root.Measure(new Size(WindowWidth, WindowHeight));
				available = new Size(
					desired.Width > 0 ? Math.Min(desired.Width, WindowWidth) : WindowWidth,
					desired.Height > 0 ? Math.Min(desired.Height, WindowHeight) : WindowHeight);
				if (root.MauiView is null && root.WidthRequest > 0) available.Width = root.WidthRequest;
				if (root.MauiView is null && root.HeightRequest > 0) available.Height = root.HeightRequest;
			}
			else
			{
				available = new Size(WindowWidth, WindowHeight);
			}

			var modals = Context.ModalViews;
			for (int pass = 0; pass < MaxLayoutPasses; pass++)
			{
				int requests = LayoutRequestCount;
				root.Measure(available);
				root.Arrange(new Rect(0, 0, available.Width, available.Height));
				for (int i = 0; i < modals.Count; i++)
				{
					modals[i].Measure(available);
					modals[i].Arrange(new Rect(0, 0, available.Width, available.Height));
				}
				if (LayoutRequestCount == requests)
					break;
			}

			Draw(root, modals, (int)Math.Ceiling(available.Width), (int)Math.Ceiling(available.Height));
		}

		SKSurface? _surface;
		int _surfaceW, _surfaceH;

		void Draw(SkiaView root, IReadOnlyList<SkiaView> modals, int w, int h)
		{
			w = Math.Max(1, w);
			h = Math.Max(1, h);
			if (_surface is null || _surfaceW != w || _surfaceH != h)
			{
				_surface?.Dispose();
				_surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
				_surfaceW = w;
				_surfaceH = h;
			}
			var canvas = _surface.Canvas;
			canvas.Clear(SKColors.White);
			root.Draw(canvas);
			for (int i = 0; i < modals.Count; i++)
				modals[i].Draw(canvas);
			SkiaView.DrawPopupOverlays(canvas, root);
			canvas.Flush();
		}

		/// <summary>The last frame, as a bitmap (what the window shows).</summary>
		public SKBitmap Snapshot()
		{
			RunFrame();
			var bmp = new SKBitmap(new SKImageInfo(_surfaceW, _surfaceH, SKColorType.Bgra8888, SKAlphaType.Premul));
			_surface!.ReadPixels(bmp.Info, bmp.GetPixels(), bmp.RowBytes, 0, 0);
			return bmp;
		}

		/// <summary>True when <paramref name="view"/> is in a tree an open window shows.</summary>
		public static bool IsInOpenWindow(SkiaView view)
		{
			var root = view;
			while (root.Parent is not null)
				root = root.Parent;
			foreach (var host in s_open)
			{
				if (host._closed)
					continue;
				if (ReferenceEquals(host.Context.RootView, root))
					return true;
				foreach (var modal in host.Context.ModalViews)
					if (ReferenceEquals(modal, root))
						return true;
			}
			return false;
		}

		public static HeadlessWindowHost? HostOf(SkiaView view)
		{
			var root = view;
			while (root.Parent is not null)
				root = root.Parent;
			foreach (var host in s_open)
			{
				if (ReferenceEquals(host.Context.RootView, root) || host.Context.ModalViews.Any(m => ReferenceEquals(m, root)))
					return host;
			}
			return null;
		}

		// ---------------- reflection helpers (OpenMaui grants this assembly no internals) ----------------

		static void SetInternal(object target, string property, object? value)
		{
			var p = target.GetType().GetProperty(property, Instance)
				?? throw new MissingMemberException(target.GetType().Name, property);
			p.SetValue(target, value);
		}

		static object? Invoke(object target, string method, params object?[] args)
		{
			var m = target.GetType().GetMethod(method, Instance)
				?? throw new MissingMethodException(target.GetType().Name, method);
			var parameters = m.GetParameters();
			var full = new object?[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
				full[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;
			try
			{
				return m.Invoke(target, full);
			}
			catch (TargetInvocationException tie) when (tie.InnerException is not null)
			{
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
				throw;
			}
		}
	}
}
