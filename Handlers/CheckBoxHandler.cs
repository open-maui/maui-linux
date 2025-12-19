// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for CheckBox on Linux using Skia rendering.
/// Maps ICheckBox interface to SkiaCheckBox platform view.
/// </summary>
public partial class CheckBoxHandler : ViewHandler<ICheckBox, SkiaCheckBox>
{
    public static IPropertyMapper<ICheckBox, CheckBoxHandler> Mapper = new PropertyMapper<ICheckBox, CheckBoxHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ICheckBox.IsChecked)] = MapIsChecked,
        [nameof(ICheckBox.Foreground)] = MapForeground,
        [nameof(IView.Background)] = MapBackground,
    };

    public static CommandMapper<ICheckBox, CheckBoxHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public CheckBoxHandler() : base(Mapper, CommandMapper)
    {
    }

    public CheckBoxHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
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

    private void OnCheckedChanged(object? sender, Platform.CheckedChangedEventArgs e)
    {
        if (VirtualView is not null && VirtualView.IsChecked != e.IsChecked)
        {
            VirtualView.IsChecked = e.IsChecked;
        }
    }

    public static void MapIsChecked(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsChecked = checkBox.IsChecked;
    }

    public static void MapForeground(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;

        if (checkBox.Foreground is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.CheckColor = solidPaint.Color.ToSKColor();
        }
    }

    public static void MapBackground(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;

        if (checkBox.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
