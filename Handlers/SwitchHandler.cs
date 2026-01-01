using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class SwitchHandler : ViewHandler<ISwitch, SkiaSwitch>
{
	public static IPropertyMapper<ISwitch, SwitchHandler> Mapper = (IPropertyMapper<ISwitch, SwitchHandler>)(object)new PropertyMapper<ISwitch, SwitchHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["IsOn"] = MapIsOn,
		["TrackColor"] = MapTrackColor,
		["ThumbColor"] = MapThumbColor,
		["Background"] = MapBackground,
		["IsEnabled"] = MapIsEnabled
	};

	public static CommandMapper<ISwitch, SwitchHandler> CommandMapper = new CommandMapper<ISwitch, SwitchHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public SwitchHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public SwitchHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaSwitch CreatePlatformView()
	{
		return new SkiaSwitch();
	}

	protected override void ConnectHandler(SkiaSwitch platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Toggled += OnToggled;
	}

	protected override void DisconnectHandler(SkiaSwitch platformView)
	{
		platformView.Toggled -= OnToggled;
		base.DisconnectHandler(platformView);
	}

	private void OnToggled(object? sender, ToggledEventArgs e)
	{
		if (base.VirtualView != null && base.VirtualView.IsOn != e.Value)
		{
			base.VirtualView.IsOn = e.Value;
		}
	}

	public static void MapIsOn(SwitchHandler handler, ISwitch @switch)
	{
		if (((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView.IsOn = @switch.IsOn;
		}
	}

	public static void MapTrackColor(SwitchHandler handler, ISwitch @switch)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView != null && @switch.TrackColor != null)
		{
			SKColor onTrackColor = @switch.TrackColor.ToSKColor();
			((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView.OnTrackColor = onTrackColor;
			((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView.OffTrackColor = ((SKColor)(ref onTrackColor)).WithAlpha((byte)128);
		}
	}

	public static void MapThumbColor(SwitchHandler handler, ISwitch @switch)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView != null && @switch.ThumbColor != null)
		{
			((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView.ThumbColor = @switch.ThumbColor.ToSKColor();
		}
	}

	public static void MapBackground(SwitchHandler handler, ISwitch @switch)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)@switch).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapIsEnabled(SwitchHandler handler, ISwitch @switch)
	{
		if (((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ISwitch, SkiaSwitch>)(object)handler).PlatformView.IsEnabled = ((IView)@switch).IsEnabled;
		}
	}
}
