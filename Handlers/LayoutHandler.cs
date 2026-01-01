using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class LayoutHandler : ViewHandler<ILayout, SkiaLayoutView>
{
	public static IPropertyMapper<ILayout, LayoutHandler> Mapper = (IPropertyMapper<ILayout, LayoutHandler>)(object)new PropertyMapper<ILayout, LayoutHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["ClipsToBounds"] = MapClipsToBounds,
		["Background"] = MapBackground,
		["Padding"] = MapPadding
	};

	public static CommandMapper<ILayout, LayoutHandler> CommandMapper = new CommandMapper<ILayout, LayoutHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper)
	{
		["Add"] = MapAdd,
		["Remove"] = MapRemove,
		["Clear"] = MapClear,
		["Insert"] = MapInsert,
		["Update"] = MapUpdate
	};

	public LayoutHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public LayoutHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaLayoutView CreatePlatformView()
	{
		return new SkiaStackLayout();
	}

	protected override void ConnectHandler(SkiaLayoutView platformView)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		base.ConnectHandler(platformView);
		if (base.VirtualView == null || ((ElementHandler)this).MauiContext == null)
		{
			return;
		}
		ILayout virtualView = base.VirtualView;
		VisualElement val = (VisualElement)(object)((virtualView is VisualElement) ? virtualView : null);
		if (val != null && val.BackgroundColor != null)
		{
			platformView.BackgroundColor = val.BackgroundColor.ToSKColor();
		}
		for (int i = 0; i < ((ICollection<IView>)base.VirtualView).Count; i++)
		{
			IView val2 = ((IList<IView>)base.VirtualView)[i];
			if (val2 != null)
			{
				if (val2.Handler == null)
				{
					val2.Handler = val2.ToViewHandler(((ElementHandler)this).MauiContext);
				}
				IViewHandler handler = val2.Handler;
				if (((handler != null) ? ((IElementHandler)handler).PlatformView : null) is SkiaView child)
				{
					platformView.AddChild(child);
				}
			}
		}
	}

	public static void MapClipsToBounds(LayoutHandler handler, ILayout layout)
	{
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.ClipToBounds = layout.ClipsToBounds;
		}
	}

	public static void MapBackground(LayoutHandler handler, ILayout layout)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)layout).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapAdd(LayoutHandler handler, ILayout layout, object? arg)
	{
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView == null || !(arg is LayoutHandlerUpdate { Index: var index } layoutHandlerUpdate))
		{
			return;
		}
		IView? view = layoutHandlerUpdate.View;
		object obj;
		if (view == null)
		{
			obj = null;
		}
		else
		{
			IViewHandler handler2 = view.Handler;
			obj = ((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null);
		}
		if (obj is SkiaView child)
		{
			if (index >= 0 && index < ((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.Children.Count)
			{
				((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.InsertChild(index, child);
			}
			else
			{
				((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.AddChild(child);
			}
		}
	}

	public static void MapRemove(LayoutHandler handler, ILayout layout, object? arg)
	{
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView != null && arg is LayoutHandlerUpdate { Index: var index } && index >= 0 && index < ((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.Children.Count)
		{
			((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.RemoveChildAt(index);
		}
	}

	public static void MapClear(LayoutHandler handler, ILayout layout, object? arg)
	{
		((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView?.ClearChildren();
	}

	public static void MapInsert(LayoutHandler handler, ILayout layout, object? arg)
	{
		MapAdd(handler, layout, arg);
	}

	public static void MapUpdate(LayoutHandler handler, ILayout layout, object? arg)
	{
		((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView?.InvalidateMeasure();
	}

	public static void MapPadding(LayoutHandler handler, ILayout layout)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView != null)
		{
			if (layout != null)
			{
				Thickness padding = ((IPadding)layout).Padding;
				((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.Padding = new SKRect((float)((Thickness)(ref padding)).Left, (float)((Thickness)(ref padding)).Top, (float)((Thickness)(ref padding)).Right, (float)((Thickness)(ref padding)).Bottom);
				((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.InvalidateMeasure();
				((ViewHandler<ILayout, SkiaLayoutView>)(object)handler).PlatformView.Invalidate();
			}
		}
	}
}
