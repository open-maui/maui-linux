// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Syncfusion.Maui.Core;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Platform view for Syncfusion's <see cref="IDrawableLayout"/> controls
/// (every <c>SfView</c>): Syncfusion's own layout code measures and arranges
/// the children, and its own <c>OnDraw</c> paints below or above them as its
/// <see cref="DrawingOrder"/> asks.
/// </summary>
/// <remarks>
/// Syncfusion reports child changes and redraws only to its own handler type,
/// whose platform-neutral build throws, so this view is told nothing: it
/// re-syncs its children on each layout pass and on ChildAdded/ChildRemoved,
/// and <see cref="SfInvalidation"/> repaints it after input, property changes
/// and animation ticks.
/// </remarks>
public class SkiaSfLayout : SkiaCrossPlatformLayout
{
    internal IMauiContext? MauiContext { get; set; }

    private IDrawableLayout? Drawable => MauiView as IDrawableLayout;

    protected override Size MeasureOverride(Size availableSize)
    {
        SkiaTextMeasurer.EnsureInstalled();
        SfPlatformShims.BeforeMeasure(MauiView, availableSize.Width, availableSize.Height);
        SyncChildren();
        var size = base.MeasureOverride(availableSize);
        // Syncfusion adds item views while measuring (a list realising rows).
        SyncChildren();
        return size;
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        SyncChildren();
        var result = base.ArrangeOverride(bounds);
        SyncChildren();
        return result;
    }

    /// <summary>
    /// Mirrors the SfView's children into this view, creating handlers for new
    /// ones. Cheap when nothing changed: an identity walk of both lists.
    /// </summary>
    internal void SyncChildren()
    {
        if (Drawable is not { } layout || MauiContext is not { } context)
            return;

        var desired = new List<SkiaView>(layout.Count);
        foreach (var child in layout.ToArray())
        {
            if (child == null)
                continue;
            try
            {
                if (child.Handler == null)
                    child.Handler = child.ToViewHandler(context);
                if (child.Handler?.PlatformView is SkiaView skia)
                    desired.Add(skia);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Syncfusion", $"Skipping {child.GetType().Name} in {layout.GetType().Name}", ex);
            }
        }

        var current = Children;
        if (current.Count == desired.Count)
        {
            bool same = true;
            for (int i = 0; i < desired.Count && same; i++)
                same = ReferenceEquals(current[i], desired[i]);
            if (same)
                return;
        }

        foreach (var stale in current.ToArray())
            if (!desired.Contains(stale))
                RemoveChild(stale);
        for (int i = 0; i < desired.Count; i++)
        {
            var view = desired[i];
            int at = IndexOfChild(view);
            if (at == i)
                continue;
            if (at >= 0)
                RemoveChild(view);
            if (i < Children.Count)
                InsertChild(i, view);
            else
                AddChild(view);
        }
        // New children need a place: a layout pass, not only a repaint.
        InvalidateMeasure();
    }

    private int IndexOfChild(SkiaView view)
    {
        var children = Children;
        for (int i = 0; i < children.Count; i++)
            if (ReferenceEquals(children[i], view))
                return i;
        return -1;
    }

