// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Platform.SwipeItem is this handler's platform element type (and the element type of
// SkiaSwipeView's item lists) until OpenMaui 11.0, when SkiaSwipeItem replaces it.
#pragma warning disable CS0618

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's SwipeItemMenuItemHandler on Linux, for any <see cref="ISwipeItemMenuItem"/> (a
/// Controls <c>SwipeItem</c> or a library's own): its platform element is the
/// <see cref="SkiaSwipeItem"/> the <see cref="SkiaSwipeView"/> draws, kept in step with the
/// item's text, colours, font, visibility and icon, and a tap on it invokes the item (its Command
/// and Invoked event), as MAUI's SwipeItemMenuItemHandler does on every platform. The icon
/// (IconImageSource) is loaded through its image-source service, any kind (file, URI, stream,
/// font glyph, an app's own), and tinted as MAUI's Windows handler tints it.
/// </summary>
public partial class SwipeItemMenuItemHandler : ElementHandler<ISwipeItemMenuItem, Platform.SwipeItem>, ISwipeItemMenuItemHandler
{
    public static IPropertyMapper<ISwipeItemMenuItem, ISwipeItemMenuItemHandler> Mapper =
        new PropertyMapper<ISwipeItemMenuItem, ISwipeItemMenuItemHandler>(ElementHandler.ElementMapper)
        {
            [nameof(ISwipeItemMenuItem.Visibility)] = MapVisibility,
            [nameof(ISwipeItemMenuItem.Background)] = MapBackground,
            [nameof(IMenuElement.Text)] = MapText,
            [nameof(ITextStyle.TextColor)] = MapTextColor,
            [nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
            [nameof(ITextStyle.Font)] = MapFont,
            [nameof(IMenuElement.Source)] = MapSource,
            [nameof(ISwipeItemMenuItemIconColor.IconColor)] = MapIconColor,
        };

    /// <summary>The logical height an icon is loaded and drawn at (WinUI's AppBarButton content height).</summary>
    public const float IconSize = 16f;

    public static CommandMapper<ISwipeItemMenuItem, ISwipeItemMenuItemHandler> CommandMapper =
        new(ElementHandler.ElementCommandMapper)
        {
        };

    public SwipeItemMenuItemHandler() : base(Mapper, CommandMapper)
    {
    }

    public SwipeItemMenuItemHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public SwipeItemMenuItemHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    ISwipeItemMenuItem ISwipeItemMenuItemHandler.VirtualView => VirtualView;

    object ISwipeItemMenuItemHandler.PlatformView => PlatformView;

    protected override Platform.SwipeItem CreatePlatformElement() => new SkiaSwipeItem();

    protected override void ConnectHandler(Platform.SwipeItem platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Invoked += OnInvoked;
        _observed = VirtualView as System.ComponentModel.INotifyPropertyChanged;
        if (_observed is not null)
            _observed.PropertyChanged += OnVirtualViewPropertyChanged;
    }

    private System.ComponentModel.INotifyPropertyChanged? _observed;

    /// <summary>
    /// A Controls MenuItem reports a change of its icon (or of the icon source's contents) as
    /// IconImageSource, a name MAUI's mapper does not list: the icon follows it.
    /// </summary>
    private void OnVirtualViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Microsoft.Maui.Controls.MenuItem.IconImageSource) && VirtualView is not null)
            UpdateValue(nameof(IMenuElement.Source));
    }

    protected override void DisconnectHandler(Platform.SwipeItem platformView)
    {
        // As MAUI's Windows handler: a load in flight is dropped and the icon released.
        platformView.ReleaseIcon();
        platformView.Invoked -= OnInvoked;
        if (_observed is not null)
            _observed.PropertyChanged -= OnVirtualViewPropertyChanged;
        _observed = null;
        base.DisconnectHandler(platformView);
    }

    // A disabled item is not invoked (MAUI's SwipeView checks IsEnabled before OnInvoked).
    private void OnInvoked(object? sender, EventArgs e)
    {
        if (VirtualView is { IsEnabled: true } item)
            item.OnInvoked();
    }

    /// <summary>The Linux platform item of a swipe item handler (this one or a subclass).</summary>
    private static Platform.SwipeItem? Item(ISwipeItemMenuItemHandler handler) => handler.PlatformView as Platform.SwipeItem;

    public static void MapText(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem)
            platformItem.Text = item.Text ?? string.Empty;
        Redraw(item);
    }

    /// <summary>
    /// The background; the text colour and icon tint follow it, since without a TextColor they
    /// contrast with the background (MAUI's <c>GetTextColor</c>, as on Windows).
    /// </summary>
    public static void MapBackground(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem && item.Background is SolidPaint { Color: { } color })
            platformItem.BackgroundColor = color;
        UpdateTextColor(handler, item);
        UpdateIconTint(handler, item);
        Redraw(item);
    }

    /// <summary>
    /// The text colour as MAUI's Windows handler sets it: the item's TextColor, else white or
    /// black contrasting with its background (none when the background is unset or a font icon
    /// has its own colour, which keeps the default). A font icon without a colour of its own
    /// follows it.
    /// </summary>
    public static void MapTextColor(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        UpdateTextColor(handler, item);
        UpdateIconTint(handler, item);
        Redraw(item);
    }

    /// <summary>The icon's tint (MAUI 11's SwipeItem.IconColor): applied without loading the icon again.</summary>
    public static void MapIconColor(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        UpdateIconTint(handler, item);
        Redraw(item);
    }

    private static void UpdateTextColor(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem)
            platformItem.TextColor = item.GetTextColor() ?? DefaultTextColor;
    }

    /// <summary>The platform item's default text colour, kept when MAUI resolves none.</summary>
    private static Color DefaultTextColor => Colors.White;

    /// <summary>Nothing on Linux, as on MAUI's Windows (the item is drawn at the swipe view's text size).</summary>
    public static void MapCharacterSpacing(ISwipeItemMenuItemHandler handler, ITextStyle view)
    {
    }

    /// <summary>Nothing on Linux, as on MAUI's Windows (the item is drawn at the swipe view's text size).</summary>
    public static void MapFont(ISwipeItemMenuItemHandler handler, ITextStyle view)
    {
    }

    /// <summary>
    /// A hidden item is neither drawn nor tapped, and the swipe view opens to the remaining
    /// items (MAUI's Windows handler rebuilds the side's items).
    /// </summary>
    public static void MapVisibility(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem)
            platformItem.IsVisible = item.Visibility == Visibility.Visible;
        Redraw(item);
    }

    /// <summary>
    /// Loads the icon through the image-source service registered for its type (as SkiaButton,
    /// toolbar and flyout icons are loaded), at <see cref="IconSize"/> and the swipe view's
    /// density. A newer source, or a disconnect, cancels a load in flight; the icon it replaces is
    /// released. A source that fails to load shows no icon, and the failure is logged, as on
    /// Windows.
    /// </summary>
    public static void MapSource(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is not { } platformItem)
            return;

        platformItem.IconSource = (item.Source as IFileImageSource)?.File;
        UpdateIconTint(handler, item);
        _ = LoadIconAsync(handler, item, platformItem);
    }

    /// <summary>The load <see cref="MapSource"/> starts; completes when the icon is shown or dropped.</summary>
    internal static async Task LoadIconAsync(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item, Platform.SwipeItem platformItem)
    {
        platformItem.IconLoad?.Cancel();
        var source = item.Source;
        if (source is null || source.IsEmpty)
        {
            platformItem.IconLoad = null;
            platformItem.SetIcon(null);
            Redraw(item);
            return;
        }

        var load = new CancellationTokenSource();
        platformItem.IconLoad = load;
        bool Current() => !load.IsCancellationRequested
            && ReferenceEquals(platformItem.IconLoad, load)
            && ReferenceEquals(handler.VirtualView, item)
            && ReferenceEquals(handler.PlatformView, platformItem)
            && ReferenceEquals(item.Source, source);

        try
        {
            var result = await LinuxImageSourceServices.LoadAsync(
                Services(handler), source, Scale(item), new Size(IconSize, IconSize), load.Token);
            if (!Current())
            {
                result?.Dispose();
                return;
            }

            platformItem.IconLoad = null;
            platformItem.SetIcon(result);
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!Current())
                return;
            platformItem.IconLoad = null;
            platformItem.SetIcon(null);
            DiagnosticLog.Warn("SwipeItemMenuItemHandler", $"Cannot load SwipeItem Icon: {ex.Message}");
        }

        UpdateIconTint(handler, item);
        Redraw(item);
    }

    /// <summary>Sets the platform item's icon tint from <see cref="IconTint"/>.</summary>
    private static void UpdateIconTint(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem)
            platformItem.IconTintColor = item.Source is { } source ? IconTint(item, source, Services(handler)) : null;
    }

    /// <summary>
    /// The colour an icon is drawn in, as MAUI's Windows handler resolves it, or null for the
    /// icon's own colours. A font glyph loaded by the built-in font service takes the item's
    /// IconColor, else its own Color, else the item's text colour (TextColor or the contrast with
    /// the background), else white (the colour Windows renders a glyph without one in). A file
    /// icon of the app (a relative name, loaded by the built-in file service) takes the IconColor
    /// as a monochrome mask. URI, stream, rooted-file and other sources keep their colours, as do
    /// sources an app's own service loads.
    /// </summary>
    internal static Color? IconTint(ISwipeItemMenuItem item, IImageSource source, IServiceProvider? services)
    {
        var iconColor = (item as ISwipeItemMenuItemIconColor)?.IconColor;
        switch (source)
        {
            case IFontImageSource font when IsBuiltIn(services, source, typeof(LinuxFontImageSourceService)):
                return iconColor ?? font.Color ?? item.GetTextColor() ?? Colors.White;
            case IFileImageSource { File: { Length: > 0 } file } when iconColor is not null
                && !Path.IsPathRooted(file)
                && IsBuiltIn(services, source, typeof(LinuxFileImageSourceService)):
                return iconColor;
            default:
                return null;
        }
    }

    private static bool IsBuiltIn(IServiceProvider? services, IImageSource source, Type serviceType)
    {
        try
        {
            return LinuxImageSourceServices.Resolve(services, source).GetType() == serviceType;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static IServiceProvider? Services(ISwipeItemMenuItemHandler handler) =>
        handler.MauiContext?.Services ?? LinuxImageSourceServices.AppServices;

    /// <summary>The density of the swipe view showing the item (1 before it is shown).</summary>
    private static float Scale(IElement item) =>
        SwipeViewOf(item) is { } swipeView ? Math.Max(1f, swipeView.DeviceScale) : Math.Max(1f, LinuxApplication.Current?.DpiScale ?? 1f);

    private static SkiaSwipeView? SwipeViewOf(IElement item)
    {
        for (var parent = item.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ISwipeView swipeView)
                return swipeView.Handler?.PlatformView as SkiaSwipeView;
        }
        return null;
    }

    /// <summary>The swipe view showing an item lays its items out again and redraws.</summary>
    private static void Redraw(IElement item) => SwipeViewOf(item)?.OnItemsChanged();

    /// <summary>MAUI's <c>MapSourceAsync</c>: loads and shows the item's icon; completes when it is shown or dropped.</summary>
    public static Task MapSourceAsync(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem image)
    {
        if (Item(handler) is not { } platformItem)
            return Task.CompletedTask;
        platformItem.IconSource = (image.Source as IFileImageSource)?.File;
        UpdateIconTint(handler, image);
        return LoadIconAsync(handler, image, platformItem);
    }

    /// <summary>MAUI's <c>MapTextColor</c> signature (an <see cref="ITextStyle"/>).</summary>
    public static void MapTextColor(ISwipeItemMenuItemHandler handler, ITextStyle view)
    {
        if (view is ISwipeItemMenuItem item)
            MapTextColor(handler, item);
    }
}
