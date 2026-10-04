// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// CommunityToolkit.Maui's DrawingView image export (<c>DrawingView.GetImageStream</c>,
/// <c>DrawingView.GetImageStream(ImageLineOptions)</c>, <c>DrawingLine.GetImageStream</c>, all
/// through <c>DrawingViewService</c>) on Linux. The toolkit's platform-neutral build returned
/// <see cref="Stream.Null"/>; this renders the lines with Skia exactly as its Windows build does
/// with Win2D: a PNG at one pixel per unit, the size of the lines' bounds grown by the widest
/// line on each side (or the canvas size for <c>DrawingViewOutputOption.FullCanvas</c>, lines at
/// their own coordinates), filled with the background (a solid colour or a linear or radial
/// gradient laid over the image; anything else, the toolkit's default background colour), each
/// line drawn as round-capped segments between its points. As on Windows the requested size is
/// not applied (the Windows build never scales), and no lines give an empty stream.
/// </summary>
internal static class DrawingImageExport
{
    /// <summary>One line to draw: its points, colour and width.</summary>
    internal readonly record struct Stroke(IReadOnlyList<PointF> Points, Color Color, float Width);

    /// <summary>The toolkit's DrawingViewDefaults.BackgroundColor (light grey).</summary>
    internal static Color DefaultBackground { get; set; } = Colors.LightGray;

