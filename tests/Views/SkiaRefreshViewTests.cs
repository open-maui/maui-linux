// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class SkiaRefreshViewTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var refreshView = new SkiaRefreshView();

        Assert.False(refreshView.IsRefreshing);
        Assert.Null(refreshView.Content);
        Assert.True(refreshView.IsEnabled);
    }

    [Fact]
    public void IsRefreshing_CanBeSet()
    {
        var refreshView = new SkiaRefreshView();

        refreshView.IsRefreshing = true;

        Assert.True(refreshView.IsRefreshing);
    }

    [Fact]
    public void Content_CanBeSet()
    {
        var refreshView = new SkiaRefreshView();
        var content = new SkiaLabel { Text = "Test" };

        refreshView.Content = content;

        Assert.Equal(content, refreshView.Content);
    }

    [Fact]
    public void Content_AddsChildToTree()
    {
        var refreshView = new SkiaRefreshView();
        var content = new SkiaLabel { Text = "Test" };

        refreshView.Content = content;

        Assert.Equal(refreshView, content.Parent);
    }

    [Fact]
    public void Content_RemovesPreviousChild()
    {
        var refreshView = new SkiaRefreshView();
        var content1 = new SkiaLabel { Text = "First" };
        var content2 = new SkiaLabel { Text = "Second" };

        refreshView.Content = content1;
        refreshView.Content = content2;

        Assert.Null(content1.Parent);
        Assert.Equal(refreshView, content2.Parent);
    }

    [Fact]
    public void RefreshThreshold_DefaultValue()
    {
        var refreshView = new SkiaRefreshView();

        Assert.Equal(80f, refreshView.RefreshThreshold);
    }

    [Fact]
    public void RefreshThreshold_CanBeSet()
    {
        var refreshView = new SkiaRefreshView();

        refreshView.RefreshThreshold = 100f;

        Assert.Equal(100f, refreshView.RefreshThreshold);
    }

    [Fact]
    public void RefreshColor_DefaultsToBlue()
    {
        var refreshView = new SkiaRefreshView();

        Assert.Equal(new SKColor(33, 150, 243), refreshView.RefreshColor);
    }

    [Fact]
    public void RefreshColor_CanBeSet()
    {
        var refreshView = new SkiaRefreshView();

        refreshView.RefreshColor = SKColors.Red;

        Assert.Equal(SKColors.Red, refreshView.RefreshColor);
    }

    [Fact]
    public void RefreshBackgroundColor_DefaultsToWhite()
    {
        var refreshView = new SkiaRefreshView();

        Assert.Equal(SKColors.White, refreshView.RefreshBackgroundColor);
    }

    [Fact]
    public void RefreshBackgroundColor_CanBeSet()
    {
        var refreshView = new SkiaRefreshView();

        refreshView.RefreshBackgroundColor = SKColors.LightGray;

        Assert.Equal(SKColors.LightGray, refreshView.RefreshBackgroundColor);
    }

    [Fact]
    public void RefreshingEvent_CanBeSubscribed()
    {
        var refreshView = new SkiaRefreshView();
        bool eventRaised = false;

        refreshView.Refreshing += (s, e) => eventRaised = true;

        Assert.False(eventRaised); // Not raised yet
    }

    [Fact]
    public void HitTest_ReturnsCorrectView()
    {
        var refreshView = new SkiaRefreshView();
        var content = new SkiaLabel { Text = "Test" };
        refreshView.Content = content;
        refreshView.Arrange(new SKRect(0, 0, 200, 400));

        var hit = refreshView.HitTest(100, 200);

        Assert.NotNull(hit);
    }

    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var refreshView = new SkiaRefreshView();

        Assert.True(refreshView.IsVisible);
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        var refreshView = new SkiaRefreshView();

        Assert.True(refreshView.IsEnabled);
    }
}
