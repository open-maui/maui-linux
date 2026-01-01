// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Frame control - a Border with shadow enabled by default.
/// Mimics the MAUI Frame control appearance.
/// </summary>
public class SkiaFrame : SkiaBorder
{
    public SkiaFrame()
    {
        HasShadow = true;
        CornerRadius = 4f;
        SetPadding(10f);
        BackgroundColor = SKColors.White;
        Stroke = SKColors.Transparent;
        StrokeThickness = 0f;
    }
}
