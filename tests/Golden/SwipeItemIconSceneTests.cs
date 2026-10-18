// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Golden;

using StackOrientation = Microsoft.Maui.Controls.StackOrientation;
using SwipeItem = Microsoft.Maui.Platform.SkiaSwipeItem;
using OpenSwipeItem = Microsoft.Maui.OpenSwipeItem;

/// <summary>
/// Swipe items as MAUI's Windows SwipeItem lays them out: the icon, 16 high, above the
/// 12-point text, centred together; a font-style (single-colour) icon drawn in its tint, a
/// coloured image in its own colours, an item with an icon only and one with text only.
/// </summary>
[Collection("LinuxApplication.Current")]
public class SwipeItemIconSceneTests
{
    public static IEnumerable<object[]> Scales => GoldenHarness.Scales.Select(s => new object[] { s });

    /// <summary>A single-colour (black) check mark, as a font glyph or a mask icon is.</summary>
    private static SKBitmap Check(float scale)
    {
        int size = (int)System.Math.Round(16 * scale);
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        using var path = new SKPath();
        path.MoveTo(2.5f, 8.5f);
        path.LineTo(6.5f, 12.5f);
        path.LineTo(13.5f, 3.5f);
        canvas.DrawPath(path, paint);
        return bitmap;
    }

    /// <summary>A coloured image (four quadrants in a circle), drawn in its own colours.</summary>
    private static SKBitmap Coloured(float scale)
    {
        int size = (int)System.Math.Round(16 * scale);
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        using var clip = new SKPath();
        clip.AddCircle(8, 8, 8);
        canvas.ClipPath(clip, antialias: true);
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = new SKColor(0xF4, 0x43, 0x36); canvas.DrawRect(0, 0, 8, 8, paint);
        paint.Color = new SKColor(0x4C, 0xAF, 0x50); canvas.DrawRect(8, 0, 8, 8, paint);
        paint.Color = new SKColor(0x21, 0x96, 0xF3); canvas.DrawRect(0, 8, 8, 8, paint);
        paint.Color = new SKColor(0xFF, 0xC1, 0x07); canvas.DrawRect(8, 8, 8, 8, paint);
        return bitmap;
    }

    private static SkiaSwipeView Row(string text, params SwipeItem[] items)
    {
        var swipe = new SkiaSwipeView
        {
            HeightRequest = 56,
            Content = new SkiaLabel { Text = text, FontSize = 15, Padding = new Thickness(12, 0), VerticalTextAlignment = TextAlignment.Center },
        };
        foreach (var item in items)
            swipe.RightItems.Add(item);
        swipe.Open(OpenSwipeItem.RightItems);
        return swipe;
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Swipe_item_icons(float scale)
    {
        using var check = Check(scale);
        using var coloured = Coloured(scale);

        var root = new SkiaStackLayout { Orientation = StackOrientation.Vertical, Spacing = 8, Padding = new Thickness(8) };
        root.AddChild(Row("Icon and text",
            new SwipeItem { Text = "Done", Icon = check, IconTintColor = Colors.White, BackgroundColor = Color.FromRgb(0x2E, 0x7D, 0x32) },
            new SwipeItem { Text = "Photos", Icon = coloured, BackgroundColor = Color.FromRgb(0x45, 0x5A, 0x64) }));
        root.AddChild(Row("Contrast tint",
            new SwipeItem { Text = "Flag", Icon = check, IconTintColor = Colors.Black, TextColor = Colors.Black, BackgroundColor = Colors.Gold },
            new SwipeItem { Text = "Delete", Icon = check, IconTintColor = Colors.Orange, BackgroundColor = Color.FromRgb(0xC6, 0x28, 0x28) }));
        root.AddChild(Row("Icon only, text only",
            new SwipeItem { Icon = check, IconTintColor = Colors.White, BackgroundColor = Color.FromRgb(0x15, 0x65, 0xC0) },
            new SwipeItem { Text = "Archive", BackgroundColor = Color.FromRgb(0x6A, 0x1B, 0x9A) }));

        GoldenHarness.Verify("swipe-item-icons", root, 320, 200, scale);
    }
}
