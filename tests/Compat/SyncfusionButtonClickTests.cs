// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Buttons;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>An SfButton raises Clicked for a press and release on it.</summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionButtonClickTests
{
    [Fact]
    public void A_press_and_release_on_an_SfButton_raises_Clicked()
    {
        int clicks = 0;
        var button = new SfButton { Text = "Begin", WidthRequest = 200, HeightRequest = 50, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End };
        button.Clicked += (_, _) => clicks++;
        using var host = new CompatHost(new ContentPage { Content = new Grid { Children = { button } } }, b => b.UseLinuxSyncfusion(), 500, 400);
        host.Render();
        host.Render();

        var (x, y) = CompatHost.CenterOf(button);
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        clicks.Should().Be(1);
    }
}
