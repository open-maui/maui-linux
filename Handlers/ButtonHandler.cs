// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Button on Linux using Skia rendering.
/// Maps IButton interface to SkiaButton platform view.
/// </summary>
public partial class ButtonHandler : LinuxViewHandler<IButton, SkiaButton>
{
    public static IPropertyMapper<IButton, ButtonHandler> Mapper = new PropertyMapper<IButton, ButtonHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IButtonStroke.StrokeColor)] = MapStrokeColor,
        [nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
        [nameof(IButtonStroke.CornerRadius)] = MapCornerRadius,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IPadding.Padding)] = MapPadding,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        // MAUI's ButtonHandler maps the text members itself, so any ITextButton (not only
        // Controls.Button, which gets TextButtonHandler) shows its text, colour and font.
        [nameof(IText.Text)] = MapText,
        [nameof(ITextStyle.TextColor)] = MapTextColor,
        [nameof(ITextStyle.Font)] = MapFont,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
    };

    public static CommandMapper<IButton, ButtonHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public ButtonHandler() : base(Mapper, CommandMapper)
    {
    }

    public ButtonHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
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
        VisualStateBridge.Attach(VirtualView, platformView);

        // Manually map all properties on connect since MAUI may not trigger updates
        // for properties that were set before handler connection
        if (VirtualView != null)
        {
            MapStrokeColor(this, VirtualView);
            MapStrokeThickness(this, VirtualView);
            MapCornerRadius(this, VirtualView);
            MapBackground(this, VirtualView);
            MapPadding(this, VirtualView);
            MapIsEnabled(this, VirtualView);
            MapText(this, VirtualView);
            MapTextColor(this, VirtualView);
            MapFont(this, VirtualView);
            MapCharacterSpacing(this, VirtualView);

            // Map size requests from MAUI Button
            if (VirtualView is Microsoft.Maui.Controls.Button mauiButton)
            {
                DiagnosticLog.Debug("ButtonHandler", $"MapSize Text='{platformView.Text}' WReq={mauiButton.WidthRequest} HReq={mauiButton.HeightRequest}");
                if (mauiButton.WidthRequest >= 0)
                    platformView.WidthRequest = mauiButton.WidthRequest;
                if (mauiButton.HeightRequest >= 0)
                    platformView.HeightRequest = mauiButton.HeightRequest;
            }
            else
            {
                DiagnosticLog.Debug("ButtonHandler", $"VirtualView is NOT Microsoft.Maui.Controls.Button, type={VirtualView?.GetType().Name}");
            }
        }
    }

    protected override void DisconnectHandler(SkiaButton platformView)
    {
        platformView.Clicked -= OnClicked;
        platformView.Pressed -= OnPressed;
        platformView.Released -= OnReleased;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnClicked(object? sender, EventArgs e) => VirtualView?.Clicked();
    private void OnPressed(object? sender, EventArgs e) => VirtualView?.Pressed();
    private void OnReleased(object? sender, EventArgs e) => VirtualView?.Released();

    public static void MapStrokeColor(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;

        var strokeColor = button.StrokeColor;
        if (strokeColor is not null)
            handler.PlatformView.BorderColor = strokeColor;
    }

    public static void MapStrokeThickness(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.BorderWidth = button.StrokeThickness;
    }

    public static void MapCornerRadius(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.CornerRadius = button.CornerRadius;
    }

    public static void MapBackground(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;

        if (button.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            // Set BackgroundColor (MAUI Color type)
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    public static void MapPadding(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;

        var padding = button.Padding;
        handler.PlatformView.Padding = new Thickness(
            padding.Left,
            padding.Top,
            padding.Right,
            padding.Bottom);
    }

    public static void MapIsEnabled(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = button.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    // The text mappers take IButton because they sit on ButtonHandler's mapper; a button that
    // is not an IText / ITextStyle has no text to map and keeps the view's defaults.

    public static void MapText(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null || button is not IText text) return;
        handler.PlatformView.Text = text.Text ?? string.Empty;
    }

    public static void MapTextColor(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null || button is not ITextStyle style) return;

        if (style.TextColor is not null)
            handler.PlatformView.TextColor = style.TextColor;
    }

    public static void MapFont(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null || button is not ITextStyle style) return;

        var font = style.Font;
        if (font.Size > 0)
            handler.PlatformView.FontSize = font.Size;

        if (!string.IsNullOrEmpty(font.Family))
            handler.PlatformView.FontFamily = font.Family;

        handler.PlatformView.FontAttributes = TextStyleMapping.ToFontAttributes(font);
    }

    public static void MapCharacterSpacing(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null || button is not ITextStyle style) return;
        handler.PlatformView.CharacterSpacing = style.CharacterSpacing;
    }
}

