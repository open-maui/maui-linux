// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

[Obsolete("Use SkiaMenuBarItem; this type is removed in OpenMaui 11.0.")]
public class MenuBarItem
{
    public string Text { get; set; } = string.Empty;

    public List<MenuItem> Items { get; } = new List<MenuItem>();

    internal SKRect Bounds { get; set; }
}

/// <summary>
/// A top-level menu of a <see cref="SkiaMenuBar"/>: its text and items.
/// (The name <c>MenuBarItem</c> is also a MAUI Controls type; this is the name to use.)
/// </summary>
#pragma warning disable CS0618 // legacy base kept for binary compatibility until OpenMaui 11.0
public class SkiaMenuBarItem : MenuBarItem
#pragma warning restore CS0618
{
}
