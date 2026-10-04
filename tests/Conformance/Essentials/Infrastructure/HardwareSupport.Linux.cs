// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.Maui.Essentials.DeviceTests
{
	/// <summary>
	/// Linux counterpart of MAUI's Tests/HardwareSupport.cs (a per-platform #if chain with
	/// no fallback): whether THIS machine has the hardware, read from the kernel, independent
	/// of what OpenMaui's services report. MAUI's tests compare it with the service's
	/// IsSupported, and MAUI's runner (Traits.GetSkipTraits) excludes a test marked
	/// Hardware&lt;X&gt;=Supported on a device without it; KnownSkips.Essentials.cs does the same.
	/// </summary>
	static class HardwareSupport
	{
		const string IioDevices = "/sys/bus/iio/devices";

		/// <summary>An IIO device exposing an accelerometer channel.</summary>
		public static bool HasAccelerometer => IioChannel("in_accel_x_raw", "in_accel_x_input");

		/// <summary>An IIO magnetometer (the compass is derived from it, as on Android).</summary>
		public static bool HasMagnetometer => IioChannel("in_magn_x_raw", "in_magn_x_input");

		/// <summary>An IIO gyroscope (angular velocity channel).</summary>
		public static bool HasGyroscope => IioChannel("in_anglvel_x_raw", "in_anglvel_x_input");

		/// <summary>A magnetometer or a tilt-compensated heading channel.</summary>
		public static bool HasCompass => HasMagnetometer || IioChannel("in_rot_from_north_magnetic_tilt_comp_raw");

		/// <summary>An IIO pressure sensor.</summary>
		public static bool HasBarometer => IioChannel("in_pressure_raw", "in_pressure_input");

		/// <summary>
		/// A system battery: a power supply of type Battery that powers the machine
		/// (scope "Device" marks a peripheral's battery, a wireless mouse for one).
		/// </summary>
		public static bool HasBattery => PowerSupplies().Any(dir =>
			Read(dir, "type") == "Battery" && Read(dir, "scope") != "Device");

		/// <summary>A camera torch/flash LED class device.</summary>
		public static bool HasFlash => Entries("/sys/class/leds").Any(dir =>
		{
			var name = Path.GetFileName(dir);
			return name.Contains("torch", StringComparison.OrdinalIgnoreCase)
				|| name.Contains("flash", StringComparison.OrdinalIgnoreCase);
		});

		/// <summary>A vibration motor (the LED-class "vibrator" node phones expose).</summary>
		public static bool HasVibrator => File.Exists("/sys/class/leds/vibrator/trigger");

		static bool IioChannel(params string[] channels) =>
			Entries(IioDevices).Any(dir => channels.Any(c => File.Exists(Path.Combine(dir, c))));

		static string[] PowerSupplies() => Entries("/sys/class/power_supply");

		static string[] Entries(string path)
		{
			try
			{
				return Directory.Exists(path) ? Directory.GetFileSystemEntries(path) : Array.Empty<string>();
			}
			catch (Exception)
			{
				return Array.Empty<string>();
			}
		}

		static string Read(string dir, string file)
		{
			try
			{
				var path = Path.Combine(dir, file);
				return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
