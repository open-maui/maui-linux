// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's SwipeItemMenuItemHandler on Linux, for any <see cref="ISwipeItemMenuItem"/> (a
/// Controls <c>SwipeItem</c> or a library's own): its platform element is the
/// <see cref="Platform.SwipeItem"/> the <see cref="SkiaSwipeView"/> draws, kept in step with the
/// item's text, colours, font, visibility and icon, and a tap on it invokes the item (its Command
/// and Invoked event), as MAUI's SwipeItemMenuItemHandler does on every platform.
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
        };

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

    protected override Platform.SwipeItem CreatePlatformElement() => new Platform.SwipeItem();

    protected override void ConnectHandler(Platform.SwipeItem platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Invoked += OnInvoked;
    }

    protected override void DisconnectHandler(Platform.SwipeItem platformView)
    {
        platformView.Invoked -= OnInvoked;
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

    public static void MapBackground(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem && item.Background is SolidPaint { Color: { } color })
            platformItem.BackgroundColor = color;
        Redraw(item);
    }

    public static void MapTextColor(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem && item is ITextStyle { TextColor: { } color })
            platformItem.TextColor = color;
        Redraw(item);
    }

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

    public static void MapSource(ISwipeItemMenuItemHandler handler, ISwipeItemMenuItem item)
    {
        if (Item(handler) is { } platformItem)
            platformItem.IconSource = (item.Source as IFileImageSource)?.File;
        Redraw(item);
    }

    /// <summary>The swipe view showing an item lays its items out again and redraws.</summary>
    private static void Redraw(IElement item)
    {
        for (var parent = item.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ISwipeView { Handler.PlatformView: SkiaSwipeView swipeView })
            {
                swipeView.OnItemsChanged();
                return;
            }
        }
    }
}
