// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Devices.Sensors;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// MAUI's motion and environment sensors (Accelerometer, Barometer, Compass, Gyroscope,
/// Magnetometer, OrientationSensor) on Linux, over the kernel's IIO devices (<see cref="IioSensors"/>).
///
/// MAUI's own implementations stay in place: their shared code (Start/Stop validation,
/// IsMonitoring, the main-thread dispatch for the Default and UI speeds, shake detection) is what
/// every platform runs. Only the platform members the portable build leaves as throwing stubs are
/// replaced, as the Windows build fills them:
/// <list type="bullet">
/// <item>IsSupported: true exactly when the machine has the hardware (desktops without sensors:
/// false, so Start and Stop throw FeatureNotSupportedException, as on Windows).</item>
/// <item>PlatformStart: polls the device at the speed's interval (Fastest 5 ms, Game 20, UI 60,
/// Default 200) and raises a reading when it changes.</item>
/// <item>PlatformStop: stops polling.</item>
/// </list>
/// Units are MAUI's: acceleration in g with the device at rest face up reading Z = +1 (IIO and
/// Windows report gravity's direction, which MAUI's Windows build negates the same way); angular
/// velocity in rad/s (IIO's unit and MAUI's documented one); magnetic field in µT (IIO: gauss);
/// pressure in hPa (IIO: kPa); heading in degrees from magnetic north.
/// </summary>
internal static class SensorPatches
{
    private sealed class Poller
    {
        public Timer? Timer;
        public int Busy;
        public object? Last;
    }

    private static readonly ConditionalWeakTable<object, Poller> s_pollers = new();

    /// <summary>Polling interval of a sensor speed, MAUI's (SensorSpeedExtensions) values.</summary>
    internal static int IntervalOf(SensorSpeed speed) => speed switch
    {
        SensorSpeed.Fastest => 5,
        SensorSpeed.Game => 20,
        SensorSpeed.UI => 60,
        _ => 200,
    };

    internal static void Install(Harmony harmony)
    {
        var asm = typeof(IAccelerometer).Assembly;
        Patch(harmony, asm, "Microsoft.Maui.Devices.Sensors.AccelerometerImplementation", "IsSupported", nameof(Accelerometer_IsSupported), nameof(Accelerometer_Start));
        Patch(harmony, asm, "Microsoft.Maui.Devices.Sensors.BarometerImplementation", "IsSupported", nameof(Barometer_IsSupported), nameof(Barometer_Start));
        Patch(harmony, asm, "Microsoft.Maui.Devices.Sensors.CompassImplementation", "PlatformIsSupported", nameof(Compass_IsSupported), nameof(Compass_Start));
        Patch(harmony, asm, "Microsoft.Maui.Devices.Sensors.GyroscopeImplementation", "PlatformIsSupported", nameof(Gyroscope_IsSupported), nameof(Gyroscope_Start));
        Patch(harmony, asm, "Microsoft.Maui.Devices.Sensors.MagnetometerImplementation", "PlatformIsSupported", nameof(Magnetometer_IsSupported), nameof(Magnetometer_Start));
        Patch(harmony, asm, "Microsoft.Maui.Devices.Sensors.OrientationSensorImplementation", "PlatformIsSupported", nameof(Orientation_IsSupported), nameof(Orientation_Start));
    }

    private static void Patch(Harmony harmony, Assembly asm, string typeName, string supportedProperty, string supportedPrefix, string startPrefix)
    {
        var type = asm.GetType(typeName);
        if (type == null)
        {
            DiagnosticLog.Error("SensorPatches", $"{typeName} not found");
            return;
        }
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var getter = type.GetProperty(supportedProperty, Instance)?.GetMethod;
        var start = type.GetMethods(Instance).FirstOrDefault(m => m.Name == "PlatformStart");
        var stop = type.GetMethod("PlatformStop", Instance, null, Type.EmptyTypes, null);
        if (getter == null || start == null || stop == null)
        {
            DiagnosticLog.Error("SensorPatches", $"{type.Name}: platform members not found");
            return;
        }
        harmony.Patch(getter, new HarmonyMethod(Method(supportedPrefix)));
        harmony.Patch(start, new HarmonyMethod(Method(startPrefix)));
        harmony.Patch(stop, new HarmonyMethod(Method(nameof(Stop_Prefix))));
    }

