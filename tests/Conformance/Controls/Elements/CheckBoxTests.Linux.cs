// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of CheckBoxTests.Windows.cs.
	public partial class CheckBoxTests
	{
		Task<float> GetPlatformOpacity(CheckBoxHandler checkBoxHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(checkBoxHandler));

		Task<bool> GetPlatformIsVisible(CheckBoxHandler checkBoxHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(checkBoxHandler));
	}
}
