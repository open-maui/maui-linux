// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of BoxViewTests.Windows.cs. MAUI handles BoxView with
	// ShapeViewHandler; the tests pass either it or BoxViewHandler.
	public partial class BoxViewTests
	{
		Task<float> GetPlatformOpacity(IElementHandler handler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(handler));

		Task<bool> GetPlatformIsVisible(IElementHandler handler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(handler));
	}
}
