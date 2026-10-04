// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Numerics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// The motion and environment sensors of a Linux machine, read from the kernel's Industrial I/O
/// (IIO) devices under <c>/sys/bus/iio/devices</c>: the HID sensor hub of convertible laptops and
/// tablets (accel_3d, gyro_3d, magn_3d, dev_rotation), and the I2C accelerometers, magnetometers
/// and pressure sensors of phones and SBCs.
///
/// iio-sensor-proxy (net.hadess.SensorProxy) is not used: it publishes only a coarse screen
/// orientation, a light level and a proximity flag, not the vectors MAUI reports. The sysfs
/// channels it reads itself are world-readable, so they are read here directly, by polling at the
/// sensor speed's interval (MAUI's own intervals: 5, 20, 60, 200 ms).
///
/// A channel's value is <c>in_X_input</c> when the driver gives it (already in IIO units), else
/// <c>(in_X_raw + offset) * scale</c>, with the channel's own offset/scale or the type's shared one.
/// A mount matrix (<c>in_TYPE_mount_matrix</c>, <c>in_mount_matrix</c>) turns the sensor's axes
/// into the device's.
/// </summary>
internal static class IioSensors
{
    /// <summary>Standard gravity, m/s².</summary>
    internal const double StandardGravity = 9.80665;

    /// <summary>The IIO device directory; replaceable by tests (a fake sysfs tree).</summary>
    internal static string Root { get; set; } = "/sys/bus/iio/devices";

    // The channel files whose presence means the hardware is there. They are the ones MAUI's
    // Linux conformance host (tests/Conformance/Essentials/Infrastructure/HardwareSupport.Linux.cs)
    // checks, so IsSupported is true exactly where the hardware is.
    internal static readonly string[] AccelerometerChannels = { "in_accel_x_raw", "in_accel_x_input" };
    internal static readonly string[] GyroscopeChannels = { "in_anglvel_x_raw", "in_anglvel_x_input" };
    internal static readonly string[] MagnetometerChannels = { "in_magn_x_raw", "in_magn_x_input" };
    internal static readonly string[] BarometerChannels = { "in_pressure_raw", "in_pressure_input" };
    internal static readonly string[] HeadingChannels = { "in_rot_from_north_magnetic_tilt_comp_raw", "in_rot_from_north_magnetic_tilt_comp_input" };
    internal static readonly string[] QuaternionChannels = { "in_rot_quaternion_raw", "in_rot_quaternion_input" };

