// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered image control with SVG support and GIF animation.
/// Full MAUI-compliant implementation.
/// </summary>
public class SkiaImage : SkiaView
{
    #region Image Cache

    /// <summary>
    /// Static image cache for decoded bitmaps to avoid re-decoding.
    /// Key is the file path or URI, value is the cached bitmap data.
    /// </summary>
    private static readonly ConcurrentDictionary<string, CachedImage> _imageCache = new();
    private static readonly object _cacheLock = new();
    private const int MaxCacheSize = 50; // Maximum number of cached images
    private const long MaxCacheMemoryBytes = 100 * 1024 * 1024; // 100MB max cache

    private class CachedImage
    {
        public SKBitmap? Bitmap { get; set; }
        public List<AnimationFrame>? Frames { get; set; }
        public bool IsAnimated { get; set; }
        public DateTime LastAccessed { get; set; }
        public long MemorySize { get; set; }
    }

    /// <summary>
    /// Clears the image cache.
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var cached in _imageCache.Values)
            {
                cached.Bitmap?.Dispose();
                if (cached.Frames != null)
                {
                    foreach (var frame in cached.Frames)
                    {
                        frame.Bitmap?.Dispose();
                    }
                }
            }
            _imageCache.Clear();
        }
    }

    private static void TrimCacheIfNeeded()
    {
        lock (_cacheLock)
        {
            if (_imageCache.Count <= MaxCacheSize)
                return;

            // Calculate total memory
            long totalMemory = 0;
            foreach (var cached in _imageCache.Values)
            {
                totalMemory += cached.MemorySize;
            }

            // If under memory limit and count limit, don't trim
            if (totalMemory < MaxCacheMemoryBytes && _imageCache.Count <= MaxCacheSize)
                return;

            // Remove oldest entries until under limits
            var sortedEntries = _imageCache.ToArray();
            Array.Sort(sortedEntries, (a, b) => a.Value.LastAccessed.CompareTo(b.Value.LastAccessed));

            int removeCount = Math.Max(1, _imageCache.Count - MaxCacheSize + 10);
            for (int i = 0; i < removeCount && i < sortedEntries.Length; i++)
            {
                if (_imageCache.TryRemove(sortedEntries[i].Key, out var removed))
                {
                    removed.Bitmap?.Dispose();
                    if (removed.Frames != null)
                    {
                        foreach (var frame in removed.Frames)
                        {
                            frame.Bitmap?.Dispose();
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Animation Support

    private class AnimationFrame
    {
        public SKBitmap? Bitmap { get; set; }
        public int Duration { get; set; } // Duration in milliseconds
    }

    private List<AnimationFrame>? _animationFrames;
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
    private bool _isLoading;
    private string? _currentFilePath;
    private string? _cacheKey;
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
            // Don't dispose if this is a cached bitmap
            if (_bitmap != null && (_cacheKey == null || !_imageCache.ContainsKey(_cacheKey)))
            {
                _bitmap.Dispose();
            }
            _bitmap = value;
            _image?.Dispose();
            _image = value != null ? SKImage.FromBitmap(value) : null;
            Invalidate();
        }
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
        if (_animationFrames == null || _animationFrames.Count <= 1 || !IsAnimationPlaying)
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
                    break;
                }
            }

            if (foundPath == null)
            {
                _isLoading = false;
                _isSvg = false;
                _currentFilePath = null;
                _cacheKey = null;
                ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(new FileNotFoundException(filePath)));
                return;
            }

            _isSvg = foundPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            _currentFilePath = foundPath;
            _cacheKey = foundPath;

            // Check cache first
            if (_imageCache.TryGetValue(foundPath, out var cached))
            {
                cached.LastAccessed = DateTime.UtcNow;
                if (cached.IsAnimated && cached.Frames != null)
                {
                    _isAnimatedImage = true;
                    _animationFrames = cached.Frames;
                    _currentFrameIndex = 0;
                    if (cached.Frames.Count > 0 && cached.Frames[0].Bitmap != null)
                    {
                        _image?.Dispose();
                        _image = SKImage.FromBitmap(cached.Frames[0].Bitmap);
                    }
                    if (IsAnimationPlaying)
                    {
                        StartAnimation();
                    }
                }
                else if (cached.Bitmap != null)
                {
                    _isAnimatedImage = false;
                    _bitmap = cached.Bitmap;
                    _image?.Dispose();
                    _image = SKImage.FromBitmap(cached.Bitmap);
                }
                _isLoading = false;
                ImageLoaded?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (_isSvg)
            {
                await LoadSvgAtSizeAsync(foundPath, WidthRequest, HeightRequest);
            }
            else
            {
                await LoadImageWithAnimationSupportAsync(foundPath);
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

    private async Task LoadImageWithAnimationSupportAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null)
            {
                // Fallback to simple decode
                stream.Position = 0;
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    CacheAndSetBitmap(filePath, bitmap, false);
                }
                return;
            }

            int frameCount = codec.FrameCount;

            if (frameCount > 1)
            {
                // Animated image (GIF)
                _isAnimatedImage = true;
                _animationFrames = new List<AnimationFrame>();
                var info = codec.Info;

                for (int i = 0; i < frameCount; i++)
                {
                    var frameInfo = codec.FrameInfo[i];
                    var bitmap = new SKBitmap(info.Width, info.Height);

                    var options = new SKCodecOptions(i);
                    codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);

                    _animationFrames.Add(new AnimationFrame
                    {
                        Bitmap = bitmap,
                        Duration = frameInfo.Duration > 0 ? frameInfo.Duration : 100
                    });
                }

                // Cache the animation frames
                long memorySize = _animationFrames.Sum(f => (long)(f.Bitmap?.ByteCount ?? 0));
                _imageCache[filePath] = new CachedImage
                {
                    Frames = _animationFrames,
                    IsAnimated = true,
                    LastAccessed = DateTime.UtcNow,
                    MemorySize = memorySize
                };
                TrimCacheIfNeeded();

                // Set first frame as current image
                _currentFrameIndex = 0;
                if (_animationFrames.Count > 0 && _animationFrames[0].Bitmap != null)
                {
                    _image?.Dispose();
                    _image = SKImage.FromBitmap(_animationFrames[0].Bitmap);
                }

                // Start animation if requested
                if (IsAnimationPlaying)
                {
                    StartAnimation();
                }
            }
            else
            {
                // Static image
                _isAnimatedImage = false;
                var bitmap = SKBitmap.Decode(codec, codec.Info);
                if (bitmap != null)
                {
                    CacheAndSetBitmap(filePath, bitmap, false);
                }
            }
        });
    }

    private void CacheAndSetBitmap(string cacheKey, SKBitmap bitmap, bool isAnimated)
    {
        _imageCache[cacheKey] = new CachedImage
        {
            Bitmap = bitmap,
            IsAnimated = isAnimated,
            LastAccessed = DateTime.UtcNow,
            MemorySize = bitmap.ByteCount
        };
        TrimCacheIfNeeded();

        _bitmap = bitmap;
        _image?.Dispose();
        _image = SKImage.FromBitmap(bitmap);
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
                }
            }, cts.Token);

            if (!cts.Token.IsCancellationRequested && newBitmap != null)
            {
                _svgLoadedWidth = (targetWidth > 0.0) ? targetWidth : newBitmap.Width;
                _svgLoadedHeight = (targetHeight > 0.0) ? targetHeight : newBitmap.Height;
                _isAnimatedImage = false;
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
        _cacheKey = null; // Streams are not cached by default
        Invalidate();

        try
        {
            await Task.Run(() =>
            {
                using var codec = SKCodec.Create(stream);

                if (codec == null)
                {
                    stream.Position = 0;
                    var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        _isAnimatedImage = false;
                        Bitmap = bitmap;
                    }
                    return;
                }

                int frameCount = codec.FrameCount;

                if (frameCount > 1)
                {
                    // Animated image
                    _isAnimatedImage = true;
                    _animationFrames = new List<AnimationFrame>();
                    var info = codec.Info;

                    for (int i = 0; i < frameCount; i++)
                    {
                        var frameInfo = codec.FrameInfo[i];
                        var bitmap = new SKBitmap(info.Width, info.Height);

                        var options = new SKCodecOptions(i);
                        codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);

                        _animationFrames.Add(new AnimationFrame
                        {
                            Bitmap = bitmap,
                            Duration = frameInfo.Duration > 0 ? frameInfo.Duration : 100
                        });
                    }

                    _currentFrameIndex = 0;
                    if (_animationFrames.Count > 0 && _animationFrames[0].Bitmap != null)
                    {
                        _image?.Dispose();
                        _image = SKImage.FromBitmap(_animationFrames[0].Bitmap);
                    }

                    if (IsAnimationPlaying)
                    {
                        StartAnimation();
                    }
                }
                else
                {
                    _isAnimatedImage = false;
                    var bitmap = SKBitmap.Decode(codec, codec.Info);
                    if (bitmap != null)
                    {
                        Bitmap = bitmap;
                    }
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
        _cacheKey = uri.ToString();
        Invalidate();

        try
        {
            // Check cache first
            if (_imageCache.TryGetValue(_cacheKey, out var cached))
            {
                cached.LastAccessed = DateTime.UtcNow;
                if (cached.IsAnimated && cached.Frames != null)
                {
                    _isAnimatedImage = true;
                    _animationFrames = cached.Frames;
                    _currentFrameIndex = 0;
                    if (cached.Frames.Count > 0 && cached.Frames[0].Bitmap != null)
                    {
                        _image?.Dispose();
                        _image = SKImage.FromBitmap(cached.Frames[0].Bitmap);
                    }
                    if (IsAnimationPlaying)
                    {
                        StartAnimation();
                    }
                }
                else if (cached.Bitmap != null)
                {
                    _isAnimatedImage = false;
                    _bitmap = cached.Bitmap;
                    _image?.Dispose();
                    _image = SKImage.FromBitmap(cached.Bitmap);
                }
                _isLoading = false;
                ImageLoaded?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            using HttpClient httpClient = new HttpClient();
            var data = await httpClient.GetByteArrayAsync(uri);
            using var stream = new MemoryStream(data);

            await Task.Run(() =>
            {
                using var codec = SKCodec.Create(stream);

                if (codec == null)
                {
                    stream.Position = 0;
                    var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        _isAnimatedImage = false;
                        CacheAndSetBitmap(_cacheKey, bitmap, false);
                    }
                    return;
                }

                int frameCount = codec.FrameCount;

                if (frameCount > 1)
                {
                    // Animated image
                    _isAnimatedImage = true;
                    _animationFrames = new List<AnimationFrame>();
                    var info = codec.Info;

                    for (int i = 0; i < frameCount; i++)
                    {
                        var frameInfo = codec.FrameInfo[i];
                        var bitmap = new SKBitmap(info.Width, info.Height);

                        var options = new SKCodecOptions(i);
                        codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);

                        _animationFrames.Add(new AnimationFrame
                        {
                            Bitmap = bitmap,
                            Duration = frameInfo.Duration > 0 ? frameInfo.Duration : 100
                        });
                    }

                    // Cache the animation frames
                    long memorySize = _animationFrames.Sum(f => (long)(f.Bitmap?.ByteCount ?? 0));
                    _imageCache[_cacheKey] = new CachedImage
                    {
                        Frames = _animationFrames,
                        IsAnimated = true,
                        LastAccessed = DateTime.UtcNow,
                        MemorySize = memorySize
                    };
                    TrimCacheIfNeeded();

                    _currentFrameIndex = 0;
                    if (_animationFrames.Count > 0 && _animationFrames[0].Bitmap != null)
                    {
                        _image?.Dispose();
                        _image = SKImage.FromBitmap(_animationFrames[0].Bitmap);
                    }

                    if (IsAnimationPlaying)
                    {
                        StartAnimation();
                    }
                }
                else
                {
                    _isAnimatedImage = false;
                    var bitmap = SKBitmap.Decode(codec, codec.Info);
                    if (bitmap != null)
                    {
                        CacheAndSetBitmap(_cacheKey, bitmap, false);
                    }
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

    public void LoadFromData(byte[] data)
    {
        try
        {
            _cacheKey = null;
            using var stream = new MemoryStream(data);
            using var codec = SKCodec.Create(stream);

            if (codec == null)
            {
                stream.Position = 0;
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    _isAnimatedImage = false;
                    Bitmap = bitmap;
                }
                ImageLoaded?.Invoke(this, EventArgs.Empty);
                return;
            }

            int frameCount = codec.FrameCount;

            if (frameCount > 1)
            {
                // Animated image
                _isAnimatedImage = true;
                _animationFrames = new List<AnimationFrame>();
                var info = codec.Info;

                for (int i = 0; i < frameCount; i++)
                {
                    var frameInfo = codec.FrameInfo[i];
                    var bitmap = new SKBitmap(info.Width, info.Height);

                    var options = new SKCodecOptions(i);
                    codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);

                    _animationFrames.Add(new AnimationFrame
                    {
                        Bitmap = bitmap,
                        Duration = frameInfo.Duration > 0 ? frameInfo.Duration : 100
                    });
                }

                _currentFrameIndex = 0;
                if (_animationFrames.Count > 0 && _animationFrames[0].Bitmap != null)
                {
                    _image?.Dispose();
                    _image = SKImage.FromBitmap(_animationFrames[0].Bitmap);
                }

                if (IsAnimationPlaying)
                {
                    StartAnimation();
                }
            }
            else
            {
                _isAnimatedImage = false;
                var bitmap = SKBitmap.Decode(codec, codec.Info);
                if (bitmap != null)
                {
                    Bitmap = bitmap;
                }
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
            _cacheKey = null;
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
                    _ = LoadSvgAtSizeAsync(_currentFilePath, width, height);
                }
            }
        }
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        var desiredWidth = DesiredSize.Width;
        var desiredHeight = DesiredSize.Height;

        if (desiredWidth > 0 && desiredHeight > 0 &&
            (desiredWidth < bounds.Width || desiredHeight < bounds.Height))
        {
            float finalWidth = Math.Min(desiredWidth, bounds.Width);
            float finalHeight = Math.Min(desiredHeight, bounds.Height);

            float x = bounds.Left;
            var hAlignValue = (int)HorizontalOptions.Alignment;
            if (hAlignValue == 1) x = bounds.Left + (bounds.Width - finalWidth) / 2;
            else if (hAlignValue == 2) x = bounds.Right - finalWidth;

            float y = bounds.Top;
            var vAlignValue = (int)VerticalOptions.Alignment;
            if (vAlignValue == 1) y = bounds.Top + (bounds.Height - finalHeight) / 2;
            else if (vAlignValue == 2) y = bounds.Bottom - finalHeight;

            return new SKRect(x, y, x + finalWidth, y + finalHeight);
        }

        return bounds;
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        double widthRequest = base.WidthRequest;
        double heightRequest = base.HeightRequest;

        if (widthRequest > 0.0 && heightRequest > 0.0)
            return new SKSize((float)widthRequest, (float)heightRequest);

        if (_image == null)
        {
            if (widthRequest > 0.0) return new SKSize((float)widthRequest, (float)widthRequest);
            if (heightRequest > 0.0) return new SKSize((float)heightRequest, (float)heightRequest);
            return new SKSize(100f, 100f);
        }

        float imageWidth = _image.Width;
        float imageHeight = _image.Height;

        if (widthRequest > 0.0)
        {
            float scale = (float)widthRequest / imageWidth;
            return new SKSize((float)widthRequest, imageHeight * scale);
        }

        if (heightRequest > 0.0)
        {
            float scale = (float)heightRequest / imageHeight;
            return new SKSize(imageWidth * scale, (float)heightRequest);
        }

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
            StopAnimation();

            // Only dispose if not cached
            if (_cacheKey == null || !_imageCache.ContainsKey(_cacheKey))
            {
                _bitmap?.Dispose();
                if (_animationFrames != null)
                {
                    foreach (var frame in _animationFrames)
                    {
                        frame.Bitmap?.Dispose();
                    }
                }
            }
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
