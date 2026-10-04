// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform;
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// What the other platforms' PerformClick() is here: the pointer events a
	/// window routes to the view for a left click at its centre.
	/// </summary>
	static class LinuxInput
	{
		public static void Click(SkiaView view)
		{
			AssertionExtensions.EnsureLaidOut(view);
			var b = view.Bounds;
			var x = (float)(b.X + b.Width / 2);
			var y = (float)(b.Y + b.Height / 2);
			view.OnPointerPressed(new PointerEventArgs(x, y, PointerButton.Left));
			view.OnPointerReleased(new PointerEventArgs(x, y, PointerButton.Left));
		}
	}
}
