// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ProgressBar on Linux using Skia rendering.
/// Maps IProgress interface to SkiaProgressBar platform view.
/// IProgress has: Progress (0-1), ProgressColor
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

    public static CommandMapper<IProgress, ProgressBarHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ProgressBarHandler() : base(Mapper, CommandMapper)
    {
    }

    public ProgressBarHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
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

        if (VirtualView is VisualElement visualElement)
        {
            platformView.IsVisible = visualElement.IsVisible;
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
        if (VirtualView is VisualElement visualElement && e.PropertyName == nameof(VisualElement.IsVisible))
        {
            PlatformView.IsVisible = visualElement.IsVisible;
            PlatformView.Invalidate();
        }
    }

    public static void MapProgress(ProgressBarHandler handler, IProgress progress)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Progress = Math.Clamp(progress.Progress, 0.0, 1.0);
    }

    public static void MapProgressColor(ProgressBarHandler handler, IProgress progress)
    {
        if (handler.PlatformView is null) return;

        if (progress.ProgressColor is not null)
        {
            handler.PlatformView.ProgressColor = progress.ProgressColor.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(ProgressBarHandler handler, IProgress progress)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.IsEnabled = progress.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(ProgressBarHandler handler, IProgress progress)
    {
        if (handler.PlatformView is null) return;

        if (progress.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(ProgressBarHandler handler, IProgress progress)
    {
        if (handler.PlatformView is null) return;

        if (progress is VisualElement visualElement && visualElement.BackgroundColor is not null)
        {
            handler.PlatformView.BackgroundColor = visualElement.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
