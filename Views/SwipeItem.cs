// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Represents an item in a swipe view. MAUI-compliant using Color types.
/// </summary>
public class SwipeItem
{
    public string Text { get; set; } = string.Empty;

    public string? IconSource { get; set; }

    /// <summary>
    /// Background color using MAUI Color type.
    /// </summary>
    public Color BackgroundColor { get; set; } = Color.FromRgb(33, 150, 243);

    /// <summary>
    /// Text color using MAUI Color type.
    /// </summary>
    public Color TextColor { get; set; } = Colors.White;

    public event EventHandler? Invoked;

    internal void OnInvoked()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Helper to convert BackgroundColor to SKColor for rendering.
    /// </summary>
    internal SKColor GetBackgroundColorSK() => BackgroundColor.ToSKColor();

    /// <summary>
    /// Helper to convert TextColor to SKColor for rendering.
    /// </summary>
    internal SKColor GetTextColorSK() => TextColor.ToSKColor();
}
