// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Frame on Linux using SkiaFrame.
/// </summary>
public partial class FrameHandler : ViewHandler<Frame, SkiaFrame>
{
    public static IPropertyMapper<Frame, FrameHandler> Mapper =
        new PropertyMapper<Frame, FrameHandler>(ViewMapper)
        {
            [nameof(Frame.BorderColor)] = MapBorderColor,
            [nameof(Frame.CornerRadius)] = MapCornerRadius,
            [nameof(Frame.HasShadow)] = MapHasShadow,
            [nameof(Frame.BackgroundColor)] = MapBackgroundColor,
            [nameof(Frame.Padding)] = MapPadding,
            [nameof(Frame.Content)] = MapContent,
        };

    public FrameHandler() : base(Mapper)
    {
    }

    public FrameHandler(IPropertyMapper? mapper)
        : base(mapper ?? Mapper)
    {
    }

    protected override SkiaFrame CreatePlatformView()
    {
        return new SkiaFrame();
    }

    protected override void ConnectHandler(SkiaFrame platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView is View view)
        {
            platformView.MauiView = view;
        }
        platformView.Tapped += OnPlatformViewTapped;
    }

    protected override void DisconnectHandler(SkiaFrame platformView)
    {
        platformView.Tapped -= OnPlatformViewTapped;
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    private void OnPlatformViewTapped(object? sender, EventArgs e)
    {
        if (VirtualView is View view)
        {
            GestureManager.ProcessTap(view, 0.0, 0.0);
        }
    }

    public static void MapBorderColor(FrameHandler handler, Frame frame)
    {
        if (frame.BorderColor != null)
        {
            handler.PlatformView.Stroke = frame.BorderColor;
        }
    }

    public static void MapCornerRadius(FrameHandler handler, Frame frame)
    {
        handler.PlatformView.CornerRadius = frame.CornerRadius;
    }

    public static void MapHasShadow(FrameHandler handler, Frame frame)
    {
        handler.PlatformView.HasShadow = frame.HasShadow;
    }

    public static void MapBackgroundColor(FrameHandler handler, Frame frame)
    {
        if (frame.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = frame.BackgroundColor;
        }
    }

    public static void MapPadding(FrameHandler handler, Frame frame)
    {
        handler.PlatformView.SetPadding(
            (float)frame.Padding.Left,
            (float)frame.Padding.Top,
            (float)frame.Padding.Right,
            (float)frame.Padding.Bottom);
    }

    public static void MapContent(FrameHandler handler, Frame frame)
    {
        if (handler.PlatformView is null || handler.MauiContext is null) return;

        handler.PlatformView.ClearChildren();

        var content = frame.Content;
        if (content != null)
        {
            // Create handler for content if it doesn't exist
            if (content.Handler == null)
            {
                content.Handler = content.ToViewHandler(handler.MauiContext);
            }

            if (content.Handler?.PlatformView is SkiaView skiaContent)
            {
                handler.PlatformView.AddChild(skiaContent);
            }
        }
    }
}
