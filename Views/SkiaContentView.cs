// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered container for a single content child (ContentView).
/// Inherits layout, measure, arrange, and draw from SkiaLayoutView.
/// </summary>
public class SkiaContentView : SkiaLayoutView
{
    // SkiaLayoutView already handles:
    // - MeasureOverride (measures children)
    // - ArrangeOverride (arranges children)
    // - OnDraw (draws background + children)
    // - AddChild / RemoveChild / ClearChildren
    // ContentView just needs to exist as a concrete type for the handler.
}
