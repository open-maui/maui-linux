// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for ScrollView control.
/// </summary>
public class ScrollViewHandler : ViewHandler<IScrollView, SkiaScrollView>
{
    public static IPropertyMapper<IScrollView, ScrollViewHandler> Mapper = new PropertyMapper<IScrollView, ScrollViewHandler>(ViewHandler.ViewMapper)
    {
        ["Content"] = MapContent,
        ["HorizontalScrollBarVisibility"] = MapHorizontalScrollBarVisibility,
        ["VerticalScrollBarVisibility"] = MapVerticalScrollBarVisibility,
        ["Orientation"] = MapOrientation
    };

    public static CommandMapper<IScrollView, ScrollViewHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
        ["RequestScrollTo"] = MapRequestScrollTo
    };

    public ScrollViewHandler() : base(Mapper, CommandMapper)
    {
    }

    public ScrollViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper, CommandMapper)
    {
    }

    protected override SkiaScrollView CreatePlatformView()
    {
        return new SkiaScrollView();
    }

    public static void MapContent(ScrollViewHandler handler, IScrollView scrollView)
    {
        if (handler.PlatformView == null || handler.MauiContext == null)
        {
            return;
        }

        var presentedContent = scrollView.PresentedContent;
        if (presentedContent != null)
        {
            Console.WriteLine("[ScrollViewHandler] MapContent: " + presentedContent.GetType().Name);

            if (presentedContent.Handler == null)
            {
                presentedContent.Handler = presentedContent.ToViewHandler(handler.MauiContext);
            }

            if (presentedContent.Handler?.PlatformView is SkiaView skiaView)
            {
                Console.WriteLine("[ScrollViewHandler] Setting content: " + skiaView.GetType().Name);
                handler.PlatformView.Content = skiaView;
            }
        }
        else
        {
            handler.PlatformView.Content = null;
        }
    }

    public static void MapHorizontalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.HorizontalScrollBarVisibility = (int)scrollView.HorizontalScrollBarVisibility switch
        {
            1 => ScrollBarVisibility.Always,
            2 => ScrollBarVisibility.Never,
            _ => ScrollBarVisibility.Default
        };
    }

    public static void MapVerticalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.VerticalScrollBarVisibility = (int)scrollView.VerticalScrollBarVisibility switch
        {
            1 => ScrollBarVisibility.Always,
            2 => ScrollBarVisibility.Never,
            _ => ScrollBarVisibility.Default
        };
    }

    public static void MapOrientation(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.Orientation = ((int)scrollView.Orientation - 1) switch
        {
            0 => ScrollOrientation.Horizontal,
            1 => ScrollOrientation.Both,
            2 => ScrollOrientation.Neither,
            _ => ScrollOrientation.Vertical
        };
    }

    public static void MapRequestScrollTo(ScrollViewHandler handler, IScrollView scrollView, object? args)
    {
        if (args is ScrollToRequest request)
        {
            handler.PlatformView.ScrollTo((float)request.HorizontalOffset, (float)request.VerticalOffset, !request.Instant);
        }
    }
}
