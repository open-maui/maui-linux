// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// Draws client-side decorations (titlebar + window-control buttons) on a Wayland
/// surface when the compositor refuses server-side decorations (Mutter/GNOME) or
/// the protocol isn't available. Pixel-exact in *logical* coordinates so the
/// SkiaRenderingEngine's existing DpiScale matrix applies uniformly.
///
/// Layout (logical pixels, with titlebar height = 32):
///
///   ┌─────────────────────────────────────────────────────────┬───┬───┬───┐
///   │  Title text (left-aligned, padding 12)                  │ _ │ □ │ × │
///   ├─────────────────────────────────────────────────────────┴───┴───┴───┤
///   │                                                                     │
///   │                       (MAUI content area)                           │
///
/// Each button is 28x28, with 4px gap, packed to the right edge. The button
/// bounds are written back to the WaylandWindow.Csd*ButtonBounds fields so the
/// pointer-button hit-test can find them.
/// </summary>
internal static class WaylandCsdRenderer
{
    private const float ButtonSize = 28f;
    private const float ButtonGap = 4f;
    private const float EdgePadding = 4f;
    private const float TitlePaddingLeft = 12f;

    /// <summary>
    /// Draws the titlebar at the top of the canvas (logical coords, origin 0,0).
    /// <paramref name="logicalWidth"/> is the surface's logical width (the
    /// titlebar spans the full width). <paramref name="title"/> is the window
    /// title; pass an empty string to draw the strip without text.
    /// </summary>
    public static void DrawTitlebar(SKCanvas canvas, WaylandWindow window, float logicalWidth, string title)
    {
        float h = WaylandWindow.CsdTitlebarHeightLogical;
        var bounds = new SKRect(0, 0, logicalWidth, h);

        bool isDark = SkiaTheme.IsDarkMode;

        // Titlebar background — theme-aware, matches the surface color used by
        // SkiaShell / SkiaPage so the seam is invisible when content is the
        // same color.
        using (var bgPaint = new SKPaint
        {
            Color = isDark ? new SKColor(0x2A, 0x2A, 0x2A) : new SKColor(0xF0, 0xF0, 0xF0),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        })
        {
            canvas.DrawRect(bounds, bgPaint);
        }

        // 1px bottom separator — visual divider between titlebar and content,
        // since they often share the same theme color.
        using (var sepPaint = new SKPaint
        {
            Color = isDark ? new SKColor(0x00, 0x00, 0x00, 0x60) : new SKColor(0x00, 0x00, 0x00, 0x20),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        })
        {
            canvas.DrawRect(new SKRect(0, h - 1, logicalWidth, h), sepPaint);
        }

        // Right-packed window-control buttons. Order from right to left:
        // close, maximize, minimize. Match standard desktop conventions.
        float buttonsRight = logicalWidth - EdgePadding;
        float buttonsTop = (h - ButtonSize) * 0.5f;

        var closeRect = new SKRect(
            buttonsRight - ButtonSize, buttonsTop,
            buttonsRight, buttonsTop + ButtonSize);

        var maxRect = new SKRect(
            closeRect.Left - ButtonGap - ButtonSize, buttonsTop,
            closeRect.Left - ButtonGap, buttonsTop + ButtonSize);

        var minRect = new SKRect(
            maxRect.Left - ButtonGap - ButtonSize, buttonsTop,
            maxRect.Left - ButtonGap, buttonsTop + ButtonSize);

        // Store hit-test rects so OnPointerButton can route clicks. Anyone
        // reading these mid-flight would race, but the pointer event is
        // dispatched on the same thread as Render so we're safe.
        window.CsdCloseButtonBounds = closeRect;
        window.CsdMaximizeButtonBounds = maxRect;
        window.CsdMinimizeButtonBounds = minRect;

        DrawCloseButton(canvas, closeRect, isDark);
        DrawMaxButton(canvas, maxRect, isDark, window.IsMaximized);
        DrawMinButton(canvas, minRect, isDark);

        // Title text — left-aligned, vertically centered. Truncate against the
        // leftmost button so long titles don't draw on top of the controls.
        if (!string.IsNullOrEmpty(title))
        {
            using var typeface = SKTypeface.Default;
            using var font = new SKFont(typeface, 13f);
            using var paint = new SKPaint(font)
            {
                Color = isDark ? new SKColor(0xE0, 0xE0, 0xE0) : new SKColor(0x20, 0x20, 0x20),
                IsAntialias = true,
            };

            float maxTextWidth = minRect.Left - TitlePaddingLeft - 8f;
            string displayed = TruncateToWidth(title, font, maxTextWidth);

            var metrics = font.Metrics;
            float baselineY = h * 0.5f - (metrics.Ascent + metrics.Descent) * 0.5f;
            canvas.DrawText(displayed, TitlePaddingLeft, baselineY, font, paint);
        }
    }

