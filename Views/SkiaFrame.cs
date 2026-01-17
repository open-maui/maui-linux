// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Frame control - a Border with shadow enabled by default.
/// Mimics the MAUI Frame control appearance.
/// Implements MAUI IFrame interface patterns.
/// </summary>
public class SkiaFrame : SkiaBorder
{
    public SkiaFrame()
    {
        HasShadow = true;
        CornerRadius = 4.0;
        SetPadding(10.0);
        BackgroundColor = Colors.White;
        Stroke = Colors.Transparent;
        StrokeThickness = 0.0;
    }
}
