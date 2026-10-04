// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Maui.Graphics.Skia;

namespace Microsoft.Maui.Graphics.Platform
{
	/// <summary>
	/// MAUI's per-platform image loader (Microsoft.Maui.Graphics.Platform
	/// .PlatformImageLoadingService exists only on the platform TFMs). Linux
	/// draws GraphicsView content with Microsoft.Maui.Graphics.Skia, whose
	/// loader is the platform one here.
	/// </summary>
	public class PlatformImageLoadingService : IImageLoadingService
	{
		readonly SkiaImageLoadingService _skia = new();

		public IImage FromStream(Stream stream, ImageFormat format = ImageFormat.Png) => _skia.FromStream(stream, format);
	}
}
