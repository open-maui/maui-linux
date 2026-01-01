using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Primitives;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class CheckBoxHandler : ViewHandler<ICheckBox, SkiaCheckBox>
{
	public static IPropertyMapper<ICheckBox, CheckBoxHandler> Mapper = (IPropertyMapper<ICheckBox, CheckBoxHandler>)(object)new PropertyMapper<ICheckBox, CheckBoxHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["IsChecked"] = MapIsChecked,
		["Foreground"] = MapForeground,
		["Background"] = MapBackground,
		["IsEnabled"] = MapIsEnabled,
		["VerticalLayoutAlignment"] = MapVerticalLayoutAlignment,
		["HorizontalLayoutAlignment"] = MapHorizontalLayoutAlignment
	};

	public static CommandMapper<ICheckBox, CheckBoxHandler> CommandMapper = new CommandMapper<ICheckBox, CheckBoxHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public CheckBoxHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public CheckBoxHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaCheckBox CreatePlatformView()
	{
		return new SkiaCheckBox();
	}

	protected override void ConnectHandler(SkiaCheckBox platformView)
	{
		base.ConnectHandler(platformView);
		platformView.CheckedChanged += OnCheckedChanged;
	}

	protected override void DisconnectHandler(SkiaCheckBox platformView)
	{
		platformView.CheckedChanged -= OnCheckedChanged;
		base.DisconnectHandler(platformView);
	}

	private void OnCheckedChanged(object? sender, CheckedChangedEventArgs e)
	{
		if (base.VirtualView != null && base.VirtualView.IsChecked != e.IsChecked)
		{
			base.VirtualView.IsChecked = e.IsChecked;
		}
	}

	public static void MapIsChecked(CheckBoxHandler handler, ICheckBox checkBox)
	{
		if (((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView.IsChecked = checkBox.IsChecked;
		}
	}

	public static void MapForeground(CheckBoxHandler handler, ICheckBox checkBox)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView != null)
		{
			Paint foreground = checkBox.Foreground;
			SolidPaint val = (SolidPaint)(object)((foreground is SolidPaint) ? foreground : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView.CheckColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackground(CheckBoxHandler handler, ICheckBox checkBox)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)checkBox).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapIsEnabled(CheckBoxHandler handler, ICheckBox checkBox)
	{
		if (((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView.IsEnabled = ((IView)checkBox).IsEnabled;
		}
	}

	public static void MapVerticalLayoutAlignment(CheckBoxHandler handler, ICheckBox checkBox)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected I4, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView != null)
		{
			SkiaCheckBox platformView = ((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView;
			LayoutAlignment verticalLayoutAlignment = ((IView)checkBox).VerticalLayoutAlignment;
			platformView.VerticalOptions = (LayoutOptions)((int)verticalLayoutAlignment switch
			{
				1 => LayoutOptions.Start, 
				2 => LayoutOptions.Center, 
				3 => LayoutOptions.End, 
				0 => LayoutOptions.Fill, 
				_ => LayoutOptions.Fill, 
			});
		}
	}

	public static void MapHorizontalLayoutAlignment(CheckBoxHandler handler, ICheckBox checkBox)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected I4, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView != null)
		{
			SkiaCheckBox platformView = ((ViewHandler<ICheckBox, SkiaCheckBox>)(object)handler).PlatformView;
			LayoutAlignment horizontalLayoutAlignment = ((IView)checkBox).HorizontalLayoutAlignment;
			platformView.HorizontalOptions = (LayoutOptions)((int)horizontalLayoutAlignment switch
			{
				1 => LayoutOptions.Start, 
				2 => LayoutOptions.Center, 
				3 => LayoutOptions.End, 
				0 => LayoutOptions.Fill, 
				_ => LayoutOptions.Start, 
			});
		}
	}
}
