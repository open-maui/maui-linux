// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// MAUI's <c>ToolTipProperties.Text</c> for one window. When the pointer rests
/// on an element with a tooltip (or on anything inside it, as WinUI's
/// ToolTipService and AppKit show them) for <see cref="ShowDelay"/>, the text
/// is drawn as a small card under the pointer, over everything else in the
/// window. It hides when the pointer leaves the element, on a press or a
/// scroll, and when the window loses focus; after a press it stays hidden
/// until the pointer moves to another element. The card takes no input.
/// </summary>
internal sealed class ToolTipController
{
    internal static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(500);

    private const float FontSize = 12f;
    private const float PaddingX = 8f;
    private const float PaddingY = 5f;
    private const float MaxTextWidth = 320f;
    private const float PointerOffsetY = 20f;
    private const float EdgeMargin = 4f;

    private readonly Action _invalidate;
    private VisualElement? _target;
    // A view that is not a Controls element (a library's core view) with a tooltip.
    private SkiaView? _coreTarget;
    private float _pointerX, _pointerY;
    private int _generation;
    private bool _suppressed;

    internal ToolTipController(Action invalidate) => _invalidate = invalidate;

    /// <summary>
    /// Runs the show after <see cref="ShowDelay"/> (replaceable, for tests). The element is null
    /// for a view that is not a Controls element.
    /// </summary>
    internal Action<VisualElement?, Action> Schedule { get; set; } =
        (target, show) => (target?.Dispatcher ?? Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread())?.DispatchDelayed(ShowDelay, show);

    /// <summary>The text shown, or null when no tooltip is up.</summary>
    internal string? ShownText { get; private set; }

    /// <summary>Where the shown card's top-left sits (logical, window coordinates before clamping).</summary>
    internal SKPoint ShownAt { get; private set; }

    /// <summary>The element whose tooltip is pending or shown.</summary>
    internal VisualElement? Target => _target;

    /// <summary>The nearest element at or above <paramref name="view"/> that has a tooltip.</summary>
    internal static VisualElement? FindTarget(SkiaView? view, out string? text)
    {
        text = null;
        for (Element? element = view?.MauiView; element is not (null or Page); element = element.Parent)
        {
            if (element is VisualElement visual && ToolTipProperties.GetText(visual)?.ToString() is { Length: > 0 } t)
            {
                text = t;
                return visual;
            }
        }
        return null;
    }

    /// <summary>
    /// The nearest view at or above <paramref name="view"/> that is not a Controls element and
    /// has a tooltip on its platform view (<see cref="SkiaView.ToolTipText"/>, a core view's
    /// <c>IToolTipElement.ToolTip</c>). Controls elements are found by <see cref="FindTarget"/>.
    /// </summary>
    internal static SkiaView? FindCoreTarget(SkiaView? view, out string? text)
    {
        text = null;
        for (var v = view; v != null; v = v.Parent)
        {
            if (v.MauiView is Page)
                return null;
            if (v.MauiView == null && v.ToolTipText is { Length: > 0 } t)
            {
                text = t;
                return v;
            }
        }
        return null;
    }

    /// <summary>The pointer moved over <paramref name="hitView"/> at (x, y).</summary>
    internal void OnPointerMoved(SkiaView? hitView, float x, float y)
    {
        _pointerX = x;
        _pointerY = y;
        var target = FindTarget(hitView, out _);
        var coreTarget = target == null ? FindCoreTarget(hitView, out _) : null;
        if (ReferenceEquals(target, _target) && ReferenceEquals(coreTarget, _coreTarget))
            return;

        Hide();
        _suppressed = false;
        _target = target;
        _coreTarget = coreTarget;
        if (target == null && coreTarget == null)
            return;

        int generation = ++_generation;
        Schedule(target, () =>
        {
            if (generation == _generation)
                ShowPending();
        });
    }

    /// <summary>Shows the pending tooltip now (the delay elapsed).</summary>
    internal void ShowPending()
    {
        if ((_target == null && _coreTarget == null) || _suppressed || ShownText != null)
            return;
        var text = _target != null ? ToolTipProperties.GetText(_target)?.ToString() : _coreTarget!.ToolTipText;
        if (text is not { Length: > 0 })
            return;
        ShownText = text;
        ShownAt = new SKPoint(_pointerX, _pointerY + PointerOffsetY);
        _invalidate();
    }

    /// <summary>A press or scroll: hide, and stay hidden until the pointer reaches another element.</summary>
    internal void Dismiss()
    {
        _suppressed = true;
        _generation++;
        Hide();
    }

    /// <summary>The pointer left the window, or the window lost focus.</summary>
    internal void Reset()
    {
        _generation++;
        _target = null;
        _coreTarget = null;
        _suppressed = false;
        Hide();
    }

    private void Hide()
    {
        if (ShownText == null)
            return;
        ShownText = null;
        _invalidate();
    }

    /// <summary>Draws the shown card, kept inside the window.</summary>
    internal void Draw(SKCanvas canvas, float windowWidth, float windowHeight)
    {
        if (ShownText is not { } text)
            return;

        var typeface = ResourceCache.Shared.GetTypeface(TextRenderingHelper.GetEffectiveFontFamily(null), SKFontStyle.Normal);
        using var font = SkiaFontFactory.Create(typeface, FontSize);
        var lines = Wrap(text, font, MaxTextWidth);
        float lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        float textWidth = 0;
        foreach (var line in lines)
            textWidth = Math.Max(textWidth, TextRenderingHelper.MeasureWidth(font, line));

        float width = textWidth + PaddingX * 2;
        float height = lines.Count * lineHeight + PaddingY * 2;
        float left = Math.Clamp(ShownAt.X, EdgeMargin, Math.Max(EdgeMargin, windowWidth - width - EdgeMargin));
        float top = ShownAt.Y;
        if (top + height > windowHeight - EdgeMargin)
            top = _pointerY - height - EdgeMargin; // no room below the pointer: above it
        top = Math.Max(EdgeMargin, top);

        var card = new SKRect(left, top, left + width, top + height);
        bool dark = SkiaTheme.IsDarkMode;
        using (var fill = new SKPaint { Color = dark ? new SKColor(0x2C, 0x2C, 0x2C) : new SKColor(0xF9, 0xF9, 0xF9), IsAntialias = true })
            canvas.DrawRoundRect(card, 4, 4, fill);
        using (var stroke = new SKPaint { Color = dark ? new SKColor(0x4A, 0x4A, 0x4A) : new SKColor(0xD0, 0xD0, 0xD0), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawRoundRect(card, 4, 4, stroke);

        using var textPaint = new SKPaint { Color = dark ? new SKColor(0xF2, 0xF2, 0xF2) : new SKColor(0x1A, 0x1A, 0x1A), IsAntialias = true };
        float baseline = top + PaddingY - font.Metrics.Ascent;
        foreach (var line in lines)
        {
            TextRenderingHelper.DrawTextWithFallback(canvas, line, left + PaddingX, baseline, textPaint, typeface, FontSize);
            baseline += lineHeight;
        }
    }

    /// <summary>Greedy word wrap at <paramref name="maxWidth"/>, keeping explicit line breaks.</summary>
    internal static List<string> Wrap(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && TextRenderingHelper.MeasureWidth(font, candidate) > maxWidth)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}
