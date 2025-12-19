// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered image button control.
/// Combines button behavior with image display.
/// </summary>
public class SkiaImageButton : SkiaView
{
    private SKBitmap? _bitmap;
    private SKImage? _image;
    private bool _isLoading;

    public SKBitmap? Bitmap
    {
        get => _bitmap;
        set
        {
            _bitmap?.Dispose();
            _bitmap = value;
            _image?.Dispose();
            _image = value != null ? SKImage.FromBitmap(value) : null;
            Invalidate();
        }
    }

    // Image properties
    public Aspect Aspect { get; set; } = Aspect.AspectFit;
    public bool IsOpaque { get; set; }
    public bool IsLoading => _isLoading;

    // Button stroke properties
    public SKColor StrokeColor { get; set; } = SKColors.Transparent;
    public float StrokeThickness { get; set; } = 0;
    public float CornerRadius { get; set; } = 0;

    // Button state
    public bool IsPressed { get; private set; }
    public bool IsHovered { get; private set; }

    // Visual state colors
    public SKColor PressedBackgroundColor { get; set; } = new SKColor(0, 0, 0, 30);
    public SKColor HoveredBackgroundColor { get; set; } = new SKColor(0, 0, 0, 15);

    // Padding for the image content
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
        IsFocusable = true;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Apply padding
        var contentBounds = new SKRect(
            bounds.Left + PaddingLeft,
            bounds.Top + PaddingTop,
            bounds.Right - PaddingRight,
            bounds.Bottom - PaddingBottom);

        // Draw background based on state
        if (IsPressed || IsHovered || !IsOpaque && BackgroundColor != SKColors.Transparent)
        {
            var bgColor = IsPressed ? PressedBackgroundColor
                        : IsHovered ? HoveredBackgroundColor
                        : BackgroundColor;

            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            if (CornerRadius > 0)
            {
                var roundRect = new SKRoundRect(bounds, CornerRadius);
                canvas.DrawRoundRect(roundRect, bgPaint);
            }
            else
            {
                canvas.DrawRect(bounds, bgPaint);
            }
        }

