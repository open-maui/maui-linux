using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ButtonHandler : ViewHandler<IButton, SkiaButton>
{
	public static IPropertyMapper<IButton, ButtonHandler> Mapper = (IPropertyMapper<IButton, ButtonHandler>)(object)new PropertyMapper<IButton, ButtonHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["StrokeColor"] = MapStrokeColor,
		["StrokeThickness"] = MapStrokeThickness,
		["CornerRadius"] = MapCornerRadius,
		["Background"] = MapBackground,
		["Padding"] = MapPadding,
		["IsEnabled"] = MapIsEnabled
	};

	public static CommandMapper<IButton, ButtonHandler> CommandMapper = new CommandMapper<IButton, ButtonHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public ButtonHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaButton CreatePlatformView()
	{
		return new SkiaButton();
	}

	protected override void ConnectHandler(SkiaButton platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Clicked += OnClicked;
		platformView.Pressed += OnPressed;
		platformView.Released += OnReleased;
		if (base.VirtualView != null)
		{
			MapStrokeColor(this, base.VirtualView);
			MapStrokeThickness(this, base.VirtualView);
			MapCornerRadius(this, base.VirtualView);
			MapBackground(this, base.VirtualView);
			MapPadding(this, base.VirtualView);
			MapIsEnabled(this, base.VirtualView);
		}
	}

	protected override void DisconnectHandler(SkiaButton platformView)
	{
		platformView.Clicked -= OnClicked;
		platformView.Pressed -= OnPressed;
		platformView.Released -= OnReleased;
		base.DisconnectHandler(platformView);
	}

	private void OnClicked(object? sender, EventArgs e)
	{
		IButton virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.Clicked();
		}
	}

	private void OnPressed(object? sender, EventArgs e)
	{
		IButton virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.Pressed();
		}
	}

	private void OnReleased(object? sender, EventArgs e)
	{
		IButton virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.Released();
		}
	}

	public static void MapStrokeColor(ButtonHandler handler, IButton button)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			Color strokeColor = ((IButtonStroke)button).StrokeColor;
			if (strokeColor != null)
			{
				((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.BorderColor = strokeColor.ToSKColor();
			}
		}
	}

	public static void MapStrokeThickness(ButtonHandler handler, IButton button)
	{
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.BorderWidth = (float)((IButtonStroke)button).StrokeThickness;
		}
	}

	public static void MapCornerRadius(ButtonHandler handler, IButton button)
	{
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.CornerRadius = ((IButtonStroke)button).CornerRadius;
		}
	}

	public static void MapBackground(ButtonHandler handler, IButton button)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)button).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.ButtonBackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapPadding(ButtonHandler handler, IButton button)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			Thickness padding = ((IPadding)button).Padding;
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.Padding = new SKRect((float)((Thickness)(ref padding)).Left, (float)((Thickness)(ref padding)).Top, (float)((Thickness)(ref padding)).Right, (float)((Thickness)(ref padding)).Bottom);
		}
	}

	public static void MapIsEnabled(ButtonHandler handler, IButton button)
	{
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			Console.WriteLine($"[ButtonHandler] MapIsEnabled - Text='{((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.Text}', IsEnabled={((IView)button).IsEnabled}");
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.IsEnabled = ((IView)button).IsEnabled;
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.Invalidate();
		}
	}
}
