// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Primitives;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for CheckBox control.
/// </summary>
public class CheckBoxHandler : ViewHandler<ICheckBox, SkiaCheckBox>
{
    public static IPropertyMapper<ICheckBox, CheckBoxHandler> Mapper = new PropertyMapper<ICheckBox, CheckBoxHandler>(ViewHandler.ViewMapper)
    {
        ["IsChecked"] = MapIsChecked,
        ["Foreground"] = MapForeground,
        ["Background"] = MapBackground,
        ["IsEnabled"] = MapIsEnabled,
        ["VerticalLayoutAlignment"] = MapVerticalLayoutAlignment,
        ["HorizontalLayoutAlignment"] = MapHorizontalLayoutAlignment
    };

    public static CommandMapper<ICheckBox, CheckBoxHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

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

    private void OnCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (VirtualView != null && VirtualView.IsChecked != e.IsChecked)
        {
            VirtualView.IsChecked = e.IsChecked;
        }
    }

    public static void MapIsChecked(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsChecked = checkBox.IsChecked;
        }
    }

    public static void MapForeground(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView != null)
        {
            if (checkBox.Foreground is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.CheckColor = solidPaint.Color.ToSKColor();
            }
        }
    }

    public static void MapBackground(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView != null)
        {
            if (checkBox.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color;
            }
        }
    }

    public static void MapIsEnabled(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsEnabled = checkBox.IsEnabled;
        }
    }

    public static void MapVerticalLayoutAlignment(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.VerticalOptions = (int)checkBox.VerticalLayoutAlignment switch
            {
                1 => LayoutOptions.Start,
                2 => LayoutOptions.Center,
                3 => LayoutOptions.End,
                0 => LayoutOptions.Fill,
                _ => LayoutOptions.Fill
            };
        }
    }

    public static void MapHorizontalLayoutAlignment(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.HorizontalOptions = (int)checkBox.HorizontalLayoutAlignment switch
            {
                1 => LayoutOptions.Start,
                2 => LayoutOptions.Center,
                3 => LayoutOptions.End,
                0 => LayoutOptions.Fill,
                _ => LayoutOptions.Start
            };
        }
    }
}
