// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for ProgressBar control.
/// </summary>
public class ProgressBarHandler : ViewHandler<IProgress, SkiaProgressBar>
{
    public static IPropertyMapper<IProgress, ProgressBarHandler> Mapper = new PropertyMapper<IProgress, ProgressBarHandler>(ViewHandler.ViewMapper)
    {
        ["Progress"] = MapProgress,
        ["ProgressColor"] = MapProgressColor,
        ["IsEnabled"] = MapIsEnabled,
        ["Background"] = MapBackground,
        ["BackgroundColor"] = MapBackgroundColor
    };

    public static CommandMapper<IProgress, ProgressBarHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public ProgressBarHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaProgressBar CreatePlatformView()
    {
        return new SkiaProgressBar();
    }

    protected override void ConnectHandler(SkiaProgressBar platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is BindableObject bindable)
        {
            bindable.PropertyChanged += OnVirtualViewPropertyChanged;
        }

        if (VirtualView is VisualElement ve)
        {
            platformView.IsVisible = ve.IsVisible;
        }
    }

    protected override void DisconnectHandler(SkiaProgressBar platformView)
    {
        if (VirtualView is BindableObject bindable)
        {
            bindable.PropertyChanged -= OnVirtualViewPropertyChanged;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnVirtualViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (VirtualView is VisualElement ve && e.PropertyName == "IsVisible")
        {
            PlatformView.IsVisible = ve.IsVisible;
            PlatformView.Invalidate();
        }
    }

    public static void MapProgress(ProgressBarHandler handler, IProgress progress)
    {
        handler.PlatformView.Progress = progress.Progress;
    }

    public static void MapProgressColor(ProgressBarHandler handler, IProgress progress)
    {
        if (progress.ProgressColor != null)
        {
            handler.PlatformView.ProgressColor = progress.ProgressColor;
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(ProgressBarHandler handler, IProgress progress)
    {
        handler.PlatformView.IsEnabled = progress.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(ProgressBarHandler handler, IProgress progress)
    {
        if (progress.Background is SolidPaint solidPaint && solidPaint.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(ProgressBarHandler handler, IProgress progress)
    {
        if (progress is VisualElement ve && ve.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = ve.BackgroundColor;
            handler.PlatformView.Invalidate();
        }
    }
}