    /// <summary>The PNG for <paramref name="strokes"/>, or <see cref="Stream.Null"/> when there is nothing to draw.</summary>
    internal static Stream Render(IReadOnlyList<Stroke> strokes, Paint? background, Size? canvasSize)
    {
        var points = strokes.SelectMany(s => s.Points).ToList();
        if (points.Count == 0)
            return Stream.Null;
        float maxLineWidth = strokes.Max(s => s.Width);

        // Windows' GetCanvasRenderTarget: bounds grown by the widest line, or the canvas.
        float minX = points.Min(p => p.X) - maxLineWidth;
        float minY = points.Min(p => p.Y) - maxLineWidth;
        float width = canvasSize is { } cw ? (float)cw.Width : points.Max(p => p.X) - minX + maxLineWidth;
        float height = canvasSize is { } ch ? (float)ch.Height : points.Max(p => p.Y) - minY + maxLineWidth;
        if (width < 1f || height < 1f)
            return Stream.Null;
        var offset = canvasSize.HasValue ? new SizeF(0, 0) : new SizeF(minX, minY);

        int pixelWidth = Math.Max(1, (int)Math.Round(width));
        int pixelHeight = Math.Max(1, (int)Math.Round(height));
        using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface == null)
            return Stream.Null;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using (var fill = BackgroundPaint(background, pixelWidth, pixelHeight))
            canvas.DrawRect(0, 0, pixelWidth, pixelHeight, fill);

        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
        };
        foreach (var line in strokes)
        {
            stroke.Color = ToSk(line.Color);
            stroke.StrokeWidth = line.Width;
            // Win2D DrawLine per segment: a lone point draws nothing.
            for (int i = 0; i < line.Points.Count - 1; i++)
            {
                var a = line.Points[i];
                var b = line.Points[i + 1];
                canvas.DrawLine(a.X - offset.Width, a.Y - offset.Height, b.X - offset.Width, b.Y - offset.Height, stroke);
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    private static SKPaint BackgroundPaint(Paint? background, int width, int height)
    {
        var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        switch (background)
        {
            case SolidPaint solid:
                paint.Color = ToSk(solid.Color ?? DefaultBackground);
                break;
            case LinearGradientPaint linear:
                paint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint((float)(linear.StartPoint.X * width), (float)(linear.StartPoint.Y * height)),
                    new SKPoint((float)(linear.EndPoint.X * width), (float)(linear.EndPoint.Y * height)),
                    linear.GradientStops.Select(s => ToSk(s.Color)).ToArray(),
                    linear.GradientStops.Select(s => s.Offset).ToArray(),
                    SKShaderTileMode.Clamp);
                break;
            case RadialGradientPaint radial:
                // Win2D's radial brush: centre and radii relative to the image size.
                var center = new SKPoint((float)(radial.Center.X * width), (float)(radial.Center.Y * height));
                float rx = (float)radial.Radius * width, ry = (float)radial.Radius * height;
                var scale = SKMatrix.CreateScale(1f, rx > 0 ? ry / rx : 1f, center.X, center.Y);
                paint.Shader = SKShader.CreateRadialGradient(
                    center,
                    Math.Max(rx, 0.0001f),
                    radial.GradientStops.Select(s => ToSk(s.Color)).ToArray(),
                    radial.GradientStops.Select(s => s.Offset).ToArray(),
                    SKShaderTileMode.Clamp,
                    scale);
                break;
            default:
                paint.Color = ToSk(DefaultBackground);
                break;
        }
        return paint;
    }

    private static SKColor ToSk(Color? color)
    {
        var c = color ?? Colors.Black;
        return new SKColor((byte)Math.Round(c.Red * 255), (byte)Math.Round(c.Green * 255), (byte)Math.Round(c.Blue * 255), (byte)Math.Round(c.Alpha * 255));
    }

    // ---- The toolkit's DrawingViewService ----------------------------------------------------

    private const string ServiceTypeName = "CommunityToolkit.Maui.Core.Views.DrawingViewService, CommunityToolkit.Maui.Core";

    /// <summary>Replaces DrawingViewService's image methods, when the toolkit is part of the app.</summary>
    internal static void Install(Harmony harmony)
    {
        Type? service;
        try
        {
            service = Type.GetType(ServiceTypeName, throwOnError: false);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
        {
            service = null;
        }
        if (service == null)
            return;

        if (service.Assembly.GetType("CommunityToolkit.Maui.Core.DrawingViewDefaults")?.GetProperty("BackgroundColor")?.GetValue(null) is Color defaultBackground)
            DefaultBackground = defaultBackground;

        var prefix = new HarmonyMethod(typeof(DrawingImageExport).GetMethod(nameof(GetImageStream_Prefix), BindingFlags.Static | BindingFlags.NonPublic));
        int patched = 0;
        // GetImageStream and GetPlatformImageStream, for lines and for points: the public entry
        // points are patched too, since the neutral GetPlatformImageStream is small enough to be
        // inlined into them.
        foreach (var method in service.GetMethods(BindingFlags.Static | BindingFlags.Public))
        {
            if (method.Name is not ("GetImageStream" or "GetPlatformImageStream") || method.GetParameters().Length != 2)
                continue;
            harmony.Patch(method, prefix: prefix);
            patched++;
        }
        if (patched == 0)
            DiagnosticLog.Warn("DrawingImageExport", "DrawingViewService's image methods not found in this toolkit release; GetImageStream is not bridged");
    }

    private static bool GetImageStream_Prefix(object __0, CancellationToken __1, ref ValueTask<Stream> __result)
    {
        __1.ThrowIfCancellationRequested();
        __result = ValueTask.FromResult(RenderOptions(__0));
        return false;
    }

    /// <summary>Renders the toolkit's ImageLineOptions or ImagePointOptions.</summary>
    internal static Stream RenderOptions(object options)
    {
        var type = options.GetType();
        var background = type.GetProperty("Background")?.GetValue(options) as Paint;
        var canvasSize = type.GetProperty("CanvasSize")?.GetValue(options) as Size?;
        var strokes = new List<Stroke>();

        if (type.GetProperty("Lines")?.GetValue(options) is IEnumerable lines)
        {
            foreach (var line in lines)
            {
                if (line == null)
                    continue;
                var lineContract = ContractOf(line);
                strokes.Add(new Stroke(
                    Points(lineContract.GetProperty("Points")?.GetValue(line)),
                    lineContract.GetProperty("LineColor")?.GetValue(line) as Color ?? Colors.Black,
                    Convert.ToSingle(lineContract.GetProperty("LineWidth")?.GetValue(line) ?? 5f, System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        else if (type.GetProperty("Points")?.GetValue(options) is { } points)
        {
            strokes.Add(new Stroke(
                Points(points),
                type.GetProperty("StrokeColor")?.GetValue(options) as Color ?? Colors.Black,
                Convert.ToSingle(type.GetProperty("LineWidth")?.GetValue(options) ?? 5f, System.Globalization.CultureInfo.InvariantCulture)));
        }

        return Render(strokes, background, canvasSize);
    }

    /// <summary>A line's IDrawingLine interface (explicit implementations), else its own type.</summary>
    private static Type ContractOf(object line)
        => line.GetType().GetInterface("CommunityToolkit.Maui.Core.IDrawingLine") ?? line.GetType();

    private static IReadOnlyList<PointF> Points(object? points)
        => points is IEnumerable e ? e.OfType<PointF>().ToArray() : Array.Empty<PointF>();
}
