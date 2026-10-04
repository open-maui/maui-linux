// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of SwipeViewTests.Android.cs / .iOS.cs.
	public partial class SwipeViewTests
	{
		// The content of a SkiaSwipeView is a child of the layout (SkiaLayoutView.Children, which
		// hides SkiaView.Children: a layout keeps its children in its own list).
		Task<bool> HasChildren(SwipeViewHandler handler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.View<SkiaSwipeView>(handler).Children.Count != 0);
	}
}
