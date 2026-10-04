// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.DeviceTests;

namespace Microsoft.Maui.DeviceTests.ImageAnalysis
{
	// Linux branch of MAUI's RawBitmapExtensions.AsRawBitmapAsync (patched in,
	// MauiPatches.props): the platform view drawn as it is in its window, its
	// pixels in the BGRA order the other platforms' captures produce.
	public static partial class RawBitmapExtensions
	{
		static RawBitmap CaptureView(object platformView)
		{
			using var bitmap = platformView.ToBitmap();
			return new RawBitmap
			{
				PixelBuffer = bitmap.Bytes,
				PixelWidth = bitmap.Width,
				PixelHeight = bitmap.Height,
				Density = 1,
			};
		}
	}
}
