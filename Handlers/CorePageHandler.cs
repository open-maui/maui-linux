// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's core PageHandler on Linux: the handler of a page that is a core
/// <see cref="IContentView"/> (with <see cref="ITitledElement"/> for its title) rather than a
/// Controls Page, which MAUI's PageHandler (a ContentViewHandler that maps Title) handles on
/// its platforms. Its platform view is a <see cref="SkiaPage"/>: the content, the title and
/// the navigation bar a navigation view shows above it (<see cref="CoreNavigationViewHandler"/>).
/// OpenMaui's <see cref="PageHandler"/> and <see cref="ContentPageHandler"/> stay the handlers of
/// Controls pages. A view given MAUI's own PageHandler gets this one.
/// </summary>
public partial class CorePageHandler : LinuxViewHandler<IContentView, SkiaPage>
{
    public static IPropertyMapper<IContentView, CorePageHandler> Mapper = new PropertyMapper<IContentView, CorePageHandler>(ViewHandler.ViewMapper)
    {
        [nameof(IContentView.Content)] = MapContent,
        [nameof(ITitledElement.Title)] = MapTitle,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IPadding.Padding)] = MapPadding,
    };

    public static CommandMapper<IContentView, CorePageHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public CorePageHandler() : base(Mapper, CommandMapper)
    {
    }

    public CorePageHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public CorePageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    // A page on its own shows no bar; a navigation view shows one on the pages of its stack.
    protected override SkiaPage CreatePlatformView() => new() { ShowNavigationBar = false };

    public static void MapContent(CorePageHandler handler, IContentView page)
    {
        if (handler.PlatformView is null || handler.MauiContext is null)
            return;
        var content = page.PresentedContent ?? page.Content as IView;
        if (content is null)
        {
            handler.PlatformView.Content = null;
            return;
        }
        try
        {
            if (content.Handler is null)
                content.ToViewHandler(handler.MauiContext);
            handler.PlatformView.Content = content.Handler?.PlatformView as SkiaView;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("CorePageHandler", $"Failed to render content ({content.GetType().Name}): {ex.Message}", ex);
        }
    }

    public static void MapTitle(CorePageHandler handler, IContentView page)
    {
        if (handler.PlatformView is null)
            return;
        handler.PlatformView.Title = (page as ITitledElement)?.Title ?? string.Empty;
    }

    public static void MapBackground(CorePageHandler handler, IContentView page)
    {
        if (handler.PlatformView is null)
            return;
        if (page.Background is SolidPaint { Color: { } color })
            handler.PlatformView.BackgroundColor = color;
    }

    public static void MapPadding(CorePageHandler handler, IContentView page)
    {
        if (handler.PlatformView is null)
            return;
        var padding = page.Padding;
        handler.PlatformView.PaddingLeft = (float)padding.Left;
        handler.PlatformView.PaddingTop = (float)padding.Top;
        handler.PlatformView.PaddingRight = (float)padding.Right;
        handler.PlatformView.PaddingBottom = (float)padding.Bottom;
        handler.PlatformView.InvalidateMeasure();
    }
}
