// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Stepper on Linux using Skia rendering.
/// </summary>
public partial class StepperHandler : ViewHandler<IStepper, SkiaStepper>
{
    public static IPropertyMapper<IStepper, StepperHandler> Mapper =
        new PropertyMapper<IStepper, StepperHandler>(ViewHandler.ViewMapper)
        {
            [nameof(IStepper.Value)] = MapValue,
            [nameof(IStepper.Minimum)] = MapMinimum,
            [nameof(IStepper.Maximum)] = MapMaximum,
            [nameof(IView.Background)] = MapBackground,
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
    }

    protected override void DisconnectHandler(SkiaStepper platformView)
    {
        platformView.ValueChanged -= OnValueChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || PlatformView is null) return;
        VirtualView.Value = PlatformView.Value;
    }

    public static void MapValue(StepperHandler handler, IStepper stepper)
    {
        if (handler.PlatformView is null) return;
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
            handler.PlatformView.BackgroundColor = solidPaint.Color.ToSKColor();
        }
    }
}
