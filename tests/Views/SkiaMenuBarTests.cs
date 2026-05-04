// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class SkiaMenuBarTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var menuBar = new SkiaMenuBar();

        Assert.Empty(menuBar.Items);
        Assert.Equal(28f, menuBar.BarHeight);
        Assert.Equal(13f, menuBar.FontSize);
    }

    [Fact]
    public void Items_CanAddMenuBarItem()
    {
        var menuBar = new SkiaMenuBar();
        var item = new MenuBarItem { Text = "File" };

        menuBar.Items.Add(item);

        Assert.Single(menuBar.Items);
        Assert.Equal("File", menuBar.Items[0].Text);
    }

    [Fact]
    public void MenuBarItem_CanAddMenuItems()
    {
        var menuBarItem = new MenuBarItem { Text = "Edit" };
        menuBarItem.Items.Add(new MenuItem { Text = "Cut" });
        menuBarItem.Items.Add(new MenuItem { Text = "Copy" });
        menuBarItem.Items.Add(new MenuItem { Text = "Paste" });

        Assert.Equal(3, menuBarItem.Items.Count);
    }

    [Fact]
    public void MenuItem_CanHaveShortcut()
    {
        var item = new MenuItem { Text = "Save", Shortcut = "Ctrl+S" };

        Assert.Equal("Ctrl+S", item.Shortcut);
    }

    [Fact]
    public void MenuItem_CanBeSeparator()
    {
        var item = new MenuItem { IsSeparator = true };

        Assert.True(item.IsSeparator);
    }

    [Fact]
    public void MenuItem_IsEnabled_DefaultsToTrue()
    {
        var item = new MenuItem { Text = "Test" };

        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void MenuItem_CanBeDisabled()
    {
        var item = new MenuItem { Text = "Test", IsEnabled = false };

        Assert.False(item.IsEnabled);
    }

    [Fact]
    public void MenuItem_CanBeChecked()
    {
        var item = new MenuItem { Text = "Option", IsChecked = true };

        Assert.True(item.IsChecked);
    }

    [Fact]
    public void MenuItem_CanHaveIcon()
    {
        var item = new MenuItem { Text = "Open", IconSource = "open.png" };

        Assert.Equal("open.png", item.IconSource);
    }

    [Fact]
    public void MenuItem_CanHaveSubItems()
    {
        var item = new MenuItem { Text = "Recent" };
        item.SubItems.Add(new MenuItem { Text = "File1.txt" });
        item.SubItems.Add(new MenuItem { Text = "File2.txt" });

        Assert.Equal(2, item.SubItems.Count);
    }

    [Fact]
    public void MenuItem_ClickedEvent_CanBeSubscribed()
    {
        var item = new MenuItem { Text = "Test" };
        bool clicked = false;

        item.Clicked += (s, e) => clicked = true;

        Assert.False(clicked); // Event not raised yet
    }

    [Fact]
    public void BarHeight_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.BarHeight = 32f;

        Assert.Equal(32f, menuBar.BarHeight);
    }

    [Fact]
    public void FontSize_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.FontSize = 14f;

        Assert.Equal(14f, menuBar.FontSize);
    }

    [Fact]
    public void ItemPadding_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.ItemPadding = 16f;

        Assert.Equal(16f, menuBar.ItemPadding);
    }

    [Fact]
    public void BackgroundColor_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.BackgroundColor = SKColors.White;

        Assert.Equal(SKColors.White, menuBar.BackgroundColor);
    }

    [Fact]
    public void TextColor_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.TextColor = SKColors.Black;

        Assert.Equal(SKColors.Black, menuBar.TextColor);
    }

    [Fact]
    public void HoverBackgroundColor_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.HoverBackgroundColor = SKColors.LightGray;

        Assert.Equal(SKColors.LightGray, menuBar.HoverBackgroundColor);
    }

    [Fact]
    public void ActiveBackgroundColor_CanBeSet()
    {
        var menuBar = new SkiaMenuBar();

        menuBar.ActiveBackgroundColor = SKColors.DarkGray;

        Assert.Equal(SKColors.DarkGray, menuBar.ActiveBackgroundColor);
    }

    [Fact]
    public void Measure_ReturnsBarHeight()
    {
        var menuBar = new SkiaMenuBar();
        menuBar.BarHeight = 30f;

        var size = menuBar.Measure(new SKSize(800, 600));

        Assert.Equal(30f, size.Height);
    }

    [Fact]
    public void Measure_ReturnsAvailableWidth()
    {
        var menuBar = new SkiaMenuBar();

        var size = menuBar.Measure(new SKSize(800, 600));

        Assert.Equal(800f, size.Width);
    }

    [Fact]
    public void HitTest_WithinBounds_ReturnsMenuBar()
    {
        var menuBar = new SkiaMenuBar();
        menuBar.Arrange(new SKRect(0, 0, 800, 28));

        var hit = menuBar.HitTest(400, 14);

        Assert.Equal(menuBar, hit);
    }

    [Fact]
    public void HitTest_OutsideBounds_ReturnsNull()
    {
        var menuBar = new SkiaMenuBar();
        menuBar.Arrange(new SKRect(0, 0, 800, 28));

        var hit = menuBar.HitTest(400, 50);

        Assert.Null(hit);
    }
}

public class SkiaMenuFlyoutTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var flyout = new SkiaMenuFlyout();

        Assert.Empty(flyout.Items);
        Assert.Equal(13f, flyout.FontSize);
        Assert.Equal(28f, flyout.ItemHeight);
        Assert.Equal(180f, flyout.MinWidth);
    }

    [Fact]
    public void Items_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();
        flyout.Items = new List<MenuItem>
        {
            new() { Text = "Item1" },
            new() { Text = "Item2" }
        };

        Assert.Equal(2, flyout.Items.Count);
    }

    [Fact]
    public void Position_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.Position = new SKPoint(100, 200);

        Assert.Equal(100, flyout.Position.X);
        Assert.Equal(200, flyout.Position.Y);
    }

    [Fact]
    public void BackgroundColor_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.BackgroundColor = SKColors.White;

        Assert.Equal(SKColors.White, flyout.BackgroundColor);
    }

    [Fact]
    public void TextColor_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.TextColor = SKColors.Black;

        Assert.Equal(SKColors.Black, flyout.TextColor);
    }

    [Fact]
    public void DisabledTextColor_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.DisabledTextColor = new SKColor(160, 160, 160);

        Assert.Equal(new SKColor(160, 160, 160), flyout.DisabledTextColor);
    }

    [Fact]
    public void HoverBackgroundColor_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.HoverBackgroundColor = SKColors.LightBlue;

        Assert.Equal(SKColors.LightBlue, flyout.HoverBackgroundColor);
    }

    [Fact]
    public void SeparatorColor_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.SeparatorColor = SKColors.Gray;

        Assert.Equal(SKColors.Gray, flyout.SeparatorColor);
    }

    [Fact]
    public void ItemHeight_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.ItemHeight = 32f;

        Assert.Equal(32f, flyout.ItemHeight);
    }

    [Fact]
    public void SeparatorHeight_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.SeparatorHeight = 12f;

        Assert.Equal(12f, flyout.SeparatorHeight);
    }

    [Fact]
    public void MinWidth_CanBeSet()
    {
        var flyout = new SkiaMenuFlyout();

        flyout.MinWidth = 200f;

        Assert.Equal(200f, flyout.MinWidth);
    }

    [Fact]
    public void ItemClickedEvent_CanBeSubscribed()
    {
        var flyout = new SkiaMenuFlyout();
        MenuItem? clickedItem = null;

        flyout.ItemClicked += (s, e) => clickedItem = e.Item;

        Assert.Null(clickedItem);
    }
}

public class MenuItemClickedEventArgsTests
{
    [Fact]
    public void Constructor_SetsItem()
    {
        var item = new MenuItem { Text = "Test" };
        var args = new MenuItemClickedEventArgs(item);

        Assert.Equal(item, args.Item);
    }
}
