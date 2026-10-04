// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of ButtonTests.Windows.cs: values read back from SkiaButton.
	public partial class ButtonTests
	{
		SkiaButton GetPlatformButton(ButtonHandler buttonHandler) =>
			buttonHandler.PlatformView;

		Task<string?> GetPlatformText(ButtonHandler buttonHandler) =>
			InvokeOnMainThreadAsync<string?>(() => LinuxPlatform.ButtonText(GetPlatformButton(buttonHandler)));

		LineBreakMode GetPlatformLineBreakMode(ButtonHandler buttonHandler) =>
			GetPlatformButton(buttonHandler).LineBreakMode;

		Task<float> GetPlatformOpacity(ButtonHandler buttonHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(buttonHandler));

		Task<bool> GetPlatformIsVisible(ButtonHandler buttonHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(buttonHandler));
	}
}
