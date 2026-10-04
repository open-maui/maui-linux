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

    /// <summary>False hides the item (SwipeItem.IsVisible): it is neither drawn nor tapped.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>False keeps the item shown but a tap does not invoke it (MAUI's SwipeItem.IsEnabled).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// A view drawn as the item instead of its text and colours (MAUI's SwipeItemView): it is
    /// laid out in the item's place, a menu item's width (100) unless it measures wider.
    /// </summary>
    public SkiaView? Content { get; set; }

    /// <summary>
    /// Background color using MAUI Color type.
    /// </summary>
    public Color BackgroundColor { get; set; } = Color.FromRgb(33, 150, 243);

    /// <summary>
    /// Text color using MAUI Color type.
    /// </summary>
    public Color TextColor { get; set; } = Colors.White;

    public event EventHandler? Invoked;

    /// <summary>The MAUI item a tap invokes, for an item no handler invokes (an item view, a plain ISwipeItem).</summary>
    internal ISwipeItem? Invoker { get; set; }

    internal void OnInvoked()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
        if (Invoker is IView { IsEnabled: false })
            return;
        Invoker?.OnInvoked();
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
