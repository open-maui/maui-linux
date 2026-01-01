using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ActivityIndicatorHandler : ViewHandler<IActivityIndicator, SkiaActivityIndicator>
{
	public static IPropertyMapper<IActivityIndicator, ActivityIndicatorHandler> Mapper = (IPropertyMapper<IActivityIndicator, ActivityIndicatorHandler>)(object)new PropertyMapper<IActivityIndicator, ActivityIndicatorHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["IsRunning"] = MapIsRunning,
		["Color"] = MapColor,
		["Background"] = MapBackground
	};

	public static CommandMapper<IActivityIndicator, ActivityIndicatorHandler> CommandMapper = new CommandMapper<IActivityIndicator, ActivityIndicatorHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public ActivityIndicatorHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ActivityIndicatorHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaActivityIndicator CreatePlatformView()
	{
		return new SkiaActivityIndicator();
	}

	public static void MapIsRunning(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
	{
		if (((ViewHandler<IActivityIndicator, SkiaActivityIndicator>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IActivityIndicator, SkiaActivityIndicator>)(object)handler).PlatformView.IsRunning = activityIndicator.IsRunning;
		}
	}

	public static void MapColor(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IActivityIndicator, SkiaActivityIndicator>)(object)handler).PlatformView != null && activityIndicator.Color != null)
		{
			((ViewHandler<IActivityIndicator, SkiaActivityIndicator>)(object)handler).PlatformView.Color = activityIndicator.Color.ToSKColor();
		}
	}

	public static void MapBackground(ActivityIndicatorHandler handler, IActivityIndicator activityIndicator)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IActivityIndicator, SkiaActivityIndicator>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)activityIndicator).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IActivityIndicator, SkiaActivityIndicator>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}
}