        // Draw image
        if (_image != null)
        {
            var imageWidth = _image.Width;
            var imageHeight = _image.Height;

            if (imageWidth > 0 && imageHeight > 0)
            {
                var destRect = CalculateDestRect(contentBounds, imageWidth, imageHeight);

                using var paint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };

                // Apply opacity when disabled
                if (!IsEnabled)
                {
                    paint.Color = paint.Color.WithAlpha(128);
                }

                canvas.DrawImage(_image, destRect, paint);
            }
        }

        // Draw stroke/border
        if (StrokeThickness > 0 && StrokeColor != SKColors.Transparent)
        {
            using var strokePaint = new SKPaint
            {
                Color = StrokeColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = StrokeThickness,
                IsAntialias = true
            };

            if (CornerRadius > 0)
            {
                var roundRect = new SKRoundRect(bounds, CornerRadius);
                canvas.DrawRoundRect(roundRect, strokePaint);
            }
            else
            {
                canvas.DrawRect(bounds, strokePaint);
            }
        }

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0x00, 0x00, 0x40),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            var focusBounds = new SKRect(bounds.Left - 2, bounds.Top - 2, bounds.Right + 2, bounds.Bottom + 2);
            if (CornerRadius > 0)
            {
                var focusRect = new SKRoundRect(focusBounds, CornerRadius + 2);
                canvas.DrawRoundRect(focusRect, focusPaint);
            }
            else
            {
                canvas.DrawRect(focusBounds, focusPaint);
            }
        }
    }

    private SKRect CalculateDestRect(SKRect bounds, float imageWidth, float imageHeight)
    {
        float destX, destY, destWidth, destHeight;

        switch (Aspect)
        {
            case Aspect.Fill:
                return bounds;

            case Aspect.AspectFit:
                var fitScale = Math.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
                destWidth = imageWidth * fitScale;
                destHeight = imageHeight * fitScale;
                destX = bounds.Left + (bounds.Width - destWidth) / 2;
                destY = bounds.Top + (bounds.Height - destHeight) / 2;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);

            case Aspect.AspectFill:
                var fillScale = Math.Max(bounds.Width / imageWidth, bounds.Height / imageHeight);
                destWidth = imageWidth * fillScale;
                destHeight = imageHeight * fillScale;
                destX = bounds.Left + (bounds.Width - destWidth) / 2;
                destY = bounds.Top + (bounds.Height - destHeight) / 2;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);

            case Aspect.Center:
                destX = bounds.Left + (bounds.Width - imageWidth) / 2;
                destY = bounds.Top + (bounds.Height - imageHeight) / 2;
                return new SKRect(destX, destY, destX + imageWidth, destY + imageHeight);

            default:
                return bounds;
        }
    }

    // Image loading methods
    public async Task LoadFromFileAsync(string filePath)
    {
        _isLoading = true;
        Invalidate();

        try
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(filePath);
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    Bitmap = bitmap;
                }
            });

            _isLoading = false;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }

        Invalidate();
    }

    public async Task LoadFromStreamAsync(Stream stream)
    {
        _isLoading = true;
        Invalidate();

        try
        {
            await Task.Run(() =>
            {
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    Bitmap = bitmap;
                }
            });

            _isLoading = false;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }

        Invalidate();
    }

    public async Task LoadFromUriAsync(Uri uri)
    {
        _isLoading = true;
        Invalidate();

        try
        {
            using var httpClient = new HttpClient();
            var data = await httpClient.GetByteArrayAsync(uri);

            using var stream = new MemoryStream(data);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                Bitmap = bitmap;
            }

            _isLoading = false;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }

        Invalidate();
    }

    public void LoadFromData(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                Bitmap = bitmap;
            }
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }
    }

    // Pointer event handlers
    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        IsHovered = true;
        Invalidate();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        if (IsPressed)
        {
            IsPressed = false;
        }
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        IsPressed = true;
        Invalidate();
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var wasPressed = IsPressed;
        IsPressed = false;
        Invalidate();

        Released?.Invoke(this, EventArgs.Empty);

        if (wasPressed && Bounds.Contains(new SKPoint(e.X, e.Y)))
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    // Keyboard event handlers
    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            IsPressed = true;
            Invalidate();
            Pressed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            if (IsPressed)
            {
                IsPressed = false;
                Invalidate();
                Released?.Invoke(this, EventArgs.Empty);
                Clicked?.Invoke(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var padding = new SKSize(PaddingLeft + PaddingRight, PaddingTop + PaddingBottom);

        if (_image == null)
            return new SKSize(44 + padding.Width, 44 + padding.Height); // Default touch target size

        var imageWidth = _image.Width;
        var imageHeight = _image.Height;

        if (availableSize.Width < float.MaxValue && availableSize.Height < float.MaxValue)
        {
            var availableContent = new SKSize(
                availableSize.Width - padding.Width,
                availableSize.Height - padding.Height);
            var scale = Math.Min(availableContent.Width / imageWidth, availableContent.Height / imageHeight);
            return new SKSize(imageWidth * scale + padding.Width, imageHeight * scale + padding.Height);
        }
        else if (availableSize.Width < float.MaxValue)
        {
            var availableWidth = availableSize.Width - padding.Width;
            var scale = availableWidth / imageWidth;
            return new SKSize(availableSize.Width, imageHeight * scale + padding.Height);
        }
        else if (availableSize.Height < float.MaxValue)
        {
            var availableHeight = availableSize.Height - padding.Height;
            var scale = availableHeight / imageHeight;
            return new SKSize(imageWidth * scale + padding.Width, availableSize.Height);
        }

        return new SKSize(imageWidth + padding.Width, imageHeight + padding.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap?.Dispose();
            _image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
