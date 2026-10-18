// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Button on Linux using Skia rendering.
/// Maps IButton interface to SkiaButton platform view.
/// </summary>
public partial class ButtonHandler : LinuxViewHandler<IButton, SkiaButton>, IButtonHandler
{
    IButton IButtonHandler.VirtualView => VirtualView;
    object IButtonHandler.PlatformView => PlatformView;

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
        // (MAUI's public MapText/MapTextColor/... take IText/ITextStyle, not IButton.)
        [nameof(IText.Text)] = MapTextCore,
        [nameof(ITextStyle.TextColor)] = MapTextColorCore,
        [nameof(ITextStyle.Font)] = MapFontCore,
        [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacingCore,
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
            MapTextCore(this, VirtualView);
            MapTextColorCore(this, VirtualView);
            MapFontCore(this, VirtualView);
            MapCharacterSpacingCore(this, VirtualView);

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

    private ImageSourcePartLoader? _imageSourceLoader;

    /// <summary>
    /// MAUI's <c>IButtonHandler.ImageSourceLoader</c>: its <see cref="ImageSourcePartLoader.UpdateImageSourceAsync"/>
    /// shows the button's image source on the <see cref="SkiaButton"/> (which loads it), as the
    /// handler's ImageSource mapping does.
    /// </summary>
    public virtual ImageSourcePartLoader ImageSourceLoader =>
        _imageSourceLoader ??= new LinuxImageSourcePartLoader(
            new LinuxImageSourcePartSetter(this, _ => { }),
            () =>
            {
                if (VirtualView is IImage image)
                    MapImageSource(this, image);
                else if (VirtualView is ITextButton textButton && this is TextButtonHandler textHandler)
                    TextButtonHandler.MapImageSource(textHandler, textButton);
                return Task.CompletedTask;
            });

    /// <summary>
    /// MAUI's <c>ButtonHandler.MapImageSource</c>: shows the image part's source (a Controls
    /// Button's ImageSource) next to the text. The <see cref="SkiaButton"/> loads it.
    /// </summary>
    public static void MapImageSource(IButtonHandler handler, IImage image)
    {
        if (handler is not ButtonHandler linuxHandler || linuxHandler.PlatformView is null)
            return;
        linuxHandler.PlatformView.ImageServices = linuxHandler.MauiContext?.Services;
        linuxHandler.PlatformView.ImageSource = image.Source as ImageSource ?? (image as Button)?.ImageSource;
    }

    private void OnClicked(object? sender, EventArgs e) => VirtualView?.Clicked();
    private void OnPressed(object? sender, EventArgs e) => VirtualView?.Pressed();
    private void OnReleased(object? sender, EventArgs e) => VirtualView?.Released();

    /// <summary>MAUI's <c>MapStrokeColor</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeColor(IButtonHandler handler, IButtonStroke buttonStroke)
    {
        if (handler is ButtonHandler linuxHandler && buttonStroke is IButton linuxView)
            MapStrokeColorCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeColor(ButtonHandler handler, IButton button) => MapStrokeColorCore(handler, button);

    private static void MapStrokeColorCore(ButtonHandler handler, IButton button)
    {
        handler.PlatformView?.UpdateStrokeColor(button);
    }

    /// <summary>MAUI's <c>MapStrokeThickness</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapStrokeThickness(IButtonHandler handler, IButtonStroke buttonStroke)
    {
        if (handler is ButtonHandler linuxHandler && buttonStroke is IButton linuxView)
            MapStrokeThicknessCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapStrokeThickness(ButtonHandler handler, IButton button) => MapStrokeThicknessCore(handler, button);

    private static void MapStrokeThicknessCore(ButtonHandler handler, IButton button)
    {
        handler.PlatformView?.UpdateStrokeThickness(button);
    }

    /// <summary>MAUI's <c>MapCornerRadius</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCornerRadius(IButtonHandler handler, IButtonStroke buttonStroke)
    {
        if (handler is ButtonHandler linuxHandler && buttonStroke is IButton linuxView)
            MapCornerRadiusCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCornerRadius(ButtonHandler handler, IButton button) => MapCornerRadiusCore(handler, button);

    private static void MapCornerRadiusCore(ButtonHandler handler, IButton button)
    {
        handler.PlatformView?.UpdateCornerRadius(button);
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

    /// <summary>MAUI's <c>MapPadding</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapPadding(IButtonHandler handler, IButton button)
    {
        if (handler is ButtonHandler linuxHandler)
            MapPaddingCore(linuxHandler, button);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapPadding(ButtonHandler handler, IButton button) => MapPaddingCore(handler, button);

    private static void MapPaddingCore(ButtonHandler handler, IButton button)
    {
        handler.PlatformView?.UpdatePadding(button);
    }

    public static void MapIsEnabled(ButtonHandler handler, IButton button)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = button.IsEnabled;
        handler.PlatformView.Invalidate();
    }

    // The text mappers take IButton because they sit on ButtonHandler's mapper; a button that
    // is not an IText / ITextStyle has no text to map and keeps the view's defaults.

    /// <summary>MAUI's <c>MapText</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapText(IButtonHandler handler, IText button)
    {
        if (handler is ButtonHandler linuxHandler && button is IButton linuxView)
            MapTextCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapText(ButtonHandler handler, IButton button) => MapTextCore(handler, button);

    private static void MapTextCore(ButtonHandler handler, IButton button)
    {
        if (button is IText text)
            handler.PlatformView?.UpdateText(text);
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapTextColor(IButtonHandler handler, ITextStyle button)
    {
        if (handler is ButtonHandler linuxHandler && button is IButton linuxView)
            MapTextColorCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapTextColor(ButtonHandler handler, IButton button) => MapTextColorCore(handler, button);

    private static void MapTextColorCore(ButtonHandler handler, IButton button)
    {
        if (button is ITextStyle style)
            handler.PlatformView?.UpdateTextColor(style);
    }

    /// <summary>MAUI's <c>MapFont</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapFont(IButtonHandler handler, ITextStyle button)
    {
        if (handler is ButtonHandler linuxHandler && button is IButton linuxView)
            MapFontCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapFont(ButtonHandler handler, IButton button) => MapFontCore(handler, button);

    private static void MapFontCore(ButtonHandler handler, IButton button)
    {
        if (button is ITextStyle style)
            handler.PlatformView?.UpdateFont(style);
    }

    /// <summary>MAUI's <c>MapCharacterSpacing</c> signature; maps the property on a Linux <see cref="ButtonHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapCharacterSpacing(IButtonHandler handler, ITextStyle button)
    {
        if (handler is ButtonHandler linuxHandler && button is IButton linuxView)
            MapCharacterSpacingCore(linuxHandler, linuxView);
    }

    [Obsolete("Use the IButtonHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapCharacterSpacing(ButtonHandler handler, IButton button) => MapCharacterSpacingCore(handler, button);

    private static void MapCharacterSpacingCore(ButtonHandler handler, IButton button)
    {
        if (button is ITextStyle style)
            handler.PlatformView?.UpdateCharacterSpacing(style);
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
