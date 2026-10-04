// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of EntryTests.Windows.cs: values read back from SkiaEntry.
	public partial class EntryTests
	{
		static SkiaEntry GetPlatformControl(EntryHandler handler) =>
			handler.PlatformView;

		static Task<string> GetPlatformText(EntryHandler handler) =>
			InvokeOnMainThreadAsync(() => GetPlatformControl(handler).Text);

		Task<float> GetPlatformOpacity(EntryHandler entryHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(entryHandler));

		static void SetPlatformText(EntryHandler entryHandler, string text) =>
			GetPlatformControl(entryHandler).Text = text;

		static int GetPlatformCursorPosition(EntryHandler entryHandler) =>
			GetPlatformControl(entryHandler).CursorPosition;

		static int GetPlatformSelectionLength(EntryHandler entryHandler) =>
			GetPlatformControl(entryHandler).SelectionLength;

		Task<bool> GetPlatformIsVisible(EntryHandler entryHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(entryHandler));
	}
}
