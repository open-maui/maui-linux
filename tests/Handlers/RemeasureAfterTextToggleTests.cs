// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

[Collection("LinuxApplication.Current")]
public class RemeasureAfterTextToggleTests
{
    [Fact]
    public void A_wrapped_button_measures_its_new_text_at_once()
    {
        var button = new Button { Text = "⚙ Refresh all" };
        var item = new ContentView { Content = new Border { Content = button, Padding = new Thickness(0) } };
        var row = new HorizontalStackLayout { Children = { item } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = row }, withEngine: true);
        host.Context.Render();
        var full = ((IView)item).Measure(double.PositiveInfinity, 40).Width;

        button.Text = "⚙";
        host.Context.Render();
        var icon = ((IView)item).Measure(double.PositiveInfinity, 40).Width;
        icon.Should().BeLessThan(full - 20);

        // Expand and measure in the same pass, before any render (MAToolbar's fit pass).
        button.Text = "⚙ Refresh all";
        var expanded = ((IView)item).Measure(double.PositiveInfinity, 40).Width;
        expanded.Should().BeApproximately(full, 0.5);

        // And fold again in the same pass.
        button.Text = "⚙";
        ((IView)item).Measure(double.PositiveInfinity, 40).Width.Should().BeApproximately(icon, 0.5);
    }
}
