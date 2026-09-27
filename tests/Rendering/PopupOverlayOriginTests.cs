// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// Popup overlays (pickers, drop-downs, Syncfusion popups) draw in view-tree
/// coordinates, below a client-drawn title bar, where their input already
/// lands; before, they drew the title bar's height above it.
/// </summary>
[Collection("LinuxApplication.Current")]
public class PopupOverlayOriginTests
{
    [Fact]
    public void Overlays_draw_below_a_client_drawn_title_bar()
    {
        var owner = new SkiaLabel();
        SKMatrix seen = default;
        SkiaView.RegisterPopupOverlay(owner, c => seen = c.TotalMatrix);
        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(100, 100));
            SkiaView.PopupDpiScale = 2f;
            SkiaView.PopupOriginY = 32f;
            SkiaView.DrawPopupOverlays(surface.Canvas, null);

            seen.ScaleY.Should().Be(2f);
            seen.TransY.Should().Be(64f); // 32 logical at scale 2
        }
        finally
        {
            SkiaView.PopupDpiScale = 1f;
            SkiaView.PopupOriginY = 0f;
            SkiaView.UnregisterPopupOverlay(owner);
        }
    }
}
