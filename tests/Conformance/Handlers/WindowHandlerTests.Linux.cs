// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of WindowHandlerTests.Windows.cs. MAUI's device runners run the window
	/// tests inside a real app: Application.Current is the test app, and OpenWindow opens a
	/// platform window. Here the app is a Controls Application with OpenMaui's
	/// ApplicationHandler, in a LinuxApplication that has no display, so OpenWindow opens a
	/// window without a native toplevel (WindowContext.IsHeadless): the Linux WindowHandler,
	/// sized by Window.Width/Height and its limits as a desktop window is.
	///
	/// Only the helpers the shared tests call are here; the Windows partial's own tests read
	/// WinUI's navigation view and toolbar, which have no Linux counterpart.
	/// </summary>
	public partial class WindowHandlerTests
	{
		public WindowHandlerTests()
		{
			LinuxWindowTestApp.Ensure(MauiContext);
		}

		/// <summary>
		/// What a user moving and resizing the window does (WinUI's AppWindow.MoveAndResize):
		/// the window context of the window this platform window belongs to takes the frame.
		/// </summary>
		void MovePlatformWindow(object platformWindow, Rect rect)
		{
			var app = LinuxApplication.Current ?? throw new XunitException("No LinuxApplication.");
			var context = app.WindowContexts.FirstOrDefault(c => ReferenceEquals(c.MauiWindow?.Handler?.PlatformView, platformWindow))
				?? throw new XunitException("No open window shows this platform window.");
			var move = typeof(WindowContext).GetMethod("MoveAndResize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new MissingMethodException(nameof(WindowContext), "MoveAndResize");
			move.Invoke(context, new object[] { rect });
		}
	}

	/// <summary>
	/// The test process's MAUI application for the window tests: one Controls Application
	/// (Application.Current) whose handler is OpenMaui's ApplicationHandler, over a
	/// LinuxApplication without a display whose MAUI context renders the windows' pages.
	/// </summary>
	static class LinuxWindowTestApp
	{
		static Microsoft.Maui.Controls.Application? s_app;

		public static void Ensure(IMauiContext mauiContext)
		{
			var linuxApp = LinuxApplication.Current ?? new LinuxApplication();
			var property = typeof(LinuxApplication).GetProperty("MauiContext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new MissingMemberException(nameof(LinuxApplication), "MauiContext");
			if (property.GetValue(linuxApp) is null)
				property.SetValue(linuxApp, new LinuxMauiContext(mauiContext.Services, linuxApp));

			if (s_app is not null && ReferenceEquals(Microsoft.Maui.Controls.Application.Current, s_app))
				return;
			var app = new Microsoft.Maui.Controls.Application();
			Microsoft.Maui.Controls.Application.Current = app;
			app.UserAppTheme = ApplicationModel.AppTheme.Light;
			MauiHandlerExtensions.ToHandler(app, (IMauiContext)property.GetValue(linuxApp)!);
			s_app = app;
		}
	}
}
