// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;

/// <summary>
/// The platform drives MAUI's own VisualStateManager: CommonStates declared
/// in a Style (Normal, PointerOver, Pressed, Focused, Disabled) follow the
/// Skia view's pointer/focus interaction and the resulting setters reach the
/// platform view.
/// </summary>
public class VisualStateBridgeTests
{
    private static readonly Color NormalColor = Colors.Blue;
    private static readonly Color PressedColor = Colors.Red;
    private static readonly Color PointerOverColor = Colors.Green;
    private static readonly Color DisabledColor = Colors.Gray;
    private static readonly Color FocusedColor = Colors.Yellow;

    private static Style CommonStatesStyle(Type targetType, bool includeFocused = false, bool includePressed = true)
    {
        var states = new VisualStateGroup { Name = "CommonStates" };
        states.States.Add(State("Normal", NormalColor));
        if (includePressed) states.States.Add(State("Pressed", PressedColor));
        states.States.Add(State("PointerOver", PointerOverColor));
        states.States.Add(State("Disabled", DisabledColor));
        if (includeFocused) states.States.Add(State("Focused", FocusedColor));

        return new Style(targetType)
        {
            Setters =
            {
                new Setter
                {
                    Property = VisualStateManager.VisualStateGroupsProperty,
                    Value = new VisualStateGroupList { states }
                }
            }
        };
    }

