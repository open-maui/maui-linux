// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

#region LabelPropertyMappingTests

public class LabelPropertyMappingTests
{
    [Fact]
    public void Text_NullMapsToEmpty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.Text = null ?? "";

        // Assert
        label.Text.Should().BeEmpty();
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();
        var color = Colors.Red;

        // Act
        label.TextColor = color;

        // Assert
        label.TextColor.Should().Be(color);
    }

    [Fact]
    public void FontSize_WhenPositive_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontSize = 20.0;

        // Assert
        label.FontSize.Should().Be(20.0);
        label.FontSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FontFamily_WhenNotEmpty_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontFamily = "Roboto";

        // Assert
        label.FontFamily.Should().Be("Roboto");
        label.FontFamily.Should().NotBeEmpty();
    }

    [Fact]
    public void FontAttributes_Bold_SetsCorrectly()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontAttributes = FontAttributes.Bold;

        // Assert
        label.FontAttributes.Should().Be(FontAttributes.Bold);
    }

    [Fact]
    public void HorizontalTextAlignment_Center_MapsCorrectly()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.HorizontalTextAlignment = TextAlignment.Center;

        // Assert
        label.HorizontalTextAlignment.Should().Be(TextAlignment.Center);
    }

    [Fact]
    public void VerticalTextAlignment_End_MapsCorrectly()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.VerticalTextAlignment = TextAlignment.End;

        // Assert
        label.VerticalTextAlignment.Should().Be(TextAlignment.End);
    }

    [Fact]
    public void TextDecorations_Underline_SetsCorrectly()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.TextDecorations = TextDecorations.Underline;

        // Assert
        label.TextDecorations.Should().Be(TextDecorations.Underline);
    }

    [Fact]
    public void LineHeight_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.LineHeight = 1.5;

        // Assert
        label.LineHeight.Should().Be(1.5);
    }

    [Fact]
    public void CharacterSpacing_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.CharacterSpacing = 2.0;

        // Assert
        label.CharacterSpacing.Should().Be(2.0);
    }

    [Fact]
    public void MaxLines_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.MaxLines = 3;

        // Assert
        label.MaxLines.Should().Be(3);
    }

    [Fact]
    public void Padding_WhenSet_UpdatesProperty()
    {
        // Arrange
        var label = new SkiaLabel();
        var padding = new Thickness(5, 10, 15, 20);

        // Act
        label.Padding = padding;

        // Assert
        label.Padding.Left.Should().Be(5);
        label.Padding.Top.Should().Be(10);
        label.Padding.Right.Should().Be(15);
        label.Padding.Bottom.Should().Be(20);
    }
}

#endregion

#region EntryPropertyMappingTests

public class EntryPropertyMappingTests
{
    [Fact]
    public void Text_NullMapsToEmpty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.Text = null ?? "";

        // Assert
        entry.Text.Should().BeEmpty();
    }

    [Fact]
    public void Placeholder_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.Placeholder = "Enter text here";

        // Assert
        entry.Placeholder.Should().Be("Enter text here");
    }

    [Fact]
    public void IsReadOnly_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.IsReadOnly = true;

        // Assert
        entry.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void MaxLength_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.MaxLength = 50;

        // Assert
        entry.MaxLength.Should().Be(50);
    }

    [Fact]
    public void IsPassword_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.IsPassword = true;

        // Assert
        entry.IsPassword.Should().BeTrue();
    }

    [Fact]
    public void CursorPosition_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();
        entry.Text = "Hello World";

        // Act
        entry.CursorPosition = 5;

        // Assert
        entry.CursorPosition.Should().Be(5);
    }

    [Fact]
    public void SelectionLength_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();
        entry.Text = "Hello World";

        // Act
        entry.SelectionLength = 5;

        // Assert
        entry.SelectionLength.Should().Be(5);
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var entry = new SkiaEntry();
        var color = Colors.Blue;

        // Act
        entry.TextColor = color;

        // Assert
        entry.TextColor.Should().Be(color);
    }

    [Fact]
    public void HorizontalTextAlignment_Center_MapsCorrectly()
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.HorizontalTextAlignment = TextAlignment.Center;

        // Assert
        entry.HorizontalTextAlignment.Should().Be(TextAlignment.Center);
    }
}

