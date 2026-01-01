// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SwipeItem
{
    public string Text { get; set; } = string.Empty;

    public string? IconSource { get; set; }

    public SKColor BackgroundColor { get; set; } = new SKColor(33, 150, 243);

    public SKColor TextColor { get; set; } = SKColors.White;

    public event EventHandler? Invoked;

    internal void OnInvoked()
    {
        Invoked?.Invoke(this, EventArgs.Empty);
    }
}
