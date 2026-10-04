// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of SearchBarTests.Windows.cs: values read back from SkiaSearchBar.
	public partial class SearchBarTests
	{
		static SkiaSearchBar GetPlatformControl(SearchBarHandler handler) =>
			handler.PlatformView;

		static Task<string> GetPlatformText(SearchBarHandler handler) =>
			InvokeOnMainThreadAsync(() => GetPlatformControl(handler).Text);

		static int GetPlatformSelectionLength(SearchBarHandler searchBarHandler) =>
			GetPlatformControl(searchBarHandler).SelectionLength;

		static int GetPlatformCursorPosition(SearchBarHandler searchBarHandler) =>
			GetPlatformControl(searchBarHandler).CursorPosition;

		Task<float> GetPlatformOpacity(SearchBarHandler searchBarHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(searchBarHandler));

		Task<bool> GetPlatformIsVisible(SearchBarHandler searchBarHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(searchBarHandler));
	}
}
