// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for RadioButton on Linux using Skia rendering.
/// </summary>
public partial class RadioButtonHandler : ViewHandler<IRadioButton, SkiaRadioButton>
{
    public static IPropertyMapper<IRadioButton, RadioButtonHandler> Mapper =
        new PropertyMapper<IRadioButton, RadioButtonHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IRadioButton.IsChecked)] = MapIsChecked,
            [nameof(ITextStyle.TextColor)] = MapTextColor,
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(IView.Background)] = MapBackground,
        };

    public static CommandMapper<IRadioButton, RadioButtonHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

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

        // Set content if available
        if (VirtualView is RadioButton rb)
        {
            platformView.Content = rb.Content?.ToString() ?? "";
            platformView.GroupName = rb.GroupName;
            platformView.Value = rb.Value;
        }
    }

    protected override void DisconnectHandler(SkiaRadioButton platformView)
    {
        platformView.CheckedChanged -= OnCheckedChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnCheckedChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;
        VirtualView.IsChecked = PlatformView.IsChecked;
    }

    public static void MapIsChecked(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsChecked = radioButton.IsChecked;
    }

    public static void MapTextColor(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        if (radioButton.TextColor is not null)
        {
            handler.PlatformView.TextColor = radioButton.TextColor.ToSKColor();
        }
    }

    public static void MapFont(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        if (radioButton.Font.Size > 0)
        {
            handler.PlatformView.FontSize = (float)radioButton.Font.Size;
        }
    }

    public static void MapBackground(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        if (radioButton.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
