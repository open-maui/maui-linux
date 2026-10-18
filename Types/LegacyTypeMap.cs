// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This file converts between the obsolete OpenMaui duplicates of MAUI types
// (kept for binary compatibility until OpenMaui 11.0) and the MAUI types the
// views use internally. Naming the obsolete types is its whole purpose.
#pragma warning disable CS0618

using MauiLayouts = Microsoft.Maui.Layouts;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Conversions between the obsolete OpenMaui enums and structs in
/// <c>Microsoft.Maui.Platform</c> and the MAUI types that replace them.
/// </summary>
internal static class LegacyTypeMap
{
    // Same names and ordinals: plain casts.
    internal static Microsoft.Maui.Controls.StackOrientation ToMaui(this StackOrientation value) => (Microsoft.Maui.Controls.StackOrientation)(int)value;
    internal static StackOrientation ToLegacy(this Microsoft.Maui.Controls.StackOrientation value) => (StackOrientation)(int)value;

    internal static Microsoft.Maui.Controls.ItemsLayoutOrientation ToMaui(this ItemsLayoutOrientation value) => (Microsoft.Maui.Controls.ItemsLayoutOrientation)(int)value;
    internal static ItemsLayoutOrientation ToLegacy(this Microsoft.Maui.Controls.ItemsLayoutOrientation value) => (ItemsLayoutOrientation)(int)value;

    internal static Microsoft.Maui.ScrollOrientation ToMaui(this ScrollOrientation value) => (Microsoft.Maui.ScrollOrientation)(int)value;
    internal static ScrollOrientation ToLegacy(this Microsoft.Maui.ScrollOrientation value) => (ScrollOrientation)(int)value;

    internal static Microsoft.Maui.Controls.ScrollToPosition ToMaui(this ScrollToPosition value) => (Microsoft.Maui.Controls.ScrollToPosition)(int)value;
    internal static ScrollToPosition ToLegacy(this Microsoft.Maui.Controls.ScrollToPosition value) => (ScrollToPosition)(int)value;

    internal static Microsoft.Maui.Primitives.LayoutAlignment ToMaui(this LayoutAlignment value) => (Microsoft.Maui.Primitives.LayoutAlignment)(int)value;
    internal static LayoutAlignment ToLegacy(this Microsoft.Maui.Primitives.LayoutAlignment value) => (LayoutAlignment)(int)value;

    internal static Microsoft.Maui.SwipeMode ToMaui(this SwipeMode value) => (Microsoft.Maui.SwipeMode)(int)value;
    internal static SwipeMode ToLegacy(this Microsoft.Maui.SwipeMode value) => (SwipeMode)(int)value;

    internal static Microsoft.Maui.GridUnitType ToMaui(this GridUnitType value) => (Microsoft.Maui.GridUnitType)(int)value;
    internal static GridUnitType ToLegacy(this Microsoft.Maui.GridUnitType value) => (GridUnitType)(int)value;

    // OpenMaui's extra Auto behaved as Default (only Always and Never are distinguished).
    internal static Microsoft.Maui.ScrollBarVisibility ToMaui(this ScrollBarVisibility value) => value switch
    {
        ScrollBarVisibility.Always => Microsoft.Maui.ScrollBarVisibility.Always,
        ScrollBarVisibility.Never => Microsoft.Maui.ScrollBarVisibility.Never,
        _ => Microsoft.Maui.ScrollBarVisibility.Default,
    };
    internal static ScrollBarVisibility ToLegacy(this Microsoft.Maui.ScrollBarVisibility value) => (ScrollBarVisibility)(int)value;

    // Same members, different ordinals: by name.
    internal static Microsoft.Maui.Controls.FlyoutLayoutBehavior ToMaui(this FlyoutLayoutBehavior value) => value switch
    {
        FlyoutLayoutBehavior.Popover => Microsoft.Maui.Controls.FlyoutLayoutBehavior.Popover,
        FlyoutLayoutBehavior.Split => Microsoft.Maui.Controls.FlyoutLayoutBehavior.Split,
        FlyoutLayoutBehavior.SplitOnLandscape => Microsoft.Maui.Controls.FlyoutLayoutBehavior.SplitOnLandscape,
        FlyoutLayoutBehavior.SplitOnPortrait => Microsoft.Maui.Controls.FlyoutLayoutBehavior.SplitOnPortrait,
        _ => Microsoft.Maui.Controls.FlyoutLayoutBehavior.Default,
    };
    internal static FlyoutLayoutBehavior ToLegacy(this Microsoft.Maui.Controls.FlyoutLayoutBehavior value) => value switch
    {
        Microsoft.Maui.Controls.FlyoutLayoutBehavior.Popover => FlyoutLayoutBehavior.Popover,
        Microsoft.Maui.Controls.FlyoutLayoutBehavior.Split => FlyoutLayoutBehavior.Split,
        Microsoft.Maui.Controls.FlyoutLayoutBehavior.SplitOnLandscape => FlyoutLayoutBehavior.SplitOnLandscape,
        Microsoft.Maui.Controls.FlyoutLayoutBehavior.SplitOnPortrait => FlyoutLayoutBehavior.SplitOnPortrait,
        _ => FlyoutLayoutBehavior.Default,
    };

