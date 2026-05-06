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
public partial class StepperHandler : ViewHandler<IStepper, SkiaStepper>
{
    public static IPropertyMapper<IStepper, StepperHandler> Mapper =
        new PropertyMapper<IStepper, StepperHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IStepper.Value)] = MapValue,
            [nameof(IStepper.Minimum)] = MapMinimum,
            [nameof(IStepper.Maximum)] = MapMaximum,
            ["Increment"] = MapIncrement,
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

    public static void MapValue(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;

        if (Math.Abs(handler.PlatformView.Value - stepper.Value) > 0.0001)
            handler.PlatformView.Value = stepper.Value;
    }

    public static void MapMinimum(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Minimum = stepper.Minimum;
    }

    public static void MapMaximum(StepperHandler handler, IStepper stepper)
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

    public static void MapIncrement(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;

        if (stepper is Stepper stepperControl)
        {
            handler.PlatformView.Increment = stepperControl.Increment;
        }
    }

    public static void MapIsEnabled(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.IsEnabled = stepper.IsEnabled;
        handler.PlatformView.Invalidate();
    }
}
