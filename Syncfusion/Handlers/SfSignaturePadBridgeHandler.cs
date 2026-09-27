// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Handlers;
using Syncfusion.Maui.Graphics.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for <c>SfSignaturePad</c> (<see cref="ISignaturePad"/>), replacing
/// Syncfusion's <c>SignaturePadHandler</c>, whose platform-neutral platform
/// view neither draws nor takes input and throws from <c>ToImageSource</c>.
/// Strokes are drawn by <see cref="SkiaSfSignaturePad"/>; <c>DrawStarted</c>
/// (which can cancel the stroke) and <c>DrawCompleted</c> are raised through
/// the control's own interaction callbacks. <c>SfSignaturePad</c> reaches
/// <c>ToImageSource</c> and <c>GetSignaturePoints</c> only through
/// Syncfusion's handler type, so <see cref="SfSignaturePadPatches"/> routes
/// them here.
/// </summary>
public class SfSignaturePadBridgeHandler : LinuxViewHandler<ISignaturePad, SkiaSfSignaturePad>
{
    public static IPropertyMapper<ISignaturePad, SfSignaturePadBridgeHandler> Mapper =
        new PropertyMapper<ISignaturePad, SfSignaturePadBridgeHandler>(ViewHandler.ViewMapper)
        {
            [nameof(ISignaturePad.MaximumStrokeThickness)] = MapStroke,
            [nameof(ISignaturePad.MinimumStrokeThickness)] = MapStroke,
            [nameof(ISignaturePad.StrokeColor)] = MapStroke,
        };

    public static CommandMapper<ISignaturePad, SfSignaturePadBridgeHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            [nameof(ISignaturePad.Clear)] = (h, _, _) => h.PlatformView?.Clear(),
        };

    public SfSignaturePadBridgeHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaSfSignaturePad CreatePlatformView() => new();

    protected override void ConnectHandler(SkiaSfSignaturePad platformView)
    {
        base.ConnectHandler(platformView);
        platformView.MauiView = VirtualView as View;
        platformView.StrokeStarting = () => VirtualView?.StartInteraction() ?? false;
        platformView.StrokeCompleted += OnStrokeCompleted;
    }

    protected override void DisconnectHandler(SkiaSfSignaturePad platformView)
    {
        platformView.StrokeStarting = null;
        platformView.StrokeCompleted -= OnStrokeCompleted;
        platformView.MauiView = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapStroke(SfSignaturePadBridgeHandler handler, ISignaturePad pad)
    {
        if (handler.PlatformView is not { } view)
            return;
        view.MaximumStrokeThickness = (float)pad.MaximumStrokeThickness;
        view.MinimumStrokeThickness = (float)pad.MinimumStrokeThickness;
        view.StrokeColor = pad.StrokeColor;
    }

    private void OnStrokeCompleted(object? sender, EventArgs e) => VirtualView?.EndInteraction();

    /// <summary>
    /// The drawn signature as a PNG image source (strokes on a transparent
    /// background, at the display's scale), or null before the pad is laid out.
    /// </summary>
    public ImageSource? ToImageSource()
    {
        if (PlatformView?.ToPng(Math.Max(1f, PlatformView.DeviceScale)) is not { } png)
            return null;
        return ImageSource.FromStream(() => new MemoryStream(png));
    }

    /// <summary>The completed strokes' points, x and y alternating.</summary>
    public List<List<float>>? GetSignaturePoints() => PlatformView?.PointsCollection;
}
