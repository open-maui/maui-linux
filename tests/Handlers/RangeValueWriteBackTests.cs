// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A Slider's or Stepper's bound Value survives getting its handler. The Skia view clamps its
/// value to its own range while the handler maps the range one bound at a time; that clamp was
/// written back to the control and through a TwoWay binding to the source (a Slider with
/// Minimum 0.5 set its source to 0.5; a Stepper with Maximum 200 set a source of 150 to 100).
/// </summary>
[Collection("LinuxApplication.Current")]
public class RangeValueWriteBackTests
{
    private sealed class Model : INotifyPropertyChanged
    {
        private double _period;

        public double Period
        {
            get => _period;
            set
            {
                _period = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Period)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static Model Connect(View view, double value)
    {
        var model = new Model { Period = value };
        view.BindingContext = model;
        view.SetBinding(view is Slider ? Slider.ValueProperty : Stepper.ValueProperty, nameof(Model.Period));
        using var host = new HeadlessMauiHost(new ContentPage());
        Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(view, host.MauiContext);
        return model;
    }

    [Fact]
    public void Slider_with_minimum_above_zero_keeps_bound_value()
    {
        var slider = new Slider { Minimum = 0.5, Maximum = 30 };
        var model = Connect(slider, 4);

        model.Period.Should().Be(4);
        ((Microsoft.Maui.Platform.SkiaSlider)slider.Handler!.PlatformView!).Value.Should().Be(4);
    }

    [Fact]
    public void Stepper_with_minimum_above_zero_keeps_bound_value()
    {
        var stepper = new Stepper { Minimum = 2, Maximum = 10 };
        var model = Connect(stepper, 5);

        model.Period.Should().Be(5);
        ((Microsoft.Maui.Platform.SkiaStepper)stepper.Handler!.PlatformView!).Value.Should().Be(5);
    }

    [Fact]
    public void Stepper_with_value_above_default_maximum_keeps_bound_value()
    {
        var stepper = new Stepper { Minimum = 0, Maximum = 200 };
        var model = Connect(stepper, 150);

        model.Period.Should().Be(150);
        ((Microsoft.Maui.Platform.SkiaStepper)stepper.Handler!.PlatformView!).Value.Should().Be(150);
    }

    [Fact]
    public void Slider_drag_still_reaches_the_source()
    {
        var slider = new Slider { Minimum = 0.5, Maximum = 30 };
        var model = Connect(slider, 4);

        ((Microsoft.Maui.Platform.SkiaSlider)slider.Handler!.PlatformView!).Value = 12;

        model.Period.Should().Be(12);
    }
}
