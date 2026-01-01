using System;
using System.IO;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ImageHandler : ViewHandler<IImage, SkiaImage>
{
	internal class ImageSourceServiceResultManager
	{
		private readonly ImageHandler _handler;

		private CancellationTokenSource? _cts;

		public ImageSourceServiceResultManager(ImageHandler handler)
		{
			_handler = handler;
		}

		public async void UpdateImageSourceAsync()
		{
			_cts?.Cancel();
			_cts = new CancellationTokenSource();
			CancellationToken token = _cts.Token;
			try
			{
				IImage virtualView = ((ViewHandler<IImage, SkiaImage>)(object)_handler).VirtualView;
				IImageSource val = ((virtualView != null) ? ((IImageSourcePart)virtualView).Source : null);
				if (val == null)
				{
					((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView?.LoadFromData(Array.Empty<byte>());
					return;
				}
				IImageSourcePart virtualView2 = (IImageSourcePart)(object)((ViewHandler<IImage, SkiaImage>)(object)_handler).VirtualView;
				if (virtualView2 != null)
				{
					virtualView2.UpdateIsLoading(true);
				}
				IFileImageSource val2 = (IFileImageSource)(object)((val is IFileImageSource) ? val : null);
				if (val2 != null)
				{
					string file = val2.File;
					if (!string.IsNullOrEmpty(file))
					{
						await ((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView.LoadFromFileAsync(file);
					}
					return;
				}
				IUriImageSource val3 = (IUriImageSource)(object)((val is IUriImageSource) ? val : null);
				if (val3 != null)
				{
					Uri uri = val3.Uri;
					if (uri != null)
					{
						await ((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView.LoadFromUriAsync(uri);
					}
					return;
				}
				IStreamImageSource val4 = (IStreamImageSource)(object)((val is IStreamImageSource) ? val : null);
				if (val4 != null)
				{
					Stream stream = await val4.GetStreamAsync(token);
					if (stream != null)
					{
						await ((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView.LoadFromStreamAsync(stream);
					}
					return;
				}
				FontImageSource val5 = (FontImageSource)(object)((val is FontImageSource) ? val : null);
				if (val5 != null)
				{
					SKBitmap val6 = RenderFontImageSource(val5, ((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView.WidthRequest, ((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView.HeightRequest);
					if (val6 != null)
					{
						((ViewHandler<IImage, SkiaImage>)(object)_handler).PlatformView.LoadFromBitmap(val6);
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception)
			{
				IImageSourcePart virtualView3 = (IImageSourcePart)(object)((ViewHandler<IImage, SkiaImage>)(object)_handler).VirtualView;
				if (virtualView3 != null)
				{
					virtualView3.UpdateIsLoading(false);
				}
			}
		}

		private static SKBitmap? RenderFontImageSource(FontImageSource fontSource, double requestedWidth, double requestedHeight)
		{
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Expected O, but got Unknown
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Expected O, but got Unknown
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_016a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0171: Expected O, but got Unknown
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Expected O, but got Unknown
			//IL_0191: Unknown result type (might be due to invalid IL or missing references)
			string glyph = fontSource.Glyph;
			if (string.IsNullOrEmpty(glyph))
			{
				return null;
			}
			int val = (int)Math.Max((requestedWidth > 0.0) ? requestedWidth : 24.0, (requestedHeight > 0.0) ? requestedHeight : 24.0);
			val = Math.Max(val, 16);
			SKColor color = fontSource.Color?.ToSKColor() ?? SKColors.Black;
			SKBitmap val2 = new SKBitmap(val, val, false);
			SKCanvas val3 = new SKCanvas(val2);
			try
			{
				val3.Clear(SKColors.Transparent);
				SKTypeface val4 = null;
				if (!string.IsNullOrEmpty(fontSource.FontFamily))
				{
					string[] array = new string[4]
					{
						"/usr/share/fonts/truetype/" + fontSource.FontFamily + ".ttf",
						"/usr/share/fonts/opentype/" + fontSource.FontFamily + ".otf",
						"/usr/local/share/fonts/" + fontSource.FontFamily + ".ttf",
						Path.Combine(AppContext.BaseDirectory, fontSource.FontFamily + ".ttf")
					};
					foreach (string text in array)
					{
						if (File.Exists(text))
						{
							val4 = SKTypeface.FromFile(text, 0);
							if (val4 != null)
							{
								break;
							}
						}
					}
					if (val4 == null)
					{
						val4 = SKTypeface.FromFamilyName(fontSource.FontFamily);
					}
				}
				if (val4 == null)
				{
					val4 = SKTypeface.Default;
				}
				float num = (float)val * 0.8f;
				SKFont val5 = new SKFont(val4, num, 1f, 0f);
				try
				{
					SKPaint val6 = new SKPaint(val5)
					{
						Color = color,
						IsAntialias = true,
						TextAlign = (SKTextAlign)1
					};
					try
					{
						SKRect val7 = default(SKRect);
						val6.MeasureText(glyph, ref val7);
						float num2 = (float)val / 2f;
						float num3 = ((float)val - ((SKRect)(ref val7)).Top - ((SKRect)(ref val7)).Bottom) / 2f;
						val3.DrawText(glyph, num2, num3, val6);
						return val2;
					}
					finally
					{
						((IDisposable)val6)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
	}

	public static IPropertyMapper<IImage, ImageHandler> Mapper = (IPropertyMapper<IImage, ImageHandler>)(object)new PropertyMapper<IImage, ImageHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Aspect"] = MapAspect,
		["IsOpaque"] = MapIsOpaque,
		["Source"] = MapSource,
		["Background"] = MapBackground,
		["Width"] = MapWidth,
		["Height"] = MapHeight
	};

	public static CommandMapper<IImage, ImageHandler> CommandMapper = new CommandMapper<IImage, ImageHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	private ImageSourceServiceResultManager _sourceLoader;

	private ImageSourceServiceResultManager SourceLoader => _sourceLoader ?? (_sourceLoader = new ImageSourceServiceResultManager(this));

	public ImageHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ImageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaImage CreatePlatformView()
	{
		return new SkiaImage();
	}

	protected override void ConnectHandler(SkiaImage platformView)
	{
		base.ConnectHandler(platformView);
		platformView.ImageLoaded += OnImageLoaded;
		platformView.ImageLoadingError += OnImageLoadingError;
	}

	protected override void DisconnectHandler(SkiaImage platformView)
	{
		platformView.ImageLoaded -= OnImageLoaded;
		platformView.ImageLoadingError -= OnImageLoadingError;
		base.DisconnectHandler(platformView);
	}

	private void OnImageLoaded(object? sender, EventArgs e)
	{
		IImageSourcePart virtualView = (IImageSourcePart)(object)base.VirtualView;
		if (virtualView != null)
		{
			virtualView.UpdateIsLoading(false);
		}
	}

	private void OnImageLoadingError(object? sender, ImageLoadingErrorEventArgs e)
	{
		IImageSourcePart virtualView = (IImageSourcePart)(object)base.VirtualView;
		if (virtualView != null)
		{
			virtualView.UpdateIsLoading(false);
		}
	}

	public static void MapAspect(ImageHandler handler, IImage image)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.Aspect = image.Aspect;
		}
	}

	public static void MapIsOpaque(ImageHandler handler, IImage image)
	{
		if (((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.IsOpaque = image.IsOpaque;
		}
	}

	public static void MapSource(ImageHandler handler, IImage image)
	{
		if (((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView == null)
		{
			return;
		}
		Image val = (Image)(object)((image is Image) ? image : null);
		if (val != null)
		{
			if (((VisualElement)val).WidthRequest > 0.0)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.WidthRequest = ((VisualElement)val).WidthRequest;
			}
			if (((VisualElement)val).HeightRequest > 0.0)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.HeightRequest = ((VisualElement)val).HeightRequest;
			}
		}
		handler.SourceLoader.UpdateImageSourceAsync();
	}

	public static void MapBackground(ImageHandler handler, IImage image)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)image).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapWidth(ImageHandler handler, IImage image)
	{
		if (((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView != null)
		{
			Image val = (Image)(object)((image is Image) ? image : null);
			if (val != null && ((VisualElement)val).WidthRequest > 0.0)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.WidthRequest = ((VisualElement)val).WidthRequest;
				Console.WriteLine($"[ImageHandler] MapWidth: {((VisualElement)val).WidthRequest}");
			}
			else if (((IView)image).Width > 0.0)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.WidthRequest = ((IView)image).Width;
			}
		}
	}

	public static void MapHeight(ImageHandler handler, IImage image)
	{
		if (((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView != null)
		{
			Image val = (Image)(object)((image is Image) ? image : null);
			if (val != null && ((VisualElement)val).HeightRequest > 0.0)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.HeightRequest = ((VisualElement)val).HeightRequest;
				Console.WriteLine($"[ImageHandler] MapHeight: {((VisualElement)val).HeightRequest}");
			}
			else if (((IView)image).Height > 0.0)
			{
				((ViewHandler<IImage, SkiaImage>)(object)handler).PlatformView.HeightRequest = ((IView)image).Height;
			}
		}
	}
}
