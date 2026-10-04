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
public partial class RadioButtonHandler : LinuxViewHandler<IRadioButton, SkiaRadioButton>
{
    public static IPropertyMapper<IRadioButton, RadioButtonHandler> Mapper =
        new PropertyMapper<IRadioButton, RadioButtonHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IRadioButton.IsChecked)] = MapIsChecked,
            [nameof(ITextStyle.TextColor)] = MapTextColor,
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(IContentView.Content)] = MapContent,
            [nameof(IButtonStroke.StrokeColor)] = MapStrokeColor,
            [nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
            [nameof(IButtonStroke.CornerRadius)] = MapCornerRadius,
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
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.CheckedChanged += OnCheckedChanged;

        // Set content if available
        if (VirtualView is not null)
            MapContent(this, VirtualView);
        if (VirtualView is RadioButton rb)
        {
            platformView.GroupName = rb.GroupName;
            platformView.Value = rb.Value;
        }
    }

    protected override void DisconnectHandler(SkiaRadioButton platformView)
    {
        platformView.CheckedChanged -= OnCheckedChanged;
        VisualStateBridge.Detach(platformView);
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
            handler.PlatformView.TextColor = radioButton.TextColor;
        }
    }

    public static void MapFont(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        var font = radioButton.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        // Null family means the default face; clearing it lets a family change back take effect.
        handler.PlatformView.FontFamily = string.IsNullOrEmpty(font.Family) ? null : font.Family;
        handler.PlatformView.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    public static void MapCharacterSpacing(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = radioButton.CharacterSpacing;
    }

    public static void MapContent(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        // SkiaRadioButton draws text only; like Android's native RadioButton, non-string
        // content shows as its ToString().
        handler.PlatformView.Content = radioButton.Content?.ToString() ?? string.Empty;
    }

    public static void MapStrokeColor(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeColor = radioButton.StrokeColor;
    }

    public static void MapStrokeThickness(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        // RadioButton.BorderWidth defaults to -1 ("unset"): no outline.
        handler.PlatformView.StrokeThickness = Math.Max(0, radioButton.StrokeThickness);
    }

    public static void MapCornerRadius(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        // RadioButton.CornerRadius defaults to -1 ("unset"): square corners.
        handler.PlatformView.CornerRadius = Math.Max(0, radioButton.CornerRadius);
    }

    public static void MapBackground(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        if (radioButton.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }
}
