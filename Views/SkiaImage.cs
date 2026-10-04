// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered image control with SVG support and GIF animation.
/// Full MAUI-compliant implementation.
/// </summary>
public class SkiaImage : SkiaView
{
    /// <summary>
    /// Clears the image cache.
    /// </summary>
    public static void ClearCache() => LinuxImageCache.Clear();

    #region Animation Support

    private IReadOnlyList<ImageFrame>? _animationFrames;
    private int _currentFrameIndex;
    private System.Timers.Timer? _animationTimer;
    private bool _isAnimatedImage;

    #endregion

    #region BindableProperties

    /// <summary>
    /// Bindable property for Aspect.
    /// </summary>
    public static readonly BindableProperty AspectProperty =
        BindableProperty.Create(
            nameof(Aspect),
            typeof(Aspect),
            typeof(SkiaImage),
            Aspect.AspectFit,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaImage)b).Invalidate());

    /// <summary>
    /// Bindable property for IsOpaque.
    /// </summary>
    public static readonly BindableProperty IsOpaqueProperty =
        BindableProperty.Create(
            nameof(IsOpaque),
            typeof(bool),
            typeof(SkiaImage),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaImage)b).Invalidate());

    /// <summary>
    /// Bindable property for IsAnimationPlaying.
    /// </summary>
    public static readonly BindableProperty IsAnimationPlayingProperty =
        BindableProperty.Create(
            nameof(IsAnimationPlaying),
            typeof(bool),
            typeof(SkiaImage),
            false,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaImage)b).OnIsAnimationPlayingChanged((bool)n));

    /// <summary>
    /// Bindable property for ImageBackgroundColor (MAUI Color for background).
    /// </summary>
    public static readonly BindableProperty ImageBackgroundColorProperty =
        BindableProperty.Create(
            nameof(ImageBackgroundColor),
            typeof(Color),
            typeof(SkiaImage),
            Colors.Transparent,
            BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaImage)b).Invalidate());

    #endregion

    #region Color Conversion Helper

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor.
    /// Uses the ToSKColor() extension from ColorExtensions for MAUI-compliant theming.
    /// </summary>
    private static SKColor ToSKColor(Color color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    #endregion

    private SKBitmap? _bitmap;
    private SKImage? _image;
    // The image-source result _bitmap came from: it owns the bitmap (and any animation
    // frames), so the view disposes the result, never its bitmap. Null for a bitmap set
    // directly, which the view owns unless the image cache shares it.
    private IImageSourceServiceResult? _result;
    // Pixels per logical unit of _bitmap: the scale a resolution-dependent picture (SVG,
    // font glyph) was rendered at, so it measures and centres at its logical size.
    private float _density = 1f;
    private bool _isLoading;
    private string? _currentFilePath;
    private bool _isSvg;
    private CancellationTokenSource? _loadCts;
    private double _svgLoadedWidth;
    private double _svgLoadedHeight;
    private bool _pendingSvgReload;
    private SKRect _lastArrangedBounds;

    public SKBitmap? Bitmap
    {
        get => _bitmap;
        set => ShowBitmap(value, null, 1f);
    }

    private void ShowBitmap(SKBitmap? bitmap, IImageSourceServiceResult? result, float density)
    {
        var oldBitmap = _bitmap;
        var oldResult = _result;
        _bitmap = bitmap;
        _result = result;
        _density = density > 0f ? density : 1f;
        _image?.Dispose();
        _image = bitmap != null ? SKImage.FromBitmap(bitmap) : null;

        // Released after the new picture is in place. A cached bitmap is shared with other
        // views: only one this view owns is disposed.
        if (oldResult != null)
        {
            if (!ReferenceEquals(oldResult, result))
                oldResult.Dispose();
        }
        else if (oldBitmap != null && !ReferenceEquals(oldBitmap, bitmap) && !LinuxImageCache.IsShared(oldBitmap))
        {
            oldBitmap.Dispose();
        }
        Invalidate();
    }

    /// <summary>
    /// Gets or sets the aspect ratio scaling mode.
    /// </summary>
    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the image is opaque.
    /// </summary>
    public bool IsOpaque
    {
        get => (bool)GetValue(IsOpaqueProperty);
        set => SetValue(IsOpaqueProperty, value);
    }

    /// <summary>
    /// Gets whether the image is currently loading.
    /// </summary>
    public bool IsLoading => _isLoading;

    /// <summary>
    /// Gets or sets whether animation is playing (for GIF support).
    /// When set to true, animated GIFs will play their animation.
    /// When set to false, the first frame is displayed.
    /// </summary>
    public bool IsAnimationPlaying
    {
        get => (bool)GetValue(IsAnimationPlayingProperty);
        set => SetValue(IsAnimationPlayingProperty, value);
    }

    /// <summary>
    /// Gets or sets the image background color (MAUI Color type).
    /// </summary>
    public Color ImageBackgroundColor
    {
        get => (Color)GetValue(ImageBackgroundColorProperty);
        set => SetValue(ImageBackgroundColorProperty, value);
    }

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

    /// <summary>
    /// Raised by <see cref="ClearImage"/> once the picture has been removed: the
    /// counterpart of <see cref="ImageLoaded"/> for a null picture.
    /// </summary>
    public event EventHandler? ImageCleared;

    /// <summary>
    /// Removes the displayed picture, as MAUI's image handlers set a null image when
    /// the source is null or fails to load. Stops a running animation; a bitmap
    /// shared through the image cache is released, never disposed.
    /// </summary>
    public void ClearImage()
    {
        StopAnimation();
        _loadCts?.Cancel();
        // Frames are dropped, not disposed: an animation timer tick may still be reading one.
        _animationFrames = null;
        _isAnimatedImage = false;
        _currentFrameIndex = 0;
        _isSvg = false;
        _currentFilePath = null;
        _isLoading = false;
        ShowBitmap(null, null, 1f);
        ImageCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shows a bitmap an image-source service loaded. The view keeps the result while it
    /// shows the bitmap and disposes it when the picture changes (see
    /// <see cref="LinuxImageSourceServiceResult"/>).
    /// </summary>
    /// <param name="result">The loaded image.</param>
    /// <param name="scale">The scale the image was requested at.</param>
    internal void ApplyResult(IImageSourceServiceResult<SKBitmap> result, float scale)
    {
        StopAnimation();
        // A pending SVG re-render of the previous picture must not replace this one.
        _loadCts?.Cancel();

        var linux = result as LinuxImageSourceServiceResult;
        float density = result.IsResolutionDependent && scale > 0f ? scale : 1f;
        _isLoading = false;
        _currentFrameIndex = 0;
        _animationFrames = linux?.Frames is { Count: > 1 } frames ? frames : null;
        _isAnimatedImage = _animationFrames != null;
        _isSvg = linux?.SvgPath != null;
        _currentFilePath = linux?.SvgPath;
        ShowBitmap(result.Value, result, density);

        if (_isSvg)
        {
            _svgLoadedWidth = WidthRequest > 0.0 ? WidthRequest : result.Value.Width / density;
            _svgLoadedHeight = HeightRequest > 0.0 ? HeightRequest : result.Value.Height / density;
        }
        if (_isAnimatedImage && IsAnimationPlaying)
            StartAnimation();

        ImageLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void OnIsAnimationPlayingChanged(bool isPlaying)
    {
        if (_isAnimatedImage && _animationFrames != null && _animationFrames.Count > 1)
        {
            if (isPlaying)
            {
                StartAnimation();
            }
            else
            {
                StopAnimation();
            }
        }
    }

    private void StartAnimation()
    {
        if (_animationFrames == null || _animationFrames.Count <= 1)
            return;

        StopAnimation();

        var frame = _animationFrames[_currentFrameIndex];
        int duration = frame.Duration > 0 ? frame.Duration : 100; // Default 100ms if not specified

        _animationTimer = new System.Timers.Timer(duration);
        _animationTimer.Elapsed += OnAnimationTimerElapsed;
        _animationTimer.AutoReset = false;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer.Elapsed -= OnAnimationTimerElapsed;
            _animationTimer.Dispose();
            _animationTimer = null;
        }
    }

    private void OnAnimationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // A static image loaded since the animation started ends it.
        if (_animationFrames == null || _animationFrames.Count <= 1 || !IsAnimationPlaying || !_isAnimatedImage)
            return;

        // Move to next frame
        _currentFrameIndex = (_currentFrameIndex + 1) % _animationFrames.Count;

        // Update the displayed image
        var frame = _animationFrames[_currentFrameIndex];
        if (frame.Bitmap != null)
        {
            _image?.Dispose();
            _image = SKImage.FromBitmap(frame.Bitmap);
            Invalidate();
        }

        // Schedule next frame
        if (IsAnimationPlaying)
        {
            int duration = frame.Duration > 0 ? frame.Duration : 100;
            _animationTimer?.Stop();
            if (_animationTimer != null)
            {
                _animationTimer.Interval = duration;
                _animationTimer.Start();
            }
        }
    }

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
            await LoadSvgAtSizeAsync(_currentFilePath, WidthRequest, HeightRequest);
        }
    }

    private Color? _tintColor;

    /// <summary>
    /// Draws every opaque pixel of the image in this colour (null draws it as is): the tint
    /// CommunityToolkit's IconTintColorBehavior gives an icon on the other platforms.
    /// </summary>
    public Color? TintColor
    {
        get => _tintColor;
        set
        {
            if (Equals(_tintColor, value)) return;
            _tintColor = value;
            Invalidate();
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background if not opaque
        var bgColor = ImageBackgroundColor != null ? ToSKColor(ImageBackgroundColor) : SKColors.Transparent;
        if (!IsOpaque && bgColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        if (_image == null)
            return;

        // Logical size: a picture rendered for a HiDPI scale draws at its logical size.
        float width = _image.Width / _density;
        float height = _image.Height / _density;

        if (width <= 0 || height <= 0)
            return;

        SKRect destRect = CalculateDestRect(bounds, width, height);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };
        using var tint = _tintColor == null ? null : SKColorFilter.CreateBlendMode(_tintColor.ToSKColor(), SKBlendMode.SrcIn);
        paint.ColorFilter = tint;

        // AspectFill (and Center, for a large image) scales the picture past the view; the excess
        // is cropped, as an Image crops it on every platform. Unclipped it drew over the views
        // around it (CiteLynq's Daily Discovery photo, over the title under it).
        bool overflows = destRect.Left < bounds.Left || destRect.Top < bounds.Top
            || destRect.Right > bounds.Right || destRect.Bottom > bounds.Bottom;
        if (overflows)
        {
            canvas.Save();
            canvas.ClipRect(bounds);
        }

        // SKFilterQuality.High equivalent in SkiaSharp 4: cubic (Mitchell) resampling
        canvas.DrawImage(_image, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);

        if (overflows)
            canvas.Restore();
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

    public Task LoadFromFileAsync(string filePath) =>
        LoadAsync(scale => LinuxFileImageSourceService.LoadFileAsync(filePath, scale, new Size(WidthRequest, HeightRequest), CancellationToken.None));

    public Task LoadFromStreamAsync(Stream stream) =>
        LoadAsync(_ => LinuxStreamImageSourceService.LoadStreamAsync(stream, CancellationToken.None));

    public Task LoadFromUriAsync(Uri uri) =>
        LoadAsync(_ => LinuxUriImageSourceService.LoadUriAsync(uri, true, CancellationToken.None));

    // The view's own loads go through the built-in services' loaders, as the handler's do;
    // failures are reported through ImageLoadingError, not thrown.
    private async Task LoadAsync(Func<float, Task<LinuxImageSourceServiceResult>> load)
    {
        _isLoading = true;
        Invalidate();

        float scale = DeviceScale;
        try
        {
            ApplyResult(await load(scale), scale);
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
        float scale = DeviceScale;

        try
        {
            var newBitmap = await Task.Run(() => cts.Token.IsCancellationRequested
                ? null
                : LinuxImageLoader.RenderSvgFile(svgPath, targetWidth, targetHeight, scale), cts.Token);

            if (!cts.Token.IsCancellationRequested && newBitmap != null)
            {
                _svgLoadedWidth = (targetWidth > 0.0) ? targetWidth : newBitmap.Width / scale;
                _svgLoadedHeight = (targetHeight > 0.0) ? targetHeight : newBitmap.Height / scale;
                _isAnimatedImage = false;
                // Owned by the view; the result of the first render is released.
                ShowBitmap(newBitmap, null, scale);
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

    public void LoadFromData(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            var decoded = LinuxImageLoader.Decode(stream)
                ?? throw new InvalidOperationException("Unable to decode the image data.");
            ApplyResult(LinuxImageSourceServices.Owned(decoded), 1f);
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
            _isAnimatedImage = false;
            StopAnimation();
            _animationFrames = null;
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

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // If no explicit size requested and this is an SVG, check if we need to reload at larger size
        if (!(base.WidthRequest > 0.0) || !(base.HeightRequest > 0.0))
        {
            if (_isSvg && !string.IsNullOrEmpty(_currentFilePath) && !_isLoading)
            {
                float width = (float)bounds.Width;
                float height = (float)bounds.Height;

                if ((width > _svgLoadedWidth * 1.1 || height > _svgLoadedHeight * 1.1) &&
                    width > 0f && height > 0f &&
                    (width != _lastArrangedBounds.Width || height != _lastArrangedBounds.Height))
                {
                    _lastArrangedBounds = new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
                    _ = LoadSvgAtSizeAsync(_currentFilePath, width, height);
                }
            }
        }
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        // The MAUI view's alignment (Fill takes the whole slot and the image is fitted
        // inside it; Start/Center/End place the desired size), as MAUI's ComputeFrame
        // does. Placing the desired size by the Skia-side option ignored the MAUI view
        // and put a Fill image at its slot's left edge (Strikeline's welcome rotator).
        return SkiaPage.AlignContent(this, new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom), DesiredSize);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double widthRequest = base.WidthRequest;
        double heightRequest = base.HeightRequest;

        if (widthRequest > 0.0 && heightRequest > 0.0)
            return new Size(widthRequest, heightRequest);

        if (_image == null)
        {
            if (widthRequest > 0.0) return new Size(widthRequest, widthRequest);
            if (heightRequest > 0.0) return new Size(heightRequest, heightRequest);
            return new Size(100.0, 100.0);
        }

        float imageWidth = _image.Width / _density;
        float imageHeight = _image.Height / _density;

        if (widthRequest > 0.0)
        {
            double scale = widthRequest / imageWidth;
            return new Size(widthRequest, imageHeight * scale);
        }

        if (heightRequest > 0.0)
        {
            double scale = heightRequest / imageHeight;
            return new Size(imageWidth * scale, heightRequest);
        }

        if (availableSize.Width < double.MaxValue && availableSize.Height < double.MaxValue)
        {
            double scale = Math.Min(availableSize.Width / imageWidth, availableSize.Height / imageHeight);
            return new Size(imageWidth * scale, imageHeight * scale);
        }

        if (availableSize.Width < double.MaxValue)
        {
            double scale = availableSize.Width / imageWidth;
            return new Size(availableSize.Width, imageHeight * scale);
        }

        if (availableSize.Height < double.MaxValue)
        {
            double scale = availableSize.Height / imageHeight;
            return new Size(imageWidth * scale, availableSize.Height);
        }

        return new Size(imageWidth, imageHeight);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAnimation();
            _loadCts?.Cancel();

            // Only what this view owns: a result releases its own bitmap, and a cached
            // bitmap is shared with other views.
            if (_result != null)
                _result.Dispose();
            else if (!LinuxImageCache.IsShared(_bitmap))
                _bitmap?.Dispose();
            _result = null;
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
