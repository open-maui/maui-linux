// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Graphics.Internals;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Syncfusion text drawn into a rectangle narrower than one character (SfScheduler's resize
/// time strip as an appointment edge closes in) draws overflowing it, as on Windows, instead of
/// hanging MAUI Graphics' line breaker.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionNarrowTextTests
{
    private sealed class Text : Syncfusion.Maui.Graphics.Internals.ITextElement
    {
        public FontAttributes FontAttributes => FontAttributes.None;
        public string FontFamily => string.Empty;
        public double FontSize => 14;
        public Microsoft.Maui.Font Font => Microsoft.Maui.Font.Default;
        public Color TextColor { get; set; } = Colors.Black;
        public bool FontAutoScalingEnabled { get; set; }
        public void OnFontFamilyChanged(string oldValue, string newValue) { }
        public void OnFontSizeChanged(double oldValue, double newValue) { }
        public double FontSizeDefaultValueCreator() => 14;
        public void OnFontAttributesChanged(FontAttributes oldValue, FontAttributes newValue) { }
        public void OnFontChanged(Microsoft.Maui.Font oldValue, Microsoft.Maui.Font newValue) { }
        public void OnFontAutoScalingEnabledChanged(bool oldValue, bool newValue) { }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-5)]
    public void Text_in_a_strip_narrower_than_a_glyph_draws_overflowing(double width)
    {
        using var host = new CompatHost(new ContentPage(), b => b.UseLinuxSyncfusion(), 100, 100);
        using var bitmap = new SKBitmap(200, 40);
        using var surface = new SKCanvas(bitmap);
        surface.Clear(SKColors.White);
        var canvas = new SkiaCanvas { Canvas = surface };

        var draw = Task.Run(() => canvas.DrawText("10:30 AM", new Rect(100, 0, width, 40), HorizontalAlignment.Center, VerticalAlignment.Center, new Text()));
        draw.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the text layout must not loop on a width no character fits");

        int ink = 0;
        for (int y = 0; y < 40; y++)
            for (int x = 0; x < 200; x++)
                if (bitmap.GetPixel(x, y).Red < 128)
                    ink++;
        ink.Should().BeGreaterThan(0, "the text draws, overflowing the strip");
    }
}
