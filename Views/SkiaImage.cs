// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered image control with SVG support.
/// </summary>
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

    public Aspect Aspect { get; set; }

    public bool IsOpaque { get; set; }

    public bool IsLoading => _isLoading;

    public bool IsAnimationPlaying { get; set; }

    public new double WidthRequest
    {
        get => base.WidthRequest;
        set
        {
            base.WidthRequest = value;
            ScheduleSvgReloadIfNeeded();
        }
    }

    public new double HeightRequest
    {
        get => base.HeightRequest;
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
            if (widthRequest > 0.0 && heightRequest > 0.0 &&
                (Math.Abs(_svgLoadedWidth - widthRequest) > 0.5 || Math.Abs(_svgLoadedHeight - heightRequest) > 0.5) &&
                !_pendingSvgReload)
            {
                _pendingSvgReload = true;
                _ = ReloadSvgDebounced();
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

        if (_image == null)
            return;

        int width = _image.Width;
        int height = _image.Height;

        if (width <= 0 || height <= 0)
            return;

        SKRect destRect = CalculateDestRect(bounds, width, height);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        canvas.DrawImage(_image, destRect, paint);
    }

    private SKRect CalculateDestRect(SKRect bounds, float imageWidth, float imageHeight)
    {
        switch (Aspect)
        {
            case Aspect.Fill:
                return bounds;

            case Aspect.AspectFit:
            {
                float scale = Math.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
                float destWidth = imageWidth * scale;
                float destHeight = imageHeight * scale;
                float destX = bounds.Left + (bounds.Width - destWidth) / 2f;
                float destY = bounds.Top + (bounds.Height - destHeight) / 2f;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);
            }

            case Aspect.AspectFill:
            {
                float scale = Math.Max(bounds.Width / imageWidth, bounds.Height / imageHeight);
                float destWidth = imageWidth * scale;
                float destHeight = imageHeight * scale;
                float destX = bounds.Left + (bounds.Width - destWidth) / 2f;
                float destY = bounds.Top + (bounds.Height - destHeight) / 2f;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);
            }

            case Aspect.Center:
            {
                float destX = bounds.Left + (bounds.Width - imageWidth) / 2f;
                float destY = bounds.Top + (bounds.Height - imageHeight) / 2f;
                return new SKRect(destX, destY, destX + imageWidth, destY + imageHeight);
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
            List<string> searchPaths = new List<string>
            {
                filePath,
                Path.Combine(AppContext.BaseDirectory, filePath),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Images", filePath),
                Path.Combine(AppContext.BaseDirectory, "Resources", filePath)
            };

            // Also try SVG if looking for PNG
            if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                string svgPath = Path.ChangeExtension(filePath, ".svg");
                searchPaths.Add(svgPath);
                searchPaths.Add(Path.Combine(AppContext.BaseDirectory, svgPath));
                searchPaths.Add(Path.Combine(AppContext.BaseDirectory, "Resources", "Images", svgPath));
                searchPaths.Add(Path.Combine(AppContext.BaseDirectory, "Resources", svgPath));
            }

            string? foundPath = null;
            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    Console.WriteLine("[SkiaImage] Found file at: " + path);
                    break;
                }
            }

            if (foundPath == null)
            {
                Console.WriteLine("[SkiaImage] File not found: " + filePath);
                _isLoading = false;
                _isSvg = false;
                _currentFilePath = null;
                ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(new FileNotFoundException(filePath)));
                return;
            }

            _isSvg = foundPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            _currentFilePath = foundPath;

            if (!_isSvg)
            {
                await Task.Run(() =>
                {
                    using FileStream stream = File.OpenRead(foundPath);
                    SKBitmap? bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        Bitmap = bitmap;
                        Console.WriteLine("[SkiaImage] Loaded image: " + foundPath);
                    }
                });
            }
            else
            {
                await LoadSvgAtSizeAsync(foundPath, WidthRequest, HeightRequest);
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

    private async Task LoadSvgAtSizeAsync(string svgPath, double targetWidth, double targetHeight)
    {
        _loadCts?.Cancel();
        CancellationTokenSource cts = new CancellationTokenSource();
        _loadCts = cts;

        try
        {
            SKBitmap? newBitmap = null;

            await Task.Run(() =>
            {
                if (cts.Token.IsCancellationRequested)
                    return;

                using var svg = new SKSvg();
                svg.Load(svgPath);

                if (svg.Picture != null && !cts.Token.IsCancellationRequested)
                {
                    SKRect cullRect = svg.Picture.CullRect;

                    float requestedWidth = (targetWidth > 0.0)
                        ? (float)targetWidth
                        : ((cullRect.Width <= 24f) ? 24f : cullRect.Width);

                    float requestedHeight = (targetHeight > 0.0)
                        ? (float)targetHeight
                        : ((cullRect.Height <= 24f) ? 24f : cullRect.Height);

                    float scale = Math.Min(requestedWidth / cullRect.Width, requestedHeight / cullRect.Height);

                    int bitmapWidth = Math.Max(1, (int)(cullRect.Width * scale));
                    int bitmapHeight = Math.Max(1, (int)(cullRect.Height * scale));

                    newBitmap = new SKBitmap(bitmapWidth, bitmapHeight, false);

                    using var canvas = new SKCanvas(newBitmap);
                    canvas.Clear(SKColors.Transparent);
                    canvas.Scale(scale);
                    canvas.DrawPicture(svg.Picture, null);

                    Console.WriteLine($"[SkiaImage] Loaded SVG: {svgPath} at {bitmapWidth}x{bitmapHeight} (requested {targetWidth}x{targetHeight})");
                }
            }, cts.Token);

            if (!cts.Token.IsCancellationRequested && newBitmap != null)
            {
                _svgLoadedWidth = (targetWidth > 0.0) ? targetWidth : newBitmap.Width;
                _svgLoadedHeight = (targetHeight > 0.0) ? targetHeight : newBitmap.Height;
                Bitmap = newBitmap;
            }
            else
            {
                newBitmap?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when reloading SVG at different sizes
        }
    }

    public async Task LoadFromStreamAsync(Stream stream)
    {
        _isLoading = true;
        Invalidate();

        try
        {
            await Task.Run(() =>
            {
                SKBitmap? bitmap = SKBitmap.Decode(stream);
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
            using HttpClient httpClient = new HttpClient();
            using MemoryStream stream = new MemoryStream(await httpClient.GetByteArrayAsync(uri));
            SKBitmap? bitmap = SKBitmap.Decode(stream);
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
            using MemoryStream stream = new MemoryStream(data);
            SKBitmap? bitmap = SKBitmap.Decode(stream);
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

    /// <summary>
    /// Loads the image from an SKBitmap.
    /// </summary>
    public void LoadFromBitmap(SKBitmap bitmap)
    {
        try
        {
            _isSvg = false;
            _currentFilePath = null;
            Bitmap = bitmap;
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

    public override void Arrange(SKRect bounds)
    {
        base.Arrange(bounds);

        // If no explicit size requested and this is an SVG, check if we need to reload at larger size
        if (!(base.WidthRequest > 0.0) || !(base.HeightRequest > 0.0))
        {
            if (_isSvg && !string.IsNullOrEmpty(_currentFilePath) && !_isLoading)
            {
                float width = bounds.Width;
                float height = bounds.Height;

                if ((width > _svgLoadedWidth * 1.1 || height > _svgLoadedHeight * 1.1) &&
                    width > 0f && height > 0f &&
                    (width != _lastArrangedBounds.Width || height != _lastArrangedBounds.Height))
                {
                    _lastArrangedBounds = bounds;
                    Console.WriteLine($"[SkiaImage] Arrange detected larger bounds: {width}x{height} vs loaded {_svgLoadedWidth}x{_svgLoadedHeight}");
                    _ = LoadSvgAtSizeAsync(_currentFilePath, width, height);
                }
            }
        }
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        // If we have explicit size requests, constrain to desired size
        // This follows MAUI standard behavior - controls respect WidthRequest/HeightRequest
        var desiredWidth = DesiredSize.Width;
        var desiredHeight = DesiredSize.Height;

        // If desired size is smaller than available bounds, align within bounds
        if (desiredWidth > 0 && desiredHeight > 0 &&
            (desiredWidth < bounds.Width || desiredHeight < bounds.Height))
        {
            float finalWidth = Math.Min(desiredWidth, bounds.Width);
            float finalHeight = Math.Min(desiredHeight, bounds.Height);

            // Calculate position based on HorizontalOptions
            // LayoutAlignment: Start=0, Center=1, End=2, Fill=3
            float x = bounds.Left;
            var hAlignValue = (int)HorizontalOptions.Alignment;
            if (hAlignValue == 1) // Center
            {
                x = bounds.Left + (bounds.Width - finalWidth) / 2;
            }
            else if (hAlignValue == 2) // End
            {
                x = bounds.Right - finalWidth;
            }

            // Calculate position based on VerticalOptions
            float y = bounds.Top;
            var vAlignValue = (int)VerticalOptions.Alignment;
            if (vAlignValue == 1) // Center
            {
                y = bounds.Top + (bounds.Height - finalHeight) / 2;
            }
            else if (vAlignValue == 2) // End
            {
                y = bounds.Bottom - finalHeight;
            }

            return new SKRect(x, y, x + finalWidth, y + finalHeight);
        }

        return bounds;
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        double widthRequest = base.WidthRequest;
        double heightRequest = base.HeightRequest;

        // If both dimensions explicitly requested, use them
        if (widthRequest > 0.0 && heightRequest > 0.0)
        {
            return new SKSize((float)widthRequest, (float)heightRequest);
        }

        // If no image, return default or requested size
        if (_image == null)
        {
            if (widthRequest > 0.0)
                return new SKSize((float)widthRequest, (float)widthRequest);
            if (heightRequest > 0.0)
                return new SKSize((float)heightRequest, (float)heightRequest);
            return new SKSize(100f, 100f);
        }

        float imageWidth = _image.Width;
        float imageHeight = _image.Height;

        // If only width requested, scale height proportionally
        if (widthRequest > 0.0)
        {
            float scale = (float)widthRequest / imageWidth;
            return new SKSize((float)widthRequest, imageHeight * scale);
        }

        // If only height requested, scale width proportionally
        if (heightRequest > 0.0)
        {
            float scale = (float)heightRequest / imageHeight;
            return new SKSize(imageWidth * scale, (float)heightRequest);
        }

        // Scale to fit available size
        if (availableSize.Width < float.MaxValue && availableSize.Height < float.MaxValue)
        {
            float scale = Math.Min(availableSize.Width / imageWidth, availableSize.Height / imageHeight);
            return new SKSize(imageWidth * scale, imageHeight * scale);
        }

        if (availableSize.Width < float.MaxValue)
        {
            float scale = availableSize.Width / imageWidth;
            return new SKSize(availableSize.Width, imageHeight * scale);
        }

        if (availableSize.Height < float.MaxValue)
        {
            float scale = availableSize.Height / imageHeight;
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
