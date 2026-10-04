using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParityHarness.Dump;

/// <summary>
/// Harness identity for elements that cannot use AutomationId (templated, recyclable
/// views: AutomationId may be set only once). Takes precedence over AutomationId.
/// </summary>
public static class DumpKey
{
    public static readonly BindableProperty KeyProperty =
        BindableProperty.CreateAttached("Key", typeof(string), typeof(DumpKey), null);

    public static string? GetKey(BindableObject view) => (string?)view.GetValue(KeyProperty);

    public static void SetKey(BindableObject view, string? value) => view.SetValue(KeyProperty, value);

    /// <summary>
    /// Marks a non-text element whose position follows text metrics (e.g. a box after a
    /// Label in an Auto column): the diff applies the text tolerance to it.
    /// </summary>
    public static readonly BindableProperty TextDependentProperty =
        BindableProperty.CreateAttached("TextDependent", typeof(bool), typeof(DumpKey), false);

    public static bool GetTextDependent(BindableObject view) => (bool)view.GetValue(TextDependentProperty);

    public static void SetTextDependent(BindableObject view, bool value) => view.SetValue(TextDependentProperty, value);
}

public sealed record RectDto(double X, double Y, double W, double H)
{
    public static RectDto? From(Rect? r)
        => r is { } v && !double.IsNaN(v.X) && !double.IsNaN(v.Width)
            ? new RectDto(Math.Round(v.X, 2), Math.Round(v.Y, 2), Math.Round(v.Width, 2), Math.Round(v.Height, 2))
            : null;
}

public sealed record SizeDto(double W, double H);

public sealed class ElementRecord
{
    /// <summary>Stable identity: DumpKey, else AutomationId, else parentKey/Type[n].</summary>
    public string Key { get; set; } = "";
    public string? AutomationId { get; set; }
    public string Type { get; set; } = "";
    public string? ParentKey { get; set; }
    public int Depth { get; set; }
    public bool IsVisible { get; set; }
    /// <summary>The element itself shows text (IText / ITextInput): text tolerance applies.</summary>
    public bool Text { get; set; }
    /// <summary>Some descendant shows text, so the element's size may follow text metrics.</summary>
    public bool ContainsText { get; set; }
    /// <summary>Marked by the page as positioned by text metrics (DumpKey.TextDependent).</summary>
    public bool TextDependent { get; set; }
    /// <summary>MAUI layout frame, accumulated up to the root page (VisualElement.Frame chain).</summary>
    public RectDto? Frame { get; set; }
    /// <summary>Where the platform view actually is, relative to the root page's platform view.</summary>
    public RectDto? Native { get; set; }
}

public sealed class PageDump
{
    public string Page { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Os { get; set; } = "";
    public SizeDto Requested { get; set; } = new(0, 0);
    public SizeDto PageSize { get; set; } = new(0, 0);
    public bool Settled { get; set; }
    public int SettleMs { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public List<ElementRecord> Elements { get; set; } = new();
}

public sealed class DumpIndex
{
    public string Platform { get; set; } = "";
    public string Os { get; set; } = "";
    public string Framework { get; set; } = "";
    public SizeDto Requested { get; set; } = new(0, 0);
    public double DisplayDensity { get; set; }
    public string StartedUtc { get; set; } = "";
    public List<IndexEntry> Pages { get; set; } = new();
    public string? Error { get; set; }
}

public sealed record IndexEntry(string Name, string File, bool Settled, int Elements, string? Error, string? Warning);

public static class DumpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
