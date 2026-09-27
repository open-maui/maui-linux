// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

[Collection("LinuxApplication.Current")]
public class ColumnWidthChangeTests
{
    [Fact]
    public void A_column_width_set_after_start_takes_effect()
    {
        // Strikeline's Search page binds its column widths to GridLengths that start at
        // default (0, absolute) and become stars once the page knows its size.
        var column = new ColumnDefinition(new GridLength(0));
        var label = new Label { Text = "Loading Library" };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(column);
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
        grid.Add(label, 0, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        column.Width = new GridLength(1, GridUnitType.Star);

        host.Context.Render();

        ((Microsoft.Maui.Platform.SkiaView)label.Handler!.PlatformView!).Bounds.Width.Should().BeGreaterThan(100);
    }

    [Fact]
    public void A_bound_column_width_that_changes_takes_effect()
    {
        var vm = new WidthModel();
        var column = new ColumnDefinition();
        column.SetBinding(ColumnDefinition.WidthProperty, nameof(WidthModel.Primary));
        var label = new Label { Text = "Loading Library" };
        var grid = new Grid { BindingContext = vm };
        grid.ColumnDefinitions.Add(column);
        grid.Add(label, 0, 0);
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        vm.Primary = new GridLength(1, GridUnitType.Star);
        host.Context.Render();

        ((Microsoft.Maui.Platform.SkiaView)label.Handler!.PlatformView!).Bounds.Width.Should().BeGreaterThan(100);
    }

    private sealed class WidthModel : System.ComponentModel.INotifyPropertyChanged
    {
        private GridLength _primary;
        public GridLength Primary { get => _primary; set { _primary = value; PropertyChanged?.Invoke(this, new(nameof(Primary))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
