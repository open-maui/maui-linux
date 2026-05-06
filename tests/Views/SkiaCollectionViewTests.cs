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
