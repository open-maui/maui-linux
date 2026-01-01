using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class BorderHandler : ViewHandler<IBorderView, SkiaBorder>
{
	public static IPropertyMapper<IBorderView, BorderHandler> Mapper = (IPropertyMapper<IBorderView, BorderHandler>)(object)new PropertyMapper<IBorderView, BorderHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Content"] = MapContent,
		["Stroke"] = MapStroke,
		["StrokeThickness"] = MapStrokeThickness,
		["StrokeShape"] = MapStrokeShape,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor,
		["Padding"] = MapPadding
	};

	public static CommandMapper<IBorderView, BorderHandler> CommandMapper = new CommandMapper<IBorderView, BorderHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public BorderHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public BorderHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaBorder CreatePlatformView()
	{
		return new SkiaBorder();
	}

	protected override void ConnectHandler(SkiaBorder platformView)
	{
		base.ConnectHandler(platformView);
		IBorderView virtualView = base.VirtualView;
		View val = (View)(object)((virtualView is View) ? virtualView : null);
		if (val != null)
		{
			platformView.MauiView = val;
		}
		platformView.Tapped += OnPlatformViewTapped;
	}

	protected override void DisconnectHandler(SkiaBorder platformView)
	{
		platformView.Tapped -= OnPlatformViewTapped;
		platformView.MauiView = null;
		base.DisconnectHandler(platformView);
	}

	private void OnPlatformViewTapped(object? sender, EventArgs e)
	{
		IBorderView virtualView = base.VirtualView;
		View val = (View)(object)((virtualView is View) ? virtualView : null);
		if (val != null)
		{
			GestureManager.ProcessTap(val, 0.0, 0.0);
		}
	}

	public static void MapContent(BorderHandler handler, IBorderView border)
	{
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView == null || ((ElementHandler)handler).MauiContext == null)
		{
			return;
		}
		((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.ClearChildren();
		IView presentedContent = ((IContentView)border).PresentedContent;
		if (presentedContent != null)
		{
			if (presentedContent.Handler == null)
			{
				Console.WriteLine("[BorderHandler] Creating handler for content: " + ((object)presentedContent).GetType().Name);
				presentedContent.Handler = presentedContent.ToViewHandler(((ElementHandler)handler).MauiContext);
			}
			IViewHandler handler2 = presentedContent.Handler;
			if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaView skiaView)
			{
				Console.WriteLine("[BorderHandler] Adding content: " + ((object)skiaView).GetType().Name);
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.AddChild(skiaView);
			}
		}
	}

	public static void MapStroke(BorderHandler handler, IBorderView border)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView != null)
		{
			Paint stroke = ((IStroke)border).Stroke;
			SolidPaint val = (SolidPaint)(object)((stroke is SolidPaint) ? stroke : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.Stroke = val.Color.ToSKColor();
			}
		}
	}

	public static void MapStrokeThickness(BorderHandler handler, IBorderView border)
	{
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.StrokeThickness = (float)((IStroke)border).StrokeThickness;
		}
	}

	public static void MapBackground(BorderHandler handler, IBorderView border)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)border).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackgroundColor(BorderHandler handler, IBorderView border)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView != null)
		{
			VisualElement val = (VisualElement)(object)((border is VisualElement) ? border : null);
			if (val != null && val.BackgroundColor != null)
			{
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.BackgroundColor = val.BackgroundColor.ToSKColor();
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.Invalidate();
			}
		}
	}

	public static void MapPadding(BorderHandler handler, IBorderView border)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView != null)
		{
			Thickness padding = ((IPadding)border).Padding;
			((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.PaddingLeft = (float)((Thickness)(ref padding)).Left;
			((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.PaddingTop = (float)((Thickness)(ref padding)).Top;
			((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.PaddingRight = (float)((Thickness)(ref padding)).Right;
			((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.PaddingBottom = (float)((Thickness)(ref padding)).Bottom;
		}
	}

	public static void MapStrokeShape(BorderHandler handler, IBorderView border)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView == null)
		{
			return;
		}
		Border val = (Border)(object)((border is Border) ? border : null);
		if (val != null)
		{
			IShape strokeShape = val.StrokeShape;
			RoundRectangle val2 = (RoundRectangle)(object)((strokeShape is RoundRectangle) ? strokeShape : null);
			if (val2 != null)
			{
				CornerRadius cornerRadius = val2.CornerRadius;
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.CornerRadius = (float)((CornerRadius)(ref cornerRadius)).TopLeft;
			}
			else if (strokeShape is Rectangle)
			{
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.CornerRadius = 0f;
			}
			else if (strokeShape is Ellipse)
			{
				((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.CornerRadius = float.MaxValue;
			}
			((ViewHandler<IBorderView, SkiaBorder>)(object)handler).PlatformView.Invalidate();
		}
	}
}
