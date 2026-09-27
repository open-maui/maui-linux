// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Syncfusion.Maui.Graphics.Internals;
using ITextElement = Syncfusion.Maui.Graphics.Internals.ITextElement;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Syncfusion's text measurer on OpenMaui's font stack. The platform-neutral
/// <c>TextMeasurer.CreateTextMeasurer</c> throws, and controls measure header
/// and label text through it (SfTabView sizes its tabs and selection
/// indicator with it; a throw there aborted every tab change). Widths follow
/// font fallback and heights are line heights, as SkiaLabel measures.
/// </summary>
internal sealed class SkiaTextMeasurer : ITextMeasurer
{
    private static readonly LinuxFontManager s_fonts = new();

    private static readonly FieldInfo? s_instanceField = Type
        .GetType("Syncfusion.Maui.Graphics.Internals.TextMeasurer, Syncfusion.Maui.Core")
        ?.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);

    [ThreadStatic]
    private static bool t_installed;

    /// <summary>
    /// Installs the measurer for the calling thread (Syncfusion keeps it in a
    /// [ThreadStatic] field). Cheap after the first call on a thread.
    /// </summary>
    internal static void EnsureInstalled()
    {
        if (t_installed)
            return;
        t_installed = true;
        try
        {
            if (s_instanceField != null && s_instanceField.GetValue(null) == null)
                s_instanceField.SetValue(null, new SkiaTextMeasurer());
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Installing the text measurer failed", ex);
        }
    }

    public Size MeasureText(string text, float textSize, FontAttributes attributes = FontAttributes.None, string? fontFamily = null)
        => Measure(text, fontFamily, textSize, attributes, double.PositiveInfinity);

    public Size MeasureText(string text, ITextElement textElement)
        => Measure(text, textElement.FontFamily, textElement.FontSize, textElement.FontAttributes, double.PositiveInfinity);

    public Size MeasureText(string text, double width, ITextElement textElement)
        => Measure(text, textElement.FontFamily, textElement.FontSize, textElement.FontAttributes, width);

    /// <summary>The size <paramref name="text"/> takes in <paramref name="element"/>'s font, on one line per paragraph.</summary>
    internal static Size MeasureFor(string? text, ITextElement element)
        => Measure(text, element.FontFamily, element.FontSize, element.FontAttributes, double.PositiveInfinity);

    private static Size Measure(string? text, string? family, double size, FontAttributes attributes, double maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return Size.Zero;
        if (size <= 0 || double.IsNaN(size))
            size = LinuxFontManager.PlatformDefaultFontSize;

        var style = new SKFontStyle(
            attributes.HasFlag(FontAttributes.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            attributes.HasFlag(FontAttributes.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        using var font = SkiaFontFactory.Create(s_fonts.GetTypeface(family, style), (float)size);
        var metrics = font.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent;

        int lines = 0;
        float widest = 0;
        foreach (var paragraph in text.Split('\n'))
        {
            float width = TextRenderingHelper.MeasureWidth(font, paragraph);
            if (double.IsInfinity(maxWidth) || maxWidth <= 0 || width <= maxWidth)
            {
                widest = Math.Max(widest, width);
                lines++;
                continue;
            }

            // Greedy word wrap, as a wrapping label lays the text out.
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && TextRenderingHelper.MeasureWidth(font, candidate) > maxWidth)
                {
                    widest = Math.Max(widest, TextRenderingHelper.MeasureWidth(font, line));
                    lines++;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            widest = Math.Max(widest, TextRenderingHelper.MeasureWidth(font, line));
            lines++;
        }

        return new Size(Math.Ceiling(widest), Math.Ceiling(lines * lineHeight));
    }
}