    // OpenMaui's SwipeDirection has a None member MAUI's has not: it maps to null.
    internal static Microsoft.Maui.SwipeDirection? ToMaui(this SwipeDirection value) => value switch
    {
        SwipeDirection.Left => Microsoft.Maui.SwipeDirection.Left,
        SwipeDirection.Right => Microsoft.Maui.SwipeDirection.Right,
        SwipeDirection.Up => Microsoft.Maui.SwipeDirection.Up,
        SwipeDirection.Down => Microsoft.Maui.SwipeDirection.Down,
        _ => null,
    };
    internal static SwipeDirection ToLegacy(this Microsoft.Maui.SwipeDirection? value) => value switch
    {
        Microsoft.Maui.SwipeDirection.Left => SwipeDirection.Left,
        Microsoft.Maui.SwipeDirection.Right => SwipeDirection.Right,
        Microsoft.Maui.SwipeDirection.Up => SwipeDirection.Up,
        Microsoft.Maui.SwipeDirection.Down => SwipeDirection.Down,
        _ => SwipeDirection.None,
    };

    internal static MauiLayouts.AbsoluteLayoutFlags ToMaui(this AbsoluteLayoutFlags value)
        => value == AbsoluteLayoutFlags.All ? MauiLayouts.AbsoluteLayoutFlags.All : (MauiLayouts.AbsoluteLayoutFlags)((int)value & 15);
    internal static AbsoluteLayoutFlags ToLegacy(this MauiLayouts.AbsoluteLayoutFlags value) => (AbsoluteLayoutFlags)((int)value & 15);

    internal static MauiLayouts.FlexDirection ToMaui(this FlexDirection value) => value switch
    {
        FlexDirection.RowReverse => MauiLayouts.FlexDirection.RowReverse,
        FlexDirection.Column => MauiLayouts.FlexDirection.Column,
        FlexDirection.ColumnReverse => MauiLayouts.FlexDirection.ColumnReverse,
        _ => MauiLayouts.FlexDirection.Row,
    };
    internal static FlexDirection ToLegacy(this MauiLayouts.FlexDirection value) => value switch
    {
        MauiLayouts.FlexDirection.RowReverse => FlexDirection.RowReverse,
        MauiLayouts.FlexDirection.Column => FlexDirection.Column,
        MauiLayouts.FlexDirection.ColumnReverse => FlexDirection.ColumnReverse,
        _ => FlexDirection.Row,
    };

    internal static MauiLayouts.FlexWrap ToMaui(this FlexWrap value) => value switch
    {
        FlexWrap.Wrap => MauiLayouts.FlexWrap.Wrap,
        FlexWrap.WrapReverse => MauiLayouts.FlexWrap.Reverse,
        _ => MauiLayouts.FlexWrap.NoWrap,
    };
    internal static FlexWrap ToLegacy(this MauiLayouts.FlexWrap value) => value switch
    {
        MauiLayouts.FlexWrap.Wrap => FlexWrap.Wrap,
        MauiLayouts.FlexWrap.Reverse => FlexWrap.WrapReverse,
        _ => FlexWrap.NoWrap,
    };

    internal static MauiLayouts.FlexJustify ToMaui(this FlexJustify value) => value switch
    {
        FlexJustify.Center => MauiLayouts.FlexJustify.Center,
        FlexJustify.End => MauiLayouts.FlexJustify.End,
        FlexJustify.SpaceBetween => MauiLayouts.FlexJustify.SpaceBetween,
        FlexJustify.SpaceAround => MauiLayouts.FlexJustify.SpaceAround,
        FlexJustify.SpaceEvenly => MauiLayouts.FlexJustify.SpaceEvenly,
        _ => MauiLayouts.FlexJustify.Start,
    };
    internal static FlexJustify ToLegacy(this MauiLayouts.FlexJustify value) => value switch
    {
        MauiLayouts.FlexJustify.Center => FlexJustify.Center,
        MauiLayouts.FlexJustify.End => FlexJustify.End,
        MauiLayouts.FlexJustify.SpaceBetween => FlexJustify.SpaceBetween,
        MauiLayouts.FlexJustify.SpaceAround => FlexJustify.SpaceAround,
        MauiLayouts.FlexJustify.SpaceEvenly => FlexJustify.SpaceEvenly,
        _ => FlexJustify.Start,
    };

