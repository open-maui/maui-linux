// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// VisualElement.Focus()/Unfocus() through the handler's Focus command. Without
/// a mapping every Focus() threw; Syncfusion's ListViewItem focuses itself on a
/// tap, so SfListView's ItemTapped never fired.
/// </summary>
[Collection("LinuxApplication.Current")]
public class FocusCommandTests
{
    [Fact]
    public void Focus_moves_focus_to_a_focusable_view_and_reports_false_for_others()
    {
        var entry = new Entry();
        var grid = new Grid { WidthRequest = 50, HeightRequest = 50 };
        var label = new Label { Text = "x" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { entry, grid, label } }, withEngine: true);
        host.Context.Render();

        entry.Focus().Should().BeTrue();
        host.Context.FocusedView.Should().BeSameAs(entry.Handler!.PlatformView);
        grid.Focus().Should().BeFalse();
        label.Focus().Should().BeFalse();

        entry.Unfocus();
        host.Context.FocusedView.Should().BeNull();
    }
}
