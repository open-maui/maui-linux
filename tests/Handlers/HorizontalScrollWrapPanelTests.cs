// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A wrap panel in a horizontal ScrollView (MarketAlly.Foundation's MAStatCardPanel on
/// InboxRevu's Ledger page) is measured at unlimited width: its cards form one row that
/// scrolls, and what follows it in the page stays in view.
/// </summary>
[Collection("LinuxApplication.Current")]
public class HorizontalScrollWrapPanelTests
{
    private readonly ITestOutputHelper _out;
    public HorizontalScrollWrapPanelTests(ITestOutputHelper output) => _out = output;

    private sealed class WrapPanel : Layout
    {
        public List<double> MeasuredWidths { get; } = new();
        protected override ILayoutManager CreateLayoutManager() => new Manager(this);

        private sealed class Manager : ILayoutManager
        {
            private readonly WrapPanel _p;
            public Manager(WrapPanel p) => _p = p;
            private const double Cell = 190, Row = 110;

            private int PerRow(double w) => double.IsInfinity(w) ? _p.Count : Math.Clamp((int)(w / Cell), 1, Math.Max(1, _p.Count));

            public Size Measure(double widthConstraint, double heightConstraint)
            {
                _p.MeasuredWidths.Add(widthConstraint);
                foreach (var c in _p) c.Measure(Cell, Row);
                var per = PerRow(widthConstraint);
                return new Size(per * Cell, Math.Ceiling(_p.Count / (double)per) * Row);
            }

            public Size ArrangeChildren(Rect bounds)
            {
                var per = PerRow(bounds.Width);
                int i = 0;
                foreach (var c in _p) { c.Arrange(new Rect(bounds.X + i % per * Cell, bounds.Y + i / per * Row, Cell, Row)); i++; }
                return bounds.Size;
            }
        }
    }

    [Fact]
    public void Cards_in_a_horizontal_scroll_form_one_row()
    {
        var panel = new WrapPanel();
        for (int i = 0; i < 5; i++) panel.Add(new BoxView());
        var after = new Label { Text = "chips" };
        var page = new ContentPage
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16, 12), Spacing = 12,
                    Children = { new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = panel }, after },
                },
            },
        };
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();
        host.Context.Render();

        _out.WriteLine("measured widths: " + string.Join(", ", panel.MeasuredWidths));
        _out.WriteLine($"panel {panel.Bounds} after {after.Bounds}");
        panel.Height.Should().BeApproximately(110, 0.5);
        after.Y.Should().BeLessThan(200);
    }
}
