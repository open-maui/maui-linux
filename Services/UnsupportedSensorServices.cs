// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Explicit "not supported" implementations of the motion/environment sensor
/// APIs that have no desktop hardware behind them. They replace the portable
/// reference-assembly stubs, which throw
/// <c>NotImplementedInReferenceAssemblyException</c> from every member, so
/// that apps get the documented behaviour: <c>IsSupported == false</c>,
/// <c>IsMonitoring == false</c>, <c>Stop()</c> is a no-op and <c>Start()</c>
/// throws <see cref="FeatureNotSupportedException"/>.
/// </summary>
internal static class UnsupportedSensor
{
    internal static FeatureNotSupportedException NotSupported(string sensor)
        => new($"{sensor} is not supported on Linux desktop; check IsSupported before calling Start.");
}

/// <summary>Accelerometer stub: no hardware on Linux desktop.</summary>
public sealed class UnsupportedAccelerometer : IAccelerometer
{
#pragma warning disable CS0067 // never raised
    public event EventHandler<AccelerometerChangedEventArgs>? ReadingChanged;
    public event EventHandler? ShakeDetected;
#pragma warning restore CS0067

    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public void Start(SensorSpeed sensorSpeed) => throw UnsupportedSensor.NotSupported("Accelerometer");
    public void Stop() { }
}

/// <summary>Barometer stub: no hardware on Linux desktop.</summary>
public sealed class UnsupportedBarometer : IBarometer
{
#pragma warning disable CS0067
    public event EventHandler<BarometerChangedEventArgs>? ReadingChanged;
#pragma warning restore CS0067

    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public void Start(SensorSpeed sensorSpeed) => throw UnsupportedSensor.NotSupported("Barometer");
    public void Stop() { }
}

/// <summary>Compass stub: no hardware on Linux desktop.</summary>
public sealed class UnsupportedCompass : ICompass
{
#pragma warning disable CS0067
    public event EventHandler<CompassChangedEventArgs>? ReadingChanged;
#pragma warning restore CS0067

    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public void Start(SensorSpeed sensorSpeed) => throw UnsupportedSensor.NotSupported("Compass");
    public void Start(SensorSpeed sensorSpeed, bool applyLowPassFilter) => throw UnsupportedSensor.NotSupported("Compass");
    public void Stop() { }
}

/// <summary>Gyroscope stub: no hardware on Linux desktop.</summary>
public sealed class UnsupportedGyroscope : IGyroscope
{
#pragma warning disable CS0067
    public event EventHandler<GyroscopeChangedEventArgs>? ReadingChanged;
#pragma warning restore CS0067

    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public void Start(SensorSpeed sensorSpeed) => throw UnsupportedSensor.NotSupported("Gyroscope");
    public void Stop() { }
}

/// <summary>Magnetometer stub: no hardware on Linux desktop.</summary>
public sealed class UnsupportedMagnetometer : IMagnetometer
{
#pragma warning disable CS0067
    public event EventHandler<MagnetometerChangedEventArgs>? ReadingChanged;
#pragma warning restore CS0067

    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public void Start(SensorSpeed sensorSpeed) => throw UnsupportedSensor.NotSupported("Magnetometer");
    public void Stop() { }
}

/// <summary>Orientation sensor stub: no hardware on Linux desktop.</summary>
public sealed class UnsupportedOrientationSensor : IOrientationSensor
{
#pragma warning disable CS0067
    public event EventHandler<OrientationSensorChangedEventArgs>? ReadingChanged;
#pragma warning restore CS0067

    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public void Start(SensorSpeed sensorSpeed) => throw UnsupportedSensor.NotSupported("OrientationSensor");
    public void Stop() { }
}
