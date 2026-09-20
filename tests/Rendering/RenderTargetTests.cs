// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;

/// <summary>
/// Headless tests for the IRenderTarget seam: the engine's frame decisions
/// against a recording target, the raster target's presentation through the
/// window, and the factory's preference resolution. No display server, no EGL.
/// </summary>
public class RenderTargetTests
{
    #region Fakes

    private sealed class FakeDisplayWindow : IDisplayWindow
    {
        public int Width { get; set; } = 320;
        public int Height { get; set; } = 240;
        public bool IsRunning { get; private set; } = true;
        public int PresentCalls { get; private set; }
        public (int Width, int Height, int Stride) LastPresent { get; private set; }

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

        public void Show() => IsRunning = true;
        public void Hide() { }
        public void SetTitle(string title) { }
        public void Resize(int width, int height) { Width = width; Height = height; }
        public void SetCursor(CursorType cursorType) { }
        public void SetIcon(string iconPath) { }
        public void SetWMClass(string resName, string resClass) { }
        public void ProcessEvents() { }
        public void Stop() => IsRunning = false;
        public int GetFileDescriptor() => -1;
        public void Present(IntPtr pixels, int width, int height, int stride)
        {
            PresentCalls++;
            LastPresent = (width, height, stride);
        }
        public void FlushDeferredResize() { }
        public void AcknowledgeSync() { }
        public void Dispose() { }

        public void RaiseResized(int w, int h) { Width = w; Height = h; Resized?.Invoke(this, (w, h)); }
        public void RaiseExposed() => Exposed?.Invoke(this, EventArgs.Empty);

        private void Touch()
        {
            _ = KeyDown; _ = KeyUp; _ = TextInput; _ = PointerMoved; _ = PointerPressed;
            _ = PointerReleased; _ = Scroll; _ = CloseRequested; _ = FocusGained; _ = FocusLost;
        }
    }

    /// <summary>Records the engine's calls; draws into a bitmap so views can paint.</summary>
    private sealed class RecordingTarget : IRenderTarget
    {
        private SKBitmap _bitmap = new(1, 1);
        private SKCanvas _canvas;

        public RecordingTarget(bool preservesContents)
        {
            PreservesContents = preservesContents;
            _canvas = new SKCanvas(_bitmap);
        }

        public string Name => "recording";
        public bool IsGpuAccelerated => !PreservesContents;
        public bool PreservesContents { get; }
        public int Width { get; private set; } = 1;
        public int Height { get; private set; } = 1;
        public int BeginCalls { get; private set; }
        public int EndCalls { get; private set; }
        public List<(int, int)> Resizes { get; } = new();
        public bool Disposed { get; private set; }
        public List<SKRect> ClipsSeen { get; } = new();

        public void Resize(int width, int height)
        {
            Resizes.Add((width, height));
            Width = width; Height = height;
            _canvas.Dispose(); _bitmap.Dispose();
            _bitmap = new SKBitmap(Math.Max(1, width), Math.Max(1, height));
            _canvas = new SKCanvas(_bitmap);
        }

        public SKCanvas? BeginFrame() { BeginCalls++; return _canvas; }
        public void EndFrame() { EndCalls++; }
        public void Dispose() { Disposed = true; _canvas.Dispose(); _bitmap.Dispose(); }
    }

    /// <summary>A root view that records the clip it was drawn under.</summary>
    private sealed class ClipRecordingView : SkiaView
    {
        public List<SKRect> Clips { get; } = new();
        protected override void OnDraw(SKCanvas canvas, SKRect bounds)
        {
            Clips.Add(canvas.LocalClipBounds);
        }
    }

