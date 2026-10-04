// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of LayoutTests.Windows.cs.
	public partial class LayoutTests
	{
		// Windows keeps a LayoutPanel hit-test visible (its children must stay
		// reachable) and makes only the panel's own surface ignore input. A Skia
		// layout's InputTransparent already means exactly that (SkiaView.HitTestAt
		// still tests the children of an input-transparent view), so on Linux every
		// view, layout or not, carries the view's InputTransparent.
		void ValidateInputTransparentOnPlatformView(IView view)
		{
			var handler = (IPlatformViewHandler)view.ToHandler(MauiContext);
			var platformView = LinuxPlatform.View(handler);
			Assert.Equal(view.InputTransparent, platformView.InputTransparent);
		}
	}
}
