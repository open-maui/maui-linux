// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.TestUtils.DeviceTests.Runners;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of MAUI's ControlsDeviceTestExtensions.ConfigureTestBuilder
	/// (src/Controls/tests/DeviceTests/ControlsDeviceTestExtensions.cs, not compiled:
	/// its lifecycle and Maps registrations are per platform). The test app is a
	/// Linux app: Controls' remapping (what UseMauiApp does) and UseLinux, as an
	/// app's MauiProgram calls them, then MAUI's own registrations, whose handler
	/// names resolve to the Linux handlers (HandlerAliases*.cs).
	/// </summary>
	public static class ControlsDeviceTestExtensions
	{
		public static MauiAppBuilder ConfigureTestBuilder(this MauiAppBuilder mauiAppBuilder)
		{
			mauiAppBuilder
				.RemapForControls()
				.UseLinux(_ => { });

			// The app's dispatcher is the main thread's LinuxDispatcher (TestDispatcher).
			mauiAppBuilder.Services.Replace(ServiceDescriptor.Singleton<IDispatcherProvider>(_ => TestDispatcher.Provider));
			DispatcherProvider.SetCurrent(TestDispatcher.Provider);

			return mauiAppBuilder
				.ConfigureMauiHandlers(handlers =>
				{
					// MAUI's registrations (ControlsDeviceTestExtensions.cs)
					handlers.AddHandler(typeof(Editor), typeof(EditorHandler));
					handlers.AddHandler(typeof(VerticalStackLayout), typeof(LayoutHandler));
					handlers.AddHandler(typeof(Controls.Window), typeof(WindowHandlerStub));
					handlers.AddHandler(typeof(Controls.ContentPage), typeof(PageHandler));
				});
		}
	}
}