#endregion

#region ButtonPropertyMappingTests

public class ButtonPropertyMappingTests
{
    [Fact]
    public void Text_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();

        // Act
        button.Text = "Click Me";

        // Assert
        button.Text.Should().Be("Click Me");
    }

    [Fact]
    public void TextColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();
        var color = Colors.White;

        // Act
        button.TextColor = color;

        // Assert
        button.TextColor.Should().Be(color);
    }

    [Fact]
    public void IsEnabled_WhenFalse_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();

        // Act
        button.IsEnabled = false;

        // Assert
        button.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void BorderColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();
        var color = Colors.DarkGray;

        // Act
        button.BorderColor = color;

        // Assert
        button.BorderColor.Should().Be(color);
    }

    [Fact]
    public void BorderWidth_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();

        // Act
        button.BorderWidth = 2.0;

        // Assert
        button.BorderWidth.Should().Be(2.0);
    }

    [Fact]
    public void CornerRadius_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();

        // Act
        button.CornerRadius = 10;

        // Assert
        button.CornerRadius.Should().Be(10);
    }

    [Fact]
    public void Padding_WhenSet_UpdatesProperty()
    {
        // Arrange
        var button = new SkiaButton();
        var padding = new Thickness(8, 4, 8, 4);

        // Act
        button.Padding = padding;

        // Assert
        button.Padding.Left.Should().Be(8);
        button.Padding.Top.Should().Be(4);
        button.Padding.Right.Should().Be(8);
        button.Padding.Bottom.Should().Be(4);
    }
}

#endregion

#region CheckBoxPropertyMappingTests

public class CheckBoxPropertyMappingTests
{
    [Fact]
    public void IsChecked_WhenTrue_UpdatesProperty()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();

        // Act
        checkBox.IsChecked = true;

        // Assert
        checkBox.IsChecked.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenFalse_UpdatesProperty()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();

        // Act
        checkBox.IsEnabled = false;

        // Assert
        checkBox.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Color_WhenSet_UpdatesProperty()
    {
        // Arrange
        var checkBox = new SkiaCheckBox();
        var color = Colors.Green;

        // Act
        checkBox.Color = color;

        // Assert
        checkBox.Color.Should().Be(color);
    }
}

#endregion

#region SliderPropertyMappingTests

public class SliderPropertyMappingTests
{
    [Fact]
    public void Minimum_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        slider.Maximum = 100.0; // Must set Maximum first since default is 1.0

        // Act
        slider.Minimum = 10.0;

        // Assert
        slider.Minimum.Should().Be(10.0);
    }

    [Fact]
    public void Maximum_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();

        // Act
        slider.Maximum = 200.0;

        // Assert
        slider.Maximum.Should().Be(200.0);
    }

    [Fact]
    public void Value_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        slider.Maximum = 100.0;

        // Act
        slider.Value = 50.0;

        // Assert
        slider.Value.Should().Be(50.0);
    }

    [Fact]
    public void MinimumTrackColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        var color = Colors.Orange;

        // Act
        slider.MinimumTrackColor = color;

        // Assert
        slider.MinimumTrackColor.Should().Be(color);
    }

    [Fact]
    public void MaximumTrackColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        var color = Colors.LightGray;

        // Act
        slider.MaximumTrackColor = color;

        // Assert
        slider.MaximumTrackColor.Should().Be(color);
    }

    [Fact]
    public void ThumbColor_WhenSet_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();
        var color = Colors.Purple;

        // Act
        slider.ThumbColor = color;

        // Assert
        slider.ThumbColor.Should().Be(color);
    }

    [Fact]
    public void IsEnabled_WhenFalse_UpdatesProperty()
    {
        // Arrange
        var slider = new SkiaSlider();

        // Act
        slider.IsEnabled = false;

        // Assert
        slider.IsEnabled.Should().BeFalse();
    }
}

#endregion
