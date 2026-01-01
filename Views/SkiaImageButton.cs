using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform;

public class SkiaImageButton : SkiaView
{
	private SKBitmap? _bitmap;

	private SKImage? _image;

	private bool _isLoading;

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

	public SKColor StrokeColor { get; set; } = SKColors.Transparent;

	public float StrokeThickness { get; set; }

	public float CornerRadius { get; set; }

	public bool IsPressed { get; private set; }

	public bool IsHovered { get; private set; }

	public SKColor PressedBackgroundColor { get; set; } = new SKColor((byte)0, (byte)0, (byte)0, (byte)30);

	public SKColor HoveredBackgroundColor { get; set; } = new SKColor((byte)0, (byte)0, (byte)0, (byte)15);

	public float PaddingLeft { get; set; }

	public float PaddingTop { get; set; }

	public float PaddingRight { get; set; }

	public float PaddingBottom { get; set; }

	public event EventHandler? Clicked;

	public event EventHandler? Pressed;

	public event EventHandler? Released;

	public event EventHandler? ImageLoaded;

	public event EventHandler<ImageLoadingErrorEventArgs>? ImageLoadingError;

	public SkiaImageButton()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		base.IsFocusable = true;
	}

	protected override void OnDraw(SKCanvas canvas, SKRect bounds)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Expected O, but got Unknown
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Expected O, but got Unknown
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Expected O, but got Unknown
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Expected O, but got Unknown
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		SKRect bounds2 = default(SKRect);
		((SKRect)(ref bounds2))._002Ector(((SKRect)(ref bounds)).Left + PaddingLeft, ((SKRect)(ref bounds)).Top + PaddingTop, ((SKRect)(ref bounds)).Right - PaddingRight, ((SKRect)(ref bounds)).Bottom - PaddingBottom);
		if (IsPressed || IsHovered || (!IsOpaque && base.BackgroundColor != SKColors.Transparent))
		{
			SKColor color = (IsPressed ? PressedBackgroundColor : (IsHovered ? HoveredBackgroundColor : base.BackgroundColor));
			SKPaint val = new SKPaint
			{
				Color = color,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				if (CornerRadius > 0f)
				{
					SKRoundRect val2 = new SKRoundRect(bounds, CornerRadius);
					canvas.DrawRoundRect(val2, val);
				}
				else
				{
					canvas.DrawRect(bounds, val);
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (_image != null)
		{
			int width = _image.Width;
			int height = _image.Height;
			if (width > 0 && height > 0)
			{
				SKRect val3 = CalculateDestRect(bounds2, width, height);
				SKPaint val4 = new SKPaint
				{
					IsAntialias = true,
					FilterQuality = (SKFilterQuality)3
				};
				try
				{
					if (!base.IsEnabled)
					{
						SKColor color2 = val4.Color;
						val4.Color = ((SKColor)(ref color2)).WithAlpha((byte)128);
					}
					canvas.DrawImage(_image, val3, val4);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
		}
		if (StrokeThickness > 0f && StrokeColor != SKColors.Transparent)
		{
			SKPaint val5 = new SKPaint
			{
				Color = StrokeColor,
				Style = (SKPaintStyle)1,
				StrokeWidth = StrokeThickness,
				IsAntialias = true
			};
			try
			{
				if (CornerRadius > 0f)
				{
					SKRoundRect val6 = new SKRoundRect(bounds, CornerRadius);
					canvas.DrawRoundRect(val6, val5);
				}
				else
				{
					canvas.DrawRect(bounds, val5);
				}
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
		}
		if (!base.IsFocused)
		{
			return;
		}
		SKPaint val7 = new SKPaint
		{
			Color = new SKColor((byte)0, (byte)0, (byte)0, (byte)64),
			Style = (SKPaintStyle)1,
			StrokeWidth = 2f,
			IsAntialias = true
		};
		try
		{
			SKRect val8 = new SKRect(((SKRect)(ref bounds)).Left - 2f, ((SKRect)(ref bounds)).Top - 2f, ((SKRect)(ref bounds)).Right + 2f, ((SKRect)(ref bounds)).Bottom + 2f);
			if (CornerRadius > 0f)
			{
				SKRoundRect val9 = new SKRoundRect(val8, CornerRadius + 2f);
				canvas.DrawRoundRect(val9, val7);
			}
			else
			{
				canvas.DrawRect(val8, val7);
			}
		}
		finally
		{
			((IDisposable)val7)?.Dispose();
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
		Console.WriteLine("[SkiaImageButton] LoadFromFileAsync: " + filePath);
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
					Console.WriteLine("[SkiaImageButton] Found file at: " + item);
					break;
				}
			}
			if (foundPath == null)
			{
				Console.WriteLine("[SkiaImageButton] File not found: " + filePath);
				Console.WriteLine("[SkiaImageButton] Searched paths: " + string.Join(", ", list));
				_isLoading = false;
				this.ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(new FileNotFoundException(filePath)));
				return;
			}
			await Task.Run(delegate
			{
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Expected O, but got Unknown
				//IL_003a: Unknown result type (might be due to invalid IL or missing references)
				//IL_003f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0114: Unknown result type (might be due to invalid IL or missing references)
				//IL_011b: Expected O, but got Unknown
				//IL_011d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0124: Expected O, but got Unknown
				//IL_0126: Unknown result type (might be due to invalid IL or missing references)
				if (foundPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
				{
					SKSvg val = new SKSvg();
					try
					{
						val.Load(foundPath);
						if (val.Picture != null)
						{
							SKRect cullRect = val.Picture.CullRect;
							bool num = base.WidthRequest > 0.0;
							bool flag = base.HeightRequest > 0.0;
							float num2 = (num ? ((float)(base.WidthRequest - (double)PaddingLeft - (double)PaddingRight)) : ((SKRect)(ref cullRect)).Width);
							float num3 = Math.Min(val2: (flag ? ((float)(base.HeightRequest - (double)PaddingTop - (double)PaddingBottom)) : ((SKRect)(ref cullRect)).Height) / ((SKRect)(ref cullRect)).Height, val1: num2 / ((SKRect)(ref cullRect)).Width);
							int num4 = Math.Max(1, (int)(((SKRect)(ref cullRect)).Width * num3));
							int num5 = Math.Max(1, (int)(((SKRect)(ref cullRect)).Height * num3));
							SKBitmap val2 = new SKBitmap(num4, num5, false);
							SKCanvas val3 = new SKCanvas(val2);
							try
							{
								val3.Clear(SKColors.Transparent);
								val3.Scale(num3);
								val3.DrawPicture(val.Picture, (SKPaint)null);
								Bitmap = val2;
								Console.WriteLine($"[SkiaImageButton] Loaded SVG: {foundPath} ({num4}x{num5})");
								return;
							}
							finally
							{
								((IDisposable)val3)?.Dispose();
							}
						}
						return;
					}
					finally
					{
						((IDisposable)val)?.Dispose();
					}
				}
				using FileStream fileStream = File.OpenRead(foundPath);
				SKBitmap val4 = SKBitmap.Decode((Stream)fileStream);
				if (val4 != null)
				{
					Bitmap = val4;
					Console.WriteLine("[SkiaImageButton] Loaded image: " + foundPath);
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

	public override void OnPointerEntered(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			IsHovered = true;
			SkiaVisualStateManager.GoToState(this, "PointerOver");
			Invalidate();
		}
	}

	public override void OnPointerExited(PointerEventArgs e)
	{
		IsHovered = false;
		if (IsPressed)
		{
			IsPressed = false;
		}
		SkiaVisualStateManager.GoToState(this, base.IsEnabled ? "Normal" : "Disabled");
		Invalidate();
	}

	public override void OnPointerPressed(PointerEventArgs e)
	{
		if (base.IsEnabled)
		{
			IsPressed = true;
			SkiaVisualStateManager.GoToState(this, "Pressed");
			Invalidate();
			this.Pressed?.Invoke(this, EventArgs.Empty);
		}
	}

	public override void OnPointerReleased(PointerEventArgs e)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (!base.IsEnabled)
		{
			return;
		}
		bool isPressed = IsPressed;
		IsPressed = false;
		SkiaVisualStateManager.GoToState(this, IsHovered ? "PointerOver" : "Normal");
		Invalidate();
		this.Released?.Invoke(this, EventArgs.Empty);
		if (isPressed)
		{
			SKRect bounds = base.Bounds;
			if (((SKRect)(ref bounds)).Contains(new SKPoint(e.X, e.Y)))
			{
				this.Clicked?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public override void OnKeyDown(KeyEventArgs e)
	{
		if (base.IsEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
		{
			IsPressed = true;
			Invalidate();
			this.Pressed?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
	}

	public override void OnKeyUp(KeyEventArgs e)
	{
		if (base.IsEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
		{
			if (IsPressed)
			{
				IsPressed = false;
				Invalidate();
				this.Released?.Invoke(this, EventArgs.Empty);
				this.Clicked?.Invoke(this, EventArgs.Empty);
			}
			e.Handled = true;
		}
	}

	protected override SKSize MeasureOverride(SKSize availableSize)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		SKSize val = default(SKSize);
		((SKSize)(ref val))._002Ector(PaddingLeft + PaddingRight, PaddingTop + PaddingBottom);
		if (_image == null)
		{
			return new SKSize(44f + ((SKSize)(ref val)).Width, 44f + ((SKSize)(ref val)).Height);
		}
		int width = _image.Width;
		int height = _image.Height;
		if (((SKSize)(ref availableSize)).Width < float.MaxValue && ((SKSize)(ref availableSize)).Height < float.MaxValue)
		{
			SKSize val2 = default(SKSize);
			((SKSize)(ref val2))._002Ector(((SKSize)(ref availableSize)).Width - ((SKSize)(ref val)).Width, ((SKSize)(ref availableSize)).Height - ((SKSize)(ref val)).Height);
			float num = Math.Min(((SKSize)(ref val2)).Width / (float)width, ((SKSize)(ref val2)).Height / (float)height);
			return new SKSize((float)width * num + ((SKSize)(ref val)).Width, (float)height * num + ((SKSize)(ref val)).Height);
		}
		if (((SKSize)(ref availableSize)).Width < float.MaxValue)
		{
			float num2 = (((SKSize)(ref availableSize)).Width - ((SKSize)(ref val)).Width) / (float)width;
			return new SKSize(((SKSize)(ref availableSize)).Width, (float)height * num2 + ((SKSize)(ref val)).Height);
		}
		if (((SKSize)(ref availableSize)).Height < float.MaxValue)
		{
			float num3 = (((SKSize)(ref availableSize)).Height - ((SKSize)(ref val)).Height) / (float)height;
			return new SKSize((float)width * num3 + ((SKSize)(ref val)).Width, ((SKSize)(ref availableSize)).Height);
		}
		return new SKSize((float)width + ((SKSize)(ref val)).Width, (float)height + ((SKSize)(ref val)).Height);
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
