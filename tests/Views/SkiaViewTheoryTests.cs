// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

public class SkiaLabelTheoryTests
{
    [Theory]
    [InlineData(FontAttributes.None)]
    [InlineData(FontAttributes.Bold)]
    [InlineData(FontAttributes.Italic)]
    [InlineData(FontAttributes.Bold | FontAttributes.Italic)]
    public void FontAttributes_AllValues_CanBeSet(FontAttributes attributes)
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.FontAttributes = attributes;

        // Assert
        label.FontAttributes.Should().Be(attributes);
    }

    [Theory]
    [InlineData(TextAlignment.Start)]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.End)]
    public void HorizontalTextAlignment_AllValues_CanBeSet(TextAlignment alignment)
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.HorizontalTextAlignment = alignment;

        // Assert
        label.HorizontalTextAlignment.Should().Be(alignment);
    }

    [Theory]
    [InlineData(TextAlignment.Start)]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.End)]
    public void VerticalTextAlignment_AllValues_CanBeSet(TextAlignment alignment)
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.VerticalTextAlignment = alignment;

        // Assert
        label.VerticalTextAlignment.Should().Be(alignment);
    }

    [Theory]
    [InlineData(TextDecorations.None)]
    [InlineData(TextDecorations.Underline)]
    [InlineData(TextDecorations.Strikethrough)]
    public void TextDecorations_AllValues_CanBeSet(TextDecorations decorations)
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.TextDecorations = decorations;

        // Assert
        label.TextDecorations.Should().Be(decorations);
    }

    [Theory]
    [InlineData(LineBreakMode.NoWrap)]
    [InlineData(LineBreakMode.WordWrap)]
    [InlineData(LineBreakMode.CharacterWrap)]
    [InlineData(LineBreakMode.HeadTruncation)]
    [InlineData(LineBreakMode.TailTruncation)]
    [InlineData(LineBreakMode.MiddleTruncation)]
    public void LineBreakMode_AllValues_CanBeSet(LineBreakMode mode)
    {
        // Arrange
        var label = new SkiaLabel();

        // Act
        label.LineBreakMode = mode;

        // Assert
        label.LineBreakMode.Should().Be(mode);
    }
}

public class SkiaViewVisibilityTheoryTests
{
    public static IEnumerable<object[]> ViewInstances()
    {
        yield return new object[] { new SkiaLabel() };
        yield return new object[] { new SkiaButton() };
        yield return new object[] { new SkiaCheckBox() };
        yield return new object[] { new SkiaEntry() };
        yield return new object[] { new SkiaProgressBar() };
    }

    [Theory]
    [MemberData(nameof(ViewInstances))]
    public void AllViews_DefaultVisible(SkiaView view)
    {
        // Assert
        view.IsVisible.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ViewInstances))]
    public void AllViews_DefaultEnabled(SkiaView view)
    {
        // Assert
        view.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ViewInstances))]
    public void AllViews_CanBeDisabled(SkiaView view)
    {
        // Act
        view.IsEnabled = false;

        // Assert
        view.IsEnabled.Should().BeFalse();
    }
}

public class SkiaSliderTheoryTests
{
    [Theory]
    [InlineData(0, 100, 50, 50)]
    [InlineData(0, 100, -10, 0)]
    [InlineData(0, 100, 200, 100)]
    [InlineData(0, 1, 0.5, 0.5)]
    [InlineData(10, 20, 15, 15)]
    public void Value_ClampsToRange(double min, double max, double setValue, double expectedValue)
    {
        // Arrange
        var slider = new SkiaSlider();

        // Always set Maximum before Minimum when Minimum > default (1.0)
        slider.Maximum = max;
        slider.Minimum = min;

        // Act
        slider.Value = setValue;

        // Assert
        slider.Value.Should().Be(expectedValue);
    }
}

public class SkiaEntryTextTheoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("Hello World with spaces")]
    [InlineData("Special chars: !@#$%^&*()")]
    [InlineData("Unicode: \u3053\u3093\u306B\u3061\u306F")]
    [InlineData("This is a very long string that exceeds one hundred characters in length to test how the SkiaEntry control handles lengthy text input values properly")]
    public void Text_VariousStrings_SetCorrectly(string text)
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.Text = text;

        // Assert
        entry.Text.Should().Be(text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("Hello World with spaces")]
    [InlineData("Special chars: !@#$%^&*()")]
    [InlineData("Unicode: \u3053\u3093\u306B\u3061\u306F")]
    [InlineData("This is a very long string that exceeds one hundred characters in length to test how the SkiaEntry control handles lengthy placeholder text values properly")]
    public void Placeholder_VariousStrings_SetCorrectly(string placeholder)
    {
        // Arrange
        var entry = new SkiaEntry();

        // Act
        entry.Placeholder = placeholder;

        // Assert
        entry.Placeholder.Should().Be(placeholder);
    }
}
