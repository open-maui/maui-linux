// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for CheckBox on Linux using Skia rendering.
/// Maps ICheckBox interface to SkiaCheckBox platform view.
/// </summary>
public partial class CheckBoxHandler : LinuxViewHandler<ICheckBox, SkiaCheckBox>, ICheckBoxHandler
{
    ICheckBox ICheckBoxHandler.VirtualView => VirtualView;
    object ICheckBoxHandler.PlatformView => PlatformView;

    public static IPropertyMapper<ICheckBox, CheckBoxHandler> Mapper = new PropertyMapper<ICheckBox, CheckBoxHandler>(ViewHandler.ViewMapper)
    {
        [nameof(ICheckBox.IsChecked)] = MapIsChecked,
        [nameof(ICheckBox.Foreground)] = MapForeground,
        // Controls' CheckBox.Color: MAUI remaps it to refresh Foreground (CheckBox.RemapForControls).
        ["Color"] = MapForeground,
        [nameof(IView.Background)] = MapBackground,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        [nameof(IView.VerticalLayoutAlignment)] = MapVerticalLayoutAlignment,
        [nameof(IView.HorizontalLayoutAlignment)] = MapHorizontalLayoutAlignment,
    };

    public static CommandMapper<ICheckBox, CheckBoxHandler> CommandMapper = new(ViewHandler.ViewCommandMapper)
    {
    };

    public CheckBoxHandler() : base(Mapper, CommandMapper)
    {
    }

    public CheckBoxHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaCheckBox CreatePlatformView()
    {
        return new SkiaCheckBox();
    }

    protected override void ConnectHandler(SkiaCheckBox platformView)
    {
        base.ConnectHandler(platformView);
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.CheckedChanged += OnCheckedChanged;
    }

    protected override void DisconnectHandler(SkiaCheckBox platformView)
    {
        platformView.CheckedChanged -= OnCheckedChanged;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnCheckedChanged(object? sender, SkiaCheckedChangedEventArgs e)
    {
        if (VirtualView is not null && VirtualView.IsChecked != e.IsChecked)
        {
            VirtualView.IsChecked = e.IsChecked;
        }
    }

    /// <summary>MAUI's <c>MapIsChecked</c> signature; maps the property on a Linux <see cref="CheckBoxHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIsChecked(ICheckBoxHandler handler, ICheckBox check)
    {
        if (handler is CheckBoxHandler linuxHandler)
            MapIsCheckedCore(linuxHandler, check);
    }

    [Obsolete("Use the ICheckBoxHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIsChecked(CheckBoxHandler handler, ICheckBox checkBox) => MapIsCheckedCore(handler, checkBox);

    private static void MapIsCheckedCore(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsChecked = checkBox.IsChecked;
    }

    /// <summary>MAUI's <c>MapForeground</c> signature; maps the property on a Linux <see cref="CheckBoxHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapForeground(ICheckBoxHandler handler, ICheckBox check)
    {
        if (handler is CheckBoxHandler linuxHandler)
            MapForegroundCore(linuxHandler, check);
    }

    [Obsolete("Use the ICheckBoxHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapForeground(CheckBoxHandler handler, ICheckBox checkBox) => MapForegroundCore(handler, checkBox);

    private static void MapForegroundCore(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;

        if (checkBox.Foreground is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.CheckColor = solidPaint.Color;
        }
    }

    public static void MapBackground(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;

        // Background paints the view behind the box, as on MAUI's platforms; the
        // box's own colour is the Foreground (CheckBox.Color).
        handler.PlatformView.BackgroundColor =
            checkBox.Background is SolidPaint { Color: not null } solidPaint ? solidPaint.Color : null;
    }

    public static void MapIsEnabled(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = checkBox.IsEnabled;
    }

    public static void MapVerticalLayoutAlignment(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.VerticalOptions = checkBox.VerticalLayoutAlignment switch
        {
            Primitives.LayoutAlignment.Start => LayoutOptions.Start,
            Primitives.LayoutAlignment.Center => LayoutOptions.Center,
            Primitives.LayoutAlignment.End => LayoutOptions.End,
            Primitives.LayoutAlignment.Fill => LayoutOptions.Fill,
            _ => LayoutOptions.Fill
        };
    }

    public static void MapHorizontalLayoutAlignment(CheckBoxHandler handler, ICheckBox checkBox)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.HorizontalOptions = checkBox.HorizontalLayoutAlignment switch
        {
            Primitives.LayoutAlignment.Start => LayoutOptions.Start,
            Primitives.LayoutAlignment.Center => LayoutOptions.Center,
            Primitives.LayoutAlignment.End => LayoutOptions.End,
            Primitives.LayoutAlignment.Fill => LayoutOptions.Fill,
            _ => LayoutOptions.Start
        };
    }
}
