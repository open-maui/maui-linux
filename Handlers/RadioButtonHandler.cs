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
public partial class RadioButtonHandler : LinuxViewHandler<IRadioButton, SkiaRadioButton>, IRadioButtonHandler
{
    IRadioButton IRadioButtonHandler.VirtualView => VirtualView;
    object IRadioButtonHandler.PlatformView => PlatformView;

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

    /// <summary>MAUI's <c>MapIsChecked</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapIsChecked(IRadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler is RadioButtonHandler linuxHandler)
            MapIsCheckedCore(linuxHandler, radioButton);
    }

    private static void MapIsCheckedCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsChecked = radioButton.IsChecked;
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapTextColor(IRadioButtonHandler handler, ITextStyle textStyle)
    {
        if (handler is RadioButtonHandler linuxHandler && textStyle is IRadioButton linuxView)
            MapTextColorCore(linuxHandler, linuxView);
    }

    private static void MapTextColorCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        if (radioButton.TextColor is not null)
        {
            handler.PlatformView.TextColor = radioButton.TextColor;
        }
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapFont(IRadioButtonHandler handler, ITextStyle textStyle)
    {
        if (handler is RadioButtonHandler linuxHandler && textStyle is IRadioButton linuxView)
            MapFontCore(linuxHandler, linuxView);
    }

    private static void MapFontCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        var font = radioButton.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        // Null family means the default face; clearing it lets a family change back take effect.
        handler.PlatformView.FontFamily = string.IsNullOrEmpty(font.Family) ? null : font.Family;
        handler.PlatformView.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapCharacterSpacing(IRadioButtonHandler handler, ITextStyle textStyle)
    {
        if (handler is RadioButtonHandler linuxHandler && textStyle is IRadioButton linuxView)
            MapCharacterSpacingCore(linuxHandler, linuxView);
    }

    private static void MapCharacterSpacingCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CharacterSpacing = radioButton.CharacterSpacing;
    }

    /// <summary>MAUI's <c>MapContent</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapContent(IRadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler is RadioButtonHandler linuxHandler)
            MapContentCore(linuxHandler, radioButton);
    }

    private static void MapContentCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        // SkiaRadioButton draws text only; like Android's native RadioButton, non-string
        // content shows as its ToString().
        handler.PlatformView.Content = radioButton.Content?.ToString() ?? string.Empty;
    }

    /// <summary>MAUI's <c>MapStrokeColor</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapStrokeColor(IRadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler is RadioButtonHandler linuxHandler)
            MapStrokeColorCore(linuxHandler, radioButton);
    }

    private static void MapStrokeColorCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.StrokeColor = radioButton.StrokeColor;
    }

    /// <summary>MAUI's <c>MapStrokeThickness</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapStrokeThickness(IRadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler is RadioButtonHandler linuxHandler)
            MapStrokeThicknessCore(linuxHandler, radioButton);
    }

    private static void MapStrokeThicknessCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        // RadioButton.BorderWidth defaults to -1 ("unset"): no outline.
        handler.PlatformView.StrokeThickness = Math.Max(0, radioButton.StrokeThickness);
    }

    /// <summary>MAUI's <c>MapCornerRadius</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapCornerRadius(IRadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler is RadioButtonHandler linuxHandler)
            MapCornerRadiusCore(linuxHandler, radioButton);
    }

    private static void MapCornerRadiusCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;
        // RadioButton.CornerRadius defaults to -1 ("unset"): square corners.
        handler.PlatformView.CornerRadius = Math.Max(0, radioButton.CornerRadius);
    }

    /// <summary>MAUI's <c>MapBackground</c> signature; maps the property on a Linux <see cref="RadioButtonHandler"/>.</summary>
    public static void MapBackground(IRadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler is RadioButtonHandler linuxHandler)
            MapBackgroundCore(linuxHandler, radioButton);
    }

    private static void MapBackgroundCore(RadioButtonHandler handler, IRadioButton radioButton)
    {
        if (handler.PlatformView is null) return;

        if (radioButton.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }
}
