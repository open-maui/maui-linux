// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Core;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// The stamp annotation view of Syncfusion's annotation layer (Syncfusion.Maui.Core's
/// <c>StampView</c>, the base of SfPdfViewer's stamp annotations) lays out and draws on Linux, and
/// sizes its stamp text to its bounds as soon as it is laid out, as the Windows build's
/// <c>OnSizeAllocated</c> does (the neutral build waited for the next draw). SfPdfViewer itself is
/// not referenced here: adding it to this project would add its own package to the stub scan.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class SyncfusionStampViewTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    // AnnotationConstants.SelectionBorderMargin (10) and SelectionBorderThickness (2), on each side.
    private const double Inset = 2 * (10 + 2);

    [Fact]
    public void A_stamp_sizes_its_text_when_it_is_laid_out()
    {
        SfView stamp = null!;
        Label label = null!;
        using var host = new CompatHost(_ =>
        {
            var type = typeof(SfView).Assembly.GetType("Syncfusion.Maui.Core.Annotations.StampView")!;
            stamp = (SfView)Activator.CreateInstance(type, Any, null, new object[] { 1f }, null)!;
            label = new Label { Text = "APPROVED", FontSize = 20, TextColor = Colors.DarkGreen, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
            // The content shape UpdateStampSize sizes: Grid > Grid (selection padding) > Frame > Label.
            var padded = new Grid { Padding = 12 };
#pragma warning disable CS0618 // Frame: the one content shape UpdateStampSize acts on
            padded.Children.Add(new Frame { Content = label, Padding = 0, BorderColor = Colors.DarkGreen, HasShadow = false });
#pragma warning restore CS0618
            var outer = new Grid();
            outer.Children.Add(padded);
            stamp.Children.Add(outer);
            stamp.WidthRequest = 200;
            stamp.HeightRequest = 80;
            stamp.HorizontalOptions = LayoutOptions.Start;
            stamp.VerticalOptions = LayoutOptions.Start;
            return new ContentPage { BackgroundColor = Colors.White, Content = new Grid { Children = { stamp } } };
        }, b => b.UseLinuxSyncfusion(), 400, 300);
        for (int i = 0; i < 3; i++)
            host.Render();

        label.WidthRequest.Should().Be(200 - Inset);
        label.HeightRequest.Should().Be(80 - Inset);
        host.CountPixelsNear(new SKColor(0, 100, 0), new SKRectI(0, 0, 200, 80), tolerance: 120).Should().BeGreaterThan(50, "the stamp's text and frame draw");

        // A new size reaches the text on layout alone, before any draw.
        stamp.WidthRequest = 300;
        ((IView)stamp).Measure(400, 300);
        ((IView)stamp).Arrange(new Rect(0, 0, 300, 80));
        label.WidthRequest.Should().Be(300 - Inset, "the Windows StampView.OnSizeAllocated sizes the stamp on layout");
        host.Render();
        host.Render();
        label.FontSize.Should().BeGreaterThan(20, "the text scales with the width once laid out");
    }
}
