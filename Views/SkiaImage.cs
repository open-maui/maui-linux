// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered image control.
/// </summary>
public class SkiaImage : SkiaView
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

    public Aspect Aspect { get; set; } = Aspect.AspectFit;
    public bool IsOpaque { get; set; }
    public bool IsLoading => _isLoading;
    public bool IsAnimationPlaying { get; set; }

    public event EventHandler? ImageLoaded;
    public event EventHandler<ImageLoadingErrorEventArgs>? ImageLoadingError;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background if not opaque
        if (!IsOpaque && BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        if (_image == null) return;

        var imageWidth = _image.Width;
        var imageHeight = _image.Height;

        if (imageWidth <= 0 || imageHeight <= 0) return;

        var destRect = CalculateDestRect(bounds, imageWidth, imageHeight);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        canvas.DrawImage(_image, destRect, paint);
    }

    private SKRect CalculateDestRect(SKRect bounds, float imageWidth, float imageHeight)
    {
        float destX, destY, destWidth, destHeight;

        switch (Aspect)
        {
            case Aspect.Fill:
                // Stretch to fill entire bounds
                return bounds;

            case Aspect.AspectFit:
                // Scale to fit while maintaining aspect ratio
                var fitScale = Math.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
                destWidth = imageWidth * fitScale;
                destHeight = imageHeight * fitScale;
                destX = bounds.Left + (bounds.Width - destWidth) / 2;
                destY = bounds.Top + (bounds.Height - destHeight) / 2;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);

            case Aspect.AspectFill:
                // Scale to fill while maintaining aspect ratio (may crop)
                var fillScale = Math.Max(bounds.Width / imageWidth, bounds.Height / imageHeight);
                destWidth = imageWidth * fillScale;
                destHeight = imageHeight * fillScale;
                destX = bounds.Left + (bounds.Width - destWidth) / 2;
                destY = bounds.Top + (bounds.Height - destHeight) / 2;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);

            case Aspect.Center:
                // Center without scaling
                destX = bounds.Left + (bounds.Width - imageWidth) / 2;
                destY = bounds.Top + (bounds.Height - imageHeight) / 2;
                return new SKRect(destX, destY, destX + imageWidth, destY + imageHeight);

            default:
                return bounds;
        }
    }

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

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        if (_image == null)
            return new SKSize(100, 100); // Default size

        var imageWidth = _image.Width;
        var imageHeight = _image.Height;

        // If we have constraints, respect them
        if (availableSize.Width < float.MaxValue && availableSize.Height < float.MaxValue)
        {
            var scale = Math.Min(availableSize.Width / imageWidth, availableSize.Height / imageHeight);
            return new SKSize(imageWidth * scale, imageHeight * scale);
        }
        else if (availableSize.Width < float.MaxValue)
        {
            var scale = availableSize.Width / imageWidth;
            return new SKSize(availableSize.Width, imageHeight * scale);
        }
        else if (availableSize.Height < float.MaxValue)
        {
            var scale = availableSize.Height / imageHeight;
            return new SKSize(imageWidth * scale, availableSize.Height);
        }

        return new SKSize(imageWidth, imageHeight);
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

/// <summary>
/// Event args for image loading errors.
/// </summary>
public class ImageLoadingErrorEventArgs : EventArgs
{
    public Exception Exception { get; }

    public ImageLoadingErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