    internal static MauiLayouts.FlexAlignItems ToMaui(this FlexAlignItems value) => value switch
    {
        FlexAlignItems.Start => MauiLayouts.FlexAlignItems.Start,
        FlexAlignItems.Center => MauiLayouts.FlexAlignItems.Center,
        FlexAlignItems.End => MauiLayouts.FlexAlignItems.End,
        _ => MauiLayouts.FlexAlignItems.Stretch,
    };
    internal static FlexAlignItems ToLegacy(this MauiLayouts.FlexAlignItems value) => value switch
    {
        MauiLayouts.FlexAlignItems.Start => FlexAlignItems.Start,
        MauiLayouts.FlexAlignItems.Center => FlexAlignItems.Center,
        MauiLayouts.FlexAlignItems.End => FlexAlignItems.End,
        _ => FlexAlignItems.Stretch,
    };

    internal static MauiLayouts.FlexAlignContent ToMaui(this FlexAlignContent value) => value switch
    {
        FlexAlignContent.Start => MauiLayouts.FlexAlignContent.Start,
        FlexAlignContent.Center => MauiLayouts.FlexAlignContent.Center,
        FlexAlignContent.End => MauiLayouts.FlexAlignContent.End,
        FlexAlignContent.SpaceBetween => MauiLayouts.FlexAlignContent.SpaceBetween,
        FlexAlignContent.SpaceAround => MauiLayouts.FlexAlignContent.SpaceAround,
        FlexAlignContent.SpaceEvenly => MauiLayouts.FlexAlignContent.SpaceEvenly,
        _ => MauiLayouts.FlexAlignContent.Stretch,
    };
    internal static FlexAlignContent ToLegacy(this MauiLayouts.FlexAlignContent value) => value switch
    {
        MauiLayouts.FlexAlignContent.Start => FlexAlignContent.Start,
        MauiLayouts.FlexAlignContent.Center => FlexAlignContent.Center,
        MauiLayouts.FlexAlignContent.End => FlexAlignContent.End,
        MauiLayouts.FlexAlignContent.SpaceBetween => FlexAlignContent.SpaceBetween,
        MauiLayouts.FlexAlignContent.SpaceAround => FlexAlignContent.SpaceAround,
        MauiLayouts.FlexAlignContent.SpaceEvenly => FlexAlignContent.SpaceEvenly,
        _ => FlexAlignContent.Stretch,
    };

    internal static MauiLayouts.FlexAlignSelf ToMaui(this FlexAlignSelf value) => value switch
    {
        FlexAlignSelf.Start => MauiLayouts.FlexAlignSelf.Start,
        FlexAlignSelf.Center => MauiLayouts.FlexAlignSelf.Center,
        FlexAlignSelf.End => MauiLayouts.FlexAlignSelf.End,
        FlexAlignSelf.Stretch => MauiLayouts.FlexAlignSelf.Stretch,
        _ => MauiLayouts.FlexAlignSelf.Auto,
    };
    internal static FlexAlignSelf ToLegacy(this MauiLayouts.FlexAlignSelf value) => value switch
    {
        MauiLayouts.FlexAlignSelf.Start => FlexAlignSelf.Start,
        MauiLayouts.FlexAlignSelf.Center => FlexAlignSelf.Center,
        MauiLayouts.FlexAlignSelf.End => FlexAlignSelf.End,
        MauiLayouts.FlexAlignSelf.Stretch => FlexAlignSelf.Stretch,
        _ => FlexAlignSelf.Auto,
    };

    // OpenMaui's FlexBasis is Auto or an absolute length; MAUI's adds relative lengths,
    // which the legacy type cannot express: they become Auto.
    internal static MauiLayouts.FlexBasis ToMaui(this FlexBasis value)
        => value.IsAuto ? MauiLayouts.FlexBasis.Auto : new MauiLayouts.FlexBasis(value.Length);
    internal static FlexBasis ToLegacy(this MauiLayouts.FlexBasis value)
        => value.Equals(MauiLayouts.FlexBasis.Auto) || value.Equals(new MauiLayouts.FlexBasis(value.Length, isRelative: true))
            ? FlexBasis.Auto
            : FlexBasis.FromLength(value.Length);

    internal static Microsoft.Maui.GridLength ToMaui(this GridLength value)
        => new(value.Value, value.GridUnitType.ToMaui());
    internal static GridLength ToLegacy(this Microsoft.Maui.GridLength value)
        => new((float)value.Value, value.GridUnitType.ToLegacy());
}
