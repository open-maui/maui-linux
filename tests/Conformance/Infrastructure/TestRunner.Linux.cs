// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;

// Linux port of the pieces of MAUI's device-test runner
// (src/TestUtils/src/DeviceTests.Runners) that the shared handler tests use.
// The device runners host a real UI thread; the Linux handlers have no thread
// affinity when no display is attached (the main OpenMaui test suite runs them
// the same way), so "the main thread" is the calling thread and dispatches run
// inline. The xunit runner config serializes the suite.
namespace Microsoft.Maui.TestUtils.DeviceTests.Runners
{
	public static class TestDispatcher
	{
		static readonly InlineDispatcherProvider s_provider = new();

		public static IDispatcherProvider Provider => s_provider;

		public static IDispatcher Current => s_provider.Dispatcher;

		/// <summary>
		/// BindableObject captures its dispatcher at construction, so the
		/// provider must be in place before any test builds a Controls view.
		/// </summary>
		[ModuleInitializer]
		internal static void Install() => DispatcherProvider.SetCurrent(s_provider);

		sealed class InlineDispatcherProvider : IDispatcherProvider
		{
			public InlineDispatcher Dispatcher { get; } = new();
			public IDispatcher? GetForCurrentThread() => Dispatcher;
		}

		internal sealed class InlineDispatcher : IDispatcher
		{
			public bool IsDispatchRequired => false;

			public bool Dispatch(Action action)
			{
				action();
				return true;
			}

			public bool DispatchDelayed(TimeSpan delay, Action action)
			{
				_ = Task.Delay(delay).ContinueWith(_ => action(), TaskScheduler.Default);
				return true;
			}

			public IDispatcherTimer CreateTimer() => new InlineTimer();
		}

		sealed class InlineTimer : IDispatcherTimer
		{
			public TimeSpan Interval { get; set; }
			public bool IsRepeating { get; set; }
			public bool IsRunning { get; private set; }
			public event EventHandler? Tick;
			public void Start() => IsRunning = true;
			public void Stop() => IsRunning = false;
			internal void Raise() => Tick?.Invoke(this, EventArgs.Empty);
		}
	}

	/// <summary>
	/// The runner's window (MAUI's TestWindow): the headless runner has no app window, so it
	/// is a platform window of OpenMaui's WindowHandler type that no context shows.
	/// </summary>
	public static class TestWindow
	{
		static Microsoft.Maui.Platform.Linux.Handlers.SkiaWindow? s_platformWindow;

		public static object PlatformWindow => s_platformWindow ??= new Microsoft.Maui.Platform.Linux.Handlers.SkiaWindow();
	}

	/// <summary>
	/// The device runners expose the test app's services here; the headless
	/// Linux runner has no app, so only the logger lookups ContextStub makes
	/// reach it, and those fall back to the test's own registrations.
	/// </summary>
	public static class TestServices
	{
		public static IServiceProvider Services { get; } = new EmptyServices();

		sealed class EmptyServices : IServiceProvider
		{
			public object? GetService(Type serviceType) => null;
		}
	}
}
