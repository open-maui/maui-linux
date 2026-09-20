// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Golden;

using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;

/// <summary>
/// Golden-screenshot harness: renders a SkiaView scene offscreen at a scale
/// factor and compares it pixel-for-pixel (with a small tolerance) against a
/// committed baseline PNG. No display server, no GPU: the same raster path
/// the platform uses as its fallback.
/// </summary>
/// <remarks>
/// Baselines live in tests/Golden/Baselines/&lt;scene&gt;@&lt;scale&gt;x.png and
/// are tied to the fonts fontconfig resolves on the machine that recorded them
/// (Noto Sans on the reference machine). Set OPENMAUI_UPDATE_GOLDENS=1 to
/// (re)record; a missing baseline is recorded and the test fails once so the
/// new file gets reviewed and committed. On a mismatch the actual, expected
/// and a diff image are written under the test output directory (GoldenOutput/).
/// </remarks>
public static class GoldenHarness
{
    public const string UpdateEnvironmentVariable = "OPENMAUI_UPDATE_GOLDENS";

    /// <summary>The scale factors every scene is verified at.</summary>
    public static readonly float[] Scales = { 1.0f, 1.25f, 1.5f, 1.75f, 2.0f };

    /// <summary>Per-channel difference below which two pixels count as equal (antialiasing noise).</summary>
    public const int ChannelTolerance = 12;

    /// <summary>Fraction of pixels allowed to differ beyond the tolerance.</summary>
    public const double MaxDifferentPixelFraction = 0.001;

