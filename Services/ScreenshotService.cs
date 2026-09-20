// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Media;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux screenshot. Renders the application's root view tree into an
/// off-screen Skia surface, which is what MAUI's Screenshot API captures on
/// every platform (the app window, not the whole desktop). Works without a
/// display server as long as a root view with a non-empty size exists.
/// </summary>
public class ScreenshotService : IScreenshot
{
    private readonly Func<SkiaView?> _rootViewProvider;
    private readonly Func<float> _scaleProvider;

    public ScreenshotService()
        : this(() => LinuxApplication.Current?.RootView,
               () => LinuxApplication.Current?.RenderingEngine?.DpiScale ?? 1f)
    {
    }

    internal ScreenshotService(Func<SkiaView?> rootViewProvider, Func<float>? scaleProvider = null)
    {
        _rootViewProvider = rootViewProvider;
        _scaleProvider = scaleProvider ?? (() => 1f);
    }

    public bool IsCaptureSupported => TryGetSurfaceSize(out _, out _, out _);

    public Task<IScreenshotResult?> CaptureAsync()
    {
        try
        {
            if (!TryGetSurfaceSize(out var root, out var width, out var height))
                return Task.FromResult<IScreenshotResult?>(null);

            var scale = _scaleProvider();
            if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale))
                scale = 1f;

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

            using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (surface == null)
                return Task.FromResult<IScreenshotResult?>(null);

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            if (scale != 1f)
                canvas.Scale(scale);
            root!.Draw(canvas);
            canvas.Flush();

            return Task.FromResult<IScreenshotResult?>(new SkiaScreenshotResult(surface.Snapshot()));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("ScreenshotService", "Capture failed", ex);
            return Task.FromResult<IScreenshotResult?>(null);
        }
    }

    private bool TryGetSurfaceSize(out SkiaView? root, out double width, out double height)
    {
        root = null;
        width = height = 0;
        try
        {
            root = _rootViewProvider();
        }
        catch
        {
            return false;
        }

        if (root == null)
            return false;

        var bounds = root.Bounds;
        width = bounds.Width;
        height = bounds.Height;
        return width > 0 && height > 0 && !double.IsNaN(width) && !double.IsNaN(height)
            && !double.IsInfinity(width) && !double.IsInfinity(height);
    }

    private sealed class SkiaScreenshotResult : IScreenshotResult
    {
        private readonly SKImage _image;

        public SkiaScreenshotResult(SKImage image) => _image = image;

        public int Width => _image.Width;
        public int Height => _image.Height;

        public Task<Stream> OpenReadAsync(ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100)
            => Task.FromResult<Stream>(new MemoryStream(Encode(format, quality), writable: false));

        public async Task CopyToAsync(Stream destination, ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100)
        {
            var bytes = Encode(format, quality);
            await destination.WriteAsync(bytes, 0, bytes.Length);
        }

        private byte[] Encode(ScreenshotFormat format, int quality)
        {
            var skFormat = format == ScreenshotFormat.Jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
            using var data = _image.Encode(skFormat, Math.Clamp(quality, 0, 100));
            return data.ToArray();
        }
    }
}