/// <summary>
/// Handler for TextButton on Linux - extends ButtonHandler with text support.
/// Maps ITextButton interface (which includes IText properties).
/// </summary>
public partial class TextButtonHandler : ButtonHandler
{
    public static new IPropertyMapper<ITextButton, TextButtonHandler> Mapper =
        new PropertyMapper<ITextButton, TextButtonHandler>(ButtonHandler.Mapper)
    {
        // Text, TextColor, Font and CharacterSpacing come from ButtonHandler.Mapper.
        [nameof(Button.TextTransform)] = MapTextTransform,
        // Button raises "Source" for ImageSource and "ContentLayout" for its placement.
        ["Source"] = MapImageSource,
        [nameof(Button.ImageSource)] = MapImageSource,
        [nameof(Button.ContentLayout)] = MapContentLayout,
        // Controls' Button remaps LineBreakMode on MAUI's ButtonHandler.Mapper only.
        [nameof(Button.LineBreakMode)] = MapLineBreakMode,
    };

    public static void MapLineBreakMode(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null || button is not Button b) return;
        handler.PlatformView.LineBreakMode = b.LineBreakMode;
    }

    public static void MapImageSource(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.ImageServices = handler.MauiContext?.Services;
        handler.PlatformView.ImageSource = (button as Button)?.ImageSource;
    }

    public static void MapContentLayout(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null || button is not Button b) return;
        // Same enum order (Left, Top, Right, Bottom) on both sides.
        var layout = b.ContentLayout;
        handler.PlatformView.ContentLayout = new Microsoft.Maui.Platform.ButtonContentLayout(
            (Microsoft.Maui.Platform.ButtonContentLayout.ImagePosition)(int)layout.Position, layout.Spacing);
    }

    public static void MapTextTransform(TextButtonHandler handler, ITextButton button)
    {
        if (handler.PlatformView is null || button is not Button b) return;
        // SkiaButton applies the transform when it draws and measures, so Text stays the raw text.
        handler.PlatformView.TextTransform = b.TextTransform;
    }

    public TextButtonHandler() : base(Mapper)
    {
    }

    protected override void ConnectHandler(SkiaButton platformView)
    {
        DiagnosticLog.Debug("TextButtonHandler", "ConnectHandler START");
        base.ConnectHandler(platformView);

        // Manually map the Button-only properties on connect since MAUI may not trigger
        // updates for properties set before handler connection (base maps text and size).
        if (VirtualView is ITextButton textButton)
        {
            MapImageSource(this, textButton);
            MapContentLayout(this, textButton);
            MapTextTransform(this, textButton);
            MapLineBreakMode(this, textButton);
        }
        DiagnosticLog.Debug("TextButtonHandler", "ConnectHandler DONE");
    }

    // Kept for source compatibility (public API); the logic lives on ButtonHandler.
    public static void MapText(TextButtonHandler handler, ITextButton button) =>
        ButtonHandler.MapText(handler, button);

    public static void MapTextColor(TextButtonHandler handler, ITextButton button) =>
        ButtonHandler.MapTextColor(handler, button);

    public static void MapFont(TextButtonHandler handler, ITextButton button) =>
        ButtonHandler.MapFont(handler, button);

    public static void MapCharacterSpacing(TextButtonHandler handler, ITextButton button) =>
        ButtonHandler.MapCharacterSpacing(handler, button);
}
