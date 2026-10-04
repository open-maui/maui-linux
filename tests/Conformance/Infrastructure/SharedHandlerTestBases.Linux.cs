// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Hosting;

namespace Microsoft.Maui.DeviceTests
{
	// MAUI's shared text-style, text-input and focus test bases derive from
	// HandlerTestBasement directly, so they build a bare MauiApp. On the device
	// runners the platform is initialized by the test app's process; on Linux the
	// platform is what UseLinux sets up, so these bases build the same Linux test
	// app the CoreHandlerTestBase classes do.

	public partial class TextStyleHandlerTests<THandler, TStub>
	{
		protected override MauiAppBuilder ConfigureBuilder(MauiAppBuilder mauiAppBuilder) =>
			mauiAppBuilder.ConfigureTestBuilder();
	}

	public abstract partial class TextInputHandlerTests<THandler, TStub>
	{
		protected override MauiAppBuilder ConfigureBuilder(MauiAppBuilder mauiAppBuilder) =>
			mauiAppBuilder.ConfigureTestBuilder();
	}

	public abstract partial class FocusHandlerTests<THandler, TStub, TLayoutStub>
	{
		protected override MauiAppBuilder ConfigureBuilder(MauiAppBuilder mauiAppBuilder) =>
			mauiAppBuilder.ConfigureTestBuilder();
	}
}
