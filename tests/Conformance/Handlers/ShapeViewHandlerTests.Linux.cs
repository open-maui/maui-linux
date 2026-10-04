// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of ShapeViewHandlerTests.Android.cs: the platform view is a
	// SkiaShapeView drawing MAUI's ShapeDrawable.
	public partial class ShapeViewHandlerTests
	{
		SkiaShapeView GetPlatformShapeView(ShapeViewHandler shapeViewHandler) =>
			shapeViewHandler.PlatformView;

		Task ValidateNativeFill(IShapeView shapeView, Color color)
		{
			return InvokeOnMainThreadAsync(() =>
			{
				return GetPlatformShapeView(CreateHandler(shapeView)).AssertContainsColor(color, MauiContext);
			});
		}
	}
}
