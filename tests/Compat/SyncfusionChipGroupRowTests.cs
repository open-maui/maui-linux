// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Syncfusion.Maui.Core;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

/// <summary>A choice SfChipGroup lays its chips out in a row, each as wide as its text (Strikeline's Outlook range chips).</summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionChipGroupRowTests(ITestOutputHelper output)
{
    [Fact]
    public void Centred_choice_chips_sit_side_by_side()
    {
        var chips = new[] { "1D", "1W", "1M", "3M", "YTD" }.Select(t => new SfChip { Text = t }).ToArray();
        var group = new SfChipGroup { ChipType = SfChipsType.Choice, ChipCornerRadius = 20, ChipStrokeThickness = 0, HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(16, 4, 10, 10), ChipPadding = new Thickness(18, 6) };
        foreach (var c in chips) group.Items!.Add(c);
        var grid = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)) };
        grid.Add(new BoxView(), 0, 0);
        grid.Add(group, 0, 1);
        using var host = new CompatHost(new ContentPage { Content = grid }, b => b.UseLinuxSyncfusion(), 1080, 900);
        for (int i = 0; i < 3; i++) { Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); host.Render(); }

        foreach (var c in chips) output.WriteLine($"{c.Text}: {c.Bounds}");
        output.WriteLine($"group: {group.Bounds}");
        chips.Select(c => c.Y).Distinct().Should().HaveCount(1, "one row");
        chips.Should().OnlyContain(c => c.Width < 150);
    }
}