    /// <summary>
    /// A hit on this view's own surface (no child under the pointer) counts
    /// only when the control takes input or paints a background, as native
    /// hit-testing decides on the other platforms: an overlay that only draws
    /// (SfTabView's TabBarBorder, over the whole tab strip) lets clicks through
    /// to the views beneath it.
    /// </summary>
    public override SkiaView? HitTest(float x, float y)
    {
        if (ClipRectOf(MauiView) is { } clip && !clip.Contains(x, y))
            return null; // the clipped-away part takes no input either
        var hit = base.HitTest(x, y);
        return ReferenceEquals(hit, this) && !SfHitTesting.TakesOwnHits(MauiView) ? null : hit;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo?> s_clipRectProperties = new();

    /// <summary>
    /// A Syncfusion view's own <c>ClipRect</c> (SfListView's rows), in window
    /// coordinates, or null when it has none. The native builds apply it to the
    /// platform view (ApplyPlatformViewClip), which the platform-neutral build
    /// leaves empty: with sticky group headers each row is clipped where the
    /// header covers it, so a translucent header shades the list background
    /// rather than showing the rows scrolled beneath it.
    /// </summary>
    private SKRect? ClipRectOf(View? view)
    {
        if (view == null)
            return null;
        var property = s_clipRectProperties.GetOrAdd(view.GetType(), type =>
            type.GetProperty("ClipRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) is { } p
                && p.PropertyType == typeof(Rect) ? p : null);
        if (property?.GetValue(view) is not Rect clip || clip == Rect.Zero)
            return null;
        return new SKRect((float)(Bounds.Left + clip.X), (float)(Bounds.Top + clip.Y),
                          (float)(Bounds.Left + clip.Right), (float)(Bounds.Top + clip.Bottom));
    }

    public override void Draw(SKCanvas canvas)
    {
        if (ClipRectOf(MauiView) is not { } clip)
        {
            base.Draw(canvas);
            return;
        }
        canvas.Save();
        canvas.ClipRect(clip);
        base.Draw(canvas);
        canvas.Restore();
    }

    /// <summary>True when the control paints anything itself.</summary>
    internal bool Draws => Drawable is { DrawingOrder: not DrawingOrder.NoDraw };

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Syncfusion adds children after the first layout (a combo box's entry
        // and buttons) without telling the layout; the renderer only lays out
        // what asked to be, so the children are picked up here, and a change
        // asks for the layout they need.
        SyncChildren();
        var order = Drawable?.DrawingOrder ?? DrawingOrder.NoDraw;
        if (order == DrawingOrder.NoDraw)
        {
            base.OnDraw(canvas, bounds);
            return;
        }

        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var paint = new SKPaint { Color = GetEffectiveBackgroundColor(), Style = SKPaintStyle.Fill };
            canvas.DrawRect(bounds, paint);
        }

        // SfView.ClipToBounds (and IsClippedToBounds) keeps the children
        // inside: SfTabView slides its pages in a clipped strip.
        bool clip = ClipToBounds || (MauiView as SfView)?.ClipToBounds == true;
        if (clip)
        {
            canvas.Save();
            canvas.ClipRect(bounds);
        }
        if (order == DrawingOrder.BelowContent)
            DrawControl(canvas, bounds);
        foreach (var child in ChildrenInZOrder())
            if (child.IsVisible)
                child.Draw(canvas);
        if (order is DrawingOrder.AboveContent or DrawingOrder.AboveContentWithTouch)
            DrawControl(canvas, bounds);
        if (clip)
            canvas.Restore();
    }

    private void DrawControl(SKCanvas canvas, SKRect bounds)
    {
        if (Drawable is not { } drawable)
            return;
        SfDrawing.Draw(drawable, canvas, bounds, (MauiView as SfView)?.ClipToBounds ?? true);
    }
}

/// <summary>Native-style hit-testing rules for Syncfusion views.</summary>
internal static class SfHitTesting
{
    /// <summary>
    /// True when a hit on the view's own surface should stop there: it has a
    /// Syncfusion touch or gesture detector, a MAUI gesture recognizer, or a
    /// visible background.
    /// </summary>
    public static bool TakesOwnHits(Microsoft.Maui.Controls.View? view)
    {
        if (view == null)
            return false;
        if (SfInternals.TouchDetectorOf(view) != null || SfInternals.GestureDetectorOf(view) != null)
            return true;
        if (view.GestureRecognizers.Count > 0)
            return true;
        if (view.Background is Microsoft.Maui.Controls.SolidColorBrush { Color: { } brush } && brush.Alpha > 0)
            return true;
        if (view.Background is not null and not Microsoft.Maui.Controls.SolidColorBrush)
            return true;
        return view.BackgroundColor is { } color && color.Alpha > 0;
    }
}

/// <summary>Runs a Syncfusion <c>IDrawable</c> in the view's own coordinates.</summary>
internal static class SfDrawing
{
    private static readonly HashSet<Type> s_reportedTextStub = new();
    private static readonly HashSet<Type> s_reportedFailure = new();

    [ThreadStatic]
    private static SkiaCanvas? t_canvas;

    public static void Draw(IDrawable drawable, SKCanvas canvas, SKRect bounds, bool clip)
    {
        // Restored to this depth whatever the control's drawing did: a
        // SaveState without its RestoreState (or a stub throwing between the
        // two) left the control's translation and clip on the canvas, so its
        // children (a combo box's text field and buttons) drew shifted by the
        // control's position, or clipped away entirely.
        int depth = canvas.Save();
        try
        {
            if (clip)
                canvas.ClipRect(bounds);
            canvas.Translate(bounds.Left, bounds.Top);
            // One MAUI canvas per thread, pointed at the frame's canvas: building
            // a SkiaCanvas sets up its default paints and fonts, and a list or
            // chart draws many views a frame.
            var mauiCanvas = t_canvas ??= new SkiaCanvas();
            mauiCanvas.Canvas = canvas;
            mauiCanvas.ResetState();
            drawable.Draw(mauiCanvas, new RectF(0, 0, bounds.Width, bounds.Height));
        }
        catch (NotImplementedException ex)
        {
            // A platform-neutral Syncfusion stub not yet supplied by the bridge.
            if (s_reportedTextStub.Add(drawable.GetType()))
                DiagnosticLog.Warn("Syncfusion",
                    $"{drawable.GetType().Name} reached a Syncfusion stub the Linux bridge does not supply yet: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
        }
        catch (Exception ex)
        {
            if (s_reportedFailure.Add(drawable.GetType()))
                DiagnosticLog.Error("Syncfusion", $"{drawable.GetType().Name} draw failed", ex);
        }
        finally
        {
            canvas.RestoreToCount(depth);
        }
    }
}
