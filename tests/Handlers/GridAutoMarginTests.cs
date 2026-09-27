// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// An Auto column is as wide as its child plus the child's margin, so a margin
/// moves the child without shrinking it (MAToolbar's icon-only buttons carry
/// their spacing as a trailing margin in its main-panel grid).
/// </summary>
[Collection("LinuxApplication.Current")]
public class GridAutoMarginTests
{
    [Fact]
    public void A_margin_does_not_shrink_a_child_in_an_Auto_column()
    {
        var plain = new Button { Text = "X" };
        var spaced = new Button { Text = "X", Margin = new Thickness(2, 0, 6, 0) };
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            HorizontalOptions = LayoutOptions.Start,
        };
        grid.Add(plain, 0, 0);
        grid.Add(spaced, 1, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        spaced.Width.Should().BeApproximately(plain.Width, 0.5);
        spaced.X.Should().BeApproximately(plain.Width + 2, 0.5);
    }
}
