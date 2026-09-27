// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Xunit;

namespace OpenMaui.Compat.Tests;

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
        // The entry and buttons Syncfusion adds after the first layout are drawn, inside the control.
        ((Microsoft.Maui.Platform.SkiaLayoutView)CompatHost.PlatformOf(combo)).Children.Should().NotBeEmpty();
        var sb = new System.Text.StringBuilder();
        void Walk(Microsoft.Maui.Platform.SkiaView v, int d)
        {
            sb.AppendLine($"{new string(' ', d * 2)}{v.GetType().Name}/{v.MauiView?.GetType().Name} {v.Bounds} vis={v.IsVisible}");
            if (v is Microsoft.Maui.Platform.SkiaLayoutView l) foreach (var c in l.Children) Walk(c, d + 1);
        }
        Walk(CompatHost.PlatformOf(combo), 0);
        File.WriteAllText("/tmp/claude-1000/-home-logikonline-Documents-Gitea/f31e418a-42c5-4638-9e39-ccb0289874a1/scratchpad/combo-tree.txt", sb.ToString());
        host.CountPixelsNot(SkiaSharp.SKColors.White, new SkiaSharp.SKRectI(64, 54, 276, 90)).Should().BeGreaterThan(30, "the selected text draws");
        // Nothing drawn below or right of the control.
        host.CountPixelsNot(SkiaSharp.SKColors.White, new SkiaSharp.SKRectI(0, 96, 400, 200)).Should().Be(0);
    }
}
