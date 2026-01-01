using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class FlexLayoutHandler : LayoutHandler
{
	public new static IPropertyMapper<FlexLayout, FlexLayoutHandler> Mapper = (IPropertyMapper<FlexLayout, FlexLayoutHandler>)(object)new PropertyMapper<FlexLayout, FlexLayoutHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)LayoutHandler.Mapper })
	{
		["Direction"] = MapDirection,
		["Wrap"] = MapWrap,
		["JustifyContent"] = MapJustifyContent,
		["AlignItems"] = MapAlignItems,
		["AlignContent"] = MapAlignContent
	};

	public FlexLayoutHandler()
		: base((IPropertyMapper?)(object)Mapper)
	{
	}

	protected override SkiaLayoutView CreatePlatformView()
	{
		return new SkiaFlexLayout();
	}

	public static void MapDirection(FlexLayoutHandler handler, FlexLayout layout)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected I4, but got Unknown
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaFlexLayout skiaFlexLayout)
		{
			SkiaFlexLayout skiaFlexLayout2 = skiaFlexLayout;
			FlexDirection direction = layout.Direction;
			skiaFlexLayout2.Direction = (int)direction switch
			{
				0 => FlexDirection.Row, 
				1 => FlexDirection.RowReverse, 
				2 => FlexDirection.Column, 
				3 => FlexDirection.ColumnReverse, 
				_ => FlexDirection.Row, 
			};
		}
	}

	public static void MapWrap(FlexLayoutHandler handler, FlexLayout layout)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected I4, but got Unknown
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaFlexLayout skiaFlexLayout)
		{
			SkiaFlexLayout skiaFlexLayout2 = skiaFlexLayout;
			FlexWrap wrap = layout.Wrap;
			skiaFlexLayout2.Wrap = (int)wrap switch
			{
				0 => FlexWrap.NoWrap, 
				1 => FlexWrap.Wrap, 
				2 => FlexWrap.WrapReverse, 
				_ => FlexWrap.NoWrap, 
			};
		}
	}

	public static void MapJustifyContent(FlexLayoutHandler handler, FlexLayout layout)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected I4, but got Unknown
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaFlexLayout skiaFlexLayout)
		{
			SkiaFlexLayout skiaFlexLayout2 = skiaFlexLayout;
			FlexJustify justifyContent = layout.JustifyContent;
			skiaFlexLayout2.JustifyContent = (justifyContent - 2) switch
			{
				1 => FlexJustify.Start, 
				0 => FlexJustify.Center, 
				2 => FlexJustify.End, 
				3 => FlexJustify.SpaceBetween, 
				4 => FlexJustify.SpaceAround, 
				5 => FlexJustify.SpaceEvenly, 
				_ => FlexJustify.Start, 
			};
		}
	}

	public static void MapAlignItems(FlexLayoutHandler handler, FlexLayout layout)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected I4, but got Unknown
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaFlexLayout skiaFlexLayout)
		{
			SkiaFlexLayout skiaFlexLayout2 = skiaFlexLayout;
			FlexAlignItems alignItems = layout.AlignItems;
			skiaFlexLayout2.AlignItems = (alignItems - 1) switch
			{
				2 => FlexAlignItems.Start, 
				1 => FlexAlignItems.Center, 
				3 => FlexAlignItems.End, 
				0 => FlexAlignItems.Stretch, 
				_ => FlexAlignItems.Stretch, 
			};
		}
	}

	public static void MapAlignContent(FlexLayoutHandler handler, FlexLayout layout)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected I4, but got Unknown
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaFlexLayout skiaFlexLayout)
		{
			SkiaFlexLayout skiaFlexLayout2 = skiaFlexLayout;
			FlexAlignContent alignContent = layout.AlignContent;
			skiaFlexLayout2.AlignContent = (alignContent - 1) switch
			{
				2 => FlexAlignContent.Start, 
				1 => FlexAlignContent.Center, 
				3 => FlexAlignContent.End, 
				0 => FlexAlignContent.Stretch, 
				4 => FlexAlignContent.SpaceBetween, 
				5 => FlexAlignContent.SpaceAround, 
				6 => FlexAlignContent.SpaceAround, 
				_ => FlexAlignContent.Stretch, 
			};
		}
	}
}
