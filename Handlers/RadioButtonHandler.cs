using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class RadioButtonHandler : ViewHandler<IRadioButton, SkiaRadioButton>
{
	public static IPropertyMapper<IRadioButton, RadioButtonHandler> Mapper = (IPropertyMapper<IRadioButton, RadioButtonHandler>)(object)new PropertyMapper<IRadioButton, RadioButtonHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["IsChecked"] = MapIsChecked,
		["TextColor"] = MapTextColor,
		["Font"] = MapFont,
		["Background"] = MapBackground
	};

	public static CommandMapper<IRadioButton, RadioButtonHandler> CommandMapper = new CommandMapper<IRadioButton, RadioButtonHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public RadioButtonHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public RadioButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaRadioButton CreatePlatformView()
	{
		return new SkiaRadioButton();
	}

	protected override void ConnectHandler(SkiaRadioButton platformView)
	{
		base.ConnectHandler(platformView);
		platformView.CheckedChanged += OnCheckedChanged;
		IRadioButton virtualView = base.VirtualView;
		RadioButton val = (RadioButton)(object)((virtualView is RadioButton) ? virtualView : null);
		if (val != null)
		{
			platformView.Content = val.Content?.ToString() ?? "";
			platformView.GroupName = val.GroupName;
			platformView.Value = val.Value;
		}
	}

	protected override void DisconnectHandler(SkiaRadioButton platformView)
	{
		platformView.CheckedChanged -= OnCheckedChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnCheckedChanged(object? sender, EventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null)
		{
			base.VirtualView.IsChecked = base.PlatformView.IsChecked;
		}
	}

	public static void MapIsChecked(RadioButtonHandler handler, IRadioButton radioButton)
	{
		if (((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView.IsChecked = radioButton.IsChecked;
		}
	}

	public static void MapTextColor(RadioButtonHandler handler, IRadioButton radioButton)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView != null && ((ITextStyle)radioButton).TextColor != null)
		{
			((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView.TextColor = ((ITextStyle)radioButton).TextColor.ToSKColor();
		}
	}

	public static void MapFont(RadioButtonHandler handler, IRadioButton radioButton)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView != null)
		{
			Font font = ((ITextStyle)radioButton).Font;
			if (((Font)(ref font)).Size > 0.0)
			{
				SkiaRadioButton platformView = ((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView;
				font = ((ITextStyle)radioButton).Font;
				platformView.FontSize = (float)((Font)(ref font)).Size;
			}
		}
	}

	public static void MapBackground(RadioButtonHandler handler, IRadioButton radioButton)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)radioButton).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IRadioButton, SkiaRadioButton>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}
}