    public static string BaselineDirectory { get; } = Path.Combine(FindRepoRoot(), "tests", "Golden", "Baselines");
    public static string OutputDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "GoldenOutput");

    private static bool UpdateRequested
    {
        get
        {
            var v = Environment.GetEnvironmentVariable(UpdateEnvironmentVariable);
            return !string.IsNullOrEmpty(v) && v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OpenMaui.Controls.Linux.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    /// <summary>
    /// Renders the scene through a real <see cref="SkiaRenderingEngine"/> over
    /// a bitmap render target, so layout at logical size, DpiScale, dirty-region
    /// handling and typeface resolution via the engine's resource cache all
    /// match production. The window is a fake sized to the physical pixels.
    /// </summary>
    public static SKBitmap Render(SkiaView root, int logicalWidth, int logicalHeight, float scale, SKColor? background = null)
    {
        int w = (int)Math.Round(logicalWidth * scale);
        int h = (int)Math.Round(logicalHeight * scale);

        var window = new HeadlessWindow(w, h);
        var target = new BitmapTarget(w, h, background ?? SKColors.White);
        using var engine = new SkiaRenderingEngine(window, target) { DpiScale = scale };
        root.RenderContext = engine;
        engine.Render(root);

        // The engine clears each region to transparent before drawing (view
        // backgrounds are expected to paint); composite over the scene
        // background so baselines are opaque and viewer-independent.
        using var frame = target.Detach();
        root.RenderContext = null;
        var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background ?? SKColors.White);
            canvas.DrawBitmap(frame, 0, 0);
        }
        return bitmap;
    }

    /// <summary>Render target that keeps the frame in a bitmap for comparison.</summary>
    private sealed class BitmapTarget : IRenderTarget
    {
        private SKBitmap? _bitmap;
        private SKCanvas? _canvas;
        private readonly SKColor _background;

        public BitmapTarget(int width, int height, SKColor background)
        {
            _background = background;
            Resize(width, height);
        }

        public string Name => "golden-bitmap";
        public bool IsGpuAccelerated => false;
        public bool PreservesContents => true;
        public int Width => _bitmap?.Width ?? 0;
        public int Height => _bitmap?.Height ?? 0;

        public void Resize(int width, int height)
        {
            _canvas?.Dispose();
            _bitmap?.Dispose();
            _bitmap = new SKBitmap(new SKImageInfo(Math.Max(1, width), Math.Max(1, height), SKColorType.Bgra8888, SKAlphaType.Premul));
            _canvas = new SKCanvas(_bitmap);
            _canvas.Clear(_background);
        }

        public SKCanvas? BeginFrame()
        {
            // The engine clears regions to transparent before drawing; lay the
            // scene background under it so the PNG is opaque and stable.
            _canvas!.Clear(_background);
            return _canvas;
        }

        public void EndFrame() => _canvas?.Flush();

        public SKBitmap Detach()
        {
            var b = _bitmap!;
            _bitmap = null;
            _canvas?.Dispose();
            _canvas = null;
            return b;
        }

        public void Dispose()
        {
            _canvas?.Dispose();
            _bitmap?.Dispose();
        }
    }

    /// <summary>Minimal IDisplayWindow: only size matters to the engine.</summary>
    private sealed class HeadlessWindow : Microsoft.Maui.Platform.Linux.Services.IDisplayWindow
    {
        public HeadlessWindow(int width, int height) { Width = width; Height = height; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsRunning => true;
        public event EventHandler<KeyEventArgs>? KeyDown;
        public event EventHandler<KeyEventArgs>? KeyUp;
        public event EventHandler<TextInputEventArgs>? TextInput;
        public event EventHandler<PointerEventArgs>? PointerMoved;
        public event EventHandler<PointerEventArgs>? PointerPressed;
        public event EventHandler<PointerEventArgs>? PointerReleased;
        public event EventHandler<ScrollEventArgs>? Scroll;
        public event EventHandler? Exposed;
        public event EventHandler<(int Width, int Height)>? Resized;
        public event EventHandler? CloseRequested;
        public event EventHandler? FocusGained;
        public event EventHandler? FocusLost;
        public void Show() { }
        public void Hide() { }
        public void SetTitle(string title) { }
        public void Resize(int width, int height) { Width = width; Height = height; }
        public void SetCursor(Microsoft.Maui.Platform.Linux.Window.CursorType cursorType) { }
        public void SetIcon(string iconPath) { }
        public void SetWMClass(string resName, string resClass) { }
        public void ProcessEvents() { }
        public void Stop() { }
        public int GetFileDescriptor() => -1;
        public void Present(IntPtr pixels, int width, int height, int stride) { }
        public void FlushDeferredResize() { }
        public void AcknowledgeSync() { }
        public void Dispose() { }
        private void Touch() { _ = KeyDown; _ = KeyUp; _ = TextInput; _ = PointerMoved; _ = PointerPressed; _ = PointerReleased; _ = Scroll; _ = Exposed; _ = Resized; _ = CloseRequested; _ = FocusGained; _ = FocusLost; }
    }

    /// <summary>Renders the scene and asserts it matches the committed baseline.</summary>
    public static void Verify(string scene, SkiaView root, int logicalWidth, int logicalHeight, float scale)
    {
        using var actual = Render(root, logicalWidth, logicalHeight, scale);
        Verify(scene, actual, scale);
    }

    public static void Verify(string scene, SKBitmap actual, float scale)
    {
        string name = $"{scene}@{scale:0.00}x";
        string baselinePath = Path.Combine(BaselineDirectory, name + ".png");

        if (UpdateRequested)
        {
            Save(actual, baselinePath);
            return;
        }

        if (!File.Exists(baselinePath))
        {
            Save(actual, baselinePath);
            Assert.Fail($"No baseline for {name}; recorded {baselinePath}. Review the image and commit it, then re-run.");
        }

        using var expected = SKBitmap.Decode(baselinePath);
        Assert.NotNull(expected);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            var mismatchDir = WriteFailureImages(name, actual, expected, null);
            Assert.Fail($"{name}: size {actual.Width}x{actual.Height} differs from baseline {expected.Width}x{expected.Height}. Images in {mismatchDir}");
        }

        var (differing, diff) = Compare(expected, actual);
        double fraction = (double)differing / (actual.Width * actual.Height);
        if (fraction > MaxDifferentPixelFraction)
        {
            var dir = WriteFailureImages(name, actual, expected, diff);
            diff.Dispose();
            Assert.Fail($"{name}: {differing} pixels ({fraction:P3}) differ beyond tolerance (limit {MaxDifferentPixelFraction:P2}). " +
                        $"Actual, expected and diff written to {dir}. If the change is intended, re-record with {UpdateEnvironmentVariable}=1.");
        }
        diff.Dispose();
    }

    /// <summary>Counts pixels differing beyond the channel tolerance; the diff image marks them red.</summary>
    public static (int Differing, SKBitmap Diff) Compare(SKBitmap expected, SKBitmap actual)
    {
        var diff = new SKBitmap(new SKImageInfo(actual.Width, actual.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        int differing = 0;
        unsafe
        {
            var e = (byte*)expected.GetPixels();
            var a = (byte*)actual.GetPixels();
            var d = (byte*)diff.GetPixels();
            long count = (long)actual.Width * actual.Height;
            for (long i = 0; i < count; i++)
            {
                long o = i * 4;
                bool same = Math.Abs(e[o] - a[o]) <= ChannelTolerance
                         && Math.Abs(e[o + 1] - a[o + 1]) <= ChannelTolerance
                         && Math.Abs(e[o + 2] - a[o + 2]) <= ChannelTolerance
                         && Math.Abs(e[o + 3] - a[o + 3]) <= ChannelTolerance;
                if (same)
                {
                    // Faded copy of the expected image so the red marks have context.
                    d[o] = (byte)(224 + e[o] / 8); d[o + 1] = (byte)(224 + e[o + 1] / 8); d[o + 2] = (byte)(224 + e[o + 2] / 8); d[o + 3] = 255;
                }
                else
                {
                    differing++;
                    d[o] = 0; d[o + 1] = 0; d[o + 2] = 255; d[o + 3] = 255; // BGRA red
                }
            }
        }
        return (differing, diff);
    }

    private static string WriteFailureImages(string name, SKBitmap actual, SKBitmap expected, SKBitmap? diff)
    {
        var dir = Path.Combine(OutputDirectory, name);
        Directory.CreateDirectory(dir);
        Save(actual, Path.Combine(dir, "actual.png"));
        Save(expected, Path.Combine(dir, "expected.png"));
        if (diff != null) Save(diff, Path.Combine(dir, "diff.png"));
        return dir;
    }

    private static void Save(SKBitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
