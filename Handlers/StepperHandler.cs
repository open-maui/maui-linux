using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class StepperHandler : ViewHandler<IStepper, SkiaStepper>
{
	public static IPropertyMapper<IStepper, StepperHandler> Mapper = (IPropertyMapper<IStepper, StepperHandler>)(object)new PropertyMapper<IStepper, StepperHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Value"] = MapValue,
		["Minimum"] = MapMinimum,
		["Maximum"] = MapMaximum,
		["Increment"] = MapIncrement,
		["Background"] = MapBackground,
		["IsEnabled"] = MapIsEnabled
	};

	public static CommandMapper<IStepper, StepperHandler> CommandMapper = new CommandMapper<IStepper, StepperHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public StepperHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public StepperHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaStepper CreatePlatformView()
	{
		return new SkiaStepper();
	}

	protected override void ConnectHandler(SkiaStepper platformView)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		base.ConnectHandler(platformView);
		platformView.ValueChanged += OnValueChanged;
		Application current = Application.Current;
		if (current != null && (int)current.UserAppTheme == 2)
		{
			platformView.ButtonBackgroundColor = new SKColor((byte)66, (byte)66, (byte)66);
			platformView.ButtonPressedColor = new SKColor((byte)97, (byte)97, (byte)97);
			platformView.ButtonDisabledColor = new SKColor((byte)48, (byte)48, (byte)48);
			platformView.SymbolColor = new SKColor((byte)224, (byte)224, (byte)224);
			platformView.SymbolDisabledColor = new SKColor((byte)97, (byte)97, (byte)97);
			platformView.BorderColor = new SKColor((byte)97, (byte)97, (byte)97);
		}
	}

	protected override void DisconnectHandler(SkiaStepper platformView)
	{
		platformView.ValueChanged -= OnValueChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnValueChanged(object? sender, EventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null)
		{
			((IRange)base.VirtualView).Value = base.PlatformView.Value;
		}
	}

	public static void MapValue(StepperHandler handler, IStepper stepper)
	{
		if (((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView.Value = ((IRange)stepper).Value;
		}
	}

	public static void MapMinimum(StepperHandler handler, IStepper stepper)
	{
		if (((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView.Minimum = ((IRange)stepper).Minimum;
		}
	}

	public static void MapMaximum(StepperHandler handler, IStepper stepper)
	{
		if (((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView.Maximum = ((IRange)stepper).Maximum;
		}
	}

	public static void MapBackground(StepperHandler handler, IStepper stepper)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)stepper).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapIncrement(StepperHandler handler, IStepper stepper)
	{
		if (((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView != null)
		{
			Stepper val = (Stepper)(object)((stepper is Stepper) ? stepper : null);
			if (val != null)
			{
				((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView.Increment = val.Increment;
			}
		}
	}

	public static void MapIsEnabled(StepperHandler handler, IStepper stepper)
	{
		if (((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IStepper, SkiaStepper>)(object)handler).PlatformView.IsEnabled = ((IView)stepper).IsEnabled;
		}
	}
}
