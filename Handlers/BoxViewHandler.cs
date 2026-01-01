// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for BoxView on Linux.
/// </summary>
public partial class BoxViewHandler : ViewHandler<BoxView, SkiaBoxView>
{
    public static IPropertyMapper<BoxView, BoxViewHandler> Mapper =
        new PropertyMapper<BoxView, BoxViewHandler>(ViewMapper)
        {
            [nameof(BoxView.Color)] = MapColor,
            [nameof(BoxView.CornerRadius)] = MapCornerRadius,
            [nameof(IView.Background)] = MapBackground,
            ["BackgroundColor"] = MapBackgroundColor,
        };

    public BoxViewHandler() : base(Mapper)
    {
    }

    protected override SkiaBoxView CreatePlatformView()
    {
        return new SkiaBoxView();
    }

    public static void MapColor(BoxViewHandler handler, BoxView boxView)
    {
        if (boxView.Color != null)
        {
            handler.PlatformView.Color = new SKColor(
                (byte)(boxView.Color.Red * 255),
                (byte)(boxView.Color.Green * 255),
                (byte)(boxView.Color.Blue * 255),
                (byte)(boxView.Color.Alpha * 255));
        }
    }

    public static void MapCornerRadius(BoxViewHandler handler, BoxView boxView)
    {
        handler.PlatformView.CornerRadius = (float)boxView.CornerRadius.TopLeft;
    }

    public static void MapBackground(BoxViewHandler handler, BoxView boxView)
    {
        if (boxView.Background is SolidColorBrush solidBrush && solidBrush.Color != null)
        {
            handler.PlatformView.BackgroundColor = solidBrush.Color.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }

    public static void MapBackgroundColor(BoxViewHandler handler, BoxView boxView)
    {
        if (boxView.BackgroundColor != null)
        {
            handler.PlatformView.BackgroundColor = boxView.BackgroundColor.ToSKColor();
            handler.PlatformView.Invalidate();
        }
    }
}
