// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// What a library bridge adds to a view it does not own, as a native platform lets a library
/// give a view an automation peer and a focus visual (CommunityToolkit's Expander layout): it
/// draws over the view's children, and can stand in for the view's accessible role, name,
/// description, states and actions. A member left at its default keeps the view's own.
/// </summary>
internal abstract class SkiaViewDecorator
{
    /// <summary>Drawn after the view's children, in the view's coordinates (<paramref name="bounds"/> are its bounds).</summary>
    public virtual void DrawOverlay(SkiaView view, SKCanvas canvas, SKRect bounds)
    {
    }

    public virtual AccessibleRole? Role => null;

    public virtual string? Name => null;

    public virtual string? Description => null;

    /// <summary>The view's accessible states, given the ones the view itself reports.</summary>
    public virtual AccessibleStates States(AccessibleStates states) => states;

    public virtual IReadOnlyList<AccessibleAction>? Actions => null;

    /// <summary>Performs an accessible action; null leaves it to the view.</summary>
    public virtual bool? DoAction(string actionName) => null;
}

public partial class SkiaView
{
    private SkiaViewDecorator? _decorator;

    /// <summary>A bridge's additions to this view (null: none).</summary>
    internal SkiaViewDecorator? Decorator
    {
        get => _decorator;
        set
        {
            if (ReferenceEquals(_decorator, value))
                return;
            _decorator = value;
            Invalidate();
        }
    }
}

/// <summary>
/// WinUI's system focus visual (high visibility): a 2-pixel outer ring and a 1-pixel inner ring
/// along a rectangle's edges, dark outside and light inside on a light theme, and the reverse on
/// a dark one. Drawn for keyboard focus only.
/// </summary>
internal static class SkiaFocusVisual
{
    public static void Draw(SKCanvas canvas, SKRect bounds)
    {
        if (bounds.Width < 6 || bounds.Height < 6)
            return;
        bool dark = SkiaTheme.IsDarkMode;
        using var outer = new SKPaint
        {
            Color = dark ? new SKColor(0xFF, 0xFF, 0xFF, 0xFF) : new SKColor(0x00, 0x00, 0x00, 0xE4),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
        };
        using var inner = new SKPaint
        {
            Color = dark ? new SKColor(0x00, 0x00, 0x00, 0xB3) : new SKColor(0xFF, 0xFF, 0xFF, 0xB3),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true,
        };
        var outerRect = bounds;
        outerRect.Inflate(-1f, -1f);
        canvas.DrawRect(outerRect, outer);
        var innerRect = bounds;
        innerRect.Inflate(-2.5f, -2.5f);
        canvas.DrawRect(innerRect, inner);
    }
}
