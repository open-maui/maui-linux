// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;
using Syncfusion.Maui.Graphics.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Platform view for Syncfusion's childless <see cref="IDrawableView"/>
/// controls: the control's own <c>Draw</c> paints the whole view.
/// </summary>
public class SkiaSfDrawable : SkiaView
{
    protected override Size MeasureOverride(Size availableSize)
    {
        // Explicit and min/max sizes are applied by the handler; otherwise
        // take the space offered, like GraphicsView.
        double w = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        double h = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        return new Size(w, h);
    }

    /// <inheritdoc cref="SkiaSfLayout.HitTest"/>
    public override SkiaView? HitTest(float x, float y)
    {
        var hit = base.HitTest(x, y);
        return ReferenceEquals(hit, this) && !SfHitTesting.TakesOwnHits(MauiView) ? null : hit;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (MauiView is IDrawableView drawable)
            SfDrawing.Draw(drawable, canvas, bounds, clip: true);
    }
}
