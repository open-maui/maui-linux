using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class GraphicsViewHandler : ViewHandler<IGraphicsView, SkiaGraphicsView>
{
	public static IPropertyMapper<IGraphicsView, GraphicsViewHandler> Mapper = (IPropertyMapper<IGraphicsView, GraphicsViewHandler>)(object)new PropertyMapper<IGraphicsView, GraphicsViewHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Drawable"] = MapDrawable,
		["Background"] = MapBackground
	};

	public static CommandMapper<IGraphicsView, GraphicsViewHandler> CommandMapper = new CommandMapper<IGraphicsView, GraphicsViewHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper) { ["Invalidate"] = MapInvalidate };

	public GraphicsViewHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public GraphicsViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaGraphicsView CreatePlatformView()
	{
		return new SkiaGraphicsView();
	}

	public static void MapDrawable(GraphicsViewHandler handler, IGraphicsView graphicsView)
	{
		if (((ViewHandler<IGraphicsView, SkiaGraphicsView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IGraphicsView, SkiaGraphicsView>)(object)handler).PlatformView.Drawable = graphicsView.Drawable;
		}
	}

	public static void MapBackground(GraphicsViewHandler handler, IGraphicsView graphicsView)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IGraphicsView, SkiaGraphicsView>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)graphicsView).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IGraphicsView, SkiaGraphicsView>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapInvalidate(GraphicsViewHandler handler, IGraphicsView graphicsView, object? args)
	{
		((ViewHandler<IGraphicsView, SkiaGraphicsView>)(object)handler).PlatformView?.Invalidate();
	}
}
