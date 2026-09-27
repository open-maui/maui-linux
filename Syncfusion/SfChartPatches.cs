// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Syncfusion.Maui.Graphics.Internals;
using SfCanvasExtensions = Syncfusion.Maui.Graphics.Internals.CanvasExtensions;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// What Syncfusion Charts needs from the drawing layer that the platform-neutral
/// build, or MAUI Graphics' Skia backend, leaves out:
/// <list type="bullet">
/// <item><c>CanvasExtensions.DrawLines</c> is empty in the platform-neutral
/// build: FastLineSeries and a histogram's normal-distribution curve drew
/// nothing. It strokes the points as a polyline, as the iOS build does.</item>
/// <item>FastLineSegment's platform-neutral layout (shared with iOS in shape,
/// but without iOS's seeding of the array) never records the first point, so
/// the line started at the second one; the first point is put back.</item>
/// <item>Pie, doughnut and radial-bar wedges rely on arcs joining the
/// current point, which OpenMaui core supplies for every IDrawable
/// (Rendering/GraphicsPathPatches.cs).</item>
/// <item><c>ChartZoomPanBehavior.SetTouchHandled</c> is empty in the
/// platform-neutral build; as on Windows, a chart with panning enabled marks
/// its touch handled from press to release.</item>
/// </list>
/// Charts is optional, so its types are looked up by name.
/// </summary>
internal static class SfChartPatches
{
    private static int s_installed;

    private static FieldInfo? s_fastLineDrawPoints;
    private static FieldInfo? s_fastLineXValues;
    private static FieldInfo? s_fastLineYValues;
    private static FieldInfo? s_fastLineSplit;
    private static FieldInfo? s_fastLineSegmentStart;
    private static PropertyInfo? s_segmentSeries;
    private static MethodInfo? s_transformX;
    private static MethodInfo? s_transformY;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var harmony = new Harmony("com.openmaui.syncfusion.charts");
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Try("Syncfusion line drawing", () =>
        {
            var drawLines = typeof(SfCanvasExtensions).GetMethod(nameof(SfCanvasExtensions.DrawLines), Static, null,
                new[] { typeof(ICanvas), typeof(float[]), typeof(ILineDrawing) }, null);
            // Only the empty stub, and only once (SfCanvasPatches may cover it too).
            if (drawLines != null && drawLines.GetMethodBody()?.GetILAsByteArray()?.Length <= 2
                && Harmony.GetPatchInfo(drawLines) is not { Prefixes.Count: > 0 })
                harmony.Patch(drawLines, new HarmonyMethod(typeof(SfChartPatches).GetMethod(nameof(DrawLines_Prefix), Static)));
        });

        Try("FastLineSeries layout", () =>
        {
            var segment = Type.GetType("Syncfusion.Maui.Charts.FastLineSegment, Syncfusion.Maui.Charts");
            var chartSeries = Type.GetType("Syncfusion.Maui.Charts.ChartSeries, Syncfusion.Maui.Charts");
            if (segment == null || chartSeries == null)
                return;
            s_fastLineDrawPoints = segment.GetField("drawPoints", Instance);
            s_fastLineXValues = segment.GetField("XValues", Instance);
            s_fastLineYValues = segment.GetField("YValues", Instance);
            s_fastLineSplit = segment.GetField("IsSegementSplited", Instance);
            s_fastLineSegmentStart = segment.GetField("segmentStart", Instance);
            s_segmentSeries = segment.GetProperty("Series", Instance);
            s_transformX = chartSeries.GetMethod("TransformToVisibleX", Instance, null, new[] { typeof(double), typeof(double) }, null);
            s_transformY = chartSeries.GetMethod("TransformToVisibleY", Instance, null, new[] { typeof(double), typeof(double) }, null);
            var onLayout = segment.GetMethod("OnLayout", Instance, null, Type.EmptyTypes, null);
            if (onLayout == null || s_fastLineDrawPoints == null || s_fastLineXValues == null || s_fastLineYValues == null
                || s_segmentSeries == null || s_transformX == null || s_transformY == null)
                return;
            harmony.Patch(onLayout, postfix: new HarmonyMethod(typeof(SfChartPatches).GetMethod(nameof(FastLineOnLayout_Postfix), Static)));
        });

