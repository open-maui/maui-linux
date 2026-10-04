// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of ContentViewTests.Windows.cs: the platform children of
	// the SkiaContentView, and of its content's layout.
	public partial class ContentViewTests
	{
		static int GetChildCount(ContentViewHandler contentViewHandler) =>
			contentViewHandler.PlatformView.Children.Count;

		static int GetContentChildCount(ContentViewHandler contentViewHandler)
		{
			if (contentViewHandler.PlatformView.Children.Count > 0 && contentViewHandler.PlatformView.Children[0] is SkiaLayoutView childLayout)
				return childLayout.Children.Count;
			return 0;
		}
	}
}
