// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Represents an item in a swipe view. MAUI-compliant using Color types.
/// </summary>
public class SwipeItem
{
    public string Text { get; set; } = string.Empty;

    /// <summary>The file name of a file icon (informational; the icon drawn is <see cref="Icon"/>).</summary>
    public string? IconSource { get; set; }

    /// <summary>
    /// The icon drawn above the text (MAUI's IconImageSource, loaded by the item's handler through
    /// its image-source service), 16 high, as Windows' SwipeItem shows it. Null draws the text only.
    /// </summary>
    public SKBitmap? Icon { get; set; }

    /// <summary>
    /// The colour the icon is drawn in (its alpha kept as a mask), or null to draw the icon in its
    /// own colours. The handler sets it as MAUI's Windows handler tints: font icons always, file
    /// icons when the item has an IconColor.
    /// </summary>
    public Color? IconTintColor { get; set; }

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

    /// <summary>The icon load in flight (cancelled by a newer one or a disconnect).</summary>
    internal CancellationTokenSource? IconLoad { get; set; }

    /// <summary>The image-source result that owns <see cref="Icon"/>; released with it.</summary>
    internal IImageSourceServiceResult<SKBitmap>? IconResult { get; private set; }

    /// <summary>Shows a loaded icon, releasing the one it replaces.</summary>
    internal void SetIcon(IImageSourceServiceResult<SKBitmap>? result)
    {
        var old = IconResult;
        IconResult = result;
        Icon = result?.Value;
        if (old != null && !ReferenceEquals(old, result))
            old.Dispose();
    }

    /// <summary>Cancels a load in flight and releases the icon.</summary>
    internal void ReleaseIcon()
    {
        IconLoad?.Cancel();
        IconLoad = null;
        SetIcon(null);
    }

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
