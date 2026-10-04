// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Dispatching;

// Linux port of the pieces of MAUI's device-test runner
// (src/TestUtils/src/DeviceTests.Runners) that the Controls tests use.
//
// The Controls suite runs an OpenMaui main thread, as an app has one: a thread
// that owns GLib's default main context, on which LinuxDispatcher (the
// dispatcher UseLinux registers and apps run on) is initialized. Dispatches,
// dispatcher timers and await continuations of code started there go through
// the GLib loop exactly as in an app, and HeadlessWindowHost renders frames on
// it. Test methods themselves run on xunit's threads and reach the main thread
// through InvokeOnMainThreadAsync, which is how MAUI's device runners run them.
namespace Microsoft.Maui.TestUtils.DeviceTests.Runners
{
	public static class TestDispatcher
	{
		static readonly ManualResetEventSlim s_started = new(false);
		static Thread? s_thread;

		/// <summary>The dispatcher apps get from UseLinux (LinuxDispatcherProvider).</summary>
		public static IDispatcherProvider Provider
		{
			get
			{
				EnsureStarted();
				return LinuxDispatcherProvider.Instance;
			}
		}

		public static IDispatcher Current
		{
			get
			{
				EnsureStarted();
				return LinuxDispatcher.Main!;
			}
		}

		/// <summary>The OpenMaui main thread.</summary>
		public static Thread MainThread
		{
			get
			{
				EnsureStarted();
				return s_thread!;
			}
		}

		/// <summary>
		/// The provider must be in place before any test builds a view. The main
		/// thread is started here but not waited for: it runs code of this
		/// assembly, which the runtime holds until the module initializer returns.
		/// </summary>
		[ModuleInitializer]
		internal static void Install()
		{
			StartThread();
			DispatcherProvider.SetCurrent(LinuxDispatcherProvider.Instance);
		}

		static void StartThread()
		{
			lock (s_started)
			{
				if (s_thread is null)
				{
					s_thread = new Thread(MainLoop) { IsBackground = true, Name = "OpenMaui main thread" };
					s_thread.Start();
				}
			}
		}

		static void EnsureStarted()
		{
			if (s_started.IsSet)
				return;
			StartThread();
			s_started.Wait();
			DispatcherProvider.SetCurrent(LinuxDispatcherProvider.Instance);
		}

		static void MainLoop()
		{
			// Same as LinuxApplication.Run's startup: the main thread's dispatcher
			// and SynchronizationContext.
			LinuxDispatcher.Initialize();
			s_started.Set();
			while (true)
				g_main_context_iteration(IntPtr.Zero, true);
		}

		[DllImport("libglib-2.0.so.0")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool g_main_context_iteration(IntPtr context, [MarshalAs(UnmanagedType.Bool)] bool mayBlock);
	}

	/// <summary>
	/// The device runners expose the test app's services here; the Linux runner
	/// has no runner app, so lookups fall back to the test's own registrations.
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
