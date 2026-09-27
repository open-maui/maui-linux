// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp;
using MauiAbsoluteLayoutFlags = Microsoft.Maui.Layouts.AbsoluteLayoutFlags;
using PlatformAbsoluteLayoutFlags = Microsoft.Maui.Platform.AbsoluteLayoutFlags;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for AbsoluteLayout: positions children by their
/// <c>AbsoluteLayout.LayoutBounds</c> / <c>LayoutFlags</c> attached properties
/// on a <see cref="SkiaAbsoluteLayout"/>. Previously AbsoluteLayout resolved to
/// the generic layout handler and rendered as a vertical stack.
/// </summary>
public class AbsoluteLayoutHandler : LayoutHandler
{
    public new static IPropertyMapper<AbsoluteLayout, AbsoluteLayoutHandler> Mapper = new PropertyMapper<AbsoluteLayout, AbsoluteLayoutHandler>(LayoutHandler.Mapper);

    public new static CommandMapper<AbsoluteLayout, AbsoluteLayoutHandler> CommandMapper = new(LayoutHandler.CommandMapper)
    {
        ["Add"] = MapAddAbsolute,
        ["Insert"] = MapAddAbsolute,
        ["Update"] = MapUpdateAbsolute,
    };

    public AbsoluteLayoutHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView() => new SkiaAbsoluteLayout();

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        base.ConnectHandler(platformView);
        // Children present before the handler connected (XAML, or a layout that
        // fills itself in its constructor like LiveCharts' MotionCanvas) are
        // added by the base without going through the Add command mapper, so
        // their LayoutBounds/LayoutFlags must be applied here.
        SyncAllChildBounds();
    }

    private static void MapAddAbsolute(AbsoluteLayoutHandler handler, AbsoluteLayout layout, object? arg)
    {
        LayoutHandler.MapAdd(handler, layout, arg);
        if (arg is LayoutHandlerUpdate update)
            ApplyBounds(handler, update.View);
    }

    private static void MapUpdateAbsolute(AbsoluteLayoutHandler handler, AbsoluteLayout layout, object? arg)
    {
        LayoutHandler.MapUpdate(handler, layout, arg);
        if (arg is LayoutHandlerUpdate update)
            ApplyBounds(handler, update.View);
    }

    /// <summary>Re-reads every child's bounds and flags (after a property change on the layout).</summary>
    public void SyncAllChildBounds()
    {
        if (VirtualView is not AbsoluteLayout layout) return;
        foreach (var child in layout.Children)
            ApplyBounds(this, child);
    }

    private static void ApplyBounds(AbsoluteLayoutHandler handler, IView? child)
    {
        if (handler.PlatformView is not SkiaAbsoluteLayout absolute) return;
        if (child is not BindableObject bindable || child.Handler?.PlatformView is not SkiaView skiaView) return;

        var (rect, flags) = ReadBounds(bindable);
        absolute.SetLayoutBounds(skiaView, rect, flags);
        absolute.InvalidateMeasure();
        absolute.Invalidate();
    }

    /// <summary>A MAUI child's LayoutBounds and LayoutFlags, as the platform layout reads them.</summary>
    internal static (SKRect Rect, PlatformAbsoluteLayoutFlags Flags) ReadBounds(BindableObject child)
    {
        var bounds = AbsoluteLayout.GetLayoutBounds(child);
        var flags = AbsoluteLayout.GetLayoutFlags(child);

        // AutoSize (-1) width/height: SkiaAbsoluteLayout treats <= 0 as "measure the child".
        var rect = new SKRect(
            (float)bounds.X,
            (float)bounds.Y,
            (float)(bounds.X + (bounds.Width == AbsoluteLayout.AutoSize ? 0 : bounds.Width)),
            (float)(bounds.Y + (bounds.Height == AbsoluteLayout.AutoSize ? 0 : bounds.Height)));
        return (rect, Convert(flags));
    }

    private static PlatformAbsoluteLayoutFlags Convert(MauiAbsoluteLayoutFlags flags)
    {
        var result = PlatformAbsoluteLayoutFlags.None;
        if (flags.HasFlag(MauiAbsoluteLayoutFlags.XProportional)) result |= PlatformAbsoluteLayoutFlags.XProportional;
        if (flags.HasFlag(MauiAbsoluteLayoutFlags.YProportional)) result |= PlatformAbsoluteLayoutFlags.YProportional;
        if (flags.HasFlag(MauiAbsoluteLayoutFlags.WidthProportional)) result |= PlatformAbsoluteLayoutFlags.WidthProportional;
        if (flags.HasFlag(MauiAbsoluteLayoutFlags.HeightProportional)) result |= PlatformAbsoluteLayoutFlags.HeightProportional;
        return result;
    }
}
