// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	public abstract partial class ImageHandlerTests<TImageHandler, TStub>
	{
		// What the Linux image views report when a picture is set (CountedImageHandler logs it).
		const string ImageEventAppResourceMemberName = "Bitmap";
		const string ImageEventCustomMemberName = "Bitmap";

		static SkiaView GetPlatformImageView(IImageHandler handler) => (SkiaView)((IElementHandler)handler).PlatformView;

		static bool GetNativeIsAnimationPlaying(IImageHandler handler) => GetPlatformImageView(handler) switch
		{
			SkiaImage image => image.IsAnimationPlaying,
			var other => throw Missing.Property(other, "IsAnimationPlaying"),
		};

		static Aspect GetNativeAspect(IImageHandler handler) => GetPlatformImageView(handler) switch
		{
			SkiaImage image => image.Aspect,
			SkiaImageButton button => button.Aspect,
			var other => throw Missing.Property(other, "Aspect"),
		};
	}

	public partial class ImageButtonHandlerTests
	{
		SkiaImageButton GetNativeImageButton(ImageButtonHandler handler) => handler.PlatformView;

		Thickness GetNativePadding(ImageButtonHandler handler) => GetNativeImageButton(handler).Padding;

		bool ImageSourceLoaded(ImageButtonHandler handler) => GetNativeImageButton(handler).Bitmap != null;

		Task PerformClick(IImageButton button) =>
			InvokeOnMainThreadAsync(() => LinuxInput.Click(GetNativeImageButton(CreateHandler(button))));
	}

	public abstract partial class BaseImageSourceServiceTests
	{
		public static string CreateBitmapFile(int width, int height, Color color, string filename = null)
		{
			filename ??= Guid.NewGuid().ToString("N") + ".png";
			if (!Path.IsPathRooted(filename))
				filename = Path.Combine(Path.GetTempPath(), "openmaui-conformance", Guid.NewGuid().ToString("N"), filename);
			Directory.CreateDirectory(Path.GetDirectoryName(filename));
			using var src = CreateBitmapStream(width, height, color);
			using var dst = File.Create(filename);
			src.CopyTo(dst);
			return filename;
		}

		public static Stream CreateBitmapStream(int width, int height, Color color)
		{
			using var bitmap = new SKBitmap(width, height);
			bitmap.Erase(new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255)));
			var stream = new MemoryStream();
			using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
				data.SaveTo(stream);
			stream.Position = 0;
			return stream;
		}
	}
}

namespace Microsoft.Maui.DeviceTests.Stubs
{
	/// <summary>
	/// Linux CountedImageHandler (MAUI's CountedImageHandler.*.cs log every
	/// picture the native image view is given): logs each bitmap the Skia image
	/// view receives, which is the Linux equivalent of the native setter, and a
	/// null when the view's picture is cleared (the native setter given null).
	/// </summary>
	public class CountedImageHandler : ImageHandler
	{
		public List<(string Member, object Value)> ImageEvents { get; } = new();

		protected override SkiaImage CreatePlatformView()
		{
			var view = base.CreatePlatformView();
			view.ImageLoaded += (_, _) => ImageEvents.Add(("Bitmap", view.Bitmap));
			view.ImageCleared += (_, _) => ImageEvents.Add(("Bitmap", null));
			return view;
		}
	}

	/// <summary>
	/// The Linux load method of MAUI's counted image-source service (the
	/// counterpart of CountedImageSourceServiceStub.Android.cs / .iOS.cs): lets a
	/// test hold a load at "starting" until it signals DoWork, then returns a
	/// bitmap filled with the stub's colour.
	/// </summary>
	public partial class CountedImageSourceServiceStub : ILinuxImageSourceService
	{
		public async Task<IImageSourceServiceResult<SKBitmap>> GetImageAsync(IImageSource imageSource, float scale = 1, System.Threading.CancellationToken cancellationToken = default)
		{
			if (imageSource is not ICountedImageSourceStub imageSourceStub)
				return null;

			try
			{
				Starting.Set();

				// simulate actual work
				var bitmap = await Task.Run(() =>
				{
					if (imageSourceStub.Wait)
						DoWork.WaitOne();

					var color = imageSourceStub.Color;
					var result = new SKBitmap(100, 100);
					result.Erase(new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255)));
					return result;
				}).ConfigureAwait(false);

				return new LinuxImageSourceServiceResult(bitmap, imageSourceStub.IsResolutionDependent, bitmap.Dispose);
			}
			finally
			{
				Finishing.Set();
			}
		}
	}
}
