// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Shared StrokeDashArray → SKPathEffect mapping for the Skia shape views.
/// MAUI dash values are multiples of the stroke thickness (WPF/Xamarin
/// semantics), so each interval is scaled by <c>StrokeThickness</c>.
/// </summary>
internal static class ShapeDashing
{
    /// <summary>
    /// Attaches a dash effect to <paramref name="paint"/> when the array holds
    /// at least one positive interval. Odd-length arrays are repeated so Skia
    /// gets the even count it requires.
    /// </summary>
    public static void Apply(SKPaint paint, IReadOnlyList<double>? dashArray, double dashOffset, double strokeThickness)
    {
        if (dashArray == null || dashArray.Count == 0) return;

        float unit = strokeThickness > 0 ? (float)strokeThickness : 1f;
        int count = dashArray.Count % 2 == 0 ? dashArray.Count : dashArray.Count * 2;
        var intervals = new float[count];
        bool anyPositive = false;
        for (int i = 0; i < count; i++)
        {
            float v = (float)dashArray[i % dashArray.Count] * unit;
            if (v < 0) v = 0;
            if (v > 0) anyPositive = true;
            intervals[i] = v;
        }
        if (!anyPositive) return;

        paint.PathEffect = SKPathEffect.CreateDash(intervals, (float)dashOffset * unit);
    }
}
