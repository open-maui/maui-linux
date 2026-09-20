// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

public class SemanticMapperTests
{
    [Fact]
    public void Apply_CopiesSemanticDescriptionHintAndHeading()
    {
        var label = new Label { Text = "Hello" };
        SemanticProperties.SetDescription(label, "Greeting");
        SemanticProperties.SetHint(label, "Says hello");
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level2);
        var platform = new SkiaLabel();

        SemanticMapper.Apply(label, platform);

        platform.SemanticName.Should().Be("Greeting");
        platform.SemanticHint.Should().Be("Says hello");
        platform.SemanticHeadingLevel.Should().Be(SemanticHeadingLevel.Level2);
        platform.SemanticDescription.Should().BeNull();
    }

    [Fact]
    public void Apply_NoSemantics_LeavesEverythingNull()
    {
        var button = new Button { Text = "Go" };
        var platform = new SkiaButton();

        SemanticMapper.Apply(button, platform);

        platform.SemanticName.Should().BeNull();
        platform.SemanticHint.Should().BeNull();
        platform.SemanticDescription.Should().BeNull();
        platform.SemanticHeadingLevel.Should().Be(SemanticHeadingLevel.None);
        platform.IsInAccessibleTree.Should().BeNull();
    }

    [Fact]
    public void Apply_AutomationPropertiesName_FillsSemanticName()
    {
        var entry = new Entry();
        AutomationProperties.SetName(entry, "User name");
        AutomationProperties.SetHelpText(entry, "Enter your login");
        AutomationProperties.SetIsInAccessibleTree(entry, true);
        var platform = new SkiaEntry();

        SemanticMapper.Apply(entry, platform);

        platform.SemanticName.Should().Be("User name");
        platform.SemanticDescription.Should().Be("Enter your login");
        platform.SemanticHint.Should().Be("Enter your login");
        platform.IsInAccessibleTree.Should().BeTrue();
    }

    [Fact]
    public void Apply_SemanticDescription_WinsOverAutomationName()
    {
        var label = new Label();
        AutomationProperties.SetName(label, "Old name");
        SemanticProperties.SetDescription(label, "New description");
        SemanticProperties.SetHint(label, "Semantic hint");
        AutomationProperties.SetHelpText(label, "Help text");
        var platform = new SkiaLabel();

        SemanticMapper.Apply(label, platform);

        platform.SemanticName.Should().Be("New description");
        platform.SemanticHint.Should().Be("Semantic hint");
        platform.SemanticDescription.Should().Be("Help text");
    }

    [Fact]
    public void Apply_LabeledBy_UsesLabelText()
    {
        var caption = new Label { Text = "Volume" };
        var slider = new Slider();
        AutomationProperties.SetLabeledBy(slider, caption);
        var platform = new SkiaSlider();

        SemanticMapper.Apply(slider, platform);

        platform.SemanticName.Should().Be("Volume");
    }

    [Fact]
    public void Apply_LabeledBy_PrefersLabelSemanticDescription()
    {
        var caption = new Label { Text = "Vol" };
        SemanticProperties.SetDescription(caption, "Volume level");
        var slider = new Slider();
        AutomationProperties.SetLabeledBy(slider, caption);
        var platform = new SkiaSlider();

        SemanticMapper.Apply(slider, platform);

        platform.SemanticName.Should().Be("Volume level");
    }

    [Fact]
    public void Apply_TracksLaterPropertyChanges()
    {
        var label = new Label();
        var platform = new SkiaLabel();
        SemanticMapper.Apply(label, platform);
        platform.SemanticName.Should().BeNull();

        SemanticProperties.SetDescription(label, "Late");
        platform.SemanticName.Should().Be("Late");

        SemanticProperties.SetHint(label, "Late hint");
        platform.SemanticHint.Should().Be("Late hint");

        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level1);
        platform.SemanticHeadingLevel.Should().Be(SemanticHeadingLevel.Level1);

        AutomationProperties.SetIsInAccessibleTree(label, false);
        platform.IsInAccessibleTree.Should().BeFalse();
    }

    [Fact]
    public void Apply_ClearingDescription_FallsBackToAutomationName()
    {
        var label = new Label();
        AutomationProperties.SetName(label, "Fallback");
        SemanticProperties.SetDescription(label, "Primary");
        var platform = new SkiaLabel();
        SemanticMapper.Apply(label, platform);
        platform.SemanticName.Should().Be("Primary");

        SemanticProperties.SetDescription(label, null);

        platform.SemanticName.Should().Be("Fallback");
    }

    [Fact]
    public void Detach_StopsTrackingChanges()
    {
        var label = new Label();
        var platform = new SkiaLabel();
        SemanticMapper.Apply(label, platform);
        SemanticProperties.SetDescription(label, "Before");
        platform.SemanticName.Should().Be("Before");

        SemanticMapper.Detach(label);
        SemanticProperties.SetDescription(label, "After");

        platform.SemanticName.Should().Be("Before");
        var detachAgain = () => SemanticMapper.Detach(label);
        detachAgain.Should().NotThrow();
    }

    [Fact]
    public void Apply_Twice_IsIdempotent()
    {
        var label = new Label();
        SemanticProperties.SetDescription(label, "Once");
        var platform = new SkiaLabel();

        SemanticMapper.Apply(label, platform);
        SemanticMapper.Apply(label, platform);
        SemanticProperties.SetDescription(label, "Twice");

        platform.SemanticName.Should().Be("Twice");
    }

    [Fact]
    public void Apply_EmptyStrings_TreatedAsUnset()
    {
        var label = new Label();
        SemanticProperties.SetDescription(label, "");
        AutomationProperties.SetName(label, "Real");
        var platform = new SkiaLabel();

        SemanticMapper.Apply(label, platform);

        platform.SemanticName.Should().Be("Real");
    }

    [Fact]
    public void Apply_NullArguments_Throw()
    {
        var platform = new SkiaLabel();
        var nullView = () => SemanticMapper.Apply(null!, platform);
        nullView.Should().Throw<ArgumentNullException>();
        var nullPlatform = () => SemanticMapper.Apply(new Label(), null!);
        nullPlatform.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AccessibleName_ReflectsMappedSemantics()
    {
        var label = new Label { Text = "Body" };
        SemanticProperties.SetDescription(label, "Spoken name");
        SemanticProperties.SetHint(label, "Spoken hint");
        var platform = new SkiaLabel { Text = "Body" };

        SemanticMapper.Apply(label, platform);

        IAccessible accessible = platform;
        accessible.AccessibleName.Should().Be("Spoken name");
        accessible.AccessibleDescription.Should().Be("Spoken hint");
    }
}
