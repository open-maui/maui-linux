// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.TestUtils.DeviceTests.Runners;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of MAUI's CoreDeviceTestExtensions.ConfigureTestBuilder
	/// (src/Core/tests/DeviceTests/CoreDeviceTestExtensions.cs): the app the
	/// handler tests build is a Linux app (UseLinux, as a real app calls it),
	/// plus the stub-to-handler registrations MAUI's version makes. The
	/// handler names resolve to the Linux handlers (see HandlerAliases.cs).
	/// MAUI's font registrations point at fonts bundled with its device-test
	/// app, which this suite does not ship, so they are left out.
	/// </summary>
	public static class CoreDeviceTestExtensions
	{
		public static MauiAppBuilder ConfigureTestBuilder(this MauiAppBuilder mauiAppBuilder)
		{
			mauiAppBuilder.UseLinux(_ => { });

			// UseLinux TryAdds the GLib dispatcher; the test base registered the
			// test dispatcher first, but make the intent explicit.
			mauiAppBuilder.Services.Replace(ServiceDescriptor.Singleton<IDispatcherProvider>(_ => TestDispatcher.Provider));
			// UseLinux installs the Linux provider globally; Controls views
			// created by tests must keep capturing the inline test dispatcher.
			DispatcherProvider.SetCurrent(TestDispatcher.Provider);

			return mauiAppBuilder
				.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler(typeof(ElementStub), typeof(ElementHandlerStub));
					HandlerAliases.Register(handlers);
				});
		}
	}
}
