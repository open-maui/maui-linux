// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

[Obsolete("Use SkiaMenuItem; this type is removed in OpenMaui 11.0.")]
public class MenuItem
{
    public string Text { get; set; } = string.Empty;

    public string? Shortcut { get; set; }

    public bool IsSeparator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsChecked { get; set; }

    public string? IconSource { get; set; }

    public List<MenuItem> SubItems { get; } = new List<MenuItem>();

    public event EventHandler? Clicked;

    internal void OnClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// An item of a Skia menu (<see cref="SkiaMenuBar"/>, <see cref="SkiaMenuFlyout"/>): text, shortcut, check state and sub-items.
/// (The name <c>MenuItem</c> is also a MAUI Controls type; this is the name to use.)
/// </summary>
#pragma warning disable CS0618 // legacy base kept for binary compatibility until OpenMaui 11.0
public class SkiaMenuItem : MenuItem
#pragma warning restore CS0618
{
}