        Try("chart pan handling", () =>
        {
            var zoomPan = Type.GetType("Syncfusion.Maui.Charts.ChartZoomPanBehavior, Syncfusion.Maui.Charts");
            var cartesian = Type.GetType("Syncfusion.Maui.Charts.SfCartesianChart, Syncfusion.Maui.Charts");
            s_isHandled = cartesian?.GetProperty("IsHandled", Instance);
            s_enablePanning = zoomPan?.GetProperty("EnablePanning", Instance);
            var setTouchHandled = zoomPan?.GetMethod("SetTouchHandled", Instance);
            var onTouchUp = cartesian?.GetMethods(Instance).FirstOrDefault(m => m.Name == "OnTouchUp" && m.GetParameters().Length == 3);
            if (s_isHandled == null || s_enablePanning == null || setTouchHandled == null || onTouchUp == null
                || setTouchHandled.GetMethodBody()?.GetILAsByteArray()?.Length > 2)
                return;
            harmony.Patch(setTouchHandled, new HarmonyMethod(typeof(SfChartPatches).GetMethod(nameof(SetTouchHandled_Prefix), Static)));
            harmony.Patch(onTouchUp, new HarmonyMethod(typeof(SfChartPatches).GetMethod(nameof(CartesianOnTouchUp_Prefix), Static)));
        });
    }

    private static PropertyInfo? s_isHandled;
    private static PropertyInfo? s_enablePanning;

    // ChartZoomPanBehavior.SetTouchHandled, empty in the platform-neutral build:
    // the Windows build marks a panning chart's touch as handled (IGestureListener.IsTouchHandled).
    private static bool SetTouchHandled_Prefix(object __instance, object chart)
    {
        try
        {
            if (s_isHandled!.DeclaringType!.IsInstanceOfType(chart) && s_enablePanning!.GetValue(__instance) is true)
                s_isHandled.SetValue(chart, true);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Marking a chart pan as handled failed", ex);
        }
        return false;
    }

    // SfCartesianChart.OnTouchUp: the Windows build clears the handled mark when the touch ends.
    private static void CartesianOnTouchUp_Prefix(object __instance)
    {
        try
        {
            s_isHandled!.SetValue(__instance, false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Clearing a chart's handled touch failed", ex);
        }
    }

    private static void Try(string what, Action patch)
    {
        try
        {
            patch();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Patching {what} failed", ex);
        }
    }

    // Parameter names match CanvasExtensions.DrawLines.
    private static bool DrawLines_Prefix(ICanvas canvas, float[] points, ILineDrawing lineDrawing)
    {
        if (canvas == null || points == null || points.Length < 4 || lineDrawing == null)
            return false;
        try
        {
            var path = new PathF();
            path.MoveTo(points[0], points[1]);
            for (int i = 2; i + 1 < points.Length; i += 2)
            {
                if (float.IsNaN(points[i]) || float.IsNaN(points[i + 1]))
                    continue;
                path.LineTo(points[i], points[i + 1]);
            }

            canvas.SaveState();
            canvas.StrokeColor = lineDrawing.Stroke ?? Colors.Transparent;
            canvas.StrokeSize = (float)lineDrawing.StrokeWidth;
            canvas.Antialias = lineDrawing.EnableAntiAliasing;
            canvas.Alpha = lineDrawing.Opacity;
            canvas.StrokeLineJoin = LineJoin.Round;
            if (lineDrawing.StrokeDashArray is { Count: > 0 } dashes)
                canvas.StrokeDashPattern = dashes.Select(d => (float)d).ToArray();
            canvas.DrawPath(path);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Drawing Syncfusion lines failed", ex);
        }
        finally
        {
            canvas.RestoreState();
        }
        return false;
    }

    private static void FastLineOnLayout_Postfix(object __instance)
    {
        try
        {
            if (s_fastLineDrawPoints!.GetValue(__instance) is not float[] points
                || s_fastLineXValues!.GetValue(__instance) is not IList xValues
                || s_fastLineYValues!.GetValue(__instance) is not IList yValues
                || s_segmentSeries!.GetValue(__instance) is not { } series)
                return;
            int first = s_fastLineSplit?.GetValue(__instance) is true && s_fastLineSegmentStart?.GetValue(__instance) is int start ? start : 0;
            if (first >= xValues.Count || first >= yValues.Count)
                return;
            double x = Convert.ToDouble(xValues[first]);
            double y = Convert.ToDouble(yValues[first]);
            if (double.IsNaN(y))
                return;
            float px = (float)s_transformX!.Invoke(series, new object[] { x, y })!;
            float py = (float)s_transformY!.Invoke(series, new object[] { x, y })!;
            if (points.Length >= 2 && points[0] == px && points[1] == py)
                return;

            var withFirst = new float[points.Length + 2];
            withFirst[0] = px;
            withFirst[1] = py;
            Array.Copy(points, 0, withFirst, 2, points.Length);
            s_fastLineDrawPoints.SetValue(__instance, withFirst);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Completing a FastLineSeries' points failed", ex);
        }
    }
}
