using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ProgressBarHandler : ViewHandler<IProgress, SkiaProgressBar>
{
	public static IPropertyMapper<IProgress, ProgressBarHandler> Mapper = (IPropertyMapper<IProgress, ProgressBarHandler>)(object)new PropertyMapper<IProgress, ProgressBarHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Progress"] = MapProgress,
		["ProgressColor"] = MapProgressColor,
		["IsEnabled"] = MapIsEnabled,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public static CommandMapper<IProgress, ProgressBarHandler> CommandMapper = new CommandMapper<IProgress, ProgressBarHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public ProgressBarHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	protected override SkiaProgressBar CreatePlatformView()
	{
		return new SkiaProgressBar();
	}

	protected override void ConnectHandler(SkiaProgressBar platformView)
	{
		base.ConnectHandler(platformView);
		IProgress virtualView = base.VirtualView;
		BindableObject val = (BindableObject)(object)((virtualView is BindableObject) ? virtualView : null);
		if (val != null)
		{
			val.PropertyChanged += OnVirtualViewPropertyChanged;
		}
		IProgress virtualView2 = base.VirtualView;
		VisualElement val2 = (VisualElement)(object)((virtualView2 is VisualElement) ? virtualView2 : null);
		if (val2 != null)
		{
			platformView.IsVisible = val2.IsVisible;
		}
	}

	protected override void DisconnectHandler(SkiaProgressBar platformView)
	{
		IProgress virtualView = base.VirtualView;
		BindableObject val = (BindableObject)(object)((virtualView is BindableObject) ? virtualView : null);
		if (val != null)
		{
			val.PropertyChanged -= OnVirtualViewPropertyChanged;
		}
		base.DisconnectHandler(platformView);
	}

	private void OnVirtualViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		IProgress virtualView = base.VirtualView;
		VisualElement val = (VisualElement)(object)((virtualView is VisualElement) ? virtualView : null);
		if (val != null && e.PropertyName == "IsVisible")
		{
			base.PlatformView.IsVisible = val.IsVisible;
			base.PlatformView.Invalidate();
		}
	}

	public static void MapProgress(ProgressBarHandler handler, IProgress progress)
	{
		((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.Progress = progress.Progress;
	}

	public static void MapProgressColor(ProgressBarHandler handler, IProgress progress)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (progress.ProgressColor != null)
		{
			((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.ProgressColor = progress.ProgressColor.ToSKColor();
		}
		((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.Invalidate();
	}

	public static void MapIsEnabled(ProgressBarHandler handler, IProgress progress)
	{
		((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.IsEnabled = ((IView)progress).IsEnabled;
		((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.Invalidate();
	}

	public static void MapBackground(ProgressBarHandler handler, IProgress progress)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		Paint background = ((IView)progress).Background;
		SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
		if (val != null && val.Color != null)
		{
			((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.Invalidate();
		}
	}

	public static void MapBackgroundColor(ProgressBarHandler handler, IProgress progress)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		VisualElement val = (VisualElement)(object)((progress is VisualElement) ? progress : null);
		if (val != null && val.BackgroundColor != null)
		{
			((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.BackgroundColor = val.BackgroundColor.ToSKColor();
			((ViewHandler<IProgress, SkiaProgressBar>)(object)handler).PlatformView.Invalidate();
		}
	}
}
