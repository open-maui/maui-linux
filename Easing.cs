// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux;

public class Easing
{
    private readonly Func<double, double> _easingFunc;

    public static readonly Easing Linear = new(v => v);

    public static readonly Easing SinIn = new(v => 1.0 - Math.Cos(v * Math.PI / 2.0));

    public static readonly Easing SinOut = new(v => Math.Sin(v * Math.PI / 2.0));

    public static readonly Easing SinInOut = new(v => -(Math.Cos(Math.PI * v) - 1.0) / 2.0);

    public static readonly Easing CubicIn = new(v => v * v * v);

    public static readonly Easing CubicOut = new(v => 1.0 - Math.Pow(1.0 - v, 3.0));

    public static readonly Easing CubicInOut = new(v =>
        v < 0.5 ? 4.0 * v * v * v : 1.0 - Math.Pow(-2.0 * v + 2.0, 3.0) / 2.0);

    public static readonly Easing BounceIn = new(v => 1.0 - BounceOut.Ease(1.0 - v));

    public static readonly Easing BounceOut = new(v =>
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;

        if (v < 1 / d1)
            return n1 * v * v;
        if (v < 2 / d1)
            return n1 * (v -= 1.5 / d1) * v + 0.75;
        if (v < 2.5 / d1)
            return n1 * (v -= 2.25 / d1) * v + 0.9375;
        return n1 * (v -= 2.625 / d1) * v + 0.984375;
    });

    public static readonly Easing SpringIn = new(v => v * v * (2.70158 * v - 1.70158));

    public static readonly Easing SpringOut = new(v =>
        (v - 1.0) * (v - 1.0) * (2.70158 * (v - 1.0) + 1.70158) + 1.0);

    public Easing(Func<double, double> easingFunc)
    {
        _easingFunc = easingFunc;
    }

    public double Ease(double v) => _easingFunc(v);
}
