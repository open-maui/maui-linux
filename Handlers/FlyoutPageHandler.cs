using System;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class FlyoutPageHandler : ViewHandler<IFlyoutView, SkiaFlyoutPage>
{
	public static IPropertyMapper<IFlyoutView, FlyoutPageHandler> Mapper = (IPropertyMapper<IFlyoutView, FlyoutPageHandler>)(object)new PropertyMapper<IFlyoutView, FlyoutPageHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["IsPresented"] = MapIsPresented,
		["FlyoutWidth"] = MapFlyoutWidth,
		["IsGestureEnabled"] = MapIsGestureEnabled,
		["FlyoutBehavior"] = MapFlyoutBehavior
	};

	public static CommandMapper<IFlyoutView, FlyoutPageHandler> CommandMapper = new CommandMapper<IFlyoutView, FlyoutPageHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public FlyoutPageHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public FlyoutPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaFlyoutPage CreatePlatformView()
	{
		return new SkiaFlyoutPage();
	}

	protected override void ConnectHandler(SkiaFlyoutPage platformView)
	{
		base.ConnectHandler(platformView);
		platformView.IsPresentedChanged += OnIsPresentedChanged;
	}

	protected override void DisconnectHandler(SkiaFlyoutPage platformView)
	{
		platformView.IsPresentedChanged -= OnIsPresentedChanged;
		platformView.Flyout = null;
		platformView.Detail = null;
		base.DisconnectHandler(platformView);
	}

	private void OnIsPresentedChanged(object? sender, EventArgs e)
	{
	}

	public static void MapIsPresented(FlyoutPageHandler handler, IFlyoutView flyoutView)
	{
		if (((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView.IsPresented = flyoutView.IsPresented;
		}
	}

	public static void MapFlyoutWidth(FlyoutPageHandler handler, IFlyoutView flyoutView)
	{
		if (((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView.FlyoutWidth = (float)flyoutView.FlyoutWidth;
		}
	}

	public static void MapIsGestureEnabled(FlyoutPageHandler handler, IFlyoutView flyoutView)
	{
		if (((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView.GestureEnabled = flyoutView.IsGestureEnabled;
		}
	}

	public static void MapFlyoutBehavior(FlyoutPageHandler handler, IFlyoutView flyoutView)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		if (((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView != null)
		{
			SkiaFlyoutPage platformView = ((ViewHandler<IFlyoutView, SkiaFlyoutPage>)(object)handler).PlatformView;
			FlyoutBehavior flyoutBehavior = flyoutView.FlyoutBehavior;
			platformView.FlyoutLayoutBehavior = (int)flyoutBehavior switch
			{
				0 => FlyoutLayoutBehavior.Default, 
				1 => FlyoutLayoutBehavior.Popover, 
				2 => FlyoutLayoutBehavior.Split, 
				_ => FlyoutLayoutBehavior.Default, 
			};
		}
	}
}
