// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Golden;

// Inside the namespace so they beat the enclosing Microsoft.Maui types in lookup.
using GridLength = Microsoft.Maui.Platform.GridLength;
using StackOrientation = Microsoft.Maui.Platform.StackOrientation;

/// <summary>
/// The scene catalog. Each scene is rendered at every scale factor and
/// compared against its committed baseline. Scenes are built from the Skia
/// views directly so they exercise measure, arrange and draw without a
/// display server. The first scenes cover exactly the regressions found by
/// on-screen review in 10.0.101.1: glyph spacing at fractional scale, label
/// heights, wrapped text, and text baselines in buttons.
/// </summary>
[Collection("LinuxApplication.Current")]
public class GoldenSceneTests
{
    public static IEnumerable<object[]> Scales => GoldenHarness.Scales.Select(s => new object[] { s });

    private static SkiaStackLayout Column(double spacing = 8, double padding = 12)
        => new() { Orientation = StackOrientation.Vertical, Spacing = spacing, Padding = new Thickness(padding) };

    private static SkiaStackLayout Row(double spacing = 8)
        => new() { Orientation = StackOrientation.Horizontal, Spacing = spacing };

    private static SkiaLabel Label(string text, double size = 16, FontAttributes attrs = FontAttributes.None)
        => new() { Text = text, FontSize = size, FontAttributes = attrs };

