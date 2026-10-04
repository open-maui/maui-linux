// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of SwipeViewTests.Android.cs / .iOS.cs.
	public partial class SwipeViewTests
	{
		Task<bool> HasChildren(SwipeViewHandler handler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.View(handler).Children.Count != 0);
	}
}
