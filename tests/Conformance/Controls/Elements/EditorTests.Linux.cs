// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of EditorTests.Windows.cs: values read back from SkiaEditor.
	public partial class EditorTests
	{
		static SkiaEditor GetPlatformControl(EditorHandler handler) =>
			handler.PlatformView;

		static Task<string> GetPlatformText(EditorHandler handler) =>
			InvokeOnMainThreadAsync(() => GetPlatformControl(handler).Text);

		Task<float> GetPlatformOpacity(EditorHandler editorHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(editorHandler));

		static void SetPlatformText(EditorHandler editorHandler, string text) =>
			GetPlatformControl(editorHandler).Text = text;

		static int GetPlatformCursorPosition(EditorHandler editorHandler) =>
			GetPlatformControl(editorHandler).CursorPosition;

		static int GetPlatformSelectionLength(EditorHandler editorHandler) =>
			GetPlatformControl(editorHandler).SelectionLength;

		Task<bool> GetPlatformIsVisible(EditorHandler editorHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(editorHandler));
	}
}