    /// <summary>The first IIO device that has one of <paramref name="channels"/>, or null.</summary>
    internal static string? FindDevice(IReadOnlyList<string> channels)
    {
        string[] devices;
        try
        {
            if (!Directory.Exists(Root))
                return null;
            devices = Directory.GetFileSystemEntries(Root);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        Array.Sort(devices, StringComparer.Ordinal);
        foreach (var device in devices)
        {
            foreach (var channel in channels)
            {
                if (File.Exists(Path.Combine(device, channel)))
                    return device;
            }
        }
        return null;
    }

    internal static bool HasAccelerometer => FindDevice(AccelerometerChannels) != null;
    internal static bool HasGyroscope => FindDevice(GyroscopeChannels) != null;
    internal static bool HasMagnetometer => FindDevice(MagnetometerChannels) != null;
    internal static bool HasBarometer => FindDevice(BarometerChannels) != null;
    internal static bool HasHeading => FindDevice(HeadingChannels) != null;
    internal static bool HasQuaternion => FindDevice(QuaternionChannels) != null;

    /// <summary>A compass: a tilt-compensated heading channel, or a magnetometer to derive it from (as Android does).</summary>
    internal static bool HasCompass => HasHeading || HasMagnetometer;

    /// <summary>An orientation: a fused rotation quaternion (HID dev_rotation, what Windows reads), or an accelerometer and a magnetometer to derive it from.</summary>
    internal static bool HasOrientation => HasQuaternion || (HasAccelerometer && HasMagnetometer);

    /// <summary>
    /// The value of channel <paramref name="channel"/> ("accel_x", "pressure",
    /// "rot_from_north_magnetic_tilt_comp") of <paramref name="device"/> in IIO units, or null
    /// when it cannot be read (no such channel, or the driver is busy).
    /// </summary>
    internal static double? ReadChannel(string device, string channel)
    {
        var input = ReadNumber(Path.Combine(device, $"in_{channel}_input"));
        if (input.HasValue)
            return input;

        var raw = ReadNumber(Path.Combine(device, $"in_{channel}_raw"));
        if (!raw.HasValue)
            return null;

        var type = TypeOf(channel);
        var offset = ReadNumber(Path.Combine(device, $"in_{channel}_offset"))
            ?? ReadNumber(Path.Combine(device, $"in_{type}_offset")) ?? 0;
        var scale = ReadNumber(Path.Combine(device, $"in_{channel}_scale"))
            ?? ReadNumber(Path.Combine(device, $"in_{type}_scale")) ?? 1;
        return (raw.Value + offset) * scale;
    }

    /// <summary>An x/y/z channel triple ("accel", "anglvel", "magn") with the device's mount matrix applied.</summary>
    internal static Vector3? ReadVector(string device, string type)
    {
        var x = ReadChannel(device, type + "_x");
        var y = ReadChannel(device, type + "_y");
        var z = ReadChannel(device, type + "_z");
        if (!x.HasValue || !y.HasValue || !z.HasValue)
            return null;
        var v = new Vector3((float)x.Value, (float)y.Value, (float)z.Value);
        var matrix = ReadMountMatrix(device, type);
        return matrix.HasValue ? Apply(matrix.Value, v) : v;
    }

    /// <summary>
    /// The rotation quaternion of a dev_rotation device (in_rot_quaternion_raw is "x y z w"),
    /// scaled; null when it cannot be read.
    /// </summary>
    internal static Quaternion? ReadQuaternion(string device)
    {
        var input = ReadText(Path.Combine(device, "in_rot_quaternion_input"));
        var text = input ?? ReadText(Path.Combine(device, "in_rot_quaternion_raw"));
        if (text == null)
            return null;
        var parts = text.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return null;
        var values = new double[4];
        for (int i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return null;
        }
        var scale = input != null ? 1 : ReadNumber(Path.Combine(device, "in_rot_quaternion_scale")) ?? ReadNumber(Path.Combine(device, "in_rot_scale")) ?? 1;
        return new Quaternion((float)(values[0] * scale), (float)(values[1] * scale), (float)(values[2] * scale), (float)(values[3] * scale));
    }

    /// <summary>The mount matrix of <paramref name="type"/>'s channels (rows separated by ';'), or null for identity.</summary>
    internal static Matrix4x4? ReadMountMatrix(string device, string type)
    {
        var text = ReadText(Path.Combine(device, $"in_{type}_mount_matrix")) ?? ReadText(Path.Combine(device, "in_mount_matrix"));
        return text == null ? null : ParseMountMatrix(text);
    }

    internal static Matrix4x4? ParseMountMatrix(string text)
    {
        var rows = text.Split(';');
        if (rows.Length != 3)
            return null;
        var m = new float[3, 3];
        for (int r = 0; r < 3; r++)
        {
            var cells = rows[r].Split(',');
            if (cells.Length != 3)
                return null;
            for (int c = 0; c < 3; c++)
            {
                if (!float.TryParse(cells[c].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out m[r, c]))
                    return null;
            }
        }
        return new Matrix4x4(
            m[0, 0], m[0, 1], m[0, 2], 0,
            m[1, 0], m[1, 1], m[1, 2], 0,
            m[2, 0], m[2, 1], m[2, 2], 0,
            0, 0, 0, 1);
    }

    /// <summary>The matrix (row-major, as IIO writes it) times the column vector <paramref name="v"/>.</summary>
    internal static Vector3 Apply(Matrix4x4 m, Vector3 v) => new(
        m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
        m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
        m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);

    /// <summary>
    /// The heading from magnetic north (degrees, 0 to 360) of a device whose magnetometer reads
    /// <paramref name="magnetic"/>, tilt-compensated with <paramref name="up"/> (the reaction to
    /// gravity in the device's axes, the vector MAUI's accelerometer reports; null: lying flat).
    /// Android's SensorManager.getRotationMatrix and getOrientation.
    /// </summary>
    internal static double? HeadingFromMagnetometer(Vector3 magnetic, Vector3? up)
    {
        var rotation = RotationMatrix(magnetic, up ?? Vector3.UnitZ);
        if (rotation == null)
            return null;
        var (east, north, _) = rotation.Value;
        var azimuth = Math.Atan2(east.Y, north.Y) * 180 / Math.PI;
        return (azimuth + 360) % 360;
    }

    /// <summary>
    /// The device's rotation (device axes to East-North-Up) from its magnetometer and the
    /// reaction to gravity, as the quaternion Android's rotation vector gives; null when the
    /// vectors are degenerate (free fall, or a field parallel to gravity).
    /// </summary>
    internal static Quaternion? OrientationFromMagnetometer(Vector3 magnetic, Vector3 up)
    {
        var rotation = RotationMatrix(magnetic, up);
        if (rotation == null)
            return null;
        var (e, n, u) = rotation.Value;
        // Rows are the world axes in device coordinates: world = R * device.
        var m = new Matrix4x4(
            e.X, n.X, u.X, 0,
            e.Y, n.Y, u.Y, 0,
            e.Z, n.Z, u.Z, 0,
            0, 0, 0, 1);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }

    private static (Vector3 East, Vector3 North, Vector3 Up)? RotationMatrix(Vector3 magnetic, Vector3 up)
    {
        var east = Vector3.Cross(magnetic, up);
        var normE = east.Length();
        var normA = up.Length();
        // Degenerate: no gravity (free fall), no field, or a field parallel to gravity.
        if (normA < 1e-6f || normE <= 1e-3f * normA * magnetic.Length() || normE < 1e-12f)
            return null;
        east /= normE;
        var a = up / normA;
        var north = Vector3.Cross(a, east);
        return (east, north, a);
    }

    private static string TypeOf(string channel)
    {
        var underscore = channel.IndexOf('_');
        return underscore < 0 ? channel : channel[..underscore];
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            // EBUSY while a buffered capture owns the channel, or the device went away.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static double? ReadNumber(string path)
    {
        var text = ReadText(path);
        return text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