    private static SkiaView Root()
    {
        var v = new ClipRecordingView();
        v.Measure(new Microsoft.Maui.Graphics.Size(320, 240));
        v.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, 320, 240));
        return v;
    }

    #endregion

    #region Engine over a target

    [Fact]
    public void Engine_sizes_target_from_window_and_follows_resize()
    {
        var window = new FakeDisplayWindow { Width = 640, Height = 480 };
        var target = new RecordingTarget(preservesContents: true);
        using var engine = new SkiaRenderingEngine(window, target);

        target.Resizes.Should().ContainSingle().Which.Should().Be((640, 480));
        engine.Width.Should().Be(640);
        engine.Height.Should().Be(480);

        window.RaiseResized(800, 600);
        target.Resizes.Should().HaveCount(2);
        target.Resizes[1].Should().Be((800, 600));
        engine.Width.Should().Be(800);
    }

    [Fact]
    public void First_frame_is_full_and_presents_once()
    {
        var window = new FakeDisplayWindow();
        var target = new RecordingTarget(preservesContents: true);
        using var engine = new SkiaRenderingEngine(window, target);

        engine.Render(Root());

        target.BeginCalls.Should().Be(1);
        target.EndCalls.Should().Be(1);
    }

    [Fact]
    public void Nothing_dirty_means_no_frame_on_any_target()
    {
        foreach (var preserves in new[] { true, false })
        {
            var window = new FakeDisplayWindow();
            var target = new RecordingTarget(preserves);
            using var engine = new SkiaRenderingEngine(window, target);
            var root = Root();

            engine.Render(root);          // initial full frame
            engine.Render(root);          // nothing invalidated since
            engine.Render(root);

            target.BeginCalls.Should().Be(1, $"preservesContents={preserves}: an idle loop must not repaint");
            target.EndCalls.Should().Be(1);
        }
    }

    [Fact]
    public void Dirty_region_on_preserving_target_repaints_only_that_region()
    {
        var window = new FakeDisplayWindow();
        var target = new RecordingTarget(preservesContents: true);
        using var engine = new SkiaRenderingEngine(window, target);
        var root = (ClipRecordingView)Root();

        engine.Render(root);
        root.Clips.Clear();

        engine.InvalidateRegion(new SKRect(10, 10, 50, 40));
        engine.Render(root);

        target.EndCalls.Should().Be(2);
        root.Clips.Should().ContainSingle();
        root.Clips[0].Width.Should().BeLessThan(320, "a partial repaint clips to the dirty rect");
    }

    [Fact]
    public void Dirty_region_on_non_preserving_target_repaints_everything()
    {
        var window = new FakeDisplayWindow();
        var target = new RecordingTarget(preservesContents: false);
        using var engine = new SkiaRenderingEngine(window, target);
        var root = (ClipRecordingView)Root();

        engine.Render(root);
        root.Clips.Clear();

        engine.InvalidateRegion(new SKRect(10, 10, 50, 40));
        engine.Render(root);

        target.EndCalls.Should().Be(2);
        root.Clips.Should().ContainSingle();
        // LocalClipBounds is conservatively outset by a pixel for antialiasing.
        root.Clips[0].Width.Should().BeGreaterThanOrEqualTo(320, "a GPU swapchain does not keep the previous frame");
        root.Clips[0].Height.Should().BeGreaterThanOrEqualTo(240);
    }

    [Fact]
    public void Expose_and_resize_force_a_full_frame()
    {
        var window = new FakeDisplayWindow();
        var target = new RecordingTarget(preservesContents: true);
        using var engine = new SkiaRenderingEngine(window, target);
        var root = Root();

        engine.Render(root);
        window.RaiseExposed();
        engine.Render(root);
        target.EndCalls.Should().Be(2);

        window.RaiseResized(400, 300);
        engine.Render(root);
        target.EndCalls.Should().Be(3);
    }

    [Fact]
    public void Disposing_engine_disposes_target()
    {
        var window = new FakeDisplayWindow();
        var target = new RecordingTarget(preservesContents: true);
        var engine = new SkiaRenderingEngine(window, target);

        engine.Dispose();

        target.Disposed.Should().BeTrue();
    }

    #endregion

    #region Raster target

    [Fact]
    public void Raster_target_presents_through_window_with_bgra_stride()
    {
        var window = new FakeDisplayWindow { Width = 100, Height = 50 };
        using var target = new RasterRenderTarget(window);

        target.Name.Should().Be("raster");
        target.IsGpuAccelerated.Should().BeFalse();
        target.PreservesContents.Should().BeTrue();
        target.Width.Should().Be(100);

        var canvas = target.BeginFrame();
        canvas.Should().NotBeNull();
        canvas!.Clear(SKColors.Red);
        target.EndFrame();

        window.PresentCalls.Should().Be(1);
        window.LastPresent.Should().Be((100, 50, 400));
    }

    [Fact]
    public void Raster_target_resize_updates_presented_size()
    {
        var window = new FakeDisplayWindow { Width = 10, Height = 10 };
        using var target = new RasterRenderTarget(window);
        target.BeginFrame()!.Clear(SKColors.Blue);

        target.Resize(20, 20);
        target.Width.Should().Be(20);
        target.BeginFrame().Should().NotBeNull();
        target.EndFrame();

        window.LastPresent.Should().Be((20, 20, 80));
    }

    [Fact]
    public void Default_engine_constructor_uses_raster_target()
    {
        var window = new FakeDisplayWindow();
        using var engine = new SkiaRenderingEngine(window);

        engine.RenderTarget.Should().BeOfType<RasterRenderTarget>();
        engine.Render(Root());
        window.PresentCalls.Should().Be(1);
    }

    #endregion

    #region Factory

    [Theory]
    [InlineData("raster", RendererPreference.Auto, RendererPreference.Raster)]
    [InlineData("cpu", RendererPreference.Gpu, RendererPreference.Raster)]
    [InlineData("gpu", RendererPreference.Raster, RendererPreference.Gpu)]
    [InlineData("egl", RendererPreference.Auto, RendererPreference.Gpu)]
    [InlineData("auto", RendererPreference.Gpu, RendererPreference.Auto)]
    [InlineData("GPU", RendererPreference.Auto, RendererPreference.Gpu)]
    [InlineData("bogus", RendererPreference.Gpu, RendererPreference.Gpu)]
    [InlineData("", RendererPreference.Raster, RendererPreference.Raster)]
    public void Environment_variable_overrides_configured_preference(string env, RendererPreference configured, RendererPreference expected)
    {
        var previous = Environment.GetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable, env);
            RenderTargetFactory.ResolvePreference(configured).Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable, previous);
        }
    }

    [Fact]
    public void Factory_falls_back_to_raster_for_windows_without_native_surface()
    {
        var previous = Environment.GetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable, null);
            var window = new FakeDisplayWindow();

            using var auto = RenderTargetFactory.Create(window, RendererPreference.Auto);
            auto.Should().BeOfType<RasterRenderTarget>();

            // Even an explicit GPU request must produce a working target.
            using var gpu = RenderTargetFactory.Create(window, RendererPreference.Gpu);
            gpu.Should().BeOfType<RasterRenderTarget>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable, previous);
        }
    }

    #endregion
}
