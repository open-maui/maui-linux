// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Linux handler for Button control.
/// </summary>
public partial class ButtonHandler : ViewHandler<IButton, SkiaButton>
{
    /// <summary>
    /// Maps the property mapper for the handler.
    /// </summary>
    public static IPropertyMapper<IButton, ButtonHandler> Mapper = new PropertyMapper<IButton, ButtonHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IButton.Text)] = MapText,
        [nameof(IButton.TextColor)] = MapTextColor,
        [nameof(IButton.Background)] = MapBackground,
        [nameof(IButton.Font)] = MapFont,
        [nameof(IButton.Padding)] = MapPadding,
        [nameof(IButton.CornerRadius)] = MapCornerRadius,
        [nameof(IButton.BorderColor)] = MapBorderColor,
        [nameof(IButton.BorderWidth)] = MapBorderWidth,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
    };

    /// <summary>
    /// Maps the command mapper for the handler.
    /// </summary>
    public static CommandMapper<IButton, ButtonHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ButtonHandler() : base(Mapper, CommandMapper)
    {
    }

    public ButtonHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public ButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaButton CreatePlatformView()
    {
        var button = new SkiaButton();
        return button;
    }

    protected override void ConnectHandler(SkiaButton platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Clicked += OnClicked;
        platformView.Pressed += OnPressed;
        platformView.Released += OnReleased;

        // Manually map all properties on connect since MAUI may not trigger updates
        // for properties that were set before handler connection
        if (VirtualView != null)
        {
            MapText(this, VirtualView);
            MapTextColor(this, VirtualView);
            MapBackground(this, VirtualView);
            MapFont(this, VirtualView);
            MapPadding(this, VirtualView);
            MapCornerRadius(this, VirtualView);
            MapBorderColor(this, VirtualView);
            MapBorderWidth(this, VirtualView);
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

    public static void MapText(ButtonHandler handler, IButton button)
    {
        handler.PlatformView.Text = button.Text ?? "";
        handler.PlatformView.Invalidate();
    }

    public static void MapTextColor(ButtonHandler handler, IButton button)
    {
        if (button.TextColor != null)
        {
            handler.PlatformView.TextColor = button.TextColor.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapBackground(ButtonHandler handler, IButton button)
    {
        var background = button.Background;
        if (background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            // Use ButtonBackgroundColor which is used for rendering, not base BackgroundColor
            handler.PlatformView.ButtonBackgroundColor = solidBrush.Color.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapFont(ButtonHandler handler, IButton button)
    {
        var font = button.Font;
        if (font.Family != null)
        {
            handler.PlatformView.FontFamily = font.Family;
        }
        handler.PlatformView.FontSize = (float)font.Size;
        handler.PlatformView.IsBold = font.Weight == FontWeight.Bold;
        handler.PlatformView.Invalidate();
    }

    public static void MapPadding(ButtonHandler handler, IButton button)
    {
        var padding = button.Padding;
        handler.PlatformView.Padding = new SKRect(
            (float)padding.Left,
            (float)padding.Top,
            (float)padding.Right,
            (float)padding.Bottom);
        handler.PlatformView.Invalidate();
    }

    public static void MapCornerRadius(ButtonHandler handler, IButton button)
    {
        handler.PlatformView.CornerRadius = button.CornerRadius;
        handler.PlatformView.Invalidate();
    }

    public static void MapBorderColor(ButtonHandler handler, IButton button)
    {
        if (button.StrokeColor != null)
        {
            handler.PlatformView.BorderColor = button.StrokeColor.ToSKColor();
        }
        handler.PlatformView.Invalidate();
    }

    public static void MapBorderWidth(ButtonHandler handler, IButton button)
    {
        handler.PlatformView.BorderWidth = (float)button.StrokeThickness;
        handler.PlatformView.Invalidate();
    }

    public static void MapIsEnabled(ButtonHandler handler, IButton button)
    {
        Console.WriteLine($"[ButtonHandler] MapIsEnabled called - Text='{handler.PlatformView.Text}', IsEnabled={button.IsEnabled}");
        handler.PlatformView.IsEnabled = button.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
