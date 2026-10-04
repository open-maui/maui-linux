// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Graphics.Internals;
using ITextElement = Syncfusion.Maui.Graphics.Internals.ITextElement;
using SfCanvasExtensions = Syncfusion.Maui.Graphics.Internals.CanvasExtensions;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Syncfusion's text drawing (<c>CanvasExtensions.DrawText</c>), which its
/// platform-neutral build throws for (<c>NotImplementedException</c>): every
/// chart axis label, data label and legend-less caption drawn through it
/// aborted its view's paint, taking the axis lines with it, and button text,
/// badges and busy-indicator titles were missing. Implemented on MAUI's
/// <c>ICanvas.DrawString</c> with the text element's font, size and colour;
/// (x, y) is the text's top-left, as the native builds place it.
/// </summary>
internal static class SfCanvasPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var harmony = new Harmony("com.openmaui.syncfusion.canvas");
            const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var atPoint = typeof(SfCanvasExtensions).GetMethod(nameof(SfCanvasExtensions.DrawText), Static, null,
                new[] { typeof(ICanvas), typeof(string), typeof(float), typeof(float), typeof(ITextElement) }, null);
            var inRect = typeof(SfCanvasExtensions).GetMethod(nameof(SfCanvasExtensions.DrawText), Static, null,
                new[] { typeof(ICanvas), typeof(string), typeof(Rect), typeof(HorizontalAlignment), typeof(VerticalAlignment), typeof(ITextElement) }, null);
            if (atPoint != null)
                harmony.Patch(atPoint, new HarmonyMethod(typeof(SfCanvasPatches).GetMethod(nameof(DrawTextAtPoint_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
            if (inRect != null)
                harmony.Patch(inRect, new HarmonyMethod(typeof(SfCanvasPatches).GetMethod(nameof(DrawTextInRect_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching Syncfusion text drawing failed", ex);
        }

        // Default text sizes the platform-neutral build throws for: a chart
        // tooltip's text (TooltipHelper) and SfBusyIndicator's title. 14, the
        // documented default, as the native builds use.
        PatchFontSizeDefault("Syncfusion.Maui.Core.TooltipHelper, Syncfusion.Maui.Core");
        PatchFontSizeDefault("Syncfusion.Maui.Core.SfBusyIndicator, Syncfusion.Maui.Core");
    }

    private static void PatchFontSizeDefault(string typeName)
    {
        try
        {
            var type = Type.GetType(typeName);
            var method = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.EndsWith("FontSizeDefaultValueCreator", StringComparison.Ordinal) && m.GetParameters().Length == 0);
            if (method == null)
                return;
            new Harmony("com.openmaui.syncfusion.fontsize").Patch(method,
                new HarmonyMethod(typeof(SfCanvasPatches).GetMethod(nameof(FontSizeDefault_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Patching {typeName} default font size failed", ex);
        }
    }

    private static bool FontSizeDefault_Prefix(ref double __result)
    {
        __result = 14;
        return false;
    }

    // Parameter names match CanvasExtensions.DrawText.
    private static bool DrawTextAtPoint_Prefix(ICanvas canvas, string value, float x, float y, ITextElement textElement)
    {
        // Top-left at (x, y): a rectangle with room for the text, aligned top-left.
        var size = SkiaTextMeasurer.MeasureFor(value, textElement);
        Draw(canvas, value, new Rect(x, y, size.Width + 2, size.Height + 2), HorizontalAlignment.Left, VerticalAlignment.Top, textElement);
        return false;
    }

    private static bool DrawTextInRect_Prefix(ICanvas canvas, string value, Rect rect, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, ITextElement textElement)
    {
        Draw(canvas, value, rect, horizontalAlignment, verticalAlignment, textElement);
        return false;
    }

    private static void Draw(ICanvas canvas, string value, Rect rect, HorizontalAlignment horizontal, VerticalAlignment vertical, ITextElement textElement)
    {
        if (canvas == null || string.IsNullOrEmpty(value))
            return;
        try
        {
            canvas.SaveState();
            canvas.FontColor = textElement.TextColor ?? Colors.Black;
            canvas.FontSize = (float)(textElement.FontSize > 0 ? textElement.FontSize : 14);
            var weight = textElement.FontAttributes.HasFlag(Microsoft.Maui.Controls.FontAttributes.Bold) ? FontWeights.Bold : FontWeights.Normal;
            var style = textElement.FontAttributes.HasFlag(Microsoft.Maui.Controls.FontAttributes.Italic) ? FontStyleType.Italic : FontStyleType.Normal;
            canvas.Font = new Microsoft.Maui.Graphics.Font(string.IsNullOrEmpty(textElement.FontFamily) ? null : textElement.FontFamily, weight, style);
            rect = AtLeastOneGlyphWide(rect, value, textElement, horizontal);
            canvas.DrawString(value, (float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height, horizontal, vertical, TextFlow.OverflowBounds);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Drawing Syncfusion text failed", ex);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    /// <summary>
    /// A text rectangle no narrower than the widest glyph of the text. MAUI Graphics' Skia text
    /// layout breaks overflowing text into lines and, when not even one character fits the width,
    /// never advances: the frame hangs while the line list grows (SfScheduler draws the resized
    /// time in a strip that shrinks to nothing as an appointment edge moves). Windows draws such
    /// text overflowing the strip; so does this, the strip widened about its alignment.
    /// </summary>
    private static Rect AtLeastOneGlyphWide(Rect rect, string value, ITextElement textElement, HorizontalAlignment horizontal)
    {
        if (rect.Width >= SkiaTextMeasurer.MeasureFor(value, textElement).Width)
            return rect;
        double widest = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (!System.Text.Rune.IsWhiteSpace(rune))
                widest = Math.Max(widest, SkiaTextMeasurer.MeasureFor(rune.ToString(), textElement).Width);
        }
        widest = Math.Ceiling(widest) + 1;
        if (rect.Width >= widest)
            return rect;
        double x = horizontal switch
        {
            HorizontalAlignment.Center => rect.Center.X - widest / 2,
            HorizontalAlignment.Right => rect.Right - widest,
            _ => rect.X,
        };
        return new Rect(x, rect.Y, widest, rect.Height);
    }
}
