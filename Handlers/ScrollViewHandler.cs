// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for ScrollView on Linux using SkiaScrollView.
/// </summary>
public partial class ScrollViewHandler : ViewHandler<IScrollView, SkiaScrollView>
{
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

    protected override SkiaScrollView CreatePlatformView()
    {
        return new SkiaScrollView();
    }

    public static void MapContent(ScrollViewHandler handler, IScrollView scrollView)
    {
        if (handler.PlatformView == null || handler.MauiContext == null)
            return;

        var content = scrollView.PresentedContent;
        if (content != null)
        {
            Console.WriteLine($"[ScrollViewHandler] MapContent: {content.GetType().Name}");

            // Create handler for content if it doesn't exist
            if (content.Handler == null)
            {
                content.Handler = content.ToHandler(handler.MauiContext);
            }

            if (content.Handler?.PlatformView is SkiaView skiaContent)
            {
                Console.WriteLine($"[ScrollViewHandler] Setting content: {skiaContent.GetType().Name}");
                handler.PlatformView.Content = skiaContent;
            }
        }
        else
        {
            handler.PlatformView.Content = null;
        }
    }

    public static void MapHorizontalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.HorizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility switch
        {
            Microsoft.Maui.ScrollBarVisibility.Always => ScrollBarVisibility.Always,
            Microsoft.Maui.ScrollBarVisibility.Never => ScrollBarVisibility.Never,
            _ => ScrollBarVisibility.Default
        };
    }

    public static void MapVerticalScrollBarVisibility(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.VerticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility switch
        {
            Microsoft.Maui.ScrollBarVisibility.Always => ScrollBarVisibility.Always,
            Microsoft.Maui.ScrollBarVisibility.Never => ScrollBarVisibility.Never,
            _ => ScrollBarVisibility.Default
        };
    }

    public static void MapOrientation(ScrollViewHandler handler, IScrollView scrollView)
    {
        handler.PlatformView.Orientation = scrollView.Orientation switch
        {
            Microsoft.Maui.ScrollOrientation.Horizontal => ScrollOrientation.Horizontal,
            Microsoft.Maui.ScrollOrientation.Both => ScrollOrientation.Both,
            Microsoft.Maui.ScrollOrientation.Neither => ScrollOrientation.Neither,
            _ => ScrollOrientation.Vertical
        };
    }

    public static void MapRequestScrollTo(ScrollViewHandler handler, IScrollView scrollView, object? args)
    {
        if (args is ScrollToRequest request)
        {
            // Instant means no animation, so we pass !Instant for animated parameter
            handler.PlatformView.ScrollTo((float)request.HorizontalOffset, (float)request.VerticalOffset, !request.Instant);
        }
    }
}
