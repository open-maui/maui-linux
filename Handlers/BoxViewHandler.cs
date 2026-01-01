using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class BoxViewHandler : ViewHandler<BoxView, SkiaBoxView>
{
	public static IPropertyMapper<BoxView, BoxViewHandler> Mapper = (IPropertyMapper<BoxView, BoxViewHandler>)(object)new PropertyMapper<BoxView, BoxViewHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Color"] = MapColor,
		["CornerRadius"] = MapCornerRadius,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public BoxViewHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)null)
	{
	}

	protected override SkiaBoxView CreatePlatformView()
	{
		return new SkiaBoxView();
	}

	public static void MapColor(BoxViewHandler handler, BoxView boxView)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (boxView.Color != null)
		{
			((ViewHandler<BoxView, SkiaBoxView>)(object)handler).PlatformView.Color = new SKColor((byte)(boxView.Color.Red * 255f), (byte)(boxView.Color.Green * 255f), (byte)(boxView.Color.Blue * 255f), (byte)(boxView.Color.Alpha * 255f));
		}
	}

	public static void MapCornerRadius(BoxViewHandler handler, BoxView boxView)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		SkiaBoxView platformView = ((ViewHandler<BoxView, SkiaBoxView>)(object)handler).PlatformView;
		CornerRadius cornerRadius = boxView.CornerRadius;
		platformView.CornerRadius = (float)((CornerRadius)(ref cornerRadius)).TopLeft;
	}

	public static void MapBackground(BoxViewHandler handler, BoxView boxView)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		Brush background = ((VisualElement)boxView).Background;
		SolidColorBrush val = (SolidColorBrush)(object)((background is SolidColorBrush) ? background : null);
		if (val != null && val.Color != null)
		{
			((ViewHandler<BoxView, SkiaBoxView>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			((ViewHandler<BoxView, SkiaBoxView>)(object)handler).PlatformView.Invalidate();
		}
	}

	public static void MapBackgroundColor(BoxViewHandler handler, BoxView boxView)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (((VisualElement)boxView).BackgroundColor != null)
		{
			((ViewHandler<BoxView, SkiaBoxView>)(object)handler).PlatformView.BackgroundColor = ((VisualElement)boxView).BackgroundColor.ToSKColor();
			((ViewHandler<BoxView, SkiaBoxView>)(object)handler).PlatformView.Invalidate();
		}
	}
}
