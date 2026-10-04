// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Media;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// <c>IView.CaptureAsync()</c> and <c>IWindow.CaptureAsync()</c> (and so
/// <c>VisualElement.CaptureAsync()</c> and <c>Window.CaptureAsync()</c>) on Linux. MAUI built
/// for a platform it has no screenshot code for routes these through keyed DI hooks
/// (<c>"Microsoft.Maui.ViewCapture"</c>, <c>"Microsoft.Maui.WindowCapture"</c>) that receive
/// the handler's platform view; without them both return null. A view is captured by drawing
/// its Skia view, as laid out, into an off-screen surface at its window's scale; a window by
/// drawing what it shows (its page, modal pages and popups), as Screenshot.CaptureAsync does
/// for the app's main window.
/// </summary>
internal static class ViewCapture
{
    internal const string ViewCaptureKey = "Microsoft.Maui.ViewCapture";
    internal const string WindowCaptureKey = "Microsoft.Maui.WindowCapture";

    internal static void Register(IServiceCollection services)
    {
        services.AddKeyedSingleton<Func<object, Task<IScreenshotResult?>>>(ViewCaptureKey,
            (_, _) => platformView => Task.FromResult(CaptureView(platformView)));
        services.AddKeyedSingleton<Func<object, Task<IScreenshotResult?>>>(WindowCaptureKey,
            (_, _) => platformWindow => Task.FromResult(CaptureWindow(platformWindow)));
    }

    /// <summary>The view as drawn at its bounds, or null when it is not a laid-out Skia view.</summary>
    internal static IScreenshotResult? CaptureView(object platformView)
    {
        if (platformView is not SkiaView view)
            return null;
        try
        {
            var bounds = view.Bounds;
            if (!IsDrawable(bounds.Width, bounds.Height))
                return null;
            float scale = ScaleOf(ContextOf(view));
            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (surface == null)
                return null;
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            // Skia bounds are window coordinates: the view's top-left goes to the image's.
            canvas.Translate((float)-bounds.X, (float)-bounds.Y);
            view.Draw(canvas);
            canvas.Flush();
            return new SkiaCaptureResult(surface.Snapshot());
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("ViewCapture", "View capture failed", ex);
            return null;
        }
    }

    /// <summary>What the window shows (page, modal pages, popups), or null when it shows nothing.</summary>
    internal static IScreenshotResult? CaptureWindow(object platformWindow)
    {
        var context = LinuxApplication.Current?.WindowContexts
            .FirstOrDefault(c => ReferenceEquals(c.MauiWindow?.Handler?.PlatformView, platformWindow));
        if (context?.RootView is not { } root)
            return null;
        try
        {
            var size = context.LogicalSize;
            if (!IsDrawable(size.Width, size.Height))
                return null;
            float scale = ScaleOf(context);
            int width = Math.Max(1, (int)Math.Ceiling(size.Width * scale));
            int height = Math.Max(1, (int)Math.Ceiling(size.Height * scale));
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (surface == null)
                return null;
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.Scale(scale);
            root.Draw(canvas);
            foreach (var modal in context.ModalViews)
                modal.Draw(canvas);
            SkiaView.DrawPopupOverlays(canvas, root);
            canvas.Flush();
            return new SkiaCaptureResult(surface.Snapshot());
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("ViewCapture", "Window capture failed", ex);
            return null;
        }
    }

    static bool IsDrawable(double width, double height) =>
        width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height);

    static WindowContext? ContextOf(SkiaView view)
    {
        var root = view;
        while (root.Parent != null)
            root = root.Parent;
        var app = LinuxApplication.Current;
        if (app == null)
            return null;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, root) || context.ModalViews.Contains(root))
                return context;
        }
        return null;
    }

    static float ScaleOf(WindowContext? context)
    {
        float scale = context?.Scale ?? LinuxApplication.Current?.DpiScale ?? 1f;
        return scale > 0f && float.IsFinite(scale) ? scale : 1f;
    }

    /// <summary>A captured image, encoded on request (PNG or JPEG), as MAUI's platform results are.</summary>
    private sealed class SkiaCaptureResult : IScreenshotResult
    {
        private readonly SKImage _image;

        public SkiaCaptureResult(SKImage image) => _image = image;

        public int Width => _image.Width;

        public int Height => _image.Height;

        public Task<Stream> OpenReadAsync(ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100) =>
            Task.FromResult<Stream>(new MemoryStream(Encode(format, quality), writable: false));

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
