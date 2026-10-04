namespace ParityHarness.Gallery;

public sealed record GalleryEntry(string Name, Func<Page> Create, string Description);

/// <summary>The pages the dump visits, in order. Names become the dump file names.</summary>
public static class GalleryCatalog
{
    public static IReadOnlyList<GalleryEntry> All { get; } = new List<GalleryEntry>
    {
        new("GridStars", LayoutPages.GridStars, "Absolute/star rows and columns, spans, row/column spacing, padding"),
        new("GridAutos", LayoutPages.GridAutos, "Auto rows/columns, child margins, alignment, Fill with an explicit size"),
        new("Stacks", LayoutPages.Stacks, "Vertical/Horizontal stacks: spacing, alignment, padding, nested"),
        new("Flex", LayoutPages.Flex, "FlexLayout wrap/justify/align, column direction with grow and basis"),
        new("Absolute", LayoutPages.Absolute, "AbsoluteLayout absolute and proportional bounds"),
        new("ScrollShort", ScrollPages.ScrollShort, "ScrollView content shorter than the viewport; scroll centred in a frame"),
        new("ScrollTall", ScrollPages.ScrollTall, "ScrollView content taller than the viewport; horizontal scroll"),
        new("BorderFrame", ContainerPages.BorderFrame, "Border/Frame padding, stroke, auto-size around fixed content"),
        new("Labels", ContainerPages.Labels, "Label wrapping, truncation, MaxLines, padding (text tolerance)"),
        new("Images", ContainerPages.Images, "Image Aspect modes in fixed boxes and unsized images"),
        new("CollectionView", ItemsPages.Collection, "CollectionView header, item spacing, template selector"),
        new("CollectionEmpty", ItemsPages.CollectionEmpty, "CollectionView EmptyView"),
        new("NavigationPage", ChromePages.Navigation, "NavigationPage title bar and content area"),
        new("Shell", ChromePages.ShellPage, "Shell title area and content area"),
        new("Nested", LayoutPages.Nested, "Scroll > Stack > Border > Grid > Flex with margins and padding"),
    };

    public static GalleryEntry? Find(string name)
        => All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
