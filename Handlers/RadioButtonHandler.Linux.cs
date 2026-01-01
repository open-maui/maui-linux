// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for RadioButton control.
/// </summary>
public class RadioButtonHandler : ViewHandler<IRadioButton, SkiaRadioButton>
{
    public static IPropertyMapper<IRadioButton, RadioButtonHandler> Mapper = new PropertyMapper<IRadioButton, RadioButtonHandler>(ViewHandler.ViewMapper)
    {
        ["IsChecked"] = MapIsChecked,
        ["TextColor"] = MapTextColor,
        ["Font"] = MapFont,
        ["Background"] = MapBackground
    };

    public static CommandMapper<IRadioButton, RadioButtonHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public RadioButtonHandler() : base(Mapper, CommandMapper)
    {
    }

    public RadioButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
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

        if (VirtualView is RadioButton radioButton)
        {
            platformView.Content = radioButton.Content?.ToString() ?? "";
            platformView.GroupName = radioButton.GroupName;
            platformView.Value = radioButton.Value;
        }
    }

    protected override void DisconnectHandler(SkiaRadioButton platformView)
    {
        platformView.CheckedChanged -= OnCheckedChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnCheckedChanged(object? sender, EventArgs e)
    {
        if (VirtualView != null && PlatformView != null)
        {
            VirtualView.IsChecked = PlatformView.IsChecked;
        }
    }

    public static void MapIsChecked(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsChecked = radioButton.IsChecked;
        }
    }

    public static void MapTextColor(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView != null && radioButton.TextColor != null)
        {
            handler.PlatformView.TextColor = radioButton.TextColor.ToSKColor();
        }
    }

    public static void MapFont(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView != null)
        {
            var font = radioButton.Font;
            if (font.Size > 0)
            {
                handler.PlatformView.FontSize = (float)font.Size;
            }
        }
    }

    public static void MapBackground(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView != null)
        {
            if (radioButton.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
            }
        }
    }
}
