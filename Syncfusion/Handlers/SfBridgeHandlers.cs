// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Handlers;
using Syncfusion.Maui.Graphics.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for Syncfusion's <c>IDrawableLayout</c> controls (every
/// <c>SfView</c>), replacing the platform-neutral <c>SfViewHandler</c> whose
/// platform view throws. Deliberately not an <c>SfViewHandler</c>: SfView only
/// calls that type's (throwing) Invalidate/SetDrawingOrder members when its
/// handler is one, so with this handler those calls become no-ops.
/// </summary>
public class SfLayoutBridgeHandler : CrossPlatformLayoutHandler
{
    protected override SkiaLayoutView CreatePlatformView() => new SkiaSfLayout();

    protected override void ConnectHandler(SkiaLayoutView platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is SkiaSfLayout sf)
        {
            sf.MauiContext = MauiContext;
            SfInvalidation.Track(sf);
        }
        if (VirtualView != null && IsTabStop(VirtualView.GetType()))
            platformView.IsFocusable = true;
        if (VirtualView is Element element)
        {
            element.ChildAdded += OnChildrenChanged;
            element.ChildRemoved += OnChildrenChanged;
            element.PropertyChanged += OnPropertyChanged;
        }
    }

    protected override void DisconnectHandler(SkiaLayoutView platformView)
    {
        if (VirtualView is Element element)
        {
            element.ChildAdded -= OnChildrenChanged;
            element.ChildRemoved -= OnChildrenChanged;
            element.PropertyChanged -= OnPropertyChanged;
        }
        SfInvalidation.Untrack(platformView);
        base.DisconnectHandler(platformView);
    }

    /// <summary>
    /// The controls whose Windows <c>OnHandlerChanged</c> makes the native view
    /// a tab stop (<c>IsTabStop = true</c>): Tab reaches them, and OpenMaui's
    /// focusable views are its tab stops. The segmented control's parts
    /// (<c>KeyNavigationView</c>, <c>OutlinedBorderView</c>,
    /// <c>SelectionView</c>) are explicitly not, and stay unfocusable here.
    /// </summary>
    private static readonly HashSet<string> s_tabStops = new()
    {
        "Syncfusion.Maui.Buttons.SfButton",
        "Syncfusion.Maui.Buttons.SfCheckBox",
        "Syncfusion.Maui.Buttons.SfRadioButton",
        "Syncfusion.Maui.Buttons.SfSwitch",
        "Syncfusion.Maui.Buttons.SfSegmentedControl",
    };

    internal static bool IsTabStop(Type? type)
    {
        for (; type != null && type != typeof(object); type = type.BaseType)
        {
            if (type.FullName is { } name && s_tabStops.Contains(name))
                return true;
        }
        return false;
    }

    private void OnChildrenChanged(object? sender, ElementEventArgs e)
    {
        if (PlatformView is not SkiaSfLayout sf)
            return;
        sf.SyncChildren();
        sf.InvalidateMeasure();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => PlatformView?.Invalidate();
}

/// <summary>
/// Handler for Syncfusion's childless <c>IDrawableView</c> controls,
/// replacing the platform-neutral <c>SfDrawableViewHandler</c>.
/// </summary>
public class SfDrawableBridgeHandler : LinuxViewHandler<IDrawableView, SkiaSfDrawable>
{
    public static IPropertyMapper<IDrawableView, SfDrawableBridgeHandler> Mapper =
        new PropertyMapper<IDrawableView, SfDrawableBridgeHandler>(ViewHandler.ViewMapper);

    public SfDrawableBridgeHandler() : base(Mapper)
    {
    }

    protected override SkiaSfDrawable CreatePlatformView() => new SkiaSfDrawable();

    protected override void ConnectHandler(SkiaSfDrawable platformView)
    {
        base.ConnectHandler(platformView);
        SfInvalidation.Track(platformView);
        if (VirtualView is BindableObject bindable)
            bindable.PropertyChanged += OnPropertyChanged;
    }

    protected override void DisconnectHandler(SkiaSfDrawable platformView)
    {
        if (VirtualView is BindableObject bindable)
            bindable.PropertyChanged -= OnPropertyChanged;
        SfInvalidation.Untrack(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => PlatformView?.Invalidate();
}
