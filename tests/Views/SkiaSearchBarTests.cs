// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaSearchBarTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var searchBar = new SkiaSearchBar();

        // Assert
        searchBar.Text.Should().BeEmpty();
        searchBar.Placeholder.Should().Be("Search...");
        searchBar.IsFocusable.Should().BeTrue();
    }

    [Fact]
    public void Text_WhenSet_UpdatesProperty()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();

        // Act
        searchBar.Text = "Hello World";

        // Assert
        searchBar.Text.Should().Be("Hello World");
    }

    [Fact]
    public void Placeholder_WhenSet_UpdatesProperty()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();

        // Act
        searchBar.Placeholder = "Type to search...";

        // Assert
        searchBar.Placeholder.Should().Be("Type to search...");
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();
        var color = Microsoft.Maui.Graphics.Colors.Red;

        // Act
        searchBar.TextColor = color;

        // Assert
        searchBar.TextColor.Should().Be(color);
    }

    [Fact]
    public void PlaceholderColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();
        var color = Microsoft.Maui.Graphics.Colors.Gray;

        // Act
        searchBar.PlaceholderColor = color;

        // Assert
        searchBar.PlaceholderColor.Should().Be(color);
    }

    [Fact]
    public void SearchButtonPressed_EventCanBeSubscribed()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();
        var eventSubscribed = false;

        // Act
        searchBar.SearchButtonPressed += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse();
    }

    [Fact]
    public void TextChanged_EventCanBeSubscribed()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();
        var eventSubscribed = false;

        // Act
        searchBar.TextChanged += (s, e) => eventSubscribed = true;

        // Assert - Just verify we can subscribe without error
        eventSubscribed.Should().BeFalse();
    }

    [Fact]
    public void Draw_DoesNotThrow()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();
        searchBar.Bounds = new Rect(0, 0, 250, 40);

        using var surface = SKSurface.Create(new SKImageInfo(300, 100));
        var canvas = surface.Canvas;

        // Act & Assert
        var exception = Record.Exception(() => searchBar.Draw(canvas));
        exception.Should().BeNull();
    }

    [Fact]
    public void Measure_ReturnsPositiveSize()
    {
        // Arrange
        var searchBar = new SkiaSearchBar();

        // Act
        var size = searchBar.Measure(new Size(1000, 1000));

        // Assert
        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }
}
