using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class TimePickerHandler : ViewHandler<ITimePicker, SkiaTimePicker>
{
	public static IPropertyMapper<ITimePicker, TimePickerHandler> Mapper = (IPropertyMapper<ITimePicker, TimePickerHandler>)(object)new PropertyMapper<ITimePicker, TimePickerHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Time"] = MapTime,
		["Format"] = MapFormat,
		["TextColor"] = MapTextColor,
		["CharacterSpacing"] = MapCharacterSpacing,
		["Background"] = MapBackground
	};

	public static CommandMapper<ITimePicker, TimePickerHandler> CommandMapper = new CommandMapper<ITimePicker, TimePickerHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public TimePickerHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public TimePickerHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaTimePicker CreatePlatformView()
	{
		return new SkiaTimePicker();
	}

	protected override void ConnectHandler(SkiaTimePicker platformView)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		base.ConnectHandler(platformView);
		platformView.TimeSelected += OnTimeSelected;
		Application current = Application.Current;
		if (current != null && (int)current.UserAppTheme == 2)
		{
			platformView.ClockBackgroundColor = new SKColor((byte)30, (byte)30, (byte)30);
			platformView.ClockFaceColor = new SKColor((byte)45, (byte)45, (byte)45);
			platformView.TextColor = new SKColor((byte)224, (byte)224, (byte)224);
			platformView.BorderColor = new SKColor((byte)97, (byte)97, (byte)97);
			platformView.BackgroundColor = new SKColor((byte)45, (byte)45, (byte)45);
		}
	}

	protected override void DisconnectHandler(SkiaTimePicker platformView)
	{
		platformView.TimeSelected -= OnTimeSelected;
		base.DisconnectHandler(platformView);
	}

	private void OnTimeSelected(object? sender, EventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null)
		{
			base.VirtualView.Time = base.PlatformView.Time;
		}
	}

	public static void MapTime(TimePickerHandler handler, ITimePicker timePicker)
	{
		if (((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView.Time = timePicker.Time;
		}
	}

	public static void MapFormat(TimePickerHandler handler, ITimePicker timePicker)
	{
		if (((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView.Format = timePicker.Format ?? "t";
		}
	}

	public static void MapTextColor(TimePickerHandler handler, ITimePicker timePicker)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView != null && ((ITextStyle)timePicker).TextColor != null)
		{
			((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView.TextColor = ((ITextStyle)timePicker).TextColor.ToSKColor();
		}
	}

	public static void MapCharacterSpacing(TimePickerHandler handler, ITimePicker timePicker)
	{
	}

	public static void MapBackground(TimePickerHandler handler, ITimePicker timePicker)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)timePicker).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ITimePicker, SkiaTimePicker>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}
}
