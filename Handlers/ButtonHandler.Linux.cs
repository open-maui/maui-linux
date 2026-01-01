// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Button control.
/// </summary>
public class ButtonHandler : ViewHandler<IButton, SkiaButton>
{
    public static IPropertyMapper<IButton, ButtonHandler> Mapper = new PropertyMapper<IButton, ButtonHandler>(ViewHandler.ViewMapper)
    {
        ["StrokeColor"] = MapStrokeColor,
        ["StrokeThickness"] = MapStrokeThickness,
        ["CornerRadius"] = MapCornerRadius,
        ["Background"] = MapBackground,
        ["Padding"] = MapPadding,
        ["IsEnabled"] = MapIsEnabled
    };

    public static CommandMapper<IButton, ButtonHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

    public ButtonHandler() : base(Mapper, CommandMapper)
    {
    }

    public ButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaButton CreatePlatformView()
    {
        return new SkiaButton();
    }

    protected override void ConnectHandler(SkiaButton platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Clicked += OnClicked;
        platformView.Pressed += OnPressed;
        platformView.Released += OnReleased;

        if (VirtualView != null)
        {
            MapStrokeColor(this, VirtualView);
            MapStrokeThickness(this, VirtualView);
            MapCornerRadius(this, VirtualView);
            MapBackground(this, VirtualView);
            MapPadding(this, VirtualView);
            MapIsEnabled(this, VirtualView);
        }
    }

    protected override void DisconnectHandler(SkiaButton platformView)
    {
        platformView.Clicked -= OnClicked;
        platformView.Pressed -= OnPressed;
        platformView.Released -= OnReleased;
        base.DisconnectHandler(platformView);
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        VirtualView?.Clicked();
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        VirtualView?.Pressed();
    }

    private void OnReleased(object? sender, EventArgs e)
    {
        VirtualView?.Released();
    }

    public static void MapStrokeColor(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView != null)
        {
            var strokeColor = button.StrokeColor;
            if (strokeColor != null)
            {
                handler.PlatformView.BorderColor = strokeColor.ToSKColor();
            }
        }
    }

    public static void MapStrokeThickness(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.BorderWidth = (float)button.StrokeThickness;
        }
    }

    public static void MapCornerRadius(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.CornerRadius = button.CornerRadius;
        }
    }

    public static void MapBackground(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView != null)
        {
            var background = button.Background;
            if (background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.ButtonBackgroundColor = solidPaint.Color.ToSKColor();
            }
        }
    }

    public static void MapPadding(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView != null)
        {
            var padding = button.Padding;
            handler.PlatformView.Padding = new SKRect(
                (float)padding.Left,
                (float)padding.Top,
                (float)padding.Right,
                (float)padding.Bottom);
        }
    }

    public static void MapIsEnabled(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView != null)
        {
            Console.WriteLine($"[ButtonHandler] MapIsEnabled - Text='{handler.PlatformView.Text}', IsEnabled={button.IsEnabled}");
            handler.PlatformView.IsEnabled = button.IsEnabled;
            handler.PlatformView.Invalidate();
        }
    }
}
