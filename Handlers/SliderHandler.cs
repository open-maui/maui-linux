using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class SliderHandler : ViewHandler<ISlider, SkiaSlider>
{
	public static IPropertyMapper<ISlider, SliderHandler> Mapper = (IPropertyMapper<ISlider, SliderHandler>)(object)new PropertyMapper<ISlider, SliderHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Minimum"] = MapMinimum,
		["Maximum"] = MapMaximum,
		["Value"] = MapValue,
		["MinimumTrackColor"] = MapMinimumTrackColor,
		["MaximumTrackColor"] = MapMaximumTrackColor,
		["ThumbColor"] = MapThumbColor,
		["Background"] = MapBackground,
		["IsEnabled"] = MapIsEnabled
	};

	public static CommandMapper<ISlider, SliderHandler> CommandMapper = new CommandMapper<ISlider, SliderHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public SliderHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public SliderHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaSlider CreatePlatformView()
	{
		return new SkiaSlider();
	}

	protected override void ConnectHandler(SkiaSlider platformView)
	{
		base.ConnectHandler(platformView);
		platformView.ValueChanged += OnValueChanged;
		platformView.DragStarted += OnDragStarted;
		platformView.DragCompleted += OnDragCompleted;
		if (base.VirtualView != null)
		{
			MapMinimum(this, base.VirtualView);
			MapMaximum(this, base.VirtualView);
			MapValue(this, base.VirtualView);
			MapIsEnabled(this, base.VirtualView);
		}
	}

	protected override void DisconnectHandler(SkiaSlider platformView)
	{
		platformView.ValueChanged -= OnValueChanged;
		platformView.DragStarted -= OnDragStarted;
		platformView.DragCompleted -= OnDragCompleted;
		base.DisconnectHandler(platformView);
	}

	private void OnValueChanged(object? sender, SliderValueChangedEventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null && Math.Abs(((IRange)base.VirtualView).Value - e.NewValue) > 0.0001)
		{
			((IRange)base.VirtualView).Value = e.NewValue;
		}
	}

	private void OnDragStarted(object? sender, EventArgs e)
	{
		ISlider virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.DragStarted();
		}
	}

	private void OnDragCompleted(object? sender, EventArgs e)
	{
		ISlider virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.DragCompleted();
		}
	}

	public static void MapMinimum(SliderHandler handler, ISlider slider)
	{
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.Minimum = ((IRange)slider).Minimum;
		}
	}

	public static void MapMaximum(SliderHandler handler, ISlider slider)
	{
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.Maximum = ((IRange)slider).Maximum;
		}
	}

	public static void MapValue(SliderHandler handler, ISlider slider)
	{
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null && Math.Abs(((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.Value - ((IRange)slider).Value) > 0.0001)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.Value = ((IRange)slider).Value;
		}
	}

	public static void MapMinimumTrackColor(SliderHandler handler, ISlider slider)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null && slider.MinimumTrackColor != null)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.ActiveTrackColor = slider.MinimumTrackColor.ToSKColor();
		}
	}

	public static void MapMaximumTrackColor(SliderHandler handler, ISlider slider)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null && slider.MaximumTrackColor != null)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.TrackColor = slider.MaximumTrackColor.ToSKColor();
		}
	}

	public static void MapThumbColor(SliderHandler handler, ISlider slider)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null && slider.ThumbColor != null)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.ThumbColor = slider.ThumbColor.ToSKColor();
		}
	}

	public static void MapBackground(SliderHandler handler, ISlider slider)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)slider).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapIsEnabled(SliderHandler handler, ISlider slider)
	{
		if (((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.IsEnabled = ((IView)slider).IsEnabled;
			((ViewHandler<ISlider, SkiaSlider>)(object)handler).PlatformView.Invalidate();
		}
	}
}
