using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for FlexLayout on Linux: MAUI's FlexLayoutManager (the flexbox
/// engine FlexLayout carries) places the children, with their Order, Grow,
/// Shrink, Basis and AlignSelf, as on every platform.
/// </summary>
public class FlexLayoutHandler : LayoutHandler
{
    public new static IPropertyMapper<FlexLayout, FlexLayoutHandler> Mapper = new PropertyMapper<FlexLayout, FlexLayoutHandler>(LayoutHandler.Mapper)
    {
        ["Direction"] = MapDirection,
        ["Wrap"] = MapWrap,
        ["JustifyContent"] = MapJustifyContent,
        ["AlignItems"] = MapAlignItems,
        ["AlignContent"] = MapAlignContent
    };

    public FlexLayoutHandler() : base(Mapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView()
    {
        return new SkiaCrossPlatformLayout();
    }

    /// <summary>Read by the layout's manager; the platform view is re-laid out.</summary>
    public static void MapDirection(FlexLayoutHandler handler, FlexLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the layout's manager; the platform view is re-laid out.</summary>
    public static void MapWrap(FlexLayoutHandler handler, FlexLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the layout's manager; the platform view is re-laid out.</summary>
    public static void MapJustifyContent(FlexLayoutHandler handler, FlexLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the layout's manager; the platform view is re-laid out.</summary>
    public static void MapAlignItems(FlexLayoutHandler handler, FlexLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();

    /// <summary>Read by the layout's manager; the platform view is re-laid out.</summary>
    public static void MapAlignContent(FlexLayoutHandler handler, FlexLayout layout) =>
        handler.PlatformView?.InvalidateMeasure();
}
