// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaNavigationPageTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var navPage = new SkiaNavigationPage();

        // Assert
        navPage.CurrentPage.Should().BeNull();
        navPage.IsVisible.Should().BeTrue();
        navPage.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void BarBackgroundColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var navPage = new SkiaNavigationPage();
        var color = Microsoft.Maui.Graphics.Colors.DarkBlue;

        // Act
        navPage.BarBackgroundColor = color;

        // Assert
        navPage.BarBackgroundColor.Should().Be(color);
    }

    [Fact]
    public void BarTextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var navPage = new SkiaNavigationPage();
        var color = Microsoft.Maui.Graphics.Colors.Yellow;

        // Act
        navPage.BarTextColor = color;

        // Assert
        navPage.BarTextColor.Should().Be(color);
    }

    [Fact]
    public void Pop_Animated_FiresAppearingOnRestoredPageBeforeItIsPresented()
    {
        // OnAppearing must complete BEFORE the restored page's first presented
        // frame (matching other platforms). Pop() therefore fires it synchronously
        // before starting the pop animation - i.e. before Pop() returns.
        var rootPage = new SkiaPage { Title = "Root" };
        var detailPage = new SkiaPage { Title = "Detail" };
        var navPage = new SkiaNavigationPage(rootPage);
        navPage.Push(detailPage, animated: false);

        var appearedBeforeReturn = false;
        rootPage.Appearing += (s, e) => appearedBeforeReturn = true;

        navPage.Pop(animated: true);

        appearedBeforeReturn.Should().BeTrue(
            "the restored page's OnAppearing must run before the first frame that shows it");
    }

    [Fact]
    public void Push_Animated_FiresAppearingOnNewPageBeforeItIsPresented()
    {
        var rootPage = new SkiaPage { Title = "Root" };
        var detailPage = new SkiaPage { Title = "Detail" };
        var navPage = new SkiaNavigationPage(rootPage);

        var appearedBeforeReturn = false;
        detailPage.Appearing += (s, e) => appearedBeforeReturn = true;

        navPage.Push(detailPage, animated: true);

        appearedBeforeReturn.Should().BeTrue(
            "the pushed page's OnAppearing must run before the first frame that shows it");
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var navPage = new SkiaNavigationPage();
        navPage.Bounds = new Rect(0, 0, 400, 600);

        using var surface = SKSurface.Create(new SKImageInfo(400, 600));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => navPage.Draw(canvas));
        exception.Should().BeNull();
    }
}
