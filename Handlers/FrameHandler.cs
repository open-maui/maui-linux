using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class FrameHandler : ViewHandler<Frame, SkiaFrame>
{
	public static IPropertyMapper<Frame, FrameHandler> Mapper = (IPropertyMapper<Frame, FrameHandler>)(object)new PropertyMapper<Frame, FrameHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["BorderColor"] = MapBorderColor,
		["CornerRadius"] = MapCornerRadius,
		["HasShadow"] = MapHasShadow,
		["BackgroundColor"] = MapBackgroundColor,
		["Padding"] = MapPadding,
		["Content"] = MapContent
	};

	public FrameHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)null)
	{
	}

	public FrameHandler(IPropertyMapper? mapper)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)null)
	{
	}

	protected override SkiaFrame CreatePlatformView()
	{
		return new SkiaFrame();
	}

	protected override void ConnectHandler(SkiaFrame platformView)
	{
		base.ConnectHandler(platformView);
		View virtualView = (View)(object)base.VirtualView;
		if (virtualView != null)
		{
			platformView.MauiView = virtualView;
		}
		platformView.Tapped += OnPlatformViewTapped;
	}

	protected override void DisconnectHandler(SkiaFrame platformView)
	{
		platformView.Tapped -= OnPlatformViewTapped;
		platformView.MauiView = null;
		base.DisconnectHandler(platformView);
	}

	private void OnPlatformViewTapped(object? sender, EventArgs e)
	{
		View virtualView = (View)(object)base.VirtualView;
		if (virtualView != null)
		{
			GestureManager.ProcessTap(virtualView, 0.0, 0.0);
		}
	}

	public static void MapBorderColor(FrameHandler handler, Frame frame)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (frame.BorderColor != null)
		{
			((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView.Stroke = new SKColor((byte)(frame.BorderColor.Red * 255f), (byte)(frame.BorderColor.Green * 255f), (byte)(frame.BorderColor.Blue * 255f), (byte)(frame.BorderColor.Alpha * 255f));
		}
	}

	public static void MapCornerRadius(FrameHandler handler, Frame frame)
	{
		((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView.CornerRadius = frame.CornerRadius;
	}

	public static void MapHasShadow(FrameHandler handler, Frame frame)
	{
		((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView.HasShadow = frame.HasShadow;
	}

	public static void MapBackgroundColor(FrameHandler handler, Frame frame)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (((VisualElement)frame).BackgroundColor != null)
		{
			((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView.BackgroundColor = new SKColor((byte)(((VisualElement)frame).BackgroundColor.Red * 255f), (byte)(((VisualElement)frame).BackgroundColor.Green * 255f), (byte)(((VisualElement)frame).BackgroundColor.Blue * 255f), (byte)(((VisualElement)frame).BackgroundColor.Alpha * 255f));
		}
	}

	public static void MapPadding(FrameHandler handler, Frame frame)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		SkiaFrame platformView = ((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView;
		Thickness padding = ((Layout)frame).Padding;
		float left = (float)((Thickness)(ref padding)).Left;
		padding = ((Layout)frame).Padding;
		float top = (float)((Thickness)(ref padding)).Top;
		padding = ((Layout)frame).Padding;
		float right = (float)((Thickness)(ref padding)).Right;
		padding = ((Layout)frame).Padding;
		platformView.SetPadding(left, top, right, (float)((Thickness)(ref padding)).Bottom);
	}

	public static void MapContent(FrameHandler handler, Frame frame)
	{
		if (((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView == null || ((ElementHandler)handler).MauiContext == null)
		{
			return;
		}
		((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView.ClearChildren();
		View content = ((ContentView)frame).Content;
		if (content != null)
		{
			if (((VisualElement)content).Handler == null)
			{
				((VisualElement)content).Handler = ((IView)(object)content).ToViewHandler(((ElementHandler)handler).MauiContext);
			}
			IViewHandler handler2 = ((VisualElement)content).Handler;
			if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaView child)
			{
				((ViewHandler<Frame, SkiaFrame>)(object)handler).PlatformView.AddChild(child);
			}
		}
	}
}
