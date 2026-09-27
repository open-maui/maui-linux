// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;

/// <summary>
/// Partial damage: a changed view repaints only the physical pixels it
/// covered and now covers (not its ancestors' bounds), GPU targets catch up
/// with the damage their buffer missed according to buffer age, and anything
/// that cannot be tracked falls back to a whole frame.
/// </summary>
[Collection("LinuxApplication.Current")]
public class PartialDamageTests
{
    private sealed class Window : IDisplayWindow
    {
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 300;
        public bool IsRunning => true;
#pragma warning disable CS0067
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
#pragma warning restore CS0067
        public void Show() { }
        public void Hide() { }
        public void SetTitle(string title) { }
        public void Resize(int width, int height) { Width = width; Height = height; }
        public void SetCursor(CursorType cursorType) { }
        public void SetIcon(string iconPath) { }
        public void SetWMClass(string resName, string resClass) { }
        public void ProcessEvents() { }
        public void Stop() { }
        public int GetFileDescriptor() => -1;
        public void Present(IntPtr pixels, int width, int height, int stride) { }
        public void FlushDeferredResize() { }
        public void AcknowledgeSync() { }
        public void Dispose() { }
    }

    /// <summary>GPU-like target: no preserved contents, reports a scripted buffer age.</summary>
    private sealed class AgedTarget : IRenderTarget, IDamageAwareRenderTarget
    {
        private SKBitmap _bitmap = new(1, 1);
        private SKCanvas _canvas;
        public AgedTarget() => _canvas = new SKCanvas(_bitmap);

        public int NextAge { get; set; } = 1;
        public IReadOnlyList<SKRectI>? SubmittedDamage { get; private set; }
        public bool SubmittedWhole { get; private set; }

        public string Name => "aged";
        public bool IsGpuAccelerated => true;
        public bool PreservesContents => false;
        public int Width { get; private set; } = 1;
        public int Height { get; private set; } = 1;
        public void Resize(int width, int height)
        {
            Width = width; Height = height;
            _canvas.Dispose(); _bitmap.Dispose();
            _bitmap = new SKBitmap(width, height);
            _canvas = new SKCanvas(_bitmap);
        }
        public int QueryBufferAge() => NextAge;
        public void SetFrameDamage(IReadOnlyList<SKRectI>? damage)
        {
            SubmittedDamage = damage;
            SubmittedWhole = damage == null;
        }
        public SKCanvas? BeginFrame() => _canvas;
        public void EndFrame() { }
        public void Dispose() { _canvas.Dispose(); _bitmap.Dispose(); }
    }

    private sealed class Container : SkiaView { }

    private static (SkiaRenderingEngine Engine, Container Root, SkiaBoxView Box, AgedTarget Target) Scene(float scale)
    {
        var target = new AgedTarget();
        var engine = new SkiaRenderingEngine(new Window { Width = (int)(400 * scale), Height = (int)(300 * scale) }, target) { DpiScale = scale };
        var root = new Container();
        var box = new SkiaBoxView { Color = Colors.Red };
        root.AddChild(box);
        root.RenderContext = engine;
        root.Arrange(new Rect(0, 0, 400, 300));
        box.Arrange(new Rect(100, 50, 40, 20));
        engine.InvalidateAll();
        engine.Render(root);
        return (engine, root, box, target);
    }

    private static SKRect Union(IEnumerable<SKRect> rects) =>
        rects.Aggregate(SKRect.Empty, (a, r) => a.IsEmpty ? r : SKRect.Union(a, r));

    [Fact]
    public void Invalidating_a_view_damages_only_its_physical_rect()
    {
        var (engine, root, box, target) = Scene(2f);
        target.NextAge = 1;

        box.Invalidate();
        engine.Render(root);

        engine.LastFrameWasFull.Should().BeFalse();
        var damage = Union(engine.LastFrameDamage!);
        // Logical (100,50,40,20) at 2x is (200,100,80,40), plus a few pixels of overflow margin.
        damage.Left.Should().BeInRange(185, 200);
        damage.Top.Should().BeInRange(85, 100);
        damage.Right.Should().BeInRange(280, 295);
        damage.Bottom.Should().BeInRange(140, 155);
        target.SubmittedDamage.Should().NotBeNull();
    }

