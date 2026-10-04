// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of TemplatedViewTests.Windows.cs.
	public partial class TemplatedViewTests
	{
		static int GetChildCount(ContentViewHandler contentViewHandler) =>
			contentViewHandler.PlatformView.Children.Count;

		static SkiaView GetChild(ContentViewHandler contentViewHandler, int index = 0) =>
			contentViewHandler.PlatformView.Children[index];
	}
}
