// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of FlyoutPageTests.Windows.cs: the SkiaFlyoutPage a view is in.
	public partial class FlyoutPageTests
	{
		SkiaFlyoutPage? FindPlatformFlyoutView(object platformView)
		{
			var view = (platformView as SkiaView)?.Parent;
			while (view is not null && view is not SkiaFlyoutPage)
				view = view.Parent;
			return view as SkiaFlyoutPage;
		}
	}
}
