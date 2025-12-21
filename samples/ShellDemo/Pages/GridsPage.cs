// GridsPage - Demonstrates Grid layouts with various options

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ShellDemo;

public class GridsPage : ContentPage
{
    public GridsPage()
    {
        Title = "Grids";

        Content = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = new VerticalStackLayout
            {
                Spacing = 25,
                Children =
                {
                    CreateSectionHeader("Basic Grid (2x2)"),
                    CreateBasicGrid(),

                    CreateSectionHeader("Column Definitions"),
                    CreateColumnDefinitionsDemo(),

                    CreateSectionHeader("Row Definitions"),
                    CreateRowDefinitionsDemo(),

                    CreateSectionHeader("Auto Rows (Empty vs Content)"),
                    CreateAutoRowsDemo(),

                    CreateSectionHeader("Star Sizing (Proportional)"),
                    CreateStarSizingDemo(),

                    CreateSectionHeader("Row & Column Spacing"),
                    CreateSpacingDemo(),

                    CreateSectionHeader("Row & Column Span"),
                    CreateSpanDemo(),

                    CreateSectionHeader("Mixed Sizing"),
                    CreateMixedSizingDemo(),

                    CreateSectionHeader("Nested Grids"),
                    CreateNestedGridDemo(),

                    new BoxView { HeightRequest = 20 } // Bottom padding
                }
            }
        };
    }

    private Label CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2196F3"),
            Margin = new Thickness(0, 10, 0, 5)
        };
    }

    private View CreateBasicGrid()
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var cell1 = CreateCell("Row 0, Col 0", "#E3F2FD");
        var cell2 = CreateCell("Row 0, Col 1", "#E8F5E9");
        var cell3 = CreateCell("Row 1, Col 0", "#FFF3E0");
        var cell4 = CreateCell("Row 1, Col 1", "#FCE4EC");

        Grid.SetRow(cell1, 0); Grid.SetColumn(cell1, 0);
        Grid.SetRow(cell2, 0); Grid.SetColumn(cell2, 1);
        Grid.SetRow(cell3, 1); Grid.SetColumn(cell3, 0);
        Grid.SetRow(cell4, 1); Grid.SetColumn(cell4, 1);

        grid.Children.Add(cell1);
        grid.Children.Add(cell2);
        grid.Children.Add(cell3);
        grid.Children.Add(cell4);

        return CreateDemoContainer(grid, "Equal columns using Star sizing");
    }

    private View CreateColumnDefinitionsDemo()
    {
        var stack = new VerticalStackLayout { Spacing = 15 };

        // Auto width columns
        var autoGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var a1 = CreateCell("Auto", "#BBDEFB");
        var a2 = CreateCell("Auto Width", "#C8E6C9");
        var a3 = CreateCell("A", "#FFECB3");
        Grid.SetColumn(a1, 0);
        Grid.SetColumn(a2, 1);
        Grid.SetColumn(a3, 2);
        autoGrid.Children.Add(a1);
        autoGrid.Children.Add(a2);
        autoGrid.Children.Add(a3);

        stack.Children.Add(new Label { Text = "Auto: Sizes to content", FontSize = 12, TextColor = Colors.Gray });
        stack.Children.Add(autoGrid);

        // Absolute width columns
        var absoluteGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(50) },
                new ColumnDefinition { Width = new GridLength(100) },
                new ColumnDefinition { Width = new GridLength(150) }
            },
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var b1 = CreateCell("50px", "#BBDEFB");
        var b2 = CreateCell("100px", "#C8E6C9");
        var b3 = CreateCell("150px", "#FFECB3");
        Grid.SetColumn(b1, 0);
        Grid.SetColumn(b2, 1);
        Grid.SetColumn(b3, 2);
        absoluteGrid.Children.Add(b1);
        absoluteGrid.Children.Add(b2);
        absoluteGrid.Children.Add(b3);

        stack.Children.Add(new Label { Text = "Absolute: Fixed pixel widths (50, 100, 150)", FontSize = 12, TextColor = Colors.Gray, Margin = new Thickness(0, 10, 0, 0) });
        stack.Children.Add(absoluteGrid);

        return stack;
    }

    private View CreateRowDefinitionsDemo()
    {
        var grid = new Grid
        {
            WidthRequest = 200,
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(30) },
                new RowDefinition { Height = new GridLength(50) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(40) }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var r1 = CreateCell("30px height", "#BBDEFB");
        var r2 = CreateCell("50px height", "#C8E6C9");
        var r3 = CreateCell("Auto height\n(fits content)", "#FFECB3");
        var r4 = CreateCell("40px height", "#F8BBD9");

        Grid.SetRow(r1, 0);
        Grid.SetRow(r2, 1);
        Grid.SetRow(r3, 2);
        Grid.SetRow(r4, 3);

        grid.Children.Add(r1);
        grid.Children.Add(r2);
        grid.Children.Add(r3);
        grid.Children.Add(r4);

        return CreateDemoContainer(grid, "Different row heights: 30px, 50px, Auto, 40px");
    }

    private View CreateAutoRowsDemo()
    {
        var stack = new VerticalStackLayout { Spacing = 15 };

        // Grid with empty Auto row
        var emptyAutoGrid = new Grid
        {
            WidthRequest = 250,
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(40) },
                new RowDefinition { Height = GridLength.Auto },  // Empty - should collapse
                new RowDefinition { Height = new GridLength(40) }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#E0E0E0")
        };

        var r1 = CreateCell("Row 0: 40px", "#BBDEFB");
        // Row 1 is Auto with NO content - should be 0 height
        var r3 = CreateCell("Row 2: 40px", "#C8E6C9");

        Grid.SetRow(r1, 0);
        Grid.SetRow(r3, 2);  // Skip row 1

        emptyAutoGrid.Children.Add(r1);
        emptyAutoGrid.Children.Add(r3);

        stack.Children.Add(new Label { Text = "Empty Auto row (Row 1) should collapse to 0 height:", FontSize = 12, TextColor = Colors.Gray });
        stack.Children.Add(emptyAutoGrid);

        // Grid with Auto row that has content
        var contentAutoGrid = new Grid
        {
            WidthRequest = 250,
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(40) },
                new RowDefinition { Height = GridLength.Auto },  // Has content
                new RowDefinition { Height = new GridLength(40) }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#E0E0E0")
        };

        var c1 = CreateCell("Row 0: 40px", "#BBDEFB");
        var c2 = CreateCell("Row 1: Auto (sized to this content)", "#FFECB3");
        var c3 = CreateCell("Row 2: 40px", "#C8E6C9");

        Grid.SetRow(c1, 0);
        Grid.SetRow(c2, 1);
        Grid.SetRow(c3, 2);

        contentAutoGrid.Children.Add(c1);
        contentAutoGrid.Children.Add(c2);
        contentAutoGrid.Children.Add(c3);

        stack.Children.Add(new Label { Text = "Auto row with content sizes to fit:", FontSize = 12, TextColor = Colors.Gray, Margin = new Thickness(0, 10, 0, 0) });
        stack.Children.Add(contentAutoGrid);

        return stack;
    }

    private View CreateStarSizingDemo()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var s1 = CreateCell("1*", "#BBDEFB");
        var s2 = CreateCell("2* (double)", "#C8E6C9");
        var s3 = CreateCell("1*", "#FFECB3");

        Grid.SetColumn(s1, 0);
        Grid.SetColumn(s2, 1);
        Grid.SetColumn(s3, 2);

        grid.Children.Add(s1);
        grid.Children.Add(s2);
        grid.Children.Add(s3);

        return CreateDemoContainer(grid, "Star proportions: 1* | 2* | 1* = 25% | 50% | 25%");
    }

    private View CreateSpacingDemo()
    {
        var stack = new VerticalStackLayout { Spacing = 15 };

        // No spacing
        var noSpacing = new Grid
        {
            RowSpacing = 0,
            ColumnSpacing = 0,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        AddFourCells(noSpacing);
        stack.Children.Add(new Label { Text = "No spacing (RowSpacing=0, ColumnSpacing=0)", FontSize = 12, TextColor = Colors.Gray });
        stack.Children.Add(noSpacing);

        // With spacing
        var withSpacing = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        AddFourCells(withSpacing);
        stack.Children.Add(new Label { Text = "With spacing (RowSpacing=10, ColumnSpacing=10)", FontSize = 12, TextColor = Colors.Gray, Margin = new Thickness(0, 10, 0, 0) });
        stack.Children.Add(withSpacing);

        // Different row/column spacing
        var mixedSpacing = new Grid
        {
            RowSpacing = 5,
            ColumnSpacing = 20,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        AddFourCells(mixedSpacing);
        stack.Children.Add(new Label { Text = "Mixed spacing (RowSpacing=5, ColumnSpacing=20)", FontSize = 12, TextColor = Colors.Gray, Margin = new Thickness(0, 10, 0, 0) });
        stack.Children.Add(mixedSpacing);

        return stack;
    }

    private View CreateSpanDemo()
    {
        var grid = new Grid
        {
            RowSpacing = 5,
            ColumnSpacing = 5,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        // Spanning header
        var header = CreateCell("ColumnSpan=3 (Header)", "#1976D2", Colors.White);
        Grid.SetRow(header, 0);
        Grid.SetColumn(header, 0);
        Grid.SetColumnSpan(header, 3);

        // Left sidebar spanning 2 rows
        var sidebar = CreateCell("RowSpan=2\n(Sidebar)", "#388E3C", Colors.White);
        Grid.SetRow(sidebar, 1);
        Grid.SetColumn(sidebar, 0);
        Grid.SetRowSpan(sidebar, 2);

        // Content cells
        var content1 = CreateCell("Content 1", "#E3F2FD");
        Grid.SetRow(content1, 1);
        Grid.SetColumn(content1, 1);

        var content2 = CreateCell("Content 2", "#E8F5E9");
        Grid.SetRow(content2, 1);
        Grid.SetColumn(content2, 2);

        var content3 = CreateCell("Content 3", "#FFF3E0");
        Grid.SetRow(content3, 2);
        Grid.SetColumn(content3, 1);

        var content4 = CreateCell("Content 4", "#FCE4EC");
        Grid.SetRow(content4, 2);
        Grid.SetColumn(content4, 2);

        grid.Children.Add(header);
        grid.Children.Add(sidebar);
        grid.Children.Add(content1);
        grid.Children.Add(content2);
        grid.Children.Add(content3);
        grid.Children.Add(content4);

        return CreateDemoContainer(grid, "Header spans 3 columns, Sidebar spans 2 rows");
    }

    private View CreateMixedSizingDemo()
    {
        var grid = new Grid
        {
            ColumnSpacing = 5,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(60) },    // Fixed
                new ColumnDefinition { Width = GridLength.Star },        // Fill
                new ColumnDefinition { Width = GridLength.Auto },        // Auto
                new ColumnDefinition { Width = new GridLength(60) }     // Fixed
            },
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var c1 = CreateCell("60px", "#BBDEFB");
        var c2 = CreateCell("Star (fills remaining)", "#C8E6C9");
        var c3 = CreateCell("Auto", "#FFECB3");
        var c4 = CreateCell("60px", "#F8BBD9");

        Grid.SetColumn(c1, 0);
        Grid.SetColumn(c2, 1);
        Grid.SetColumn(c3, 2);
        Grid.SetColumn(c4, 3);

        grid.Children.Add(c1);
        grid.Children.Add(c2);
        grid.Children.Add(c3);
        grid.Children.Add(c4);

        return CreateDemoContainer(grid, "Mixed: 60px | Star | Auto | 60px");
    }

    private View CreateNestedGridDemo()
    {
        var outerGrid = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            Padding = new Thickness(10)
        };

        // Nested grid 1
        var innerGrid1 = new Grid
        {
            RowSpacing = 2,
            ColumnSpacing = 2,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        var i1a = CreateCell("A", "#BBDEFB", null, 8);
        var i1b = CreateCell("B", "#90CAF9", null, 8);
        var i1c = CreateCell("C", "#64B5F6", null, 8);
        var i1d = CreateCell("D", "#42A5F5", null, 8);
        Grid.SetRow(i1a, 0); Grid.SetColumn(i1a, 0);
        Grid.SetRow(i1b, 0); Grid.SetColumn(i1b, 1);
        Grid.SetRow(i1c, 1); Grid.SetColumn(i1c, 0);
        Grid.SetRow(i1d, 1); Grid.SetColumn(i1d, 1);
        innerGrid1.Children.Add(i1a);
        innerGrid1.Children.Add(i1b);
        innerGrid1.Children.Add(i1c);
        innerGrid1.Children.Add(i1d);

        // Nested grid 2
        var innerGrid2 = new Grid
        {
            RowSpacing = 2,
            ColumnSpacing = 2,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        var i2a = CreateCell("1", "#C8E6C9", null, 8);
        var i2b = CreateCell("2", "#A5D6A7", null, 8);
        var i2c = CreateCell("3", "#81C784", null, 8);
        var i2d = CreateCell("4", "#66BB6A", null, 8);
        Grid.SetRow(i2a, 0); Grid.SetColumn(i2a, 0);
        Grid.SetRow(i2b, 0); Grid.SetColumn(i2b, 1);
        Grid.SetRow(i2c, 1); Grid.SetColumn(i2c, 0);
        Grid.SetRow(i2d, 1); Grid.SetColumn(i2d, 1);
        innerGrid2.Children.Add(i2a);
        innerGrid2.Children.Add(i2b);
        innerGrid2.Children.Add(i2c);
        innerGrid2.Children.Add(i2d);

        Grid.SetRow(innerGrid1, 0); Grid.SetColumn(innerGrid1, 0);
        Grid.SetRow(innerGrid2, 0); Grid.SetColumn(innerGrid2, 1);

        var label1 = new Label { Text = "Outer Grid Row 1", HorizontalOptions = LayoutOptions.Center };
        var label2 = new Label { Text = "Spans both columns", HorizontalOptions = LayoutOptions.Center };
        Grid.SetRow(label1, 1); Grid.SetColumn(label1, 0);
        Grid.SetRow(label2, 1); Grid.SetColumn(label2, 1);

        outerGrid.Children.Add(innerGrid1);
        outerGrid.Children.Add(innerGrid2);
        outerGrid.Children.Add(label1);
        outerGrid.Children.Add(label2);

        return CreateDemoContainer(outerGrid, "Outer grid contains two nested 2x2 grids");
    }

    private Border CreateCell(string text, string bgColor, Color? textColor = null, float fontSize = 12)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb(bgColor),
            Padding = new Thickness(10, 8),
            StrokeThickness = 0,
            Content = new Label
            {
                Text = text,
                FontSize = fontSize,
                TextColor = textColor ?? Colors.Black,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }

    private void AddFourCells(Grid grid)
    {
        var c1 = CreateCell("0,0", "#BBDEFB");
        var c2 = CreateCell("0,1", "#C8E6C9");
        var c3 = CreateCell("1,0", "#FFECB3");
        var c4 = CreateCell("1,1", "#F8BBD9");

        Grid.SetRow(c1, 0); Grid.SetColumn(c1, 0);
        Grid.SetRow(c2, 0); Grid.SetColumn(c2, 1);
        Grid.SetRow(c3, 1); Grid.SetColumn(c3, 0);
        Grid.SetRow(c4, 1); Grid.SetColumn(c4, 1);

        grid.Children.Add(c1);
        grid.Children.Add(c2);
        grid.Children.Add(c3);
        grid.Children.Add(c4);
    }

    private View CreateDemoContainer(View content, string description)
    {
        return new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                new Label { Text = description, FontSize = 12, TextColor = Colors.Gray },
                content
            }
        };
    }
}