    private static VisualState State(string name, Color background) => new()
    {
        Name = name,
        Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = background } }
    };

    private static PointerEventArgs Pointer() => new(1, 1, PointerButton.Left);

    [Fact]
    public void Button_StyleStates_FollowPlatformPressHoverAndDisable()
    {
        var button = new Button { Text = "Go", Style = CommonStatesStyle(typeof(Button)) };
        var platform = HeadlessMauiContext.Realize<SkiaButton>(button);

        VisualStateBridge.IsAttached(platform).Should().BeTrue();
        platform.BackgroundColor.Should().Be(NormalColor, "the style's Normal state applies on creation");

        platform.OnPointerEntered(Pointer());
        button.BackgroundColor.Should().Be(PointerOverColor);
        platform.BackgroundColor.Should().Be(PointerOverColor);

        platform.OnPointerPressed(Pointer());
        button.IsPressed.Should().BeTrue();
        button.BackgroundColor.Should().Be(PressedColor);
        platform.BackgroundColor.Should().Be(PressedColor);

        platform.OnPointerReleased(Pointer());
        button.IsPressed.Should().BeFalse();
        platform.BackgroundColor.Should().Be(PointerOverColor, "release while still hovered returns to PointerOver, not Normal");

        platform.OnPointerExited(Pointer());
        platform.BackgroundColor.Should().Be(NormalColor);

        button.IsEnabled = false;
        platform.BackgroundColor.Should().Be(DisabledColor);

        platform.OnPointerEntered(Pointer());
        platform.BackgroundColor.Should().Be(DisabledColor, "hover must not override Disabled");

        button.IsEnabled = true;
        platform.BackgroundColor.Should().Be(NormalColor);
    }

    [Fact]
    public void Button_PressWithoutHover_ReturnsToNormalOnRelease()
    {
        var button = new Button { Style = CommonStatesStyle(typeof(Button)) };
        var platform = HeadlessMauiContext.Realize<SkiaButton>(button);

        platform.OnPointerPressed(Pointer());
        platform.BackgroundColor.Should().Be(PressedColor);

        platform.OnPointerReleased(Pointer());
        platform.BackgroundColor.Should().Be(NormalColor);
    }

    [Fact]
    public void Entry_PlatformFocus_SetsIsFocusedRaisesEventsAndAppliesFocusedState()
    {
        var entry = new Entry { Style = CommonStatesStyle(typeof(Entry), includeFocused: true, includePressed: false) };
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);

        int focused = 0, unfocused = 0;
        entry.Focused += (_, _) => focused++;
        entry.Unfocused += (_, _) => unfocused++;

        platform.OnFocusGained();
        entry.IsFocused.Should().BeTrue();
        focused.Should().Be(1);
        platform.BackgroundColor.Should().Be(FocusedColor);

        platform.OnFocusLost();
        entry.IsFocused.Should().BeFalse();
        unfocused.Should().Be(1);
        platform.BackgroundColor.Should().Be(NormalColor);
    }

    [Fact]
    public void Label_BasePointerEvents_DrivePointerOverAndPressed()
    {
        var label = new Label { Text = "hover me", Style = CommonStatesStyle(typeof(Label)) };
        var platform = HeadlessMauiContext.Realize<SkiaLabel>(label);

        platform.OnPointerEntered(Pointer());
        platform.BackgroundColor.Should().Be(PointerOverColor);

        platform.OnPointerPressed(Pointer());
        platform.BackgroundColor.Should().Be(PressedColor);

        platform.OnPointerReleased(Pointer());
        platform.BackgroundColor.Should().Be(PointerOverColor);

        platform.OnPointerExited(Pointer());
        platform.BackgroundColor.Should().Be(NormalColor);
    }

    [Fact]
    public void CheckBox_CheckedState_SurvivesHover()
    {
        // MAUI's CheckBox.ChangeVisualState prefers IsChecked over PointerOver
        // while enabled; the bridge must feed MAUI's state machine rather than
        // force CommonStates itself, or hovering would clobber IsChecked.
        var states = new VisualStateGroup { Name = "CommonStates" };
        states.States.Add(State("Normal", NormalColor));
        states.States.Add(State("PointerOver", PointerOverColor));
        states.States.Add(State(CheckBox.IsCheckedVisualState, PressedColor));
        var checkBox = new CheckBox
        {
            Style = new Style(typeof(CheckBox))
            {
                Setters = { new Setter { Property = VisualStateManager.VisualStateGroupsProperty, Value = new VisualStateGroupList { states } } }
            }
        };
        var platform = HeadlessMauiContext.Realize<SkiaCheckBox>(checkBox);

        platform.OnPointerEntered(Pointer());
        platform.BackgroundColor.Should().Be(PointerOverColor);
        platform.OnPointerExited(Pointer());
        platform.BackgroundColor.Should().Be(NormalColor);

        checkBox.IsChecked = true;
        platform.BackgroundColor.Should().Be(PressedColor);

        platform.OnPointerEntered(Pointer());
        platform.BackgroundColor.Should().Be(PressedColor, "IsChecked wins over PointerOver in MAUI's CheckBox");

        platform.OnPointerExited(Pointer());
        checkBox.IsChecked = false;
        platform.BackgroundColor.Should().Be(NormalColor);
    }

    [Fact]
    public void Attach_IsIdempotent_AndDisconnectDetaches()
    {
        var button = new Button { Style = CommonStatesStyle(typeof(Button)) };
        var handler = HeadlessMauiContext.CreateHandler(button);
        var platform = (SkiaButton)handler.PlatformView!;

        // The generic hook in the handler-creation path may call Attach again
        // for the same pair; that must not double-subscribe.
        VisualStateBridge.Attach(button, platform);
        VisualStateBridge.Attach(button, platform);

        platform.OnPointerEntered(Pointer());
        platform.BackgroundColor.Should().Be(PointerOverColor);
        platform.OnPointerExited(Pointer());
        platform.BackgroundColor.Should().Be(NormalColor);

        handler.DisconnectHandler();
        VisualStateBridge.IsAttached(platform).Should().BeFalse();

        platform.OnPointerEntered(Pointer());
        button.BackgroundColor.Should().Be(NormalColor, "a detached platform view no longer drives the element");
    }

    [Fact]
    public void Attach_IgnoresNonVisualElements()
    {
        var platform = new SkiaLabel();
        VisualStateBridge.Attach(null, platform);
        VisualStateBridge.IsAttached(platform).Should().BeFalse();
        VisualStateBridge.Detach(platform);
    }
}
