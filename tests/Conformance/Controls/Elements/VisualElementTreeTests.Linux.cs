// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	public static class LinuxScreenExtensions
	{
		/// <summary>
		/// MAUI's platform ViewExtensions.GetLocationOnScreen(IElement) (platform TFMs
		/// only): the platform view's position in the window (SkiaView.ScreenBounds),
		/// offset by the window's position on screen (the headless window is at 0,0).
		/// </summary>
		public static Point? GetLocationOnScreen(this IElement element) =>
			element.Handler?.PlatformView is SkiaView view ? view.ScreenBounds.Location : null;
	}
}
