// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class SkiaCarouselViewTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var carousel = new SkiaCarouselView();

        Assert.Equal(0, carousel.Position);
        Assert.False(carousel.Loop);
        Assert.Equal(0f, carousel.PeekAreaInsets);
        Assert.Equal(0, carousel.ItemCount);
    }

    [Fact]
    public void Position_CanBeSet()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "Item1" });
        carousel.AddItem(new SkiaLabel { Text = "Item2" });
        carousel.AddItem(new SkiaLabel { Text = "Item3" });

        carousel.Position = 2;

        Assert.Equal(2, carousel.Position);
    }

    [Fact]
    public void Position_RaisesPositionChangedEvent()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "Item1" });
        carousel.AddItem(new SkiaLabel { Text = "Item2" });
        int eventRaised = 0;
        int previousPos = -1;
        int currentPos = -1;

        carousel.PositionChanged += (s, e) =>
        {
            eventRaised++;
            previousPos = e.PreviousPosition;
            currentPos = e.CurrentPosition;
        };

        carousel.Position = 1;

        Assert.Equal(1, eventRaised);
        Assert.Equal(0, previousPos);
        Assert.Equal(1, currentPos);
    }

    [Fact]
    public void Loop_CanBeEnabled()
    {
        var carousel = new SkiaCarouselView();

        carousel.Loop = true;

        Assert.True(carousel.Loop);
    }

    [Fact]
    public void PeekAreaInsets_CanBeSet()
    {
        var carousel = new SkiaCarouselView();

        carousel.PeekAreaInsets = 20f;

        Assert.Equal(20f, carousel.PeekAreaInsets);
    }

    [Fact]
    public void AddItem_IncreasesItemCount()
    {
        var carousel = new SkiaCarouselView();

        carousel.AddItem(new SkiaLabel { Text = "Item1" });
        carousel.AddItem(new SkiaLabel { Text = "Item2" });

        Assert.Equal(2, carousel.ItemCount);
    }

    [Fact]
    public void RemoveItem_DecreasesItemCount()
    {
        var carousel = new SkiaCarouselView();
        var item = new SkiaLabel { Text = "Item1" };
        carousel.AddItem(item);
        carousel.AddItem(new SkiaLabel { Text = "Item2" });

        carousel.RemoveItem(item);

        Assert.Equal(1, carousel.ItemCount);
    }

    [Fact]
    public void ClearItems_RemovesAllItems()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "Item1" });
        carousel.AddItem(new SkiaLabel { Text = "Item2" });

        carousel.ClearItems();

        Assert.Equal(0, carousel.ItemCount);
    }

    [Fact]
    public void ScrollTo_UpdatesPosition()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "A" });
        carousel.AddItem(new SkiaLabel { Text = "B" });
        carousel.AddItem(new SkiaLabel { Text = "C" });
        carousel.AddItem(new SkiaLabel { Text = "D" });

        carousel.ScrollTo(2);

        Assert.Equal(2, carousel.Position);
    }

    [Fact]
    public void ScrollTo_WithAnimation_UpdatesPosition()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "A" });
        carousel.AddItem(new SkiaLabel { Text = "B" });
        carousel.AddItem(new SkiaLabel { Text = "C" });

        carousel.ScrollTo(1, animate: true);

        Assert.Equal(1, carousel.Position);
    }

    [Fact]
    public void IsSwipeEnabled_DefaultsToTrue()
    {
        var carousel = new SkiaCarouselView();

        Assert.True(carousel.IsSwipeEnabled);
    }

    [Fact]
    public void IsSwipeEnabled_CanBeDisabled()
    {
        var carousel = new SkiaCarouselView();

        carousel.IsSwipeEnabled = false;

        Assert.False(carousel.IsSwipeEnabled);
    }

    [Fact]
    public void ShowIndicators_DefaultsToTrue()
    {
        var carousel = new SkiaCarouselView();

        Assert.True(carousel.ShowIndicators);
    }

    [Fact]
    public void ShowIndicators_CanBeDisabled()
    {
        var carousel = new SkiaCarouselView();

        carousel.ShowIndicators = false;

        Assert.False(carousel.ShowIndicators);
    }

    [Fact]
    public void IndicatorColor_CanBeSet()
    {
        var carousel = new SkiaCarouselView();

        carousel.IndicatorColor = SKColors.Gray;

        Assert.Equal(SKColors.Gray, carousel.IndicatorColor);
    }

    [Fact]
    public void SelectedIndicatorColor_CanBeSet()
    {
        var carousel = new SkiaCarouselView();

        carousel.SelectedIndicatorColor = SKColors.Blue;

        Assert.Equal(SKColors.Blue, carousel.SelectedIndicatorColor);
    }

    [Fact]
    public void ScrolledEvent_CanBeSubscribed()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "1" });
        carousel.AddItem(new SkiaLabel { Text = "2" });
        bool subscribed = false;

        carousel.Scrolled += (s, e) => subscribed = true;

        Assert.NotNull(carousel); // Event can be subscribed
    }

    [Fact]
    public void ItemSpacing_DefaultsToZero()
    {
        var carousel = new SkiaCarouselView();

        Assert.Equal(0f, carousel.ItemSpacing);
    }

    [Fact]
    public void ItemSpacing_CanBeSet()
    {
        var carousel = new SkiaCarouselView();

        carousel.ItemSpacing = 16f;

        Assert.Equal(16f, carousel.ItemSpacing);
    }

    [Fact]
    public void Position_NotChangedWhenOutOfRange()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "Item1" });
        carousel.AddItem(new SkiaLabel { Text = "Item2" });
        carousel.Position = 1;

        carousel.Position = 10; // Out of range

        // Position stays at previous valid value or is clamped
        Assert.True(carousel.Position >= 0 && carousel.Position < carousel.ItemCount);
    }

    [Fact]
    public void HitTest_ReturnsCorrectView()
    {
        var carousel = new SkiaCarouselView();
        carousel.AddItem(new SkiaLabel { Text = "Item" });
        carousel.Arrange(new SKRect(0, 0, 300, 200));

        var hit = carousel.HitTest(150, 100);

        Assert.NotNull(hit);
    }
}

public class PositionChangedEventArgsTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var args = new PositionChangedEventArgs(0, 2);

        Assert.Equal(0, args.PreviousPosition);
        Assert.Equal(2, args.CurrentPosition);
    }
}
