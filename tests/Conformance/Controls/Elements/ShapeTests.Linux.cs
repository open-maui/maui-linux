// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of ShapeTests.Windows.cs: a click is the pointer events
	// the window routes to the button (LinuxInput.Click).
	public partial class ShapeTests
	{
		Task PerformClick(IButton button) =>
			InvokeOnMainThreadAsync(() => LinuxInput.Click(LinuxPlatform.View(CreateHandler<ButtonHandler>(button))));
	}
}
