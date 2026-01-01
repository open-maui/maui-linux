using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.Platform.Linux.Handlers;

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
        return new SkiaFlexLayout();
    }

    public static void MapDirection(FlexLayoutHandler handler, FlexLayout layout)
    {
        if (handler.PlatformView is SkiaFlexLayout flexLayout)
        {
            flexLayout.Direction = layout.Direction switch
            {
                Microsoft.Maui.Layouts.FlexDirection.Row => FlexDirection.Row,
                Microsoft.Maui.Layouts.FlexDirection.RowReverse => FlexDirection.RowReverse,
                Microsoft.Maui.Layouts.FlexDirection.Column => FlexDirection.Column,
                Microsoft.Maui.Layouts.FlexDirection.ColumnReverse => FlexDirection.ColumnReverse,
                _ => FlexDirection.Row,
            };
        }
    }

    public static void MapWrap(FlexLayoutHandler handler, FlexLayout layout)
    {
        if (handler.PlatformView is SkiaFlexLayout flexLayout)
        {
            flexLayout.Wrap = layout.Wrap switch
            {
                Microsoft.Maui.Layouts.FlexWrap.NoWrap => FlexWrap.NoWrap,
                Microsoft.Maui.Layouts.FlexWrap.Wrap => FlexWrap.Wrap,
                Microsoft.Maui.Layouts.FlexWrap.Reverse => FlexWrap.WrapReverse,
                _ => FlexWrap.NoWrap,
            };
        }
    }

    public static void MapJustifyContent(FlexLayoutHandler handler, FlexLayout layout)
    {
        if (handler.PlatformView is SkiaFlexLayout flexLayout)
        {
            flexLayout.JustifyContent = layout.JustifyContent switch
            {
                Microsoft.Maui.Layouts.FlexJustify.Start => FlexJustify.Start,
                Microsoft.Maui.Layouts.FlexJustify.Center => FlexJustify.Center,
                Microsoft.Maui.Layouts.FlexJustify.End => FlexJustify.End,
                Microsoft.Maui.Layouts.FlexJustify.SpaceBetween => FlexJustify.SpaceBetween,
                Microsoft.Maui.Layouts.FlexJustify.SpaceAround => FlexJustify.SpaceAround,
                Microsoft.Maui.Layouts.FlexJustify.SpaceEvenly => FlexJustify.SpaceEvenly,
                _ => FlexJustify.Start,
            };
        }
    }

    public static void MapAlignItems(FlexLayoutHandler handler, FlexLayout layout)
    {
        if (handler.PlatformView is SkiaFlexLayout flexLayout)
        {
            flexLayout.AlignItems = layout.AlignItems switch
            {
                Microsoft.Maui.Layouts.FlexAlignItems.Start => FlexAlignItems.Start,
                Microsoft.Maui.Layouts.FlexAlignItems.Center => FlexAlignItems.Center,
                Microsoft.Maui.Layouts.FlexAlignItems.End => FlexAlignItems.End,
                Microsoft.Maui.Layouts.FlexAlignItems.Stretch => FlexAlignItems.Stretch,
                _ => FlexAlignItems.Stretch,
            };
        }
    }

    public static void MapAlignContent(FlexLayoutHandler handler, FlexLayout layout)
    {
        if (handler.PlatformView is SkiaFlexLayout flexLayout)
        {
            flexLayout.AlignContent = layout.AlignContent switch
            {
                Microsoft.Maui.Layouts.FlexAlignContent.Start => FlexAlignContent.Start,
                Microsoft.Maui.Layouts.FlexAlignContent.Center => FlexAlignContent.Center,
                Microsoft.Maui.Layouts.FlexAlignContent.End => FlexAlignContent.End,
                Microsoft.Maui.Layouts.FlexAlignContent.Stretch => FlexAlignContent.Stretch,
                Microsoft.Maui.Layouts.FlexAlignContent.SpaceBetween => FlexAlignContent.SpaceBetween,
                Microsoft.Maui.Layouts.FlexAlignContent.SpaceAround => FlexAlignContent.SpaceAround,
                Microsoft.Maui.Layouts.FlexAlignContent.SpaceEvenly => FlexAlignContent.SpaceAround,
                _ => FlexAlignContent.Stretch,
            };
        }
    }
}
