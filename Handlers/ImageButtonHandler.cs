using System;
using System.IO;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ImageButtonHandler : ViewHandler<IImageButton, SkiaImageButton>
{
	internal class ImageSourceServiceResultManager
	{
		private readonly ImageButtonHandler _handler;

		private CancellationTokenSource? _cts;

		public ImageSourceServiceResultManager(ImageButtonHandler handler)
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
				IImageButton virtualView = ((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).VirtualView;
				IImageSource val = ((virtualView != null) ? ((IImageSourcePart)virtualView).Source : null);
				if (val == null)
				{
					((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).PlatformView?.LoadFromData(Array.Empty<byte>());
					return;
				}
				IImageSourcePart virtualView2 = (IImageSourcePart)(object)((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).VirtualView;
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
						await ((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).PlatformView.LoadFromFileAsync(file);
					}
					return;
				}
				IUriImageSource val3 = (IUriImageSource)(object)((val is IUriImageSource) ? val : null);
				if (val3 != null)
				{
					Uri uri = val3.Uri;
					if (uri != null)
					{
						await ((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).PlatformView.LoadFromUriAsync(uri);
					}
					return;
				}
				IStreamImageSource val4 = (IStreamImageSource)(object)((val is IStreamImageSource) ? val : null);
				if (val4 != null)
				{
					Stream stream = await val4.GetStreamAsync(token);
					if (stream != null)
					{
						await ((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).PlatformView.LoadFromStreamAsync(stream);
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception)
			{
				IImageSourcePart virtualView3 = (IImageSourcePart)(object)((ViewHandler<IImageButton, SkiaImageButton>)(object)_handler).VirtualView;
				if (virtualView3 != null)
				{
					virtualView3.UpdateIsLoading(false);
				}
			}
		}
	}

	public static IPropertyMapper<IImageButton, ImageButtonHandler> Mapper = (IPropertyMapper<IImageButton, ImageButtonHandler>)(object)new PropertyMapper<IImageButton, ImageButtonHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Aspect"] = MapAspect,
		["IsOpaque"] = MapIsOpaque,
		["Source"] = MapSource,
		["StrokeColor"] = MapStrokeColor,
		["StrokeThickness"] = MapStrokeThickness,
		["CornerRadius"] = MapCornerRadius,
		["Padding"] = MapPadding,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public static CommandMapper<IImageButton, ImageButtonHandler> CommandMapper = new CommandMapper<IImageButton, ImageButtonHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	private ImageSourceServiceResultManager _sourceLoader;

	private ImageSourceServiceResultManager SourceLoader => _sourceLoader ?? (_sourceLoader = new ImageSourceServiceResultManager(this));

	public ImageButtonHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ImageButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaImageButton CreatePlatformView()
	{
		return new SkiaImageButton();
	}

	protected override void ConnectHandler(SkiaImageButton platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Clicked += OnClicked;
		platformView.Pressed += OnPressed;
		platformView.Released += OnReleased;
		platformView.ImageLoaded += OnImageLoaded;
		platformView.ImageLoadingError += OnImageLoadingError;
	}

	protected override void DisconnectHandler(SkiaImageButton platformView)
	{
		platformView.Clicked -= OnClicked;
		platformView.Pressed -= OnPressed;
		platformView.Released -= OnReleased;
		platformView.ImageLoaded -= OnImageLoaded;
		platformView.ImageLoadingError -= OnImageLoadingError;
		base.DisconnectHandler(platformView);
	}

	private void OnClicked(object? sender, EventArgs e)
	{
		IImageButton virtualView = base.VirtualView;
		if (virtualView != null)
		{
			((IButton)virtualView).Clicked();
		}
	}

	private void OnPressed(object? sender, EventArgs e)
	{
		IImageButton virtualView = base.VirtualView;
		if (virtualView != null)
		{
			((IButton)virtualView).Pressed();
		}
	}

	private void OnReleased(object? sender, EventArgs e)
	{
		IImageButton virtualView = base.VirtualView;
		if (virtualView != null)
		{
			((IButton)virtualView).Released();
		}
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

	public static void MapAspect(ImageButtonHandler handler, IImageButton imageButton)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.Aspect = ((IImage)imageButton).Aspect;
		}
	}

	public static void MapIsOpaque(ImageButtonHandler handler, IImageButton imageButton)
	{
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.IsOpaque = ((IImage)imageButton).IsOpaque;
		}
	}

	public static void MapSource(ImageButtonHandler handler, IImageButton imageButton)
	{
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			handler.SourceLoader.UpdateImageSourceAsync();
		}
	}

	public static void MapStrokeColor(ImageButtonHandler handler, IImageButton imageButton)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null && ((IButtonStroke)imageButton).StrokeColor != null)
		{
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.StrokeColor = ((IButtonStroke)imageButton).StrokeColor.ToSKColor();
		}
	}

	public static void MapStrokeThickness(ImageButtonHandler handler, IImageButton imageButton)
	{
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.StrokeThickness = (float)((IButtonStroke)imageButton).StrokeThickness;
		}
	}

	public static void MapCornerRadius(ImageButtonHandler handler, IImageButton imageButton)
	{
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.CornerRadius = ((IButtonStroke)imageButton).CornerRadius;
		}
	}

	public static void MapPadding(ImageButtonHandler handler, IImageButton imageButton)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			Thickness padding = ((IPadding)imageButton).Padding;
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.PaddingLeft = (float)((Thickness)(ref padding)).Left;
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.PaddingTop = (float)((Thickness)(ref padding)).Top;
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.PaddingRight = (float)((Thickness)(ref padding)).Right;
			((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.PaddingBottom = (float)((Thickness)(ref padding)).Bottom;
		}
	}

	public static void MapBackground(ImageButtonHandler handler, IImageButton imageButton)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)imageButton).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackgroundColor(ImageButtonHandler handler, IImageButton imageButton)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView != null)
		{
			ImageButton val = (ImageButton)(object)((imageButton is ImageButton) ? imageButton : null);
			if (val != null && ((VisualElement)val).BackgroundColor != null)
			{
				((ViewHandler<IImageButton, SkiaImageButton>)(object)handler).PlatformView.BackgroundColor = ((VisualElement)val).BackgroundColor.ToSKColor();
			}
		}
	}
}
