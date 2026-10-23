// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

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

    // Set while the virtual view's range or value is applied to the platform view.
    private bool _applyingVirtualView;

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (VirtualView is null || PlatformView is null || _applyingVirtualView) return;

        if (Math.Abs(VirtualView.Value - e.NewValue) > 0.0001)
        {
            VirtualView.Value = e.NewValue;
        }
    }

    /// <summary>MAUI's <c>MapValue</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    public static void MapValue(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapValueCore(linuxHandler, stepper);
    }

    private static void MapValueCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.ApplyFromVirtualView(stepper, null);
    }

    /// <summary>
    /// Applies the virtual view's range (one bound at a time) or value to the platform view, then
    /// its value again. The platform view clamps its value to its own range, which is stale until
    /// every bound is mapped (a Minimum above its default Value, a Value above its default
    /// Maximum); that clamp is not a user change and is not written back to the virtual view,
    /// which coerces its own Value to its range.
    /// </summary>
    private void ApplyFromVirtualView(IStepper stepper, Action<SkiaStepper>? apply)
    {
        if (PlatformView is not { } platformView) return;
        _applyingVirtualView = true;
        try
        {
            apply?.Invoke(platformView);
            if (Math.Abs(platformView.Value - stepper.Value) > 0.0001)
                platformView.Value = stepper.Value;
        }
        finally
        {
            _applyingVirtualView = false;
        }
    }

    /// <summary>MAUI's <c>MapMinimum</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    public static void MapMinimum(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapMinimumCore(linuxHandler, stepper);
    }

    private static void MapMinimumCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.ApplyFromVirtualView(stepper, p => p.Minimum = stepper.Minimum);
    }

    /// <summary>MAUI's <c>MapMaximum</c> signature; maps the property on a Linux <see cref="StepperHandler"/>.</summary>
    public static void MapMaximum(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapMaximumCore(linuxHandler, stepper);
    }

    private static void MapMaximumCore(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.ApplyFromVirtualView(stepper, p => p.Maximum = stepper.Maximum);
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
    public static void MapIncrement(IViewHandler handler, IStepper stepper)
    {
        if (handler is StepperHandler linuxHandler)
            MapIncrementCore(linuxHandler, stepper);
    }

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