    // ---- Text ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Scales))]
    public void Labels_sizes_and_descenders(float scale)
    {
        var root = Column();
        root.AddChild(Label("Quick brown fox, jumping gently: gjpqy", 12));
        root.AddChild(Label("Quick brown fox, jumping gently: gjpqy", 16));
        root.AddChild(Label("Quick brown fox, jumping gently: gjpqy", 24));
        root.AddChild(Label("Bold 0123456789 AVWA Ty", 20, FontAttributes.Bold));
        root.AddChild(Label("Italic text with 'quotes' and (parens)", 16, FontAttributes.Italic));
        root.AddChild(Label("Search Bar", 18)); // the 10.0.101.1 glyph-gap case
        GoldenHarness.Verify("labels", root, 360, 220, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Labels_wrapping_and_max_lines(float scale)
    {
        var root = Column();
        root.AddChild(new SkiaLabel
        {
            Text = "This is a long paragraph of text that should wrap onto several lines inside the available width without any of the lines overlapping each other.",
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
        });
        root.AddChild(new SkiaLabel
        {
            Text = "Two lines max: this text is long enough that it would need three or four lines but MaxLines cuts it off.",
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2,
        });
        root.AddChild(new SkiaLabel
        {
            Text = "Tail truncation of a single long line that does not fit in the width",
            FontSize = 15,
            LineBreakMode = LineBreakMode.TailTruncation,
        });
        root.AddChild(Label("Centered", 15).With(l => l.HorizontalTextAlignment = TextAlignment.Center));
        root.AddChild(Label("Right aligned", 15).With(l => l.HorizontalTextAlignment = TextAlignment.End));
        GoldenHarness.Verify("labels-wrap", root, 300, 240, scale);
    }

    // ---- Buttons and inputs ------------------------------------------------

    [Theory]
    [MemberData(nameof(Scales))]
    public void Buttons(float scale)
    {
        var root = Column();
        root.AddChild(new SkiaButton { Text = "Primary Action", FontSize = 16 });
        root.AddChild(new SkiaButton { Text = "Disabled", FontSize = 16, IsEnabled = false });
        root.AddChild(new SkiaButton { Text = "Rounded with a longer caption", FontSize = 14, CornerRadius = 18 });
        var row = Row();
        row.AddChild(new SkiaButton { Text = "OK", FontSize = 14 });
        row.AddChild(new SkiaButton { Text = "Cancel", FontSize = 14 });
        row.AddChild(new SkiaButton { Text = "gjpqy", FontSize = 14 }); // descender baseline case
        root.AddChild(row);
        GoldenHarness.Verify("buttons", root, 320, 220, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Entry_and_editor(float scale)
    {
        var root = Column();
        root.AddChild(new SkiaEntry { Placeholder = "Placeholder text", FontSize = 16 });
        root.AddChild(new SkiaEntry { Text = "Entered value", FontSize = 16 });
        root.AddChild(new SkiaEntry { Text = "secret", IsPassword = true, FontSize = 16 });
        root.AddChild(new SkiaEditor { Text = "Editor with\nmultiple lines\nof text", FontSize = 15, HeightRequest = 90 });
        root.AddChild(new SkiaSearchBar { Placeholder = "Search Bar", Text = "" });
        GoldenHarness.Verify("entry-editor", root, 320, 300, scale);
    }

    // ---- Toggles and ranges ------------------------------------------------

    [Theory]
    [MemberData(nameof(Scales))]
    public void Toggles(float scale)
    {
        var root = Column(12);
        var checks = Row(16);
        checks.AddChild(new SkiaCheckBox { IsChecked = true });
        checks.AddChild(new SkiaCheckBox { IsChecked = false });
        checks.AddChild(new SkiaCheckBox { IsChecked = true, IsEnabled = false });
        root.AddChild(checks);
        var switches = Row(16);
        switches.AddChild(new SkiaSwitch { IsOn = true });
        switches.AddChild(new SkiaSwitch { IsOn = false });
        switches.AddChild(new SkiaSwitch { IsOn = true, IsEnabled = false });
        root.AddChild(switches);
        root.AddChild(new SkiaRadioButton { Content = "Selected option", IsChecked = true, FontSize = 15 });
        root.AddChild(new SkiaRadioButton { Content = "Other option", IsChecked = false, FontSize = 15 });
        GoldenHarness.Verify("toggles", root, 300, 200, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Ranges(float scale)
    {
        var root = Column(14);
        root.AddChild(new SkiaSlider { Minimum = 0, Maximum = 100, Value = 35 });
        root.AddChild(new SkiaSlider { Minimum = 0, Maximum = 1, Value = 1 });
        root.AddChild(new SkiaProgressBar { Progress = 0.6 });
        root.AddChild(new SkiaProgressBar { Progress = 0 });
        root.AddChild(new SkiaStepper { Minimum = 0, Maximum = 10, Value = 4 });
        root.AddChild(new SkiaActivityIndicator { IsRunning = false, Size = 32 });
        GoldenHarness.Verify("ranges", root, 300, 260, scale);
    }

    // ---- Pickers -------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Scales))]
    public void Pickers(float scale)
    {
        var root = Column();
        var picker = new SkiaPicker { Title = "Choose a colour", FontSize = 16 };
        picker.Items.Add("Red"); picker.Items.Add("Green"); picker.Items.Add("Blue");
        root.AddChild(picker);
        var selected = new SkiaPicker { Title = "Choose a colour", FontSize = 16 };
        selected.Items.Add("Red"); selected.Items.Add("Green"); selected.Items.Add("Blue");
        selected.SelectedIndex = 1;
        root.AddChild(selected);
        // Fixed values: pickers default to "now", which would make the scene nondeterministic.
        root.AddChild(new SkiaDatePicker { Date = new DateTime(2026, 3, 14), FontSize = 16 });
        root.AddChild(new SkiaTimePicker { Time = new TimeSpan(9, 41, 0), FontSize = 16 });
        GoldenHarness.Verify("pickers", root, 320, 240, scale);
    }

    // ---- Layout and containers ---------------------------------------------

    [Theory]
    [MemberData(nameof(Scales))]
    public void Grid_and_border(float scale)
    {
        var grid = new SkiaGrid { Padding = new Thickness(12), Spacing = 8 };
        grid.ColumnDefinitions.Add(GridLength.Star);
        grid.ColumnDefinitions.Add(GridLength.Star);
        grid.RowDefinitions.Add(GridLength.Auto);
        grid.RowDefinitions.Add(new GridLength(60));
        grid.RowDefinitions.Add(GridLength.Star);
        grid.AddChild(Label("Header spanning two columns", 18, FontAttributes.Bold), 0, 0, 1, 2);
        grid.AddChild(new SkiaButton { Text = "Left", FontSize = 14 }, 1, 0);
        grid.AddChild(new SkiaButton { Text = "Right", FontSize = 14 }, 1, 1);

        var border = new SkiaBorder
        {
            Stroke = Colors.SteelBlue,
            StrokeThickness = 2,
            CornerRadius = 12,
            Padding = new Thickness(10),
            BackgroundColor = Color.FromArgb("#F0F4FA"),
        };
        border.AddChild(Label("Inside a rounded border", 15));
        grid.AddChild(border, 2, 0, 1, 2);
        GoldenHarness.Verify("grid-border", grid, 320, 240, scale);
    }

    // ---- Graphics and shapes -----------------------------------------------

    [Theory]
    [MemberData(nameof(Scales))]
    public void Shapes(float scale)
    {
        static SolidColorBrush Brush(Color c) => new(c);
        static PointCollection Points(params (double X, double Y)[] pts)
        {
            var c = new PointCollection();
            foreach (var (x, y) in pts) c.Add(new Point(x, y));
            return c;
        }

        var root = new SkiaAbsoluteLayout { Padding = new Thickness(10) };
        void Place(SkiaView v, float x, float y, float w, float h)
        {
            root.AddChild(v);
            root.SetLayoutBounds(v, new SkiaSharp.SKRect(x, y, x + w, y + h));
        }

        Place(new SkiaRectangle { Fill = Brush(Colors.CornflowerBlue), Stroke = Brush(Colors.Navy), StrokeThickness = 3, RadiusX = 10, RadiusY = 10 }, 0, 0, 100, 60);
        Place(new SkiaEllipse { Fill = Brush(Colors.Gold), Stroke = Brush(Colors.DarkGoldenrod), StrokeThickness = 3 }, 110, 0, 100, 60);
        Place(new SkiaLine { X1 = 0, Y1 = 0, X2 = 90, Y2 = 60, Stroke = Brush(Colors.Crimson), StrokeThickness = 4, StrokeDashArray = new DoubleCollection { 3, 2 } }, 220, 0, 100, 60);
        Place(new SkiaShapePath
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M 0 60 L 30 0 L 60 45 L 90 10 L 100 60 Z")!,
            FillColor = Colors.MediumSeaGreen,
            StrokeColor = Colors.DarkGreen,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        }, 0, 70, 100, 60);
        Place(new SkiaPolygon
        {
            Points = Points((50, 0), (100, 35), (80, 60), (20, 60), (0, 35)),
            Fill = Brush(Colors.Orchid),
            Stroke = Brush(Colors.Purple),
            StrokeThickness = 2,
        }, 110, 70, 100, 60);
        Place(new SkiaPolyline
        {
            Points = Points((0, 60), (20, 10), (40, 50), (60, 0), (80, 40), (100, 20)),
            Stroke = Brush(Colors.DarkOrange),
            StrokeThickness = 3,
        }, 220, 70, 100, 60);
        // Aspect-stretched path: a unit square scaled Uniform into a wide box.
        Place(new SkiaShapePath
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M 0 0 L 10 0 L 10 10 L 0 10 Z")!,
            FillColor = Colors.LightSlateGray,
            StrokeColor = Colors.Black,
            StrokeThickness = 1,
            Aspect = Stretch.Uniform,
        }, 0, 140, 320, 40);

        GoldenHarness.Verify("shapes", root, 340, 200, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void CollectionView_items(float scale)
    {
        var list = new SkiaCollectionView
        {
            ItemsSource = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" },
            ItemViewCreator = item =>
            {
                var row = Row(8);
                row.Padding = new Thickness(10, 6);
                row.AddChild(new SkiaCheckBox { IsChecked = (item as string) == "Charlie" });
                row.AddChild(Label((string)item, 15));
                return row;
            },
        };
        GoldenHarness.Verify("collection-view", list, 280, 220, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TableView_sections_and_cells(float scale)
    {
        var table = new SkiaTableView { Intent = SkiaTableIntent.Settings, HasUnevenRows = true };

        var account = new SkiaTableSection("Account");
        table.AddCell(account, new SkiaCellView { Text = "Name", Detail = "Ada Lovelace" });
        table.AddCell(account, new SkiaCellView
        {
            Kind = SkiaCellKind.Entry,
            Text = "Email",
            Editor = new SkiaEntry { Text = "ada@example.com" },
        });
        table.AddSection(account);

        var options = new SkiaTableSection("Options");
        // The switch stays off: SkiaSwitch animates IsOn changes on a
        // wall-clock timer, which a deterministic baseline cannot capture.
        table.AddCell(options, new SkiaCellView
        {
            Kind = SkiaCellKind.Switch,
            Text = "Notifications",
            Accessory = new SkiaSwitch(),
        });
        table.AddCell(options, new SkiaCellView { Text = "Sign out", TextColor = Colors.Red });
        table.AddSection(options);

        GoldenHarness.Verify("table-view", table, 320, 260, scale);
    }
}

internal static class GoldenExtensions
{
    public static T With<T>(this T view, Action<T> configure) where T : SkiaView
    {
        configure(view);
        return view;
    }
}
