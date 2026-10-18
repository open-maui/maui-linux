// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests;

public class SkiaIndicatorViewTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var indicator = new SkiaIndicatorView();

        Assert.Equal(0, indicator.Count);
        Assert.Equal(0, indicator.Position);
        Assert.Equal(SkiaIndicatorShape.Circle, indicator.IndicatorShape);
    }

    [Fact]
    public void Count_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.Count = 5;

        Assert.Equal(5, indicator.Count);
    }

    [Fact]
    public void Position_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();
        indicator.Count = 5;

        indicator.Position = 3;

        Assert.Equal(3, indicator.Position);
    }

    [Fact]
    public void Position_ClampedToCount()
    {
        var indicator = new SkiaIndicatorView();
        indicator.Count = 3;

        indicator.Position = 10;

        Assert.Equal(2, indicator.Position); // Clamped to max (Count - 1)
    }

    [Fact]
    public void Position_ClampedToZero()
    {
        var indicator = new SkiaIndicatorView();
        indicator.Count = 3;

        indicator.Position = -5;

        Assert.Equal(0, indicator.Position);
    }

    [Fact]
    public void IndicatorColor_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.IndicatorColor = Colors.Grey;

        Assert.Equal(Colors.Grey, indicator.IndicatorColor);
    }

    [Fact]
    public void SelectedIndicatorColor_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.SelectedIndicatorColor = Colors.Blue;

        Assert.Equal(Colors.Blue, indicator.SelectedIndicatorColor);
    }

    [Fact]
    public void IndicatorSize_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.IndicatorSize = 12.0;

        Assert.Equal(12.0, indicator.IndicatorSize);
    }

    [Fact]
    public void SelectedIndicatorSize_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.SelectedIndicatorSize = 16.0;

        Assert.Equal(16.0, indicator.SelectedIndicatorSize);
    }

    [Fact]
    public void IndicatorSpacing_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.IndicatorSpacing = 10.0;

        Assert.Equal(10.0, indicator.IndicatorSpacing);
    }

    [Fact]
    public void IndicatorShape_CanBeSetToSquare()
    {
        var indicator = new SkiaIndicatorView();

        indicator.IndicatorShape = SkiaIndicatorShape.Square;

        Assert.Equal(SkiaIndicatorShape.Square, indicator.IndicatorShape);
    }

    [Fact]
    public void IndicatorShape_CanBeSetToRoundedSquare()
    {
        var indicator = new SkiaIndicatorView();

        indicator.IndicatorShape = SkiaIndicatorShape.RoundedSquare;

        Assert.Equal(SkiaIndicatorShape.RoundedSquare, indicator.IndicatorShape);
    }

    [Fact]
    public void IndicatorShape_CanBeSetToDiamond()
    {
        var indicator = new SkiaIndicatorView();

        indicator.IndicatorShape = SkiaIndicatorShape.Diamond;

        Assert.Equal(SkiaIndicatorShape.Diamond, indicator.IndicatorShape);
    }

    [Fact]
    public void ShowBorder_DefaultsToFalse()
    {
        var indicator = new SkiaIndicatorView();

        Assert.False(indicator.ShowBorder);
    }

    [Fact]
    public void ShowBorder_CanBeEnabled()
    {
        var indicator = new SkiaIndicatorView();

        indicator.ShowBorder = true;

        Assert.True(indicator.ShowBorder);
    }

    [Fact]
    public void BorderColor_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.BorderColor = Colors.Black;

        Assert.Equal(Colors.Black, indicator.BorderColor);
    }

    [Fact]
    public void BorderWidth_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.BorderWidth = 2.0;

        Assert.Equal(2.0, indicator.BorderWidth);
    }

    [Fact]
    public void MaximumVisible_DefaultValue()
    {
        var indicator = new SkiaIndicatorView();

        Assert.Equal(10, indicator.MaximumVisible);
    }

    [Fact]
    public void MaximumVisible_CanBeSet()
    {
        var indicator = new SkiaIndicatorView();

        indicator.MaximumVisible = 5;

        Assert.Equal(5, indicator.MaximumVisible);
    }

    [Fact]
    public void HideSingle_DefaultsToTrue()
    {
        var indicator = new SkiaIndicatorView();

        Assert.True(indicator.HideSingle);
    }

    [Fact]
    public void HideSingle_CanBeDisabled()
    {
        var indicator = new SkiaIndicatorView();

        indicator.HideSingle = false;

        Assert.False(indicator.HideSingle);
    }

    [Fact]
    public void Count_AdjustsPosition_WhenReduced()
    {
        var indicator = new SkiaIndicatorView();
        indicator.Count = 5;
        indicator.Position = 4;

        indicator.Count = 3;

        Assert.Equal(2, indicator.Position); // Adjusted to max valid
    }

    [Fact]
    public void HitTest_OnIndicator_ReturnsView()
    {
        var indicator = new SkiaIndicatorView();
        indicator.Count = 5;
        indicator.IndicatorSize = 10.0;
        indicator.IndicatorSpacing = 8.0;
        indicator.Arrange(new Rect(0, 0, 200, 20));

        var hit = indicator.HitTest(100, 10);

        Assert.NotNull(hit);
    }

    [Fact]
    public void IsVisible_DefaultsToTrue()
    {
        var indicator = new SkiaIndicatorView();

        Assert.True(indicator.IsVisible);
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        var indicator = new SkiaIndicatorView();

        Assert.True(indicator.IsEnabled);
    }
}

public class IndicatorShapeTests
{
    [Fact]
    public void AllShapesAreDefined()
    {
        Assert.Equal(SkiaIndicatorShape.Circle, (SkiaIndicatorShape)0);
        Assert.Equal(SkiaIndicatorShape.Square, (SkiaIndicatorShape)1);
        Assert.Equal(SkiaIndicatorShape.RoundedSquare, (SkiaIndicatorShape)2);
        Assert.Equal(SkiaIndicatorShape.Diamond, (SkiaIndicatorShape)3);
    }
}
