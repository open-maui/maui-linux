// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfListView with AutoFitMode.Height sizes each row from its template measured at the
/// list's width; a row whose summary wraps to two lines gets the height for both.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionListAutoFitTests
{
    private readonly ITestOutputHelper _out;
    public SyncfusionListAutoFitTests(ITestOutputHelper output) => _out = output;

    private sealed record Row(string Title, string Summary, string Sender);

    private static readonly List<Label> Summaries = new();
    private static readonly List<Label> Senders = new();

    [Fact]
    public void A_wrapped_summary_keeps_its_row_height()
    {
        Summaries.Clear(); Senders.Clear();
        var rows = Enumerable.Range(1, 4).Select(i => new Row($"Harborline: sign the Q4 retainer {i}",
            "Priya sent the revised retainer (12 pages). Scope is the same as Q3, rate up 8%, payment net-15.", "Priya Nair")).ToList();
        var list = new Syncfusion.Maui.ListView.SfListView
        {
            ItemsSource = rows,
            AutoFitMode = Syncfusion.Maui.ListView.AutoFitMode.Height,
            Margin = new Thickness(8, 4),
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
                title.SetBinding(Label.TextProperty, nameof(Row.Title));
                var time = new Label { Text = "5 h ago", FontSize = 11 };
                var summary = new Label { FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2 };
                summary.SetBinding(Label.TextProperty, nameof(Row.Summary));
                var sender = new Label { FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
                sender.SetBinding(Label.TextProperty, nameof(Row.Sender));
                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(5), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
                    RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)),
                    ColumnSpacing = 12, RowSpacing = 3,
                };
                grid.Add(new BoxView { Color = Microsoft.Maui.Graphics.Colors.Red }, 0, 0);
                Grid.SetRowSpan(grid.Children[0] as BindableObject, 3);
                grid.Add(title, 1, 0);
                grid.Add(time, 2, 0);
                grid.Add(summary, 1, 1);
                Grid.SetColumnSpan(summary, 2);
                grid.Add(sender, 1, 2);
                Grid.SetColumnSpan(sender, 2);
                lock (Summaries) { Summaries.Add(summary); Senders.Add(sender); }
                return new ContentView
                {
                    Content = new Border { Margin = new Thickness(0, 4), Padding = new Thickness(12, 10), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Content = grid },
                };
            }),
        };
        using var host = new CompatHost(new ContentPage { Content = list }, b => b.UseLinuxSyncfusion(), 420, 700);
        host.Render();
        host.Render();

        var pairs = Summaries.Zip(Senders).Where(p => p.First.Handler != null && p.First.Width > 0).ToList();
        pairs.Should().NotBeEmpty();
        foreach (var (summary, sender) in pairs)
        {
            _out.WriteLine($"summary {summary.Bounds} sender {sender.Bounds}");
            summary.Height.Should().BeGreaterThan(25, "the summary wraps to two lines at this width");
            (summary.Y + summary.Height).Should().BeLessThanOrEqualTo(sender.Y + 0.5, "the sender line sits below the summary");
        }
    }
}