    private static string TruncateToWidth(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth) return text;

        // Binary-search the longest prefix that fits with an ellipsis.
        const string ellipsis = "...";
        float ellipsisWidth = font.MeasureText(ellipsis);
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            string candidate = text.Substring(0, mid);
            if (font.MeasureText(candidate) + ellipsisWidth <= maxWidth)
                lo = mid;
            else
                hi = mid - 1;
        }
        return text.Substring(0, lo) + ellipsis;
    }

    private static void DrawCloseButton(SKCanvas canvas, SKRect r, bool isDark)
    {
        using var paint = new SKPaint
        {
            Color = isDark ? new SKColor(0xE0, 0xE0, 0xE0) : new SKColor(0x40, 0x40, 0x40),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
        };
        // X glyph centered in the button rect, with a 9px arm length each way.
        float cx = r.MidX, cy = r.MidY;
        const float arm = 5.5f;
        canvas.DrawLine(cx - arm, cy - arm, cx + arm, cy + arm, paint);
        canvas.DrawLine(cx + arm, cy - arm, cx - arm, cy + arm, paint);
    }

    private static void DrawMaxButton(SKCanvas canvas, SKRect r, bool isDark, bool isMaximized)
    {
        using var paint = new SKPaint
        {
            Color = isDark ? new SKColor(0xE0, 0xE0, 0xE0) : new SKColor(0x40, 0x40, 0x40),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            IsAntialias = true,
        };
        float cx = r.MidX, cy = r.MidY;
        if (isMaximized)
        {
            // "Restore" glyph — two offset squares, suggesting overlapping windows.
            const float s = 8f;
            canvas.DrawRect(new SKRect(cx - s * 0.5f + 2f, cy - s * 0.5f - 2f,
                                       cx + s * 0.5f + 2f, cy + s * 0.5f - 2f), paint);
            canvas.DrawRect(new SKRect(cx - s * 0.5f - 2f, cy - s * 0.5f + 2f,
                                       cx + s * 0.5f - 2f, cy + s * 0.5f + 2f), paint);
        }
        else
        {
            // "Maximize" glyph — single square outline.
            const float s = 11f;
            canvas.DrawRect(new SKRect(cx - s * 0.5f, cy - s * 0.5f,
                                       cx + s * 0.5f, cy + s * 0.5f), paint);
        }
    }

    private static void DrawMinButton(SKCanvas canvas, SKRect r, bool isDark)
    {
        using var paint = new SKPaint
        {
            Color = isDark ? new SKColor(0xE0, 0xE0, 0xE0) : new SKColor(0x40, 0x40, 0x40),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
        };
        // Single horizontal line near the vertical center.
        const float armWidth = 11f;
        float cx = r.MidX, cy = r.MidY + 2f;
        canvas.DrawLine(cx - armWidth * 0.5f, cy, cx + armWidth * 0.5f, cy, paint);
    }
}
