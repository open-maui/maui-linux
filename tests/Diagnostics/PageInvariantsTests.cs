// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Diagnostics;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Diagnostics;

/// <summary>
/// PageInvariants: the generic rules a rendered page must satisfy. Each rule catches the class
/// of bug it is for on a deliberately broken page, and realistic pages built from the controls
/// those bugs were in report nothing.
/// </summary>
[Collection("LinuxApplication.Current")]
public class PageInvariantsTests
{
    static PageInvariantsTests() => PageInvariants.Install();

    private static List<PageInvariantViolation> CheckPage(Func<Page> makePage)
    {
        PageInvariants.Check(new SkiaLabel(), 1, 1); // drop failures collected before this page
        var page = makePage();
        using var host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();
        host.Context.Render();
        var root = (SkiaView)page.Handler!.PlatformView!;
        return PageInvariants.Check(root, 800, 600);
    }

    private static string WriteImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-invariant-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(160, 90);
        bitmap.Erase(SKColors.Red);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // --- each rule catches its bug ---------------------------------------------------------

    [Fact]
    public void Text_that_is_a_type_name_is_reported()
    {
        var violations = CheckPage(() => new ContentPage { Content = new Label { Text = typeof(VerticalStackLayout).FullName } });
        violations.Should().Contain(v => v.Rule == "type-name-text");
    }

    private sealed class SpillingImage : SkiaImage
    {
        protected override void OnDraw(SKCanvas canvas, SKRect bounds)
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom + 40), paint);
        }
    }

    [Fact]
    public void A_picture_drawn_outside_its_frame_is_reported()
    {
        var spilling = new SpillingImage();
        spilling.Arrange(new Rect(100, 100, 200, 100));
        var root = new SkiaStackLayout();
        root.AddChild(spilling);
        root.Arrange(new Rect(0, 0, 800, 600));
        spilling.Arrange(new Rect(100, 100, 200, 100));

        PageInvariants.Check(root, 800, 600).Should().Contain(v => v.Rule == "draws-outside-bounds");
    }

    [Fact]
    public void A_control_covered_by_something_that_takes_the_click_is_reported()
    {
        var violations = CheckPage(() =>
        {
            var button = new Button { Text = "Hidden", WidthRequest = 120, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            var cover = new BoxView { Color = Colors.Black, WidthRequest = 300, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
            cover.GestureRecognizers.Add(new TapGestureRecognizer());
            return new ContentPage { Content = new Grid { Children = { button, cover } } };
        });
        violations.Should().Contain(v => v.Rule == "unreachable-control" && v.View.Contains("SkiaButton"));
    }

    private sealed class Model
    {
        public string Name => "x";
    }

    [Fact]
    public void A_failed_binding_is_reported()
    {
        var violations = CheckPage(() =>
        {
            var label = new Label { BindingContext = new Model() };
            label.SetBinding(Label.TextProperty, "Missing");
            return new ContentPage { Content = label };
        });
        violations.Should().Contain(v => v.Rule == "binding-failed" && v.Detail.Contains("Missing"));
    }

    // --- realistic pages are clean ---------------------------------------------------------

    [Fact]
    public void Pages_built_from_the_controls_behind_recent_bugs_report_nothing()
    {
        var image = WriteImage();
        try
        {
            ContentPage ListPage()
            {
                var list = new CollectionView
                {
                    ItemsSource = Enumerable.Range(0, 30).Select(i => $"Row {i}").ToList(),
                    Header = new VerticalStackLayout { Padding = 12, Children = { new Label { Text = "12 books" }, new Button { Text = "Refresh" } } },
                    Footer = "End of list",
                    ItemTemplate = new DataTemplate(() =>
                    {
                        var title = new Label();
                        title.SetBinding(Label.TextProperty, ".");
                        var open = new Button { Text = "Open", HorizontalOptions = LayoutOptions.End };
                        var card = new Border { Padding = 8, Content = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Children = { title } } };
                        ((Grid)card.Content).Add(open, 1, 0);
                        card.GestureRecognizers.Add(new TapGestureRecognizer());
                        return card;
                    }),
                };
                return new ContentPage { Content = list };
            }

            ContentPage MediaPage() => new()
            {
                Content = new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Image { Source = ImageSource.FromFile(image), Aspect = Aspect.AspectFill, HeightRequest = 120 },
                            new Image { Source = ImageSource.FromFile(image), Aspect = Aspect.AspectFit, HeightRequest = 120 },
                            new Entry { Placeholder = "Search" },
                            new CheckBox(),
                            new Switch(),
                            new Slider(),
                        },
                    },
                },
            };

            ContentPage EmptyPage() => new()
            {
                Content = new CollectionView
                {
                    ItemsSource = new List<string>(),
                    EmptyView = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { new Label { Text = "No questions yet" }, new Button { Text = "Ask a question" } } },
                },
            };

            foreach (var (name, make) in new (string, Func<Page>)[] { ("list", ListPage), ("media", MediaPage), ("empty", EmptyPage) })
            {
                var violations = CheckPage(make);
                violations.Should().BeEmpty($"the {name} page: {string.Join("; ", violations)}");
            }
        }
        finally
        {
            File.Delete(image);
        }
    }

    public sealed class Book
    {
        public string Title { get; init; } = "";
    }

    public sealed class LibraryViewModel
    {
        public List<Book> Books { get; } = new() { new Book { Title = "Java" }, new Book { Title = "C#" } };
    }

    [Fact]
    public void Rows_bind_to_their_item_without_first_binding_to_the_page()
    {
        // A row parented before it got its item bound to the page's view model first: a compiled
        // binding (x:DataType Book) against LibraryViewModel failed once per row (CiteLynq).
        var violations = CheckPage(() =>
        {
            var list = new CollectionView
            {
                ItemTemplate = new DataTemplate(() =>
                {
                    var label = new Label();
                    label.SetBinding(Label.TextProperty, static (Book b) => b.Title);
                    return label;
                }),
            };
            list.SetBinding(ItemsView.ItemsSourceProperty, static (LibraryViewModel vm) => vm.Books);
            return new ContentPage { BindingContext = new LibraryViewModel(), Content = list };
        });
        violations.Should().NotContain(v => v.Rule == "binding-failed", string.Join("; ", violations));
    }
}
