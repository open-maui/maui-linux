// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of PageHandlerTests.Windows.cs: the page's platform view is a SkiaPage
	// (Windows: a ContentPanel) whose one child is the content's platform view.
	public partial class PageHandlerTests
	{
		public SkiaView GetNativePageContent(PageHandler handler)
		{
			var content = handler.PlatformView.Content;
			Assert.NotNull(content);
			return content;
		}
	}
}
