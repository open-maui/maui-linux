// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaCollectionViewTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var collectionView = new SkiaCollectionView();

        // Assert
        collectionView.ItemsSource.Should().BeNull();
        collectionView.SelectionMode.Should().Be(SkiaSelectionMode.Single);
        collectionView.SelectedItem.Should().BeNull();
        collectionView.SelectedIndex.Should().Be(-1);
        collectionView.Header.Should().BeNull();
        collectionView.Footer.Should().BeNull();
        collectionView.SpanCount.Should().Be(1);
    }

    [Fact]
    public void ItemsSource_WhenSet_UpdatesProperty()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();
        var items = new List<string> { "Item 1", "Item 2", "Item 3" };

        // Act
        collectionView.ItemsSource = items;

        // Assert
        collectionView.ItemsSource.Should().BeSameAs(items);
    }

    [Fact]
    public void SelectionMode_WhenSet_UpdatesProperty()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();

        // Act
        collectionView.SelectionMode = SkiaSelectionMode.Multiple;

        // Assert
        collectionView.SelectionMode.Should().Be(SkiaSelectionMode.Multiple);
    }

    [Fact]
    public void SelectionMode_WhenSetToNone_UpdatesProperty()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();

        // Act
        collectionView.SelectionMode = SkiaSelectionMode.None;

        // Assert
        collectionView.SelectionMode.Should().Be(SkiaSelectionMode.None);
    }

    [Fact]
    public void Header_WhenSet_UpdatesProperty()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();

        // Act
        collectionView.Header = "My Header";

        // Assert
        collectionView.Header.Should().Be("My Header");
    }

    [Fact]
    public void Footer_WhenSet_UpdatesProperty()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();

        // Act
        collectionView.Footer = "My Footer";

        // Assert
        collectionView.Footer.Should().Be("My Footer");
    }

    [Fact]
    public void Draw_DoesNotThrow_WhenEmpty()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();
        collectionView.Bounds = new Rect(0, 0, 300, 400);

        using var surface = SKSurface.Create(new SKImageInfo(400, 500));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => collectionView.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Draw_DoesNotThrow_WithItems()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();
        collectionView.ItemsSource = new List<string> { "Item 1", "Item 2", "Item 3" };
        collectionView.Bounds = new Rect(0, 0, 300, 400);

        using var surface = SKSurface.Create(new SKImageInfo(400, 500));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => collectionView.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void FirstDraw_AfterItemsSourceReset_PositionsRowsWithMeasuredHeights()
    {
        // Regression test for the "flash" after a navigation pop: apps commonly
        // refresh with `ItemsSource = null; ItemsSource = items;` in OnAppearing.
        // The FIRST frame drawn afterwards must already position rows using the
        // measured item heights - not the default ItemHeight (44) with a
        // corrective second frame.
        var collectionView = new SkiaCollectionView();
        collectionView.ItemViewCreator = item => new SkiaBoxView
        {
            WidthRequest = 100,
            HeightRequest = 80
        };

        var items = new List<string> { "A", "B", "C" };
        collectionView.ItemsSource = items;

        // App idiom: null-then-set refresh (both happen before the next frame)
        collectionView.ItemsSource = null;
        collectionView.ItemsSource = items;

        collectionView.Bounds = new Rect(0, 0, 300, 600);
        using var surface = SKSurface.Create(new SKImageInfo(300, 600));

        // Act - a single draw = one frame
        collectionView.Draw(surface.Canvas);

        // Assert - views for all visible items exist after one frame...
        var view0 = collectionView.GetItemView(0);
        var view1 = collectionView.GetItemView(1);
        view0.Should().NotBeNull();
        view1.Should().NotBeNull();

        // ...and the second row starts below the FULL measured height of the
        // first row (80), not the default ItemHeight (44).
        view1!.Bounds.Top.Should().BeApproximately(80, 1.5);
    }

    [Fact]
    public void FirstDraw_WithItemViewCreator_DoesNotProduceEmptyFrame()
    {
        // A synchronous null-then-set must never leave the control empty at
        // draw time: the first paint reflects the final source.
        var collectionView = new SkiaCollectionView();
        collectionView.ItemViewCreator = item => new SkiaBoxView
        {
            WidthRequest = 100,
            HeightRequest = 60
        };

        var items = new List<string> { "One", "Two" };
        collectionView.ItemsSource = items;
        collectionView.ItemsSource = null;
        collectionView.ItemsSource = items;

        collectionView.Bounds = new Rect(0, 0, 300, 400);
        using var surface = SKSurface.Create(new SKImageInfo(300, 400));

        collectionView.Draw(surface.Canvas);

        collectionView.GetItemView(0).Should().NotBeNull("first frame after a refresh must paint the new items");
        collectionView.GetItemView(1).Should().NotBeNull();
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var collectionView = new SkiaCollectionView();

        // Act
        var size = collectionView.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThanOrEqualTo(0);
        size.Height.Should().BeGreaterThanOrEqualTo(0);
    }
}
