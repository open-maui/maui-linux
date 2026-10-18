// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

/// <summary>
/// An item of a Skia menu (<see cref="SkiaMenuBar"/>, <see cref="SkiaMenuFlyout"/>): text, shortcut, check state and sub-items.
/// </summary>
public class SkiaMenuItem
{
    public string Text { get; set; } = string.Empty;

    public string? Shortcut { get; set; }

    public bool IsSeparator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsChecked { get; set; }

    public string? IconSource { get; set; }

    public List<SkiaMenuItem> SubItems { get; } = new List<SkiaMenuItem>();

    public event EventHandler? Clicked;

    internal void OnClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
