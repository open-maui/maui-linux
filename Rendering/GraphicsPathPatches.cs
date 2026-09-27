// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// MAUI Graphics' Skia path conversion (<c>SKGraphicsExtensions.AsSkiaPath</c>)
/// turns <c>PathF.AddArc</c> into <c>SKPath.AddArc</c>, which starts a new
/// contour. CoreGraphics, Win2D and Android connect an arc to the current
/// point, and shapes are drawn that way (a wedge is MoveTo(centre), AddArc,
/// Close; a ring joins its outer and inner arcs): on Skia a wedge drew as an
/// arc closed by its chord, in any GraphicsView or IDrawable (Syncfusion's
/// pie, doughnut and radial-bar charts among them). Paths with arcs are
/// converted with arcs joined to the point before them (<c>ArcTo</c> without
/// forcing a move); paths without arcs keep MAUI's conversion.
/// </summary>
internal static class GraphicsPathPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var asSkiaPath = typeof(SKGraphicsExtensions).GetMethod(nameof(SKGraphicsExtensions.AsSkiaPath), Static, null,
                new[] { typeof(PathF), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) }, null);
            if (asSkiaPath != null)
                new Harmony("com.openmaui.graphics.arcs").Patch(asSkiaPath,
                    new HarmonyMethod(typeof(GraphicsPathPatches).GetMethod(nameof(AsSkiaPath_Prefix), Static)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("GraphicsPathPatches", "Patching MAUI Graphics arc conversion failed", ex);
        }
    }

    // Parameter names match SKGraphicsExtensions.AsSkiaPath.
    private static bool AsSkiaPath_Prefix(PathF path, float ppu, float ox, float oy, float fx, float fy, ref SKPath __result)
    {
        if (path == null)
            return true;
        bool hasArc = false;
        foreach (var operation in path.SegmentTypes)
        {
            if (operation == PathOperation.Arc)
            {
                hasArc = true;
                break;
            }
        }
        if (!hasArc)
            return true;

        __result = ConvertConnectingArcs(path, ppu, ox, oy, fx, fy);
        return false;
    }

    /// <summary>
    /// <paramref name="path"/> as an SKPath with each arc joined to the point
    /// before it, as the other MAUI Graphics backends draw it.
    /// </summary>
    internal static SKPath ConvertConnectingArcs(PathF path, float ppu, float ox, float oy, float fx, float fy)
    {
        using var builder = new SKPathBuilder();
        float sx = ppu * fx;
        float sy = ppu * fy;
        int point = 0, angle = 0, clockwise = 0;
        bool open = false;
        foreach (var operation in path.SegmentTypes)
        {
            switch (operation)
            {
                case PathOperation.Move:
                {
                    var p = path[point++];
                    builder.MoveTo(ox + p.X * sx, oy + p.Y * sy);
                    open = true;
                    break;
                }
                case PathOperation.Line:
                {
                    var p = path[point++];
                    builder.LineTo(ox + p.X * sx, oy + p.Y * sy);
                    open = true;
                    break;
                }
                case PathOperation.Quad:
                {
                    var c = path[point++];
                    var p = path[point++];
                    builder.QuadTo(ox + c.X * sx, oy + c.Y * sy, ox + p.X * sx, oy + p.Y * sy);
                    open = true;
                    break;
                }
                case PathOperation.Cubic:
                {
                    var c1 = path[point++];
                    var c2 = path[point++];
                    var p = path[point++];
                    builder.CubicTo(ox + c1.X * sx, oy + c1.Y * sy, ox + c2.X * sx, oy + c2.Y * sy, ox + p.X * sx, oy + p.Y * sy);
                    open = true;
                    break;
                }
                case PathOperation.Arc:
                {
                    var topLeft = path[point++];
                    var bottomRight = path[point++];
                    float start = path.GetArcAngle(angle++);
                    float end = path.GetArcAngle(angle++);
                    bool isClockwise = path.GetArcClockwise(clockwise++);
                    while (start < 0) start += 360;
                    while (end < 0) end += 360;
                    float sweep = GeometryUtil.GetSweep(start, end, isClockwise);
                    if (!isClockwise)
                        sweep = -sweep;
                    var oval = new SKRect(ox + topLeft.X * sx, oy + topLeft.Y * sy, ox + bottomRight.X * sx, oy + bottomRight.Y * sy);
                    if (Math.Abs(sweep) >= 360)
                    {
                        // A whole ellipse is its own contour on every backend.
                        builder.AddOval(oval, isClockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise);
                    }
                    else
                    {
                        builder.ArcTo(oval, -start, sweep, !open);
                        open = true;
                    }
                    break;
                }
                case PathOperation.Close:
                    builder.Close();
                    open = false;
                    break;
            }
        }
        return builder.Detach();
    }
}
