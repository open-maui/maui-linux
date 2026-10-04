// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of PickerTests.Windows.cs: SkiaPicker shows its selected item's text.
	public partial class PickerTests
	{
		protected Task<string?> GetPlatformControlText(SkiaPicker platformView) =>
			InvokeOnMainThreadAsync(() => platformView.SelectedItem);

		Task<float> GetPlatformOpacity(PickerHandler pickerHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(pickerHandler));
	}
}
