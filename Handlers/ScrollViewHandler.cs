// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ScrollView on Linux using SkiaScrollView.
/// </summary>
public partial class ScrollViewHandler : LinuxViewHandler<IScrollView, SkiaScrollView>, IScrollViewHandler
{
    IScrollView IScrollViewHandler.VirtualView => VirtualView;
    object IScrollViewHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IScrollView, ScrollViewHandler> Mapper =
        new PropertyMapper<IScrollView, ScrollViewHandler>(ViewMapper)
        {
            [nameof(IScrollView.Content)] = MapContent,
            [nameof(IScrollView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
            [nameof(IScrollView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
            [nameof(IScrollView.Orientation)] = MapOrientation,
        };

    public static CommandMapper<IScrollView, ScrollViewHandler> CommandMapper =
        new(ViewCommandMapper)
        {
            [nameof(IScrollView.RequestScrollTo)] = MapRequestScrollTo
        };

    public ScrollViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public ScrollViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    public ScrollViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaScrollView CreatePlatformView()
    {
        return new SkiaScrollView();
    }

    protected override void ConnectHandler(SkiaScrollView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Scrolled += OnPlatformScrolled;
    }

    protected override void DisconnectHandler(SkiaScrollView platformView)
    {
        platformView.Scrolled -= OnPlatformScrolled;
        base.DisconnectHandler(platformView);
    }

    /// <summary>
    /// The scroll position, reported back as every platform does: ScrollX and
    /// ScrollY follow it and ScrollView.Scrolled fires. Controls that realise
    /// their content as it scrolls into view listen for that (Syncfusion's
    /// SfListView created only the rows of its first screen without it).
    /// </summary>
    private void OnPlatformScrolled(object? sender, SkiaScrolledEventArgs e)
    {
        if (VirtualView is not { } view)
            return;
        view.HorizontalOffset = e.ScrollX;
        view.VerticalOffset = e.ScrollY;
        view.ScrollFinished();
    }

    /// <summary>MAUI's <c>MapContent</c> signature; maps the property on a Linux <see cref="ScrollViewHandler"/>.</summary>
    public static void MapContent(IViewHandler handler, IScrollView scrollView)
    {
        if (handler is ScrollViewHandler linuxHandler)
            MapContentCore(linuxHandler, scrollView);
    }

    private static void MapContentCore(ScrollViewHandler handler, IScrollView scrollView)
    {
        if (handler.PlatformView == null || handler.MauiContext == null)
            return;

        var content = scrollView.PresentedContent;
        if (content != null)
        {
            DiagnosticLog.Debug("ScrollViewHandler", $"MapContent: {content.GetType().Name}");

            // Create handler for content if it doesn't exist
            if (content.Handler == null)
            {
                content.Handler = content.ToViewHandler(handler.MauiContext);
            }

            if (content.Handler?.PlatformView is SkiaView skiaContent)
            {
                DiagnosticLog.Debug("ScrollViewHandler", $"Setting content: {skiaContent.GetType().Name}");
                handler.PlatformView.Content = skiaContent;
            }
        }
        else
        {
            handler.PlatformView.Content = null;
        }
    }

    /// <summary>MAUI's <c>MapHorizontalScrollBarVisibility</c> signature; maps the property on a Linux <see cref="ScrollViewHandler"/>.</summary>
    public static void MapHorizontalScrollBarVisibility(IViewHandler handler, IScrollView scrollView)
    {
        if (handler is ScrollViewHandler linuxHandler)
            MapHorizontalScrollBarVisibilityCore(linuxHandler, scrollView);
    }

    private static void MapHorizontalScrollBarVisibilityCore(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.HorizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility;
    }

    /// <summary>MAUI's <c>MapVerticalScrollBarVisibility</c> signature; maps the property on a Linux <see cref="ScrollViewHandler"/>.</summary>
    public static void MapVerticalScrollBarVisibility(IViewHandler handler, IScrollView scrollView)
    {
        if (handler is ScrollViewHandler linuxHandler)
            MapVerticalScrollBarVisibilityCore(linuxHandler, scrollView);
    }

    private static void MapVerticalScrollBarVisibilityCore(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.VerticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility;
    }

    /// <summary>MAUI's <c>MapOrientation</c> signature; maps the property on a Linux <see cref="ScrollViewHandler"/>.</summary>
    public static void MapOrientation(IViewHandler handler, IScrollView scrollView)
    {
        if (handler is ScrollViewHandler linuxHandler)
            MapOrientationCore(linuxHandler, scrollView);
    }

    private static void MapOrientationCore(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.Orientation = scrollView.Orientation;
    }

    /// <summary>MAUI's <c>MapRequestScrollTo</c> signature; runs the command on a Linux <see cref="ScrollViewHandler"/>.</summary>
    public static void MapRequestScrollTo(IScrollViewHandler handler, IScrollView scrollView, object? args)
    {
        if (handler is ScrollViewHandler linuxHandler)
            MapRequestScrollToCore(linuxHandler, scrollView, args);
    }

    private static void MapRequestScrollToCore(ScrollViewHandler handler, IScrollView scrollView, object? args)
    {
        if (args is ScrollToRequest request)
        {
            // Instant means no animation, so we pass !Instant for animated parameter
            handler.PlatformView.ScrollTo((float)request.HorizontalOffset, (float)request.VerticalOffset, !request.Instant);
            // ScrollToAsync waits for this; a scroll to where the view already
            // is raises no Scrolled, and the await would never finish.
            scrollView.ScrollFinished();
        }
    }

    /// <summary>
    /// MAUI's <c>MapContentSize</c>: the scrollable extent follows the content. The
    /// <see cref="SkiaScrollView"/> takes its extent from its content's measure, so the scroll
    /// view is measured again.
    /// </summary>
    public static void MapContentSize(IViewHandler handler, IScrollView scrollView)
    {
        if (handler is ScrollViewHandler { PlatformView: { } platformView })
        {
            platformView.InvalidateMeasure();
            platformView.Invalidate();
        }
    }
}
