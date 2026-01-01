using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class DatePickerHandler : ViewHandler<IDatePicker, SkiaDatePicker>
{
	public static IPropertyMapper<IDatePicker, DatePickerHandler> Mapper = (IPropertyMapper<IDatePicker, DatePickerHandler>)(object)new PropertyMapper<IDatePicker, DatePickerHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Date"] = MapDate,
		["MinimumDate"] = MapMinimumDate,
		["MaximumDate"] = MapMaximumDate,
		["Format"] = MapFormat,
		["TextColor"] = MapTextColor,
		["CharacterSpacing"] = MapCharacterSpacing,
		["Background"] = MapBackground
	};

	public static CommandMapper<IDatePicker, DatePickerHandler> CommandMapper = new CommandMapper<IDatePicker, DatePickerHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public DatePickerHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public DatePickerHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaDatePicker CreatePlatformView()
	{
		return new SkiaDatePicker();
	}

	protected override void ConnectHandler(SkiaDatePicker platformView)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		base.ConnectHandler(platformView);
		platformView.DateSelected += OnDateSelected;
		Application current = Application.Current;
		if (current != null && (int)current.UserAppTheme == 2)
		{
			platformView.CalendarBackgroundColor = new SKColor((byte)30, (byte)30, (byte)30);
			platformView.TextColor = new SKColor((byte)224, (byte)224, (byte)224);
			platformView.BorderColor = new SKColor((byte)97, (byte)97, (byte)97);
			platformView.DisabledDayColor = new SKColor((byte)97, (byte)97, (byte)97);
			platformView.BackgroundColor = new SKColor((byte)45, (byte)45, (byte)45);
		}
	}

	protected override void DisconnectHandler(SkiaDatePicker platformView)
	{
		platformView.DateSelected -= OnDateSelected;
		base.DisconnectHandler(platformView);
	}

	private void OnDateSelected(object? sender, EventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null)
		{
			base.VirtualView.Date = base.PlatformView.Date;
		}
	}

	public static void MapDate(DatePickerHandler handler, IDatePicker datePicker)
	{
		if (((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView.Date = datePicker.Date;
		}
	}

	public static void MapMinimumDate(DatePickerHandler handler, IDatePicker datePicker)
	{
		if (((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView.MinimumDate = datePicker.MinimumDate;
		}
	}

	public static void MapMaximumDate(DatePickerHandler handler, IDatePicker datePicker)
	{
		if (((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView.MaximumDate = datePicker.MaximumDate;
		}
	}

	public static void MapFormat(DatePickerHandler handler, IDatePicker datePicker)
	{
		if (((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView.Format = datePicker.Format ?? "d";
		}
	}

	public static void MapTextColor(DatePickerHandler handler, IDatePicker datePicker)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView != null && ((ITextStyle)datePicker).TextColor != null)
		{
			((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView.TextColor = ((ITextStyle)datePicker).TextColor.ToSKColor();
		}
	}

	public static void MapCharacterSpacing(DatePickerHandler handler, IDatePicker datePicker)
	{
	}

	public static void MapBackground(DatePickerHandler handler, IDatePicker datePicker)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)datePicker).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IDatePicker, SkiaDatePicker>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}
}
