// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// An SfComboBox's body (the text field and the clear and drop-down buttons,
/// which Syncfusion adds after the first layout) draws inside the control.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionComboOffsetTests
{
    [Fact]
    public void A_combo_box_draws_its_content_inside_itself()
    {
        var combo = new Syncfusion.Maui.Inputs.SfComboBox
        {
            WidthRequest = 220, HeightRequest = 44, Margin = new Thickness(60, 50, 0, 0),
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            ItemsSource = new[] { "Alpha", "Bravo" }, SelectedIndex = 0,
        };
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new VerticalStackLayout { combo } }, b => b.UseLinuxSyncfusion(), 400, 200);
        for (int i = 0; i < 4; i++) host.Render();
        if (Environment.GetEnvironmentVariable("COMBO_FRAME") is { Length: > 0 } frame) host.SaveFrame(frame);

        ((Microsoft.Maui.Platform.SkiaLayoutView)CompatHost.PlatformOf(combo)).Children.Should().NotBeEmpty();
        var dark = new SKColor(40, 40, 40);
        // The text field (60..216) shows the selected text, left in the control.
        host.CountPixelsNear(dark, new SKRectI(64, 54, 160, 90), tolerance: 120).Should().BeGreaterThan(30, "the selected text draws in the text field");
        // The drop-down arrow draws in its button (248..280, 56..88).
        host.CountPixelsNear(dark, new SKRectI(250, 58, 278, 86), tolerance: 120).Should().BeGreaterThan(5, "the drop-down arrow draws in its button");
        // Nothing is drawn shifted below or right of the control.
        host.CountPixelsNot(SKColors.White, new SKRectI(0, 96, 400, 200)).Should().Be(0);
        host.CountPixelsNot(SKColors.White, new SKRectI(284, 0, 400, 200)).Should().Be(0);
    }
}
