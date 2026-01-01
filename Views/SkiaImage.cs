using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform;

public class SkiaImage : SkiaView
{
	private SKBitmap? _bitmap;

	private SKImage? _image;

	private bool _isLoading;

	private string? _currentFilePath;

	private bool _isSvg;

	private CancellationTokenSource? _loadCts;

	private readonly object _loadLock = new object();

	private double _svgLoadedWidth;

	private double _svgLoadedHeight;

	private bool _pendingSvgReload;

	private SKRect _lastArrangedBounds;

	public SKBitmap? Bitmap
	{
		get
		{
			return _bitmap;
		}
		set
		{
			SKBitmap? bitmap = _bitmap;
			if (bitmap != null)
			{
				((SKNativeObject)bitmap).Dispose();
			}
			_bitmap = value;
			SKImage? image = _image;
			if (image != null)
			{
				((SKNativeObject)image).Dispose();
			}
			_image = ((value != null) ? SKImage.FromBitmap(value) : null);
			Invalidate();
		}
	}

	public Aspect Aspect { get; set; }

	public bool IsOpaque { get; set; }

	public bool IsLoading => _isLoading;

	public bool IsAnimationPlaying { get; set; }

	public new double WidthRequest
	{
		get
		{
			return base.WidthRequest;
		}
		set
		{
			base.WidthRequest = value;
			ScheduleSvgReloadIfNeeded();
		}
	}

	public new double HeightRequest
	{
		get
		{
			return base.HeightRequest;
		}
		set
		{
			base.HeightRequest = value;
			ScheduleSvgReloadIfNeeded();
		}
	}

	public event EventHandler? ImageLoaded;

	public event EventHandler<ImageLoadingErrorEventArgs>? ImageLoadingError;

	private void ScheduleSvgReloadIfNeeded()
	{
		if (_isSvg && !string.IsNullOrEmpty(_currentFilePath))
		{
			double widthRequest = WidthRequest;
			double heightRequest = HeightRequest;
			if (widthRequest > 0.0 && heightRequest > 0.0 && (Math.Abs(_svgLoadedWidth - widthRequest) > 0.5 || Math.Abs(_svgLoadedHeight - heightRequest) > 0.5) && !_pendingSvgReload)
			{
				_pendingSvgReload = true;
				ReloadSvgDebounced();
			}
		}
	}

