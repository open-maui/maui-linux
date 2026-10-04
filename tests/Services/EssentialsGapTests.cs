// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Media;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Services.Camera;
using SkiaSharp;
using Tmds.DBus;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The Essentials parity gaps of docs/STUBS.md closed on Linux: motion and environment sensors
/// over IIO, the media picker's image options and camera capture, and contacts from Evolution
/// Data Server. Each is pinned against MAUI's Windows implementation.
/// </summary>
[Collection("GLibMainLoop")]
public class EssentialsGapTests : IDisposable
{
    private readonly List<string> _dirs = new();
    private readonly string _savedIioRoot = IioSensors.Root;
    private readonly string _savedV4l2Root = CameraDiscovery.Video4LinuxRoot;

    public EssentialsGapTests()
    {
        EssentialsPatches.Apply();
    }

    public void Dispose()
    {
        IioSensors.Root = _savedIioRoot;
        CameraDiscovery.Video4LinuxRoot = _savedV4l2Root;
        foreach (var dir in _dirs)
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private string TempDir(string tag)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"openmaui-gap-{tag}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    // ======================================================================
    // Sensors (IIO)
    // ======================================================================

    /// <summary>A fake /sys/bus/iio/devices with the given devices' attribute files.</summary>
    private string FakeIio(params (string Device, (string File, string Value)[] Files)[] devices)
    {
        var root = TempDir("iio");
        foreach (var (device, files) in devices)
        {
            var dir = Path.Combine(root, device);
            Directory.CreateDirectory(dir);
            foreach (var (file, value) in files)
                File.WriteAllText(Path.Combine(dir, file), value + "\n");
        }
        IioSensors.Root = root;
        return root;
    }

    /// <summary>
    /// Starts a sensor and returns its first reading. Gyroscope, Magnetometer and Compass raise
    /// on the main thread at every speed (MAUI's shared code never sets their speed), so this
    /// thread becomes the main thread and the GLib loop is pumped while waiting.
    /// </summary>
    private static T FirstReading<T>(Action<EventHandler<T>> subscribe, Action<EventHandler<T>> unsubscribe, Action start)
    {
        LinuxDispatcher.Initialize();
        T? reading = default;
        var got = false;
        EventHandler<T> handler = (_, e) => { if (!got) { reading = e; got = true; } };
        subscribe(handler);
        try
        {
            start();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!Volatile.Read(ref got) && DateTime.UtcNow < deadline)
            {
                GLibNative.ProcessPendingEvents();
                Thread.Sleep(5);
            }
            got.Should().BeTrue("a reading should arrive");
            return reading!;
        }
        finally
        {
            unsubscribe(handler);
        }
    }

    [Fact]
    public void Iio_channel_value_is_input_or_raw_plus_offset_times_scale()
    {
        var root = FakeIio(("iio:device0", new[]
        {
            ("in_accel_x_raw", "100"), ("in_accel_x_offset", "-50"), ("in_accel_scale", "0.5"),
            ("in_accel_y_raw", "10"), ("in_accel_y_scale", "2"),
            ("in_pressure_input", "101.325"),
        }));
        var device = Path.Combine(root, "iio:device0");

        IioSensors.ReadChannel(device, "accel_x").Should().Be(25);       // (100 - 50) * 0.5 (shared scale)
        IioSensors.ReadChannel(device, "accel_y").Should().Be(20);       // per-channel scale wins
        IioSensors.ReadChannel(device, "pressure").Should().Be(101.325); // _input is already scaled
        IioSensors.ReadChannel(device, "accel_z").Should().BeNull();
    }

    [Fact]
    public void Iio_mount_matrix_turns_sensor_axes_into_device_axes()
    {
        var root = FakeIio(("iio:device0", new[]
        {
            ("in_accel_x_raw", "1"), ("in_accel_y_raw", "2"), ("in_accel_z_raw", "3"),
            ("in_accel_mount_matrix", "0, 1, 0; -1, 0, 0; 0, 0, 1"),
        }));

        IioSensors.ReadVector(Path.Combine(root, "iio:device0"), "accel").Should().Be(new Vector3(2, -1, 3));
        IioSensors.ParseMountMatrix("1, 0").Should().BeNull();
    }

