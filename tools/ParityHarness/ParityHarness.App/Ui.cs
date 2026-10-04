using Microsoft.Maui.Controls.Shapes;

namespace ParityHarness;

/// <summary>
/// Small factories for gallery content. Every element a page creates gets a stable
/// AutomationId: the dump keys elements by it, so the same element is compared across
/// platforms. Measure-independent content (BoxViews with explicit sizes) is preferred;
/// text is confined to the pages that test text layout, because text metrics differ
/// across platforms by design.
/// </summary>
public static class Ui
{
    public const string FontFamily = "OpenSansRegular";
    public const double FontSize = 14;

    private static readonly Color[] Palette =
    {
        Color.FromArgb("#E57373"), Color.FromArgb("#64B5F6"), Color.FromArgb("#81C784"),
        Color.FromArgb("#FFB74D"), Color.FromArgb("#BA68C8"), Color.FromArgb("#4DB6AC"),
        Color.FromArgb("#F06292"), Color.FromArgb("#A1887F"), Color.FromArgb("#90A4AE"),
    };

    private static int _color;

    public static Color NextColor() => Palette[_color++ % Palette.Length];

    /// <summary>A BoxView; a negative size leaves that request unset.</summary>
    public static BoxView Box(string id, double width = -1, double height = -1, Color? color = null)
    {
        var b = new BoxView { AutomationId = id, Color = color ?? NextColor() };
        if (width >= 0) b.WidthRequest = width;
        if (height >= 0) b.HeightRequest = height;
        return b;
    }

    public static T Id<T>(this T view, string id) where T : Element
    {
        view.AutomationId = id;
        return view;
    }

    public static T Align<T>(this T view, LayoutOptions h, LayoutOptions v) where T : View
    {
        view.HorizontalOptions = h;
        view.VerticalOptions = v;
        return view;
    }

    public static T At<T>(this T view, int row, int column, int rowSpan = 1, int columnSpan = 1) where T : BindableObject
    {
        Grid.SetRow(view, row);
        Grid.SetColumn(view, column);
        if (rowSpan > 1) Grid.SetRowSpan(view, rowSpan);
        if (columnSpan > 1) Grid.SetColumnSpan(view, columnSpan);
        return view;
    }

    public static Label Text(string id, string text, double fontSize = FontSize)
        => new() { AutomationId = id, Text = text, FontFamily = FontFamily, FontSize = fontSize, TextColor = Colors.Black };

    public static Border Frame(string id, View content, double stroke = 1, double padding = 0)
        => new()
        {
            AutomationId = id,
            Stroke = Colors.Gray,
            StrokeThickness = stroke,
            StrokeShape = new Rectangle(),
            Padding = padding,
            Content = content,
        };

    public static ColumnDefinitionCollection Columns(params GridLength[] lengths)
    {
        var c = new ColumnDefinitionCollection();
        foreach (var l in lengths) c.Add(new ColumnDefinition(l));
        return c;
    }

    public static RowDefinitionCollection Rows(params GridLength[] lengths)
    {
        var c = new RowDefinitionCollection();
        foreach (var l in lengths) c.Add(new RowDefinition(l));
        return c;
    }

    public static GridLength Px(double v) => new(v, GridUnitType.Absolute);
    public static GridLength Star(double v = 1) => new(v, GridUnitType.Star);
    public static GridLength Auto => GridLength.Auto;

    /// <summary>
    /// The page every gallery entry uses: white, no padding, content as given. The
    /// AutomationId of the page is the gallery name.
    /// </summary>
    public static ContentPage Page(string name, View content)
        => new() { AutomationId = name, Title = name, BackgroundColor = Colors.White, Content = content };

    /// <summary>160x80 PNG (left half red, right half blue, dark 4px border).</summary>
    public static ImageSource TestImage()
        => ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(TestPngBase64)));

    public const double TestImageWidth = 160;
    public const double TestImageHeight = 80;

    private const string TestPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAKAAAABQCAIAAAARP+ljAAAAy0lEQVR42u3RAREAIAwDsYlAzpSgfWKQAfTyVwVNLUVXLgAswPoCeLrj13viBxgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMCAAQMGDBgwYMC3gJUXYMACrGc7k97+xV6afekAAAAASUVORK5CYII=";
}
