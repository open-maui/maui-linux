// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for ProgressBar control.
/// </summary>
public partial class ProgressBarHandler : ViewHandler<IProgress, SkiaProgressBar>
{
    public static IPropertyMapper<IProgress, ProgressBarHandler> Mapper = new PropertyMapper<IProgress, ProgressBarHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IProgress.Progress)] = MapProgress,
        [nameof(IProgress.ProgressColor)] = MapProgressColor,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IView.Background)] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor,
    };

    public static CommandMapper<IProgress, ProgressBarHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public ProgressBarHandler() : base(Mapper, CommandMapper) { }

    protected override SkiaProgressBar CreatePlatformView() => new SkiaProgressBar();

    public static void MapProgress(ProgressBarHandler handler, IProgress progress)
    {
        handler.PlatformView.Progress = progress.Progress;
    }

    public static void MapProgressColor(ProgressBarHandler handler, IProgress progress)
    {
        if (progress.ProgressColor != null)
            handler.PlatformView.ProgressColor = progress.ProgressColor.ToSKColor();
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(ProgressBarHandler handler, IProgress progress)
    {
        handler.PlatformView.IsEnabled = progress.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(ProgressBarHandler handler, IProgress progress)
    {
        if (progress.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(ProgressBarHandler handler, IProgress progress)
    {
        if (progress is Microsoft.Maui.Controls.VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
