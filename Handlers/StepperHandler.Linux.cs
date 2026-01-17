// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for Stepper control.
/// </summary>
public class StepperHandler : ViewHandler<IStepper, SkiaStepper>
{
    public static IPropertyMapper<IStepper, StepperHandler> Mapper = new PropertyMapper<IStepper, StepperHandler>(ViewHandler.ViewMapper)
    {
        ["Value"] = MapValue,
        ["Minimum"] = MapMinimum,
        ["Maximum"] = MapMaximum,
        ["Increment"] = MapIncrement,
        ["Background"] = MapBackground,
        ["IsEnabled"] = MapIsEnabled
    };

    public static CommandMapper<IStepper, StepperHandler> CommandMapper = new(ViewHandler.ViewCommandMapper);

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
            platformView.ButtonBackgroundColor = new SKColor(66, 66, 66);
            platformView.ButtonPressedColor = new SKColor(97, 97, 97);
            platformView.ButtonDisabledColor = new SKColor(48, 48, 48);
            platformView.SymbolColor = new SKColor(224, 224, 224);
            platformView.SymbolDisabledColor = new SKColor(97, 97, 97);
            platformView.BorderColor = new SKColor(97, 97, 97);
        }
    }

    protected override void DisconnectHandler(SkiaStepper platformView)
    {
        platformView.ValueChanged -= OnValueChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        if (VirtualView != null && PlatformView != null)
        {
            VirtualView.Value = PlatformView.Value;
        }
    }

    public static void MapValue(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Value = stepper.Value;
        }
    }

    public static void MapMinimum(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Minimum = stepper.Minimum;
        }
    }

    public static void MapMaximum(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.Maximum = stepper.Maximum;
        }
    }

    public static void MapBackground(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView != null)
        {
            if (stepper.Background is SolidPaint solidPaint && solidPaint.Color != null)
            {
                handler.PlatformView.BackgroundColor = solidPaint.Color;
            }
        }
    }

    public static void MapIncrement(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView != null)
        {
            if (stepper is Stepper mauiStepper)
            {
                handler.PlatformView.Increment = mauiStepper.Increment;
            }
        }
    }

    public static void MapIsEnabled(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView != null)
        {
            handler.PlatformView.IsEnabled = stepper.IsEnabled;
        }
    }
}
