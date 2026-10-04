// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Essentials.DeviceTests
{
	/// <summary>
	/// What MAUI's Essentials device-test app gets from its platform before any test runs
	/// (src/Essentials/test/DeviceTests/Startup.cs builds a MauiApp on a real UI thread),
	/// done the way an OpenMaui app does it:
	///
	/// - The XDG data, config and cache roots point at a private directory for this run, so
	///   Preferences, FileSystem.AppDataDirectory/CacheDirectory and VersionTracking write
	///   there and never into the user's own files. Set first: the services read them on
	///   first use.
	/// - The test assembly is the entry assembly. Under `dotnet test` the entry assembly is
	///   the test host, but AppInfo (and everything keyed on the app's identity) reads the
	///   app's metadata from the entry assembly, as MAUI does on Windows.
	/// - An OpenMaui main thread: a thread owning GLib's default main context, on which
	///   LinuxDispatcher is initialized, so MainThread.IsMainThread and
	///   MainThread.InvokeOnMainThreadAsync (Utils.OnMainThread) behave as in an app.
	/// - A runner window: the application's primary window with a laid-out root view, as MAUI's
	///   device runner shows its test list in the app's window (Screenshot captures it).
	/// - UseLinux's registration (LinuxPlatformRegistrar.Register, what the cross-platform
	///   UseLinux() calls), which installs OpenMaui's Essentials implementations behind
	///   MAUI's static facades.
	/// </summary>
	static class EssentialsTestHost
	{
		static readonly ManualResetEventSlim s_started = new(false);

		/// <summary>The private XDG root of this run.</summary>
		public static string XdgRoot { get; private set; } = "";

		[ModuleInitializer]
		internal static void Install()
		{
			XdgRoot = Path.Combine(Path.GetTempPath(), $"openmaui-essentials-conformance-{Environment.ProcessId}");
			foreach (var (variable, folder) in new[] { ("XDG_DATA_HOME", "data"), ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache") })
			{
				var dir = Path.Combine(XdgRoot, folder);
				Directory.CreateDirectory(dir);
				Environment.SetEnvironmentVariable(variable, dir);
			}
			AppDomain.CurrentDomain.ProcessExit += (_, _) =>
			{
				// The keyring is the user's own: drop what SecureStorage_Tests stored there
				// (this test app's items only; SecureStorage is per app).
				try { Microsoft.Maui.Storage.SecureStorage.Default.RemoveAll(); }
				catch (Exception) { }
				try { Directory.Delete(XdgRoot, recursive: true); }
				catch (Exception) { }
			};

			Assembly.SetEntryAssembly(typeof(EssentialsTestHost).Assembly);

			// Started, not waited for: the thread runs code of this assembly, which the runtime
			// holds until the module initializer returns (EnsureStarted waits, from discovery).
			var thread = new Thread(MainLoop) { IsBackground = true, Name = "OpenMaui main thread" };
			thread.Start();
			DispatcherProvider.SetCurrent(LinuxDispatcherProvider.Instance);

			LinuxPlatformRegistrar.Register(MauiApp.CreateBuilder());
		}

		static readonly object s_windowLock = new();
		static bool s_windowOpen;

		/// <summary>
		/// Waits for the main thread's dispatcher and opens the runner window (called before
		/// any test runs, from discovery).
		/// </summary>
		public static void EnsureStarted()
		{
			if (!s_started.Wait(TimeSpan.FromSeconds(30)))
				throw new InvalidOperationException("The OpenMaui main thread did not start (GLib missing?).");
			lock (s_windowLock)
			{
				if (s_windowOpen)
					return;
				s_windowOpen = true;
				using var opened = new ManualResetEventSlim(false);
				Exception? failure = null;
				LinuxDispatcher.Main!.Dispatch(() =>
				{
					try { OpenRunnerWindow(); }
					catch (Exception ex) { failure = ex; }
					finally { opened.Set(); }
				});
				if (!opened.Wait(TimeSpan.FromSeconds(30)))
					throw new InvalidOperationException("The runner window did not open on the main thread.");
				if (failure != null)
					throw new InvalidOperationException("Opening the runner window failed.", failure);
			}
		}

		/// <summary>
		/// MAUI's device runner shows its test list in the app's window while the tests run;
		/// Screenshot_Tests capture that window. This is the same: the application's primary
		/// window, without a display (no native toplevel), showing one label laid out at the
		/// default window size (800x600), which is what Screenshot.CaptureAsync renders.
		/// </summary>
		static void OpenRunnerWindow()
		{
			var app = LinuxApplication.Current ?? new LinuxApplication();
			var root = new SkiaLabel { Text = "Essentials Tests" };
			app.RootView = root;
			root.Measure(new Size(RunnerWindowWidth, RunnerWindowHeight));
			root.Arrange(new Rect(0, 0, RunnerWindowWidth, RunnerWindowHeight));
		}

		public const int RunnerWindowWidth = 800;
		public const int RunnerWindowHeight = 600;

		static void MainLoop()
		{
			// Same as LinuxApplication.Run's startup: the main thread's dispatcher and
			// SynchronizationContext.
			LinuxDispatcher.Initialize();
			s_started.Set();
			while (true)
				g_main_context_iteration(IntPtr.Zero, true);
		}

		[DllImport("libglib-2.0.so.0")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool g_main_context_iteration(IntPtr context, [MarshalAs(UnmanagedType.Bool)] bool mayBlock);
	}
}
