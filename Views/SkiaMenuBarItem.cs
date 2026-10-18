// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A top-level menu of a <see cref="SkiaMenuBar"/>: its text and items.
/// </summary>
public class SkiaMenuBarItem
{
    public string Text { get; set; } = string.Empty;

    public List<SkiaMenuItem> Items { get; } = new List<SkiaMenuItem>();

    internal SKRect Bounds { get; set; }
}
