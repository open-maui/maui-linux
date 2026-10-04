// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of RefreshViewHandlerTests.Windows.cs: the platform view is a
	// SkiaRefreshView (the RefreshContainer of Windows), refreshing when it shows its spinner.
	public partial class RefreshViewHandlerTests
	{
		SkiaRefreshView GetNativeRefreshView(RefreshViewHandler refreshViewHandler) =>
			refreshViewHandler.PlatformView;

		bool GetPlatformIsRefreshing(RefreshViewHandler refreshViewHandler) =>
			GetNativeRefreshView(refreshViewHandler).IsRefreshing;
	}
}
