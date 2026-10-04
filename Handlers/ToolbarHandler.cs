// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for MAUI's toolbar element (<see cref="IToolbar"/>: the NavigationPageToolbar a
/// NavigationPage puts on its window or FlyoutPage, and the Shell's ShellToolbar). MAUI gives
/// the toolbar a handler wherever a window shows a navigation bar, and code that looks for the
/// bar reads <c>Toolbar.Handler.PlatformView</c>; with no Linux handler the toolbar never got
/// one. The platform element is a <see cref="SkiaToolbar"/> that carries the toolbar's state;
/// the bar on screen is drawn by the page that owns it (SkiaShell, or the current SkiaPage of
/// a SkiaNavigationPage), which reads the same MAUI properties.
/// </summary>
public partial class ToolbarHandler : ElementHandler<IToolbar, SkiaToolbar>, IToolbarHandler
{
    IToolbar IToolbarHandler.VirtualView => VirtualView;
    object IToolbarHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IToolbar, ToolbarHandler> Mapper =
        new PropertyMapper<IToolbar, ToolbarHandler>(ElementHandler.ElementMapper)
        {
            [nameof(IToolbar.Title)] = MapTitle,
            [nameof(IToolbar.IsVisible)] = MapIsVisible,
            [nameof(IToolbar.BackButtonVisible)] = MapBackButtonVisible,
        };

    public static CommandMapper<IToolbar, ToolbarHandler> CommandMapper = new(ElementHandler.ElementCommandMapper);

    public ToolbarHandler() : base(Mapper, CommandMapper)
    {
    }

    public ToolbarHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaToolbar CreatePlatformElement() => new();

    public static void MapTitle(ToolbarHandler handler, IToolbar toolbar)
    {
        if (handler.PlatformView is { } platform)
            platform.Title = toolbar.Title ?? string.Empty;
    }

    public static void MapIsVisible(ToolbarHandler handler, IToolbar toolbar)
    {
        if (handler.PlatformView is { } platform)
            platform.IsVisible = toolbar.IsVisible;
    }

    public static void MapBackButtonVisible(ToolbarHandler handler, IToolbar toolbar)
    {
        if (handler.PlatformView is { } platform)
            platform.BackButtonVisible = toolbar.BackButtonVisible;
    }

    /// <summary>
    /// The platform toolbar of <paramref name="toolbar"/> in <paramref name="context"/>: its
    /// handler's, or a new handler's when it has none there (MAUI's ToPlatform: a window moved
    /// to another context realizes its toolbar again).
    /// </summary>
    internal static SkiaToolbar? Realize(IToolbar toolbar, IMauiContext context)
    {
        try
        {
            if (toolbar.Handler is ToolbarHandler { PlatformView: { } existing } handler && ReferenceEquals(handler.MauiContext, context))
                return existing;
            toolbar.Handler?.DisconnectHandler();
            return MauiHandlerExtensions.ToHandler(toolbar, context)?.PlatformView as SkiaToolbar;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ToolbarHandler", $"Realizing the toolbar {toolbar.GetType().Name} failed", ex);
            return null;
        }
    }
}

/// <summary>
/// The platform element of a window's toolbar (<see cref="ToolbarHandler"/>): what MAUI's
/// toolbar says the window's navigation bar shows. The bar itself is drawn by the page that
/// owns it; <see cref="SkiaWindow.Toolbar"/> is the toolbar the window shows now.
/// </summary>
public class SkiaToolbar
{
    /// <summary>The title the toolbar shows.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the toolbar is shown.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the toolbar shows a back button.</summary>
    public bool BackButtonVisible { get; set; }
}
