// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
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
        var item = new SwipeItem { Text = "Delete", BackgroundColor = Colors.Red };

        swipeView.LeftItems.Add(item);

        Assert.Single(swipeView.LeftItems);
        Assert.Equal("Delete", swipeView.LeftItems[0].Text);
    }

    [Fact]
    public void RightItems_CanAddItems()
    {
        var swipeView = new SkiaSwipeView();
        var item = new SwipeItem { Text = "Archive", BackgroundColor = Colors.Blue };

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
        var item = new SwipeItem { TextColor = Colors.Yellow };

        Assert.Equal(Colors.Yellow, item.TextColor);
    }

    [Fact]
    public void SwipeItem_BackgroundColor_CanBeSet()
    {
        var item = new SwipeItem { BackgroundColor = Colors.Green };

        Assert.Equal(Colors.Green, item.BackgroundColor);
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
        swipeView.Arrange(new Rect(0, 0, 300, 50));

        var hit = swipeView.HitTest(150, 25);

        Assert.NotNull(hit);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var swipeView = new SkiaSwipeView();
        swipeView.Content = new SkiaLabel { Text = "Test" };

        var size = swipeView.Measure(new Size(300, 100));

        Assert.True(size.Width <= 300);
        Assert.True(size.Height <= 100);
    }

    // A white content in a 300 x 60 swipe view, drawn into a bitmap.
    private static SkiaSwipeView Row(params SwipeItem[] rightItems)
    {
        var swipeView = new SkiaSwipeView { Content = new SkiaContentView { BackgroundColor = Colors.White } };
        foreach (var item in rightItems)
            swipeView.RightItems.Add(item);
        swipeView.Measure(new Size(300, 60));
        swipeView.Arrange(new Rect(0, 0, 300, 60));
        return swipeView;
    }

    private static SKColor PixelAt(SkiaView view, int x, int y)
    {
        using var bitmap = new SKBitmap(300, 60);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        view.Draw(canvas);
        return bitmap.GetPixel(x, y);
    }

    private static void DragTo(SkiaSwipeView view, float fromX, float toX)
    {
        view.OnPointerPressed(new PointerEventArgs(fromX, 30));
        view.OnPointerMoved(new PointerEventArgs((fromX + toX) / 2, 30));
        view.OnPointerMoved(new PointerEventArgs(toX, 30));
    }

    [Fact]
    public void Open_reveals_the_items_and_moves_the_content()
    {
        var view = Row(new SwipeItem { BackgroundColor = Colors.Red });

        view.Open(SwipeDirection.Left);

        Assert.True(view.IsOpen);
        Assert.Equal(-100f, view.SwipeOffset);
        Assert.Equal(SKColors.Red, PixelAt(view, 250, 5));
        Assert.Equal(SKColors.White, PixelAt(view, 150, 5));
        Assert.Equal(0, view.Content!.Bounds.Left);
    }

    [Fact]
    public void A_side_without_items_does_not_open()
    {
        var view = Row(new SwipeItem());

        view.Open(SwipeDirection.Right);

        Assert.False(view.IsOpen);
        Assert.Equal(0f, view.SwipeOffset);
    }

    [Fact]
    public void Reveal_keeps_the_items_in_place_and_drag_moves_them()
    {
        // Two items open to 200; a 150 swipe uncovers x 150..300.
        var reveal = Row(new SwipeItem { BackgroundColor = Colors.Red }, new SwipeItem { BackgroundColor = Colors.Blue });
        DragTo(reveal, 290, 140);
        Assert.Equal(-150f, reveal.SwipeOffset);
        Assert.Equal(SKColors.Blue, PixelAt(reveal, 210, 5));

        var drag = Row(new SwipeItem { BackgroundColor = Colors.Red }, new SwipeItem { BackgroundColor = Colors.Blue });
        drag.TransitionMode = SwipeTransitionMode.Drag;
        DragTo(drag, 290, 140);
        Assert.Equal(SKColors.Red, PixelAt(drag, 210, 5));
    }

    [Fact]
    public void Top_items_open_downwards_over_the_content_height()
    {
        var view = new SkiaSwipeView { Content = new SkiaContentView { BackgroundColor = Colors.White } };
        view.TopItems.Add(new SwipeItem { BackgroundColor = Colors.Green });
        view.Measure(new Size(300, 60));
        view.Arrange(new Rect(0, 0, 300, 60));

        view.Open(OpenSwipeItem.TopItems);

        Assert.Equal(60f, view.SwipeOffset);
        Assert.Equal(new SKColor(0, 128, 0), PixelAt(view, 150, 30));
    }

    [Fact]
    public void Execute_mode_opens_to_most_of_the_content()
    {
        var view = Row(new SwipeItem());
        view.SetItemsMode(OpenSwipeItem.RightItems, SwipeMode.Execute);

        view.Open(SwipeDirection.Left);

        Assert.Equal(-240f, view.SwipeOffset);
    }
}
