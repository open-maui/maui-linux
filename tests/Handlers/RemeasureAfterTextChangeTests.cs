// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MAToolbar stands a compressed button back up by giving it its text again
/// and measuring it at once; the measure must see the text. A stale (icon
/// only) width made the expansion look like it fit, the next pass folded it
/// again, and the toolbar re-laid itself out forever.
/// </summary>
[Collection("LinuxApplication.Current")]
public class RemeasureAfterTextChangeTests
{
    [Fact]
    public void A_wrapped_button_measures_its_new_text_at_once()
    {
        var button = new Button { Text = null, Padding = new Thickness(12, 6) };
        var wrapper = new ContentView { Content = new Border { StrokeThickness = 0, Content = button } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new HorizontalStackLayout { wrapper } }, withEngine: true);
        host.Context.Render();
        var folded = wrapper.Measure(double.PositiveInfinity, 48).Width;

        button.Text = "Suggest Split";
        var expanded = wrapper.Measure(double.PositiveInfinity, 48).Width;

        expanded.Should().BeGreaterThan(folded + 40, $"folded={folded}");
    }
}