	private async Task ReloadSvgDebounced()
	{
		await Task.Delay(10);
		_pendingSvgReload = false;
		if (!string.IsNullOrEmpty(_currentFilePath) && WidthRequest > 0.0 && HeightRequest > 0.0)
		{
			Console.WriteLine($"[SkiaImage] Reloading SVG at {WidthRequest}x{HeightRequest} (was {_svgLoadedWidth}x{_svgLoadedHeight})");
			await LoadSvgAtSizeAsync(_currentFilePath, WidthRequest, HeightRequest);
		}
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		if (!IsOpaque && base.BackgroundColor != SKColors.Transparent)
		{
			SKPaint val = new SKPaint
			{
				Color = base.BackgroundColor,
				Style = (SKPaintStyle)0
			};
			try
			{
				canvas.DrawRect(bounds, val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (_image == null)
		{
			return;
		}
		int width = _image.Width;
		int height = _image.Height;
		if (width <= 0 || height <= 0)
		{
			return;
		}
		SKRect val2 = CalculateDestRect(bounds, width, height);
		SKPaint val3 = new SKPaint
		{
			IsAntialias = true,
			FilterQuality = (SKFilterQuality)3
		};
		try
		{
			canvas.DrawImage(_image, val2, val3);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	private SKRect CalculateDestRect(SKRect bounds, float imageWidth, float imageHeight)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected I4, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		Aspect aspect = Aspect;
		switch ((int)aspect)
		{
		case 2:
			return bounds;
		case 0:
		{
			float num6 = Math.Min(((SKRect)(ref bounds)).Width / imageWidth, ((SKRect)(ref bounds)).Height / imageHeight);
			float num4 = imageWidth * num6;
			float num5 = imageHeight * num6;
			float num = ((SKRect)(ref bounds)).Left + (((SKRect)(ref bounds)).Width - num4) / 2f;
			float num2 = ((SKRect)(ref bounds)).Top + (((SKRect)(ref bounds)).Height - num5) / 2f;
			return new SKRect(num, num2, num + num4, num2 + num5);
		}
		case 1:
		{
			float num3 = Math.Max(((SKRect)(ref bounds)).Width / imageWidth, ((SKRect)(ref bounds)).Height / imageHeight);
			float num4 = imageWidth * num3;
			float num5 = imageHeight * num3;
			float num = ((SKRect)(ref bounds)).Left + (((SKRect)(ref bounds)).Width - num4) / 2f;
			float num2 = ((SKRect)(ref bounds)).Top + (((SKRect)(ref bounds)).Height - num5) / 2f;
			return new SKRect(num, num2, num + num4, num2 + num5);
		}
		case 3:
		{
			float num = ((SKRect)(ref bounds)).Left + (((SKRect)(ref bounds)).Width - imageWidth) / 2f;
			float num2 = ((SKRect)(ref bounds)).Top + (((SKRect)(ref bounds)).Height - imageHeight) / 2f;
			return new SKRect(num, num2, num + imageWidth, num2 + imageHeight);
		}
		default:
			return bounds;
		}
	}

	public async Task LoadFromFileAsync(string filePath)
	{
		_isLoading = true;
		Invalidate();
		Console.WriteLine($"[SkiaImage] LoadFromFileAsync: {filePath}, WidthRequest={WidthRequest}, HeightRequest={HeightRequest}");
		try
		{
			List<string> list = new List<string>
			{
				filePath,
				Path.Combine(AppContext.BaseDirectory, filePath),
				Path.Combine(AppContext.BaseDirectory, "Resources", "Images", filePath),
				Path.Combine(AppContext.BaseDirectory, "Resources", filePath)
			};
			if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
			{
				string text = Path.ChangeExtension(filePath, ".svg");
				list.Add(text);
				list.Add(Path.Combine(AppContext.BaseDirectory, text));
				list.Add(Path.Combine(AppContext.BaseDirectory, "Resources", "Images", text));
				list.Add(Path.Combine(AppContext.BaseDirectory, "Resources", text));
			}
			string foundPath = null;
			foreach (string item in list)
			{
				if (File.Exists(item))
				{
					foundPath = item;
					Console.WriteLine("[SkiaImage] Found file at: " + item);
					break;
				}
			}
			if (foundPath == null)
			{
				Console.WriteLine("[SkiaImage] File not found: " + filePath);
				_isLoading = false;
				_isSvg = false;
				_currentFilePath = null;
				this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(new FileNotFoundException(filePath)));
				return;
			}
			_isSvg = foundPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
			_currentFilePath = foundPath;
			if (!_isSvg)
			{
				await Task.Run(delegate
				{
					using FileStream fileStream = File.OpenRead(foundPath);
					SKBitmap val = SKBitmap.Decode((Stream)fileStream);
					if (val != null)
					{
						Bitmap = val;
						Console.WriteLine("[SkiaImage] Loaded image: " + foundPath);
					}
				});
			}
			else
			{
				await LoadSvgAtSizeAsync(foundPath, WidthRequest, HeightRequest);
			}
			_isLoading = false;
			this.ImageLoaded?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception exception)
		{
			_isLoading = false;
			this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(exception));
		}
		Invalidate();
	}

	private async Task LoadSvgAtSizeAsync(string svgPath, double targetWidth, double targetHeight)
	{
		_loadCts?.Cancel();
		CancellationTokenSource cts = new CancellationTokenSource();
		_loadCts = cts;
		try
		{
			SKBitmap newBitmap = null;
			await Task.Run(delegate
			{
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Expected O, but got Unknown
				//IL_0052: Unknown result type (might be due to invalid IL or missing references)
				//IL_0057: Unknown result type (might be due to invalid IL or missing references)
				//IL_0109: Unknown result type (might be due to invalid IL or missing references)
				//IL_0113: Expected O, but got Unknown
				//IL_0119: Unknown result type (might be due to invalid IL or missing references)
				//IL_0120: Expected O, but got Unknown
				//IL_0122: Unknown result type (might be due to invalid IL or missing references)
				if (cts.Token.IsCancellationRequested)
				{
					return;
				}
				SKSvg val = new SKSvg();
				try
				{
					val.Load(svgPath);
					if (val.Picture != null && !cts.Token.IsCancellationRequested)
					{
						SKRect cullRect = val.Picture.CullRect;
						float num = ((targetWidth > 0.0) ? ((float)targetWidth) : ((((SKRect)(ref cullRect)).Width <= 24f) ? 24f : ((SKRect)(ref cullRect)).Width));
						float num2 = Math.Min(val2: ((targetHeight > 0.0) ? ((float)targetHeight) : ((((SKRect)(ref cullRect)).Height <= 24f) ? 24f : ((SKRect)(ref cullRect)).Height)) / ((SKRect)(ref cullRect)).Height, val1: num / ((SKRect)(ref cullRect)).Width);
						int num3 = Math.Max(1, (int)(((SKRect)(ref cullRect)).Width * num2));
						int num4 = Math.Max(1, (int)(((SKRect)(ref cullRect)).Height * num2));
						newBitmap = new SKBitmap(num3, num4, false);
						SKCanvas val2 = new SKCanvas(newBitmap);
						try
						{
							val2.Clear(SKColors.Transparent);
							val2.Scale(num2);
							val2.DrawPicture(val.Picture, (SKPaint)null);
							Console.WriteLine($"[SkiaImage] Loaded SVG: {svgPath} at {num3}x{num4} (requested {targetWidth}x{targetHeight})");
							return;
						}
						finally
						{
							((IDisposable)val2)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}, cts.Token);
			if (!cts.Token.IsCancellationRequested && newBitmap != null)
			{
				_svgLoadedWidth = ((targetWidth > 0.0) ? targetWidth : ((double)newBitmap.Width));
				_svgLoadedHeight = ((targetHeight > 0.0) ? targetHeight : ((double)newBitmap.Height));
				Bitmap = newBitmap;
				return;
			}
			SKBitmap obj = newBitmap;
			if (obj != null)
			{
				((SKNativeObject)obj).Dispose();
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	public async Task LoadFromStreamAsync(Stream stream)
	{
		_isLoading = true;
		Invalidate();
		try
		{
			await Task.Run(delegate
			{
				SKBitmap val = SKBitmap.Decode(stream);
				if (val != null)
				{
					Bitmap = val;
				}
			});
			_isLoading = false;
			this.ImageLoaded?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception exception)
		{
			_isLoading = false;
			this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(exception));
		}
		Invalidate();
	}

	public async Task LoadFromUriAsync(Uri uri)
	{
		_isLoading = true;
		Invalidate();
		try
		{
			using HttpClient httpClient = new HttpClient();
			using MemoryStream memoryStream = new MemoryStream(await httpClient.GetByteArrayAsync(uri));
			SKBitmap val = SKBitmap.Decode((Stream)memoryStream);
			if (val != null)
			{
				Bitmap = val;
			}
			_isLoading = false;
			this.ImageLoaded?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception exception)
		{
			_isLoading = false;
			this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(exception));
		}
		Invalidate();
	}

	public void LoadFromData(byte[] data)
	{
		try
		{
			using MemoryStream memoryStream = new MemoryStream(data);
			SKBitmap val = SKBitmap.Decode((Stream)memoryStream);
			if (val != null)
			{
				Bitmap = val;
			}
			this.ImageLoaded?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception exception)
		{
			this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(exception));
		}
	}

	public void LoadFromBitmap(SKBitmap bitmap)
	{
		try
		{
			_isSvg = false;
			_currentFilePath = null;
			Bitmap = bitmap;
			_isLoading = false;
			this.ImageLoaded?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception exception)
		{
			_isLoading = false;
			this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(exception));
		}
		Invalidate();
	}

	public override void Arrange(SKRect bounds)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		base.Arrange(bounds);
		if ((!(base.WidthRequest > 0.0) || !(base.HeightRequest > 0.0)) && _isSvg && !string.IsNullOrEmpty(_currentFilePath) && !_isLoading)
		{
			float width = ((SKRect)(ref bounds)).Width;
			float height = ((SKRect)(ref bounds)).Height;
			if (((double)width > _svgLoadedWidth * 1.1 || (double)height > _svgLoadedHeight * 1.1) && width > 0f && height > 0f && (width != ((SKRect)(ref _lastArrangedBounds)).Width || height != ((SKRect)(ref _lastArrangedBounds)).Height))
			{
				_lastArrangedBounds = bounds;
				Console.WriteLine($"[SkiaImage] Arrange detected larger bounds: {width}x{height} vs loaded {_svgLoadedWidth}x{_svgLoadedHeight}");
				LoadSvgAtSizeAsync(_currentFilePath, width, height);
			}
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		double widthRequest = base.WidthRequest;
		double heightRequest = base.HeightRequest;
		if (widthRequest > 0.0 && heightRequest > 0.0)
		{
			return new SKSize((float)widthRequest, (float)heightRequest);
		}
		if (_image == null)
		{
			if (widthRequest > 0.0)
			{
				return new SKSize((float)widthRequest, (float)widthRequest);
			}
			if (heightRequest > 0.0)
			{
				return new SKSize((float)heightRequest, (float)heightRequest);
			}
			return new SKSize(100f, 100f);
		}
		float num = _image.Width;
		float num2 = _image.Height;
		if (widthRequest > 0.0)
		{
			float num3 = (float)widthRequest / num;
			return new SKSize((float)widthRequest, num2 * num3);
		}
		if (heightRequest > 0.0)
		{
			float num4 = (float)heightRequest / num2;
			return new SKSize(num * num4, (float)heightRequest);
		}
		if (((SKSize)(ref availableSize)).Width < float.MaxValue && ((SKSize)(ref availableSize)).Height < float.MaxValue)
		{
			float num5 = Math.Min(((SKSize)(ref availableSize)).Width / num, ((SKSize)(ref availableSize)).Height / num2);
			return new SKSize(num * num5, num2 * num5);
		}
		if (((SKSize)(ref availableSize)).Width < float.MaxValue)
		{
			float num6 = ((SKSize)(ref availableSize)).Width / num;
			return new SKSize(((SKSize)(ref availableSize)).Width, num2 * num6);
		}
		if (((SKSize)(ref availableSize)).Height < float.MaxValue)
		{
			float num7 = ((SKSize)(ref availableSize)).Height / num2;
			return new SKSize(num * num7, ((SKSize)(ref availableSize)).Height);
		}
		return new SKSize(num, num2);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			SKBitmap? bitmap = _bitmap;
			if (bitmap != null)
			{
				((SKNativeObject)bitmap).Dispose();
			}
			SKImage? image = _image;
			if (image != null)
			{
				((SKNativeObject)image).Dispose();
			}
		}
		base.Dispose(disposing);
	}
}
