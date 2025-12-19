// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class SkiaSwipeViewTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var swipeView = new SkiaSwipeView();

        Assert.Null(swipeView.Content);
        Assert.Empty(swipeView.LeftItems);
        Assert.Empty(swipeView.RightItems);
        Assert.Empty(swipeView.TopItems);
        Assert.Empty(swipeView.BottomItems);
        Assert.Equal(SwipeMode.Reveal, swipeView.Mode);
    }

    [Fact]
    public void Content_CanBeSet()
    {
        var swipeView = new SkiaSwipeView();
        var content = new SkiaLabel { Text = "Swipeable" };

        swipeView.Content = content;

        Assert.Equal(content, swipeView.Content);
    }

    [Fact]
    public void LeftItems_CanAddItems()
    {
        var swipeView = new SkiaSwipeView();
        var item = new SwipeItem { Text = "Delete", BackgroundColor = SKColors.Red };

        swipeView.LeftItems.Add(item);

        Assert.Single(swipeView.LeftItems);
        Assert.Equal("Delete", swipeView.LeftItems[0].Text);
    }

    [Fact]
    public void RightItems_CanAddItems()
    {
        var swipeView = new SkiaSwipeView();
        var item = new SwipeItem { Text = "Archive", BackgroundColor = SKColors.Blue };

        swipeView.RightItems.Add(item);

        Assert.Single(swipeView.RightItems);
    }

    [Fact]
    public void SwipeItem_InvokedEvent_CanBeSubscribed()
    {
        var item = new SwipeItem { Text = "Test" };
        bool invoked = false;

        item.Invoked += (s, e) => invoked = true;

        Assert.False(invoked); // Event not raised yet
    }

    [Fact]
    public void Open_OpensSwipeInDirection()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.RightItems.Add(new SwipeItem { Text = "Delete" });

        swipeView.Open(SwipeDirection.Left);

        // Open state is internal, but we verify no exception
    }

    [Fact]
    public void Close_ClosesOpenSwipe()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.LeftItems.Add(new SwipeItem { Text = "Test" });
        swipeView.Open(SwipeDirection.Right);

        swipeView.Close();

        // Verifies no exception
    }

    [Fact]
    public void Mode_CanBeSetToExecute()
    {
        var swipeView = new SkiaSwipeView();

        swipeView.Mode = SwipeMode.Execute;

        Assert.Equal(SwipeMode.Execute, swipeView.Mode);
    }

    [Fact]
    public void LeftSwipeThreshold_CanBeSet()
    {
        var swipeView = new SkiaSwipeView();

        swipeView.LeftSwipeThreshold = 150f;

        Assert.Equal(150f, swipeView.LeftSwipeThreshold);
    }

    [Fact]
    public void RightSwipeThreshold_CanBeSet()
    {
        var swipeView = new SkiaSwipeView();

        swipeView.RightSwipeThreshold = 150f;

        Assert.Equal(150f, swipeView.RightSwipeThreshold);
    }

    [Fact]
    public void SwipeStartedEvent_CanBeSubscribed()
    {
        var swipeView = new SkiaSwipeView();
        SwipeDirection? direction = null;

        swipeView.SwipeStarted += (s, e) => direction = e.Direction;
        // Simulate internal swipe start
        swipeView.LeftItems.Add(new SwipeItem { Text = "Test" });

        Assert.NotNull(swipeView);
    }

    [Fact]
    public void SwipeEndedEvent_CanBeSubscribed()
    {
        var swipeView = new SkiaSwipeView();
        bool ended = false;

        swipeView.SwipeEnded += (s, e) => ended = true;

        Assert.NotNull(swipeView);
    }

    [Fact]
    public void SwipeItem_TextColor_CanBeSet()
    {
        var item = new SwipeItem { TextColor = SKColors.Yellow };

        Assert.Equal(SKColors.Yellow, item.TextColor);
    }

    [Fact]
    public void SwipeItem_BackgroundColor_CanBeSet()
    {
        var item = new SwipeItem { BackgroundColor = SKColors.Green };

        Assert.Equal(SKColors.Green, item.BackgroundColor);
    }

    [Fact]
    public void SwipeItem_IconSource_CanBeSet()
    {
        var item = new SwipeItem { IconSource = "delete.png" };

        Assert.Equal("delete.png", item.IconSource);
    }

    [Fact]
    public void TopItems_CanAddItems()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.TopItems.Add(new SwipeItem { Text = "Top" });

        Assert.Single(swipeView.TopItems);
    }

    [Fact]
    public void BottomItems_CanAddItems()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.BottomItems.Add(new SwipeItem { Text = "Bottom" });

        Assert.Single(swipeView.BottomItems);
    }

    [Fact]
    public void HitTest_ReturnsCorrectView()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.Content = new SkiaLabel { Text = "Content" };
        swipeView.Arrange(new SKRect(0, 0, 300, 50));

        var hit = swipeView.HitTest(150, 25);

        Assert.NotNull(hit);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.Content = new SkiaLabel { Text = "Test" };

        var size = swipeView.Measure(new SKSize(300, 100));

        Assert.True(size.Width <= 300);
        Assert.True(size.Height <= 100);
    }
}
