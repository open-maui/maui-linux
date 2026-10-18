// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Stepper on Linux using Skia rendering.
/// Maps IStepper interface to SkiaStepper platform view.
/// </summary>
public partial class StepperHandler : LinuxViewHandler<IStepper, SkiaStepper>, IStepperHandler
{
    IStepper IStepperHandler.VirtualView => VirtualView;
    object IStepperHandler.PlatformView => PlatformView;

    public static IPropertyMapper<IStepper, StepperHandler> Mapper =
        new PropertyMapper<IStepper, StepperHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IStepper.Value)] = MapValue,
            [nameof(IStepper.Minimum)] = MapMinimum,
            [nameof(IStepper.Maximum)] = MapMaximum,
            ["Increment"] = MapIncrement,
            // MAUI's key for the step (IStepper.Interval, Stepper.Increment).
            [nameof(IStepper.Interval)] = MapIncrement,
            [nameof(IView.Background)] = MapBackground,
            [nameof(IView.IsEnabled)] = MapIsEnabled,
        };

    public static CommandMapper<IStepper, StepperHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
        };

    public StepperHandler() : base(Mapper, CommandMapper)
    {
    }

    public StepperHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaStepper CreatePlatformView()
    {
        return new SkiaStepper();
    }

    protected override void ConnectHandler(SkiaStepper platformView)
    {
        base.ConnectHandler(platformView);
        VisualStateBridge.Attach(VirtualView, platformView);
        platformView.ValueChanged += OnValueChanged;

        // Apply dark theme colors if needed
        if (Application.Current?.UserAppTheme == AppTheme.Dark)
        {
            platformView.ButtonBackgroundColor = Color.FromRgb(66, 66, 66);
            platformView.ButtonPressedColor = Color.FromRgb(97, 97, 97);
            platformView.ButtonDisabledColor = Color.FromRgb(48, 48, 48);
            platformView.SymbolColor = Color.FromRgb(224, 224, 224);
            platformView.SymbolDisabledColor = Color.FromRgb(97, 97, 97);
            platformView.BorderColor = Color.FromRgb(97, 97, 97);
        }

        // Sync properties
        if (VirtualView != null)
        {
            MapValue(this, VirtualView);
            MapMinimum(this, VirtualView);
            MapMaximum(this, VirtualView);
            MapIsEnabled(this, VirtualView);
        }
    }

    protected override void DisconnectHandler(SkiaStepper platformView)
    {
        platformView.ValueChanged -= OnValueChanged;
        VisualStateBridge.Detach(platformView);
        base.DisconnectHandler(platformView);
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;

        if (Math.Abs(VirtualView.Value - e.NewValue) > 0.0001)
        {
            VirtualView.Value = e.NewValue;
        }
    }

    /// <summary>MAUI's <c>MapValue</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapValue(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapValueCore(linuxHandler, stepper);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapValue(StepperHandler handler, IStepper stepper) => MapValueCore(handler, stepper);

    private static void MapValueCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;

        if (Math.Abs(handler.PlatformView.Value - stepper.Value) > 0.0001)
            handler.PlatformView.Value = stepper.Value;
    }

    /// <summary>MAUI's <c>MapMinimum</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapMinimum(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapMinimumCore(linuxHandler, stepper);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapMinimum(StepperHandler handler, IStepper stepper) => MapMinimumCore(handler, stepper);

    private static void MapMinimumCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Minimum = stepper.Minimum;
    }

    /// <summary>MAUI's <c>MapMaximum</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapMaximum(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapMaximumCore(linuxHandler, stepper);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapMaximum(StepperHandler handler, IStepper stepper) => MapMaximumCore(handler, stepper);

    private static void MapMaximumCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Maximum = stepper.Maximum;
    }

    public static void MapBackground(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;

        if (stepper.Background is SolidPaint solidPaint && solidPaint.Color is not null)
        {
            handler.PlatformView.BackgroundColor = solidPaint.Color;
        }
    }

    /// <summary>MAUI's <c>MapIncrement</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static void MapIncrement(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapIncrementCore(linuxHandler, stepper);
    }

    [Obsolete("Use the IViewHandler overload; this one is removed in OpenMaui 11.0.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MapIncrement(StepperHandler handler, IStepper stepper) => MapIncrementCore(handler, stepper);

    private static void MapIncrementCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;

        handler.PlatformView.Increment = stepper is Stepper stepperControl ? stepperControl.Increment : stepper.Interval;
    }

    public static void MapIsEnabled(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = stepper.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