    [Fact]
    public void Sensors_are_supported_exactly_where_the_IIO_channels_are()
    {
        FakeIio(("iio:device0", new[] { ("in_illuminance_raw", "5") }));
        Accelerometer.Default.IsSupported.Should().BeFalse();
        Compass.Default.IsSupported.Should().BeFalse();

        FakeIio(
            ("iio:device0", new[] { ("in_accel_x_raw", "0"), ("in_accel_y_raw", "0"), ("in_accel_z_raw", "-1") }),
            ("iio:device1", new[] { ("in_magn_x_raw", "0"), ("in_magn_y_raw", "1"), ("in_magn_z_raw", "0") }));
        Accelerometer.Default.IsSupported.Should().BeTrue();
        Magnetometer.Default.IsSupported.Should().BeTrue();
        Compass.Default.IsSupported.Should().BeTrue("a magnetometer gives a heading, as on Android");
        OrientationSensor.Default.IsSupported.Should().BeTrue("an accelerometer and a magnetometer give an orientation");
        Gyroscope.Default.IsSupported.Should().BeFalse();
        Barometer.Default.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void Accelerometer_reads_g_with_the_device_face_up_reading_plus_one()
    {
        // IIO, like Windows, reports gravity's direction (face up: z = -9.81 m/s²); MAUI reports +1 g.
        FakeIio(("iio:device0", new[]
        {
            ("in_accel_x_raw", "100"), ("in_accel_y_raw", "0"), ("in_accel_z_raw", "-1000"), ("in_accel_scale", "0.00980665"),
        }));

        var reading = FirstReading<AccelerometerChangedEventArgs>(
            h => Accelerometer.Default.ReadingChanged += h, h => Accelerometer.Default.ReadingChanged -= h,
            () => Accelerometer.Default.Start(SensorSpeed.Fastest));
        try
        {
            Accelerometer.Default.IsMonitoring.Should().BeTrue();
            reading.Reading.Acceleration.X.Should().BeApproximately(-0.1f, 1e-4f);
            reading.Reading.Acceleration.Y.Should().BeApproximately(0f, 1e-4f);
            reading.Reading.Acceleration.Z.Should().BeApproximately(1f, 1e-4f);
            Accelerometer.Default.Invoking(a => a.Start(SensorSpeed.Fastest)).Should().Throw<InvalidOperationException>("MAUI's shared code rejects a second Start");
        }
        finally
        {
            Accelerometer.Default.Stop();
        }
        Accelerometer.Default.IsMonitoring.Should().BeFalse();
        SensorPatches.IsPolling(Accelerometer.Default).Should().BeFalse();
    }

    [Fact]
    public void Gyroscope_magnetometer_and_barometer_read_in_MAUI_units()
    {
        FakeIio(
            ("iio:device0", new[] { ("in_anglvel_x_raw", "10"), ("in_anglvel_y_raw", "-20"), ("in_anglvel_z_raw", "0"), ("in_anglvel_scale", "0.01") }),
            ("iio:device1", new[] { ("in_magn_x_input", "0.2"), ("in_magn_y_input", "-0.1"), ("in_magn_z_input", "0.5") }),
            ("iio:device2", new[] { ("in_pressure_raw", "101325"), ("in_pressure_scale", "0.001") }));

        var gyro = FirstReading<GyroscopeChangedEventArgs>(
            h => Gyroscope.Default.ReadingChanged += h, h => Gyroscope.Default.ReadingChanged -= h,
            () => Gyroscope.Default.Start(SensorSpeed.Fastest));
        Gyroscope.Default.Stop();
        gyro.Reading.AngularVelocity.Should().Be(new Vector3(0.1f, -0.2f, 0f)); // rad/s

        var magn = FirstReading<MagnetometerChangedEventArgs>(
            h => Magnetometer.Default.ReadingChanged += h, h => Magnetometer.Default.ReadingChanged -= h,
            () => Magnetometer.Default.Start(SensorSpeed.Fastest));
        Magnetometer.Default.Stop();
        magn.Reading.MagneticField.X.Should().BeApproximately(20f, 1e-3f); // gauss -> µT
        magn.Reading.MagneticField.Y.Should().BeApproximately(-10f, 1e-3f);
        magn.Reading.MagneticField.Z.Should().BeApproximately(50f, 1e-3f);

        var baro = FirstReading<BarometerChangedEventArgs>(
            h => Barometer.Default.ReadingChanged += h, h => Barometer.Default.ReadingChanged -= h,
            () => Barometer.Default.Start(SensorSpeed.Fastest));
        Barometer.Default.Stop();
        baro.Reading.PressureInHectopascals.Should().BeApproximately(1013.25, 1e-6); // kPa -> hPa
    }

    [Fact]
    public void Compass_uses_the_heading_channel_and_orientation_the_quaternion()
    {
        FakeIio(("iio:device0", new[]
        {
            ("in_rot_from_north_magnetic_tilt_comp_raw", "-4500"), ("in_rot_scale", "0.01"),
            ("in_rot_quaternion_raw", "0 0 7071 7071"), ("in_rot_quaternion_scale", "0.0001"),
        }));

        var heading = FirstReading<CompassChangedEventArgs>(
            h => Compass.Default.ReadingChanged += h, h => Compass.Default.ReadingChanged -= h,
            () => Compass.Default.Start(SensorSpeed.Fastest, applyLowPassFilter: true));
        Compass.Default.Stop();
        heading.Reading.HeadingMagneticNorth.Should().BeApproximately(315, 1e-9);

        var orientation = FirstReading<OrientationSensorChangedEventArgs>(
            h => OrientationSensor.Default.ReadingChanged += h, h => OrientationSensor.Default.ReadingChanged -= h,
            () => OrientationSensor.Default.Start(SensorSpeed.Fastest));
        OrientationSensor.Default.Stop();
        orientation.Reading.Orientation.Z.Should().BeApproximately(0.7071f, 1e-4f);
        orientation.Reading.Orientation.W.Should().BeApproximately(0.7071f, 1e-4f);
    }

    [Fact]
    public void Heading_and_orientation_are_derived_from_the_magnetometer()
    {
        // Flat, face up, top edge to magnetic north (field north and down): heading 0.
        IioSensors.HeadingFromMagnetometer(new Vector3(0, 20, -40), null)!.Value.Should().BeApproximately(0, 1e-6);
        // Top edge to the east: north is the device's -X.
        IioSensors.HeadingFromMagnetometer(new Vector3(-20, 0, -40), Vector3.UnitZ)!.Value.Should().BeApproximately(90, 1e-4);
        // Top edge to the west.
        IioSensors.HeadingFromMagnetometer(new Vector3(20, 0, -40), Vector3.UnitZ)!.Value.Should().BeApproximately(270, 1e-4);
        // A field parallel to gravity has no heading.
        IioSensors.HeadingFromMagnetometer(new Vector3(0, 0, -40), Vector3.UnitZ).Should().BeNull();

        // Device axes on East-North-Up: the identity rotation.
        var q = IioSensors.OrientationFromMagnetometer(new Vector3(0, 20, -40), Vector3.UnitZ)!.Value;
        Math.Abs(q.W).Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Sensor_speeds_poll_at_MAUI_intervals()
    {
        SensorPatches.IntervalOf(SensorSpeed.Fastest).Should().Be(5);
        SensorPatches.IntervalOf(SensorSpeed.Game).Should().Be(20);
        SensorPatches.IntervalOf(SensorSpeed.UI).Should().Be(60);
        SensorPatches.IntervalOf(SensorSpeed.Default).Should().Be(200);
    }

    [Fact]
    public void UseLinux_registers_the_sensor_facades()
    {
        var builder = MauiApp.CreateBuilder();
        LinuxPlatformRegistrar.Register(builder);
        using var services = builder.Services.BuildServiceProvider();
        services.GetRequiredService<IAccelerometer>().Should().BeSameAs(Accelerometer.Default);
        services.GetRequiredService<ICompass>().Should().BeSameAs(Compass.Default);
        services.GetRequiredService<IOrientationSensor>().Should().BeSameAs(OrientationSensor.Default);
    }

    // ======================================================================
    // Media picker: image processing
    // ======================================================================

    /// <summary>A two-colour image: left half red, right half blue.</summary>
    private string TwoColourImage(int width, int height, SKEncodedImageFormat format, string extension)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, width / 2f, height, red);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        var path = Path.Combine(TempDir("img"), "photo" + extension);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>Adds an EXIF APP1 segment with <paramref name="orientation"/> right after the JPEG's SOI.</summary>
    private static void AddExifOrientation(string jpeg, ushort orientation)
    {
        var bytes = File.ReadAllBytes(jpeg);
        byte[] tiff =
        {
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // little endian, IFD at 8
            0x01, 0x00,                                                // one entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,            // 0x0112 Orientation, SHORT, 1
            (byte)orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,                                    // no next IFD
        };
        var payload = new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 }.Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload).ToArray();
        File.WriteAllBytes(jpeg, bytes.Take(2).Concat(segment).Concat(bytes.Skip(2)).ToArray());
    }

    private static SKBitmap DecodeFile(string path) => SKBitmap.Decode(path);

    private static bool IsRed(SKColor c) => c.Red > 180 && c.Blue < 80;
    private static bool IsBlue(SKColor c) => c.Blue > 180 && c.Red < 80;

    [Fact]
    public void Image_processing_rules_match_the_Windows_ImageProcessor()
    {
        MediaImageProcessor.IsProcessingNeeded(null, null, 100).Should().BeFalse();
        MediaImageProcessor.IsProcessingNeeded(800, null, 100).Should().BeTrue();
        MediaImageProcessor.IsProcessingNeeded(null, null, 99).Should().BeTrue();

        MediaImageProcessor.CalculateResizedDimensions(4000, 3000, 1000, null).Should().Be((1000f, 750f));
        MediaImageProcessor.CalculateResizedDimensions(4000, 3000, 1000, 500).Should().Be((4000f * 500 / 3000, 500f));
        MediaImageProcessor.CalculateResizedDimensions(400, 300, 1000, 1000).Should().Be((400f, 300f), "never scaled up");

        MediaImageProcessor.ShouldUsePngFormat("a.jpg", 95).Should().BeTrue();
        MediaImageProcessor.ShouldUsePngFormat("a.png", 90).Should().BeTrue();
        MediaImageProcessor.ShouldUsePngFormat("a.jpg", 90).Should().BeFalse();
        MediaImageProcessor.ShouldUsePngFormat("a.png", 80).Should().BeFalse();

        MediaImageProcessor.DetectImageFormat(new byte[] { 0x89, 0x50, 0x4E, 0x47 }).Should().Be(".png");
        MediaImageProcessor.DetectImageFormat(new byte[] { 0xFF, 0xD8, 0xFF }).Should().Be(".jpg");
        MediaImageProcessor.DetectImageFormat(new byte[] { 0x47, 0x49, 0x46 }).Should().BeNull();
    }

    [Fact]
    public async Task Picked_photo_is_resized_and_compressed_as_the_options_ask()
    {
        var original = TwoColourImage(400, 200, SKEncodedImageFormat.Png, ".png");
        var options = new MediaPickerOptions { MaximumWidth = 100, CompressionQuality = 60 };

        var result = await MediaPickerService.ResultForAsync(original, options, photo: true);

        result.FullPath.Should().NotBe(original);
        result.FileName.Should().EndWith(".jpg", "quality below 90 gives JPEG");
        result.ContentType.Should().Be("image/jpeg");
        using (var decoded = DecodeFile(result.FullPath))
        {
            decoded.Width.Should().Be(100);
            decoded.Height.Should().Be(50);
        }
        await using (var stream = await result.OpenReadAsync())
            stream.Length.Should().BeGreaterThan(0);
        File.Delete(result.FullPath);

        // High quality keeps PNG; no option at all keeps the original file.
        var png = await MediaPickerService.ResultForAsync(original, new MediaPickerOptions { MaximumHeight = 50, CompressionQuality = 95 }, photo: true);
        png.FileName.Should().EndWith(".png");
        File.Delete(png.FullPath);
        (await MediaPickerService.ResultForAsync(original, new MediaPickerOptions(), photo: true)).FullPath.Should().Be(original);
        // Videos are never processed.
        (await MediaPickerService.ResultForAsync(original, options, photo: false)).FullPath.Should().Be(original);
    }

    [Fact]
    public async Task RotateImage_applies_the_EXIF_orientation()
    {
        var jpeg = TwoColourImage(40, 20, SKEncodedImageFormat.Jpeg, ".jpg");
        AddExifOrientation(jpeg, 6); // stored on its side: turn 90 degrees clockwise to view

        var rotated = await MediaPickerService.ResultForAsync(jpeg, new MediaPickerOptions { RotateImage = true }, photo: true);

        rotated.FullPath.Should().NotBe(jpeg);
        using (var decoded = DecodeFile(rotated.FullPath))
        {
            decoded.Width.Should().Be(20);
            decoded.Height.Should().Be(40);
            IsRed(decoded.GetPixel(10, 5)).Should().BeTrue("the stored left half is now the top");
            IsBlue(decoded.GetPixel(10, 35)).Should().BeTrue();
        }
        File.Delete(rotated.FullPath);

        // Without RotateImage the pixels stay as stored.
        (await MediaPickerService.ResultForAsync(jpeg, new MediaPickerOptions(), photo: true)).FullPath.Should().Be(jpeg);

        // Rotation as part of resizing: one output, rotated and resized.
        var both = await MediaPickerService.ResultForAsync(jpeg, new MediaPickerOptions { RotateImage = true, MaximumHeight = 20 }, photo: true);
        using (var decoded = DecodeFile(both.FullPath))
        {
            decoded.Width.Should().Be(10);
            decoded.Height.Should().Be(20);
        }
        File.Delete(both.FullPath);
    }

    [Fact]
    public void Every_EXIF_orientation_turns_the_image_upright()
    {
        // A 3x2 image whose stored top-left pixel is red; upright, the red pixel is where the
        // orientation's definition puts it.
        using var stored = new SKBitmap(3, 2);
        stored.Erase(SKColors.Blue);
        stored.SetPixel(0, 0, SKColors.Red);
        var expected = new Dictionary<SKEncodedOrigin, (int W, int H, int X, int Y)>
        {
            [SKEncodedOrigin.TopLeft] = (3, 2, 0, 0),
            [SKEncodedOrigin.TopRight] = (3, 2, 2, 0),
            [SKEncodedOrigin.BottomRight] = (3, 2, 2, 1),
            [SKEncodedOrigin.BottomLeft] = (3, 2, 0, 1),
            [SKEncodedOrigin.LeftTop] = (2, 3, 0, 0),
            [SKEncodedOrigin.RightTop] = (2, 3, 1, 0),
            [SKEncodedOrigin.RightBottom] = (2, 3, 1, 2),
            [SKEncodedOrigin.LeftBottom] = (2, 3, 0, 2),
        };
        foreach (var (origin, (w, h, x, y)) in expected)
        {
            using var upright = MediaImageProcessor.ApplyOrigin(stored, origin);
            upright.Width.Should().Be(w, origin.ToString());
            upright.Height.Should().Be(h, origin.ToString());
            IsRed(upright.GetPixel(x, y)).Should().BeTrue($"{origin}: red at ({x},{y})");
        }
    }

    // ======================================================================
    // Media picker: picking and capture
    // ======================================================================

    private sealed class FakeCamera : ICameraCapture
    {
        public bool IsSupported { get; set; } = true;
        public bool IsVideoSupported { get; set; } = true;
        public string? Path { get; set; }
        public List<(bool Photo, string? Title)> Calls { get; } = new();

        public Task<string?> CaptureAsync(bool photo, string? title)
        {
            Calls.Add((photo, title));
            return Task.FromResult(Path);
        }
    }

    [Fact]
    public async Task PickPhotos_is_a_multiple_selection_unless_the_limit_is_one()
    {
        var a = TwoColourImage(10, 10, SKEncodedImageFormat.Png, ".png");
        var b = TwoColourImage(10, 10, SKEncodedImageFormat.Png, ".png");
        var calls = new List<(bool Photo, bool Multiple)>();
        var picker = new MediaPickerService(new FakeCamera(), (_, photo, multiple) =>
        {
            calls.Add((photo, multiple));
            return Task.FromResult<IReadOnlyList<string>>(multiple ? new[] { a, b } : new[] { a });
        });

        (await picker.PickPhotosAsync()).Select(f => f.FullPath).Should().Equal(a, b);
        (await picker.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 5 })).Should().HaveCount(2, "Windows does not enforce a larger limit");
        (await picker.PickPhotosAsync(new MediaPickerOptions())).Should().ContainSingle("the default limit is 1: the single picker");
        (await picker.PickVideosAsync(new MediaPickerOptions { SelectionLimit = 0 })).Should().HaveCount(2);
        (await picker.PickPhotoAsync())!.FullPath.Should().Be(a);
        calls.Should().Equal((true, true), (true, true), (true, false), (false, true), (true, false));
    }

    [Fact]
    public async Task Capture_returns_the_camera_file_processed_or_null_when_cancelled()
    {
        var shot = TwoColourImage(200, 100, SKEncodedImageFormat.Jpeg, ".jpg");
        var camera = new FakeCamera { Path = shot };
        var picker = new MediaPickerService(camera, (_, _, _) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()));

        picker.IsCaptureSupported.Should().BeTrue();
        (await picker.CapturePhotoAsync(new MediaPickerOptions { Title = "Badge" }))!.FullPath.Should().Be(shot);
        var small = await picker.CapturePhotoAsync(new MediaPickerOptions { MaximumWidth = 50 });
        using (var decoded = DecodeFile(small!.FullPath))
            decoded.Width.Should().Be(50);
        camera.Calls.Should().Equal((true, "Badge"), (true, null));

        camera.Path = null;
        (await picker.CaptureVideoAsync()).Should().BeNull("the user cancelled");
        camera.Calls.Last().Photo.Should().BeFalse();

        camera.IsVideoSupported = false;
        await picker.Invoking(p => p.CaptureVideoAsync()).Should().ThrowAsync<FeatureNotSupportedException>("no video encoder");
        (await picker.CapturePhotoAsync()).Should().BeNull("photos need no encoder");
    }

    [Fact]
    public void Zenity_picker_allows_several_files_and_parses_them()
    {
        var psi = MediaPickerService.BuildPickerStartInfo(null, MediaPickerService.ImageExtensions, "Images", "Select photos", multiple: true);
        psi.ArgumentList.Should().Contain("--multiple").And.Contain("--separator=|");

        var a = TwoColourImage(4, 4, SKEncodedImageFormat.Png, ".png");
        var b = TwoColourImage(4, 4, SKEncodedImageFormat.Png, ".png");
        MediaPickerService.ParseOutput($"{a}|{b}|/no/such/file.png").Should().Equal(a, b);
        MediaPickerService.ParseOutput("").Should().BeEmpty();
    }

    // ======================================================================
    // Camera capture
    // ======================================================================

    [Fact]
    public void Camera_pipelines_decode_the_webcam_and_record_with_sound()
    {
        var v4l2 = CameraSource.ForDevice("/dev/video2");
        CameraPipelines.Preview(v4l2).Should().StartWith("v4l2src device=\"/dev/video2\" ! decodebin ! videoconvert ! ")
            .And.Contain("appsink name=" + CameraPipelines.PreviewSinkName);
        CameraPipelines.Source(CameraSource.ForPipeWire(42)).Should().Be("pipewiresrc fd=42");

        var profile = new CameraVideoProfile(".mp4", "mp4mux", "openh264enc ! h264parse", "fdkaacenc ! aacparse");
        var recording = CameraPipelines.Recording(v4l2, profile, "/tmp/a \"b\".mp4", withAudio: true);
        recording.Should().Contain("tee name=t").And.Contain("openh264enc ! h264parse ! queue ! mp4mux name=mux ! filesink location=\"/tmp/a \\\"b\\\".mp4\"")
            .And.Contain("autoaudiosrc ! queue ! audioconvert ! audioresample ! fdkaacenc ! aacparse ! queue ! mux.");
        CameraPipelines.Recording(v4l2, profile, "/tmp/a.mp4", withAudio: false).Should().NotContain("autoaudiosrc");
    }

    [Fact]
    public void Video_profile_prefers_mp4_with_aac_then_webm()
    {
        static Func<string, bool> Has(params string[] elements) => e => elements.Contains(e);

        CameraPipelines.SelectProfile(Has("openh264enc", "h264parse", "mp4mux", "fdkaacenc", "aacparse", "autoaudiosrc", "vp8enc", "webmmux", "opusenc"))!
            .Should().Be(new CameraVideoProfile(".mp4", "mp4mux", "openh264enc ! h264parse", "fdkaacenc ! aacparse"));
        CameraPipelines.SelectProfile(Has("x264enc", "h264parse", "mp4mux", "vp8enc", "webmmux", "vorbisenc", "autoaudiosrc"))!
            .Should().Be(new CameraVideoProfile(".webm", "webmmux", "vp8enc deadline=1 cpu-used=8", "vorbisenc"), "no AAC encoder: WebM keeps the sound");
        CameraPipelines.SelectProfile(Has("x264enc", "h264parse", "mp4mux"))!.AudioEncoder.Should().BeNull();
        CameraPipelines.SelectProfile(Has("x264enc", "h264parse", "mp4mux"))!.VideoEncoder.Should().StartWith("x264enc tune=zerolatency");
        CameraPipelines.SelectProfile(Has("avenc_aac", "aacparse", "vp8enc", "webmmux"))!.Extension.Should().Be(".webm");
        CameraPipelines.SelectProfile(Has("mp4mux", "webmmux")).Should().BeNull();
    }

    [Fact]
    public void Camera_discovery_lists_capture_nodes_not_metadata_nodes()
    {
        var root = TempDir("v4l2");
        foreach (var (name, index) in new[] { ("video0", "0"), ("video1", "1"), ("video2", "0"), ("video10", "0") })
        {
            Directory.CreateDirectory(Path.Combine(root, name));
            File.WriteAllText(Path.Combine(root, name, "index"), index + "\n");
        }
        CameraDiscovery.Video4LinuxRoot = root;
        CameraDiscovery.CaptureDevices().Should().Equal("/dev/video0", "/dev/video2", "/dev/video10");

        CameraDiscovery.Video4LinuxRoot = Path.Combine(root, "missing");
        CameraDiscovery.CaptureDevices().Should().BeEmpty();
    }

    private sealed class FakeSession : ICameraSession
    {
        public FakeSession(string description) => Description = description;
        public string Description { get; }
        public bool? Finished { get; private set; }
        public event Action? FrameArrived;
        public event Action<string>? Failed;
        public long FrameCount { get; private set; }
        public SKBitmap? Frame { get; private set; }

        public void Emit(SKColor color)
        {
            var frame = new SKBitmap(64, 48);
            frame.Erase(color);
            Frame?.Dispose();
            Frame = frame;
            FrameCount++;
            FrameArrived?.Invoke();
        }

        public void Fail(string message) => Failed?.Invoke(message);

        public SKBitmap? SnapshotLatest() => Frame?.Copy();

        public Task<bool> StopAsync(bool finish, TimeSpan timeout)
        {
            Finished = finish;
            if (finish)
            {
                // A finished recording: the muxer wrote the file.
                var start = Description.IndexOf("location=\"", StringComparison.Ordinal) + "location=\"".Length;
                var path = Description[start..Description.IndexOf('"', start)];
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            }
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }
    }

    private (CameraCaptureController Controller, List<FakeSession> Sessions, string Cache) Controller(bool photo, Func<string, bool>? fails = null)
    {
        var sessions = new List<FakeSession>();
        var cache = TempDir("cache");
        var profile = new CameraVideoProfile(".mp4", "mp4mux", "openh264enc ! h264parse", "fdkaacenc ! aacparse");
        var controller = new CameraCaptureController(CameraSource.ForDevice("/dev/video0"), photo, cache, photo ? null : profile, description =>
        {
            if (fails?.Invoke(description) == true)
                throw new InvalidOperationException("Could not open audio device");
            var session = new FakeSession(description);
            sessions.Add(session);
            return session;
        });
        return (controller, sessions, cache);
    }

    [Fact]
    public async Task Photo_capture_takes_reviews_and_saves_a_jpeg_like_Windows()
    {
        var (controller, sessions, cache) = Controller(photo: true);
        controller.Start();
        controller.State.Should().Be(CameraCaptureState.Starting);
        sessions.Should().ContainSingle().Which.Description.Should().Be(CameraPipelines.Preview(CameraSource.ForDevice("/dev/video0")));

        sessions[0].Emit(SKColors.Red);
        controller.State.Should().Be(CameraCaptureState.Live);
        controller.CurrentImage()!.GetPixel(1, 1).Should().Be(SKColors.Red);

        controller.TakePhoto();
        controller.State.Should().Be(CameraCaptureState.Review);
        sessions[0].Emit(SKColors.Green);
        controller.CurrentImage()!.GetPixel(1, 1).Should().Be(SKColors.Red, "the review shows the photo taken, not the live frame");

        controller.Retake();
        controller.State.Should().Be(CameraCaptureState.Live);
        controller.CurrentImage()!.GetPixel(1, 1).Should().Be(SKColors.Green);

        controller.TakePhoto();
        await controller.AcceptAsync();
        var path = await controller.Result;
        path.Should().Be(Path.Combine(cache, ".Microsoft.Maui.Media.MediaPicker", "capture.jpg"));
        using (var photo = SKBitmap.Decode(path))
        {
            photo.Width.Should().Be(64);
            photo.Height.Should().Be(48);
        }
        sessions[0].Finished.Should().BeFalse("the preview is closed, not finished");
        controller.State.Should().Be(CameraCaptureState.Done);

        // The next capture gets a new name, as Windows' GenerateUniqueName does.
        controller.NewOutputPath(".jpg").Should().EndWith("capture (2).jpg");
        controller.Dispose();
    }

    [Fact]
    public async Task Cancelled_capture_completes_with_null()
    {
        var (controller, sessions, _) = Controller(photo: true);
        controller.Start();
        sessions[0].Emit(SKColors.Red);
        await controller.CancelAsync();
        (await controller.Result).Should().BeNull();
        sessions[0].Finished.Should().BeFalse();
        controller.Dispose();
    }

    [Fact]
    public async Task Video_capture_records_and_falls_back_to_no_sound()
    {
        var (controller, sessions, cache) = Controller(photo: false, fails: d => d.Contains("autoaudiosrc"));
        controller.Start();
        sessions[0].Emit(SKColors.Red);
        await controller.StartRecordingAsync();

        controller.State.Should().Be(CameraCaptureState.Recording);
        sessions.Should().HaveCount(2);
        sessions[1].Description.Should().Contain("mp4mux").And.NotContain("autoaudiosrc", "the microphone could not be opened");
        sessions[0].Finished.Should().BeFalse();

        await controller.StopRecordingAsync();
        var path = await controller.Result;
        path.Should().Be(Path.Combine(cache, ".Microsoft.Maui.Media.MediaPicker", "capture.mp4"));
        new FileInfo(path!).Length.Should().Be(3);
        sessions[1].Finished.Should().BeTrue("end-of-stream completes the file");
        controller.Dispose();
    }

    [Fact]
    public async Task Cancelled_recording_deletes_the_partial_file()
    {
        var (controller, sessions, cache) = Controller(photo: false);
        controller.Start();
        sessions[0].Emit(SKColors.Red);
        await controller.StartRecordingAsync();
        sessions[1].Description.Should().Contain("autoaudiosrc");
        await controller.CancelAsync();
        (await controller.Result).Should().BeNull();
        File.Exists(Path.Combine(cache, ".Microsoft.Maui.Media.MediaPicker", "capture.mp4")).Should().BeFalse();
        controller.Dispose();
    }

    [Fact]
    public async Task Camera_failure_is_shown_and_closing_cancels()
    {
        var (controller, sessions, _) = Controller(photo: true);
        var dialog = new SkiaCameraCaptureDialog(controller, null);
        controller.Start();
        dialog.Buttons.Should().Be(((string?)null, "Cancel"));
        sessions[0].Fail("Device or resource busy");
        controller.State.Should().Be(CameraCaptureState.Failed);
        controller.ErrorMessage.Should().Contain("busy");
        dialog.Buttons.Should().Be(((string?)null, "Close"));
        dialog.PressSecondary();
        (await controller.Result).Should().BeNull();
        controller.Dispose();
    }

    [Fact]
    public async Task Capture_dialog_buttons_follow_the_flow()
    {
        var (controller, sessions, _) = Controller(photo: true);
        var dialog = new SkiaCameraCaptureDialog(controller, "Profile picture");
        controller.Start();
        sessions[0].Emit(SKColors.Red);
        dialog.Buttons.Should().Be(("Take photo", "Cancel"));
        dialog.PressPrimary();
        dialog.Buttons.Should().Be(("Use photo", "Retake"));
        dialog.PressSecondary();
        dialog.Buttons.Should().Be(("Take photo", "Cancel"));

        // Escape cancels from any state.
        dialog.PressPrimary();
        dialog.OnKeyDown(new KeyEventArgs(Key.Escape));
        (await controller.Result).Should().BeNull();
        controller.Dispose();

        var (video, videoSessions, _) = Controller(photo: false);
        var videoDialog = new SkiaCameraCaptureDialog(video, null);
        video.Start();
        videoSessions[0].Emit(SKColors.Red);
        videoDialog.Buttons.Should().Be(("Record", "Cancel"));
        await video.StartRecordingAsync();
        videoDialog.Buttons.Should().Be(("Stop", (string?)null));
        videoDialog.PressPrimary();
        (await video.Result).Should().NotBeNull();
        video.Dispose();
    }

    // ======================================================================
    // Contacts (Evolution Data Server)
    // ======================================================================

    private const string AdaCard =
        "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Dr. Ada M. Lovelace\r\nN:Lovelace;Ada;Marie;Dr.;Jr.\r\n" +
        "TEL;X-EVOLUTION-E164=5550100,\"+1\";TYPE=CELL:+1 555 0100\r\nTEL;TYPE=\"home:x\":tel:+44 20 7946 0000\r\n" +
        "item1.EMAIL;TYPE=WORK:ada@example.org\r\nEMAIL:ada\\,home@example.org\r\n" +
        "NOTE:a long note that\r\n  continues\r\nUID:pas-id-9b7b\r\n END\r\nEND:VCARD";

    [Fact]
    public void VCard_converts_as_Windows_ConvertContact_does()
    {
        var contact = VCard.ToContact(AdaCard)!;
        contact.Id.Should().Be("pas-id-9b7bEND", "a folded line continues the previous one (its first space is the fold)");
        contact.DisplayName.Should().Be("Dr. Ada M. Lovelace");
        contact.NamePrefix.Should().Be("Dr.");
        contact.GivenName.Should().Be("Ada");
        contact.MiddleName.Should().Be("Marie");
        contact.FamilyName.Should().Be("Lovelace");
        contact.NameSuffix.Should().Be("Jr.");
        contact.Phones.Select(p => p.PhoneNumber).Should().Equal("+1 555 0100", "+44 20 7946 0000");
        contact.Emails.Select(e => e.EmailAddress).Should().Equal("ada@example.org", "ada,home@example.org");

        var noFn = VCard.ToContact("BEGIN:VCARD\nN:Hopper;Grace;;;\nEND:VCARD")!;
        noFn.DisplayName.Should().Be("Grace Hopper");
        noFn.Phones.Should().BeEmpty();
        VCard.ToContact("not a card").Should().BeNull();
        VCard.SplitComponents("a\\;b;c").Should().Equal("a\\;b", "c");
    }

    [Fact]
    public void Enabled_address_books_are_read_from_the_source_registry()
    {
        IDictionary<string, object> Source(string uid, string data) => new Dictionary<string, object> { ["UID"] = uid, ["Data"] = data };
        var objects = new Dictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>
        {
            [new ObjectPath("/org/gnome/evolution/dataserver/SourceManager/Source_2")] = new Dictionary<string, IDictionary<string, object>>
            {
                [EdsContactSource.SourceInterface] = Source("work-book", "[Data Source]\nDisplayName=Work\nEnabled=true\n\n[Address Book]\nBackendName=carddav\n"),
            },
            [new ObjectPath("/org/gnome/evolution/dataserver/SourceManager/Source_1")] = new Dictionary<string, IDictionary<string, object>>
            {
                [EdsContactSource.SourceInterface] = Source("system-address-book", "[Data Source]\nDisplayName=Personal\n\n[Address Book]\nBackendName=local\n"),
            },
            [new ObjectPath("/org/gnome/evolution/dataserver/SourceManager/Source_3")] = new Dictionary<string, IDictionary<string, object>>
            {
                [EdsContactSource.SourceInterface] = Source("off", "[Data Source]\nEnabled=false\n[Address Book]\n"),
            },
            [new ObjectPath("/org/gnome/evolution/dataserver/SourceManager/Source_4")] = new Dictionary<string, IDictionary<string, object>>
            {
                [EdsContactSource.SourceInterface] = Source("calendar", "[Data Source]\nEnabled=true\n[Calendar]\n"),
            },
        };

        EdsContactSource.AddressBookUids(objects).Should().Equal("system-address-book", "work-book");
    }

    [Fact]
    public void Missing_or_refused_contacts_service_throws_as_MAUI_does()
    {
        EdsContactSource.Translate(new DBusException("org.freedesktop.DBus.Error.ServiceUnknown", "The name is not activatable"))
            .Should().BeOfType<FeatureNotSupportedException>();
        EdsContactSource.Translate(new DBusException("org.freedesktop.DBus.Error.AccessDenied", "sandboxed"))
            .Should().BeOfType<PermissionException>();
    }

    private sealed class CardSource(params string[] cards) : IContactSource
    {
        public Task<IReadOnlyList<string>> GetVCardsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>(cards);
    }

    [Fact]
    public async Task Contacts_lists_and_picks_from_the_address_books()
    {
        IReadOnlyList<Contact>? offered = null;
        var service = new ContactsService(
            new CardSource(AdaCard, "BEGIN:VCARD\nFN:Grace Hopper\nUID:g\nEMAIL:grace@example.org\nEND:VCARD"),
            list => { offered = list; return Task.FromResult<Contact?>(list[1]); });

        (await service.GetAllAsync()).Select(c => c.Id).Should().Equal("pas-id-9b7bEND", "g");
        var picked = await service.PickContactAsync();
        picked!.DisplayName.Should().Be("Grace Hopper");
        offered.Should().HaveCount(2);
    }

    [Fact]
    public void Chooser_labels_tell_namesakes_apart()
    {
        var contacts = new[]
        {
            new Contact("1", null, "Sam", null, "Lee", null, Array.Empty<ContactPhone>(), new[] { new ContactEmail("sam@a.org") }, "Sam Lee"),
            new Contact("2", null, "Sam", null, "Lee", null, Array.Empty<ContactPhone>(), new[] { new ContactEmail("sam@b.org") }, "Sam Lee"),
            new Contact("3", null, null, null, null, null, new[] { new ContactPhone("555") }, Array.Empty<ContactEmail>(), null),
            new Contact("4", null, "Kim", null, null, null, Array.Empty<ContactPhone>(), Array.Empty<ContactEmail>(), null),
            new Contact("5", null, "Kim", null, null, null, Array.Empty<ContactPhone>(), Array.Empty<ContactEmail>(), null),
        };
        ContactsService.ChooserLabels(contacts).Should().Equal("Sam Lee (sam@a.org)", "Sam Lee (sam@b.org)", "555", "Kim", "Kim (2)");
    }

    // ======================================================================
    // Live checks (opt-in: they use the machine's camera and an EDS session)
    // ======================================================================

    /// <summary>
    /// OPENMAUI_LIVE_CAMERA=1: opens the first webcam through GStreamer, takes a frame and
    /// records two seconds. Turns the camera (and microphone) on.
    /// </summary>
    [Fact]
    public async Task Live_camera_preview_and_recording()
    {
        if (Environment.GetEnvironmentVariable("OPENMAUI_LIVE_CAMERA") != "1")
            return;
        var device = CameraDiscovery.CaptureDevices().First();
        var source = CameraSource.ForDevice(device);
        using (var preview = GstCameraSession.Start(CameraPipelines.Preview(source)))
        {
            for (int i = 0; i < 100 && preview.FrameCount == 0; i++)
                await Task.Delay(100);
            using var frame = preview.SnapshotLatest();
            frame.Should().NotBeNull();
            frame!.Width.Should().BeGreaterThan(0);
            (await preview.StopAsync(finish: false, TimeSpan.Zero)).Should().BeTrue();
        }

        var profile = CameraPipelines.SelectProfile(GstCameraNative.HasElement)!;
        var path = Path.Combine(TempDir("rec"), "capture" + profile.Extension);
        var recording = GstCameraSession.Start(CameraPipelines.Recording(source, profile, path, withAudio: true));
        await Task.Delay(2000);
        recording.FrameCount.Should().BeGreaterThan(0);
        (await recording.StopAsync(finish: true, TimeSpan.FromSeconds(10))).Should().BeTrue();
        new FileInfo(path).Length.Should().BeGreaterThan(1000);
    }

    /// <summary>
    /// OPENMAUI_LIVE_EDS=1: reads the session bus's Evolution Data Server address books. Run it
    /// in a private session (dbus-run-session with XDG_*_HOME in a temporary directory) after
    /// adding a contact, never against a real desktop's address books.
    /// </summary>
    [Fact]
    public async Task Live_evolution_data_server_contacts()
    {
        if (Environment.GetEnvironmentVariable("OPENMAUI_LIVE_EDS") != "1")
            return;
        var expected = Environment.GetEnvironmentVariable("OPENMAUI_LIVE_EDS_NAME") ?? "Ada";
        var contacts = (await new ContactsService(EdsContactSource.Instance, _ => Task.FromResult<Contact?>(null)).GetAllAsync()).ToList();
        contacts.Should().Contain(c => c.GivenName == expected);
    }
}
