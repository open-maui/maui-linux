// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Buttons;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>An SfButton styled with a Pressed visual state shows it while the pointer is down (Strikeline's PIN pad).</summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionButtonPressedStateTests
{
    [Fact]
    public void An_SfButton_shows_its_Pressed_state_while_held()
    {
        var pressed = new VisualState { Name = "Pressed" };
        pressed.Setters.Add(new Setter { Property = SfButton.BackgroundProperty, Value = new SolidColorBrush(Colors.Red) });
        var group = new VisualStateGroup { Name = "CommonStates" };
        group.States.Add(new VisualState { Name = "Normal" });
        group.States.Add(pressed);
        var button = new SfButton { Text = "1", WidthRequest = 80, HeightRequest = 80, CornerRadius = 40, StrokeThickness = 1,
            Background = Colors.Transparent, TextColor = Colors.Black, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        VisualStateManager.GetVisualStateGroups(button).Add(group);
        using var host = new CompatHost(new ContentPage { BackgroundColor = Colors.White, Content = new Grid { Children = { button } } }, b => b.UseLinuxSyncfusion(), 300, 300);
        host.Render();
        host.Render();

        var (x, y) = CompatHost.CenterOf(button);
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        (button.Background as SolidColorBrush)?.Color.Should().Be(Colors.Red, "the Pressed state applies");
        var (r, g, b, _) = host.DisplayWindow.PixelAt((int)x + 20, (int)y + 20);
        (r > 200 && g < 60 && b < 60).Should().BeTrue($"the pressed background is drawn, got {r},{g},{b}");

        host.DisplayWindow.RaisePointerReleased(x, y);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }
        (button.Background as SolidColorBrush)?.Color.Should().NotBe(Colors.Red, "released, it goes back to Normal");
    }
}