    private static MethodInfo Method(string name) => typeof(SensorPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    // ---------------- IsSupported ----------------

    private static bool Accelerometer_IsSupported(ref bool __result) { __result = IioSensors.HasAccelerometer; return false; }
    private static bool Barometer_IsSupported(ref bool __result) { __result = IioSensors.HasBarometer; return false; }
    private static bool Compass_IsSupported(ref bool __result) { __result = IioSensors.HasCompass; return false; }
    private static bool Gyroscope_IsSupported(ref bool __result) { __result = IioSensors.HasGyroscope; return false; }
    private static bool Magnetometer_IsSupported(ref bool __result) { __result = IioSensors.HasMagnetometer; return false; }
    private static bool Orientation_IsSupported(ref bool __result) { __result = IioSensors.HasOrientation; return false; }

    // ---------------- Start ----------------

    private static bool Accelerometer_Start(object __instance, SensorSpeed sensorSpeed)
    {
        Start(__instance, sensorSpeed, typeof(AccelerometerData), ReadAccelerometer);
        return false;
    }

    private static bool Barometer_Start(object __instance, SensorSpeed sensorSpeed)
    {
        Start(__instance, sensorSpeed, typeof(BarometerData), ReadBarometer);
        return false;
    }

    // Windows ignores applyLowPassFilter ("only used on Android"); so does Linux.
    private static bool Compass_Start(object __instance, SensorSpeed sensorSpeed)
    {
        Start(__instance, sensorSpeed, typeof(CompassData), ReadCompass);
        return false;
    }

    private static bool Gyroscope_Start(object __instance, SensorSpeed sensorSpeed)
    {
        Start(__instance, sensorSpeed, typeof(GyroscopeData), ReadGyroscope);
        return false;
    }

    private static bool Magnetometer_Start(object __instance, SensorSpeed sensorSpeed)
    {
        Start(__instance, sensorSpeed, typeof(MagnetometerData), ReadMagnetometer);
        return false;
    }

    private static bool Orientation_Start(object __instance, SensorSpeed sensorSpeed)
    {
        Start(__instance, sensorSpeed, typeof(OrientationSensorData), ReadOrientation);
        return false;
    }

    private static bool Stop_Prefix(object __instance)
    {
        Stop(__instance);
        return false;
    }

    /// <summary>
    /// Starts polling for <paramref name="implementation"/>: every interval, <paramref name="read"/>
    /// takes a reading and, when it differs from the last one, the implementation's own
    /// RaiseReadingChanged/OnChanged raises it.
    /// </summary>
    internal static void Start(object implementation, SensorSpeed speed, Type dataType, Func<object?> read)
    {
        var raise = FindRaise(implementation.GetType(), dataType)
            ?? throw new InvalidOperationException($"{implementation.GetType().Name} has no reading dispatch for {dataType.Name}");
        Stop(implementation);
        var poller = s_pollers.GetValue(implementation, _ => new Poller());
        poller.Last = null;
        var interval = IntervalOf(speed);
        poller.Timer = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref poller.Busy, 1) == 1)
                return;
            try
            {
                var reading = read();
                if (reading != null && !reading.Equals(poller.Last))
                {
                    poller.Last = reading;
                    raise.Invoke(implementation, new[] { reading });
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Warn("SensorPatches", $"{dataType.Name} reading failed: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref poller.Busy, 0);
            }
        }, null, 0, interval);
    }

    internal static void Stop(object implementation)
    {
        if (s_pollers.TryGetValue(implementation, out var poller))
        {
            poller.Timer?.Dispose();
            poller.Timer = null;
        }
    }

    /// <summary>Whether a poller is running for <paramref name="implementation"/> (tests).</summary>
    internal static bool IsPolling(object implementation) => s_pollers.TryGetValue(implementation, out var poller) && poller.Timer != null;

    private static MethodInfo? FindRaise(Type type, Type dataType) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name is "RaiseReadingChanged" or "OnChanged"
                && m.GetParameters() is { Length: 1 } p && p[0].ParameterType == dataType);

    // ---------------- Readings (MAUI units) ----------------

    /// <summary>Acceleration in g, the reaction to gravity: at rest face up, Z = +1.</summary>
    internal static Vector3? ReadUp()
    {
        var device = IioSensors.FindDevice(IioSensors.AccelerometerChannels);
        var v = device == null ? null : IioSensors.ReadVector(device, "accel");
        return v.HasValue ? -v.Value / (float)IioSensors.StandardGravity : null;
    }

    internal static object? ReadAccelerometer()
    {
        var up = ReadUp();
        return up.HasValue ? new AccelerometerData(up.Value.X, up.Value.Y, up.Value.Z) : null;
    }

    internal static object? ReadBarometer()
    {
        var device = IioSensors.FindDevice(IioSensors.BarometerChannels);
        var kpa = device == null ? null : IioSensors.ReadChannel(device, "pressure");
        return kpa.HasValue ? new BarometerData(kpa.Value * 10) : null;
    }

    internal static object? ReadGyroscope()
    {
        var device = IioSensors.FindDevice(IioSensors.GyroscopeChannels);
        var v = device == null ? null : IioSensors.ReadVector(device, "anglvel");
        return v.HasValue ? new GyroscopeData(v.Value.X, v.Value.Y, v.Value.Z) : null;
    }

    /// <summary>Magnetic field in µT (IIO reports gauss).</summary>
    internal static Vector3? ReadMagneticField()
    {
        var device = IioSensors.FindDevice(IioSensors.MagnetometerChannels);
        var v = device == null ? null : IioSensors.ReadVector(device, "magn");
        return v.HasValue ? v.Value * 100 : null;
    }

    internal static object? ReadMagnetometer()
    {
        var v = ReadMagneticField();
        return v.HasValue ? new MagnetometerData(v.Value.X, v.Value.Y, v.Value.Z) : null;
    }

    internal static object? ReadCompass()
    {
        var headingDevice = IioSensors.FindDevice(IioSensors.HeadingChannels);
        if (headingDevice != null)
        {
            var heading = IioSensors.ReadChannel(headingDevice, "rot_from_north_magnetic_tilt_comp");
            if (heading.HasValue)
                return new CompassData(((heading.Value % 360) + 360) % 360);
        }
        var field = ReadMagneticField();
        if (!field.HasValue)
            return null;
        var derived = IioSensors.HeadingFromMagnetometer(field.Value, ReadUp());
        return derived.HasValue ? new CompassData(derived.Value) : null;
    }

    internal static object? ReadOrientation()
    {
        var device = IioSensors.FindDevice(IioSensors.QuaternionChannels);
        if (device != null)
        {
            var q = IioSensors.ReadQuaternion(device);
            if (q.HasValue)
                return new OrientationSensorData(q.Value.X, q.Value.Y, q.Value.Z, q.Value.W);
        }
        var field = ReadMagneticField();
        var up = ReadUp();
        if (!field.HasValue || !up.HasValue)
            return null;
        var derived = IioSensors.OrientationFromMagnetometer(field.Value, up.Value);
        return derived.HasValue ? new OrientationSensorData(derived.Value.X, derived.Value.Y, derived.Value.Z, derived.Value.W) : null;
    }
}
