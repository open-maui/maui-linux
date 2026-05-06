// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class MenuBarItem
{
    public string Text { get; set; } = string.Empty;

    public List<MenuItem> Items { get; } = new List<MenuItem>();

    internal SKRect Bounds { get; set; }
}