    [Fact]
    public void Ancestors_do_not_add_their_own_bounds()
    {
        var (engine, root, box, _) = Scene(1f);

        box.Invalidate();
        engine.Render(root);

        var damage = Union(engine.LastFrameDamage!);
        (damage.Width * damage.Height).Should().BeLessThan(400 * 300 / 10, "the root's 400x300 bounds must not be part of the damage");
    }

    [Fact]
    public void Moving_a_view_damages_where_it_was_and_where_it_is()
    {
        var (engine, root, box, _) = Scene(1f);

        box.TranslationX = 200; // SkiaView properties invalidate on change
        box.Invalidate();
        engine.Render(root);

        engine.LastFrameWasFull.Should().BeFalse();
        var damage = engine.LastFrameDamage!;
        damage.Should().Contain(r => r.Left <= 100 && r.Right >= 140, "the old position is repainted");
        damage.Should().Contain(r => r.Left <= 300 && r.Right >= 340, "the new position is repainted");
    }

    [Fact]
    public void Buffer_age_two_also_repaints_the_previous_frames_damage()
    {
        var (engine, root, box, target) = Scene(1f);
        var other = new SkiaBoxView { Color = Colors.Blue };
        root.AddChild(other);
        other.Arrange(new Rect(300, 200, 30, 30));
        engine.InvalidateAll();
        engine.Render(root);

        target.NextAge = 1;
        other.Invalidate();
        engine.Render(root);

        target.NextAge = 2; // this buffer last saw the frame before 'other' changed
        box.Invalidate();
        engine.Render(root);

        engine.LastFrameWasFull.Should().BeFalse();
        var repaint = Union(engine.LastFrameRepaint);
        repaint.Contains(new SKRect(305, 205, 325, 225)).Should().BeTrue("the missed damage of 'other' is repainted");
        Union(engine.LastFrameDamage!).Left.Should().BeLessThan(150, "only this frame's change is reported to the compositor");
        Union(engine.LastFrameDamage!).Right.Should().BeLessThan(200);
    }

    [Fact]
    public void Unknown_buffer_age_repaints_whole()
    {
        var (engine, root, box, target) = Scene(1f);
        target.NextAge = 0;

        box.Invalidate();
        engine.Render(root);

        engine.LastFrameWasFull.Should().BeTrue();
        target.SubmittedWhole.Should().BeTrue();
    }

    [Fact]
    public void Age_older_than_the_history_repaints_whole()
    {
        var (engine, root, box, target) = Scene(1f);
        target.NextAge = 9;

        box.Invalidate();
        engine.Render(root);

        engine.LastFrameWasFull.Should().BeTrue();
    }

    [Fact]
    public void A_view_never_painted_forces_a_whole_frame()
    {
        var (engine, root, _, target) = Scene(1f);
        target.NextAge = 1;
        var fresh = new SkiaBoxView { Color = Colors.Green };
        fresh.RenderContext = engine; // attached but not in a painted tree

        fresh.Invalidate();
        engine.Render(root);

        engine.LastFrameWasFull.Should().BeTrue();
    }

    [Fact]
    public void Nothing_dirty_renders_nothing()
    {
        var (engine, root, _, target) = Scene(1f);
        target.NextAge = 1;
        var before = engine.LastFrameRepaint;

        engine.Render(root);

        engine.LastFrameRepaint.Should().BeSameAs(before);
    }

    [Fact]
    public void Disabling_partial_damage_restores_whole_gpu_frames()
    {
        var previous = SkiaRenderingEngine.EnablePartialDamage;
        try
        {
            SkiaRenderingEngine.EnablePartialDamage = false;
            var (engine, root, box, target) = Scene(1f);
            target.NextAge = 1;

            box.Invalidate();
            engine.Render(root);

            engine.LastFrameWasFull.Should().BeTrue();
        }
        finally
        {
            SkiaRenderingEngine.EnablePartialDamage = previous;
        }
    }
}
