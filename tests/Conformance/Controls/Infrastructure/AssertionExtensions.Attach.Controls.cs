// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform;
using Microsoft.Maui.TestUtils.DeviceTests.Runners;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Controls suite: what "attached" means for a platform view (MAUI's
	/// AssertionExtensions.Windows.cs AttachAndRun puts the view in a window,
	/// centred at its own size, and waits for Loaded).
	///
	/// A view already in an open headless window runs as is. A Controls view
	/// that is in no window is put in one the way an app shows a view: as the
	/// content of a ContentPage in a Window, centred at its desired size
	/// (a centred VerticalStackLayout, the Linux counterpart of the Windows
	/// runner's centred Grid), so it is Loaded and laid out by frames. A bare
	/// Skia view with no MAUI element becomes the root of a window context.
	/// </summary>
	public static partial class AssertionExtensions
	{
		public static Task AttachAndRun(this object view, Action action) =>
			view.AttachAndRun<bool>(() => { action(); return Task.FromResult(true); });

		public static Task AttachAndRun(this object view, Func<Task> action) =>
			view.AttachAndRun<bool>(async () => { await action(); return true; });

		public static Task<T> AttachAndRun<T>(this object view, Func<T> action) =>
			view.AttachAndRun<T>(() => Task.FromResult(action()));

		public static Task<T> AttachAndRun<T>(this object view, Func<Task<T>> action)
		{
			var dispatcher = TestDispatcher.Current;
			if (dispatcher.IsDispatchRequired)
				return dispatcher.DispatchAsync(() => AttachAndRunOnMainThread(view, action));
			return AttachAndRunOnMainThread(view, action);
		}

		static async Task<T> AttachAndRunOnMainThread<T>(object view, Func<Task<T>> action)
		{
			var skia = view.AsSkia();
			if (HeadlessWindowHost.IsInOpenWindow(skia))
			{
				HeadlessWindowHost.HostOf(skia)?.RunFrame();
				return await action();
			}

			var root = skia;
			while (root.Parent is not null)
				root = root.Parent;

			HeadlessWindowHost host;
			VerticalStackLayout? wrapper = null;
			View? hosted = null;
			if (root.MauiView is View mauiView && mauiView.Parent is null && mauiView.Handler?.MauiContext is IMauiContext context)
			{
				hosted = mauiView;
				wrapper = new VerticalStackLayout
				{
					HorizontalOptions = LayoutOptions.Center,
					VerticalOptions = LayoutOptions.Center,
				};
				wrapper.Add(mauiView);
				var window = new Controls.Window(new ContentPage { Content = wrapper });
				if (context.Services.GetService<IApplication>() is ApplicationStub app)
				{
					app.SetWindow(window);
					_ = ((IApplication)app).CreateWindow(null);
				}
				host = HeadlessWindowHost.Open(window, context);
			}
			else
			{
				host = HeadlessWindowHost.OpenBare(root, (root.MauiView as IElement)?.Handler?.MauiContext);
			}

			try
			{
				// Loaded and the first layout come with the first frame (run by Open);
				// let their handlers run before the action, as the Windows runner
				// waits for Loaded.
				await Task.Yield();
				return await action();
			}
			finally
			{
				host.Close();
				if (wrapper is not null && hosted is not null)
					wrapper.Remove(hosted);
			}
		}

		public static object? GetParent(this object? view) => (view as SkiaView)?.Parent;

		/// <summary>Loaded on the platform: in a tree an open window shows, and laid out.</summary>
		public static bool IsLoaded(this object? view) =>
			(view as SkiaView ?? (view as IElement)?.Handler?.PlatformView as SkiaView) is SkiaView skia
			&& HeadlessWindowHost.IsInOpenWindow(skia);

		public static IDisposable OnLoaded(this object view, Action action)
		{
			if (view.IsLoaded())
			{
				action();
				return new ActionDisposable(() => { });
			}
			var skia = view.AsSkia();
			var element = skia.MauiView as VisualElement;
			if (element is null)
				throw new Xunit.Sdk.XunitException($"{skia.GetType().Name} is not loaded and has no MAUI element to report Loaded.");
			EventHandler? handler = null;
			handler = (_, _) =>
			{
				element.Loaded -= handler;
				action();
			};
			element.Loaded += handler;
			return new ActionDisposable(() => element.Loaded -= handler);
		}

		public static IDisposable OnUnloaded(this object view, Action action)
		{
			var skia = view.AsSkia();
			if (!view.IsLoaded())
			{
				action();
				return new ActionDisposable(() => { });
			}
			var element = skia.MauiView as VisualElement;
			if (element is null)
				throw new Xunit.Sdk.XunitException($"{skia.GetType().Name} has no MAUI element to report Unloaded.");
			EventHandler? handler = null;
			handler = (_, _) =>
			{
				element.Unloaded -= handler;
				action();
			};
			element.Unloaded += handler;
			return new ActionDisposable(() => element.Unloaded -= handler);
		}

		sealed class ActionDisposable : IDisposable
		{
			readonly Action _action;
			public ActionDisposable(Action action) => _action = action;
			public void Dispose() => _action();
		}
	}
}

namespace Microsoft.Maui.DeviceTests
{
	public static partial class AssertionExtensions
	{
		/// <summary>MAUI's platform GetBoundingBox(platformView): the view's bounds in the window.</summary>
		public static Microsoft.Maui.Graphics.Rect GetBoundingBox(this object platformView) =>
			platformView.AsSkia().ScreenBounds;

		/// <summary>MAUI's platform OnUnloadedAsync(platformView): waits until the view is no longer in an open window.</summary>
		public static Task OnUnloadedAsync(this object platformView, TimeSpan? timeOut = null) =>
			AssertHelpers.AssertEventually(() => !platformView.IsLoaded(), (int)(timeOut ?? TimeSpan.FromSeconds(2)).TotalMilliseconds,
				message: "Platform view did not unload");

		/// <summary>MAUI's platform OnLoadedAsync(platformView).</summary>
		public static Task OnLoadedAsync(this object platformView, TimeSpan? timeOut = null) =>
			AssertHelpers.AssertEventually(() => platformView.IsLoaded(), (int)(timeOut ?? TimeSpan.FromSeconds(2)).TotalMilliseconds,
				message: "Platform view did not load");
	}
}
