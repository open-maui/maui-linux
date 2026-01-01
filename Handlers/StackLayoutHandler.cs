using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class StackLayoutHandler : LayoutHandler
{
	public new static IPropertyMapper<IStackLayout, StackLayoutHandler> Mapper = (IPropertyMapper<IStackLayout, StackLayoutHandler>)(object)new PropertyMapper<IStackLayout, StackLayoutHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)LayoutHandler.Mapper }) { ["Spacing"] = MapSpacing };

	public StackLayoutHandler()
		: base((IPropertyMapper?)(object)Mapper)
	{
	}

	protected override SkiaLayoutView CreatePlatformView()
	{
		return new SkiaStackLayout();
	}

	protected override void ConnectHandler(SkiaLayoutView platformView)
	{
		if (platformView is SkiaStackLayout skiaStackLayout)
		{
			ILayout virtualView = ((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView;
			IStackLayout val = (IStackLayout)(object)((virtualView is IStackLayout) ? virtualView : null);
			if (val != null)
			{
				if (((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView is HorizontalStackLayout)
				{
					skiaStackLayout.Orientation = StackOrientation.Horizontal;
				}
				else if (((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView is VerticalStackLayout || ((ViewHandler<ILayout, SkiaLayoutView>)(object)this).VirtualView is StackLayout)
				{
					skiaStackLayout.Orientation = StackOrientation.Vertical;
				}
				skiaStackLayout.Spacing = (float)val.Spacing;
			}
		}
		base.ConnectHandler(platformView);
	}

	public static void MapSpacing(StackLayoutHandler handler, IStackLayout layout)
	{
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView is SkiaStackLayout skiaStackLayout)
		{
			skiaStackLayout.Spacing = (float)layout.Spacing;
		}
	}
}
