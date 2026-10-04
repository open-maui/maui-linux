// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Essentials.DeviceTests;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Essentials tests that cannot run on this machine, reported as skipped with the reason
	/// (see ../../Infrastructure/LinuxTestFramework.cs). Mirrors what MAUI's own device runner
	/// excludes (src/Essentials/test/DeviceTests/Traits.cs, GetSkipTraits):
	///
	/// - InteractionType=Human: never run by MAUI's runner on any platform; the test opens an
	///   app, a prompt or a web sign-in that a person has to complete or close.
	/// - Hardware&lt;X&gt;=Supported on a machine without that hardware (HardwareSupport.Linux.cs
	///   reads the kernel). Where the machine has it, the test runs.
	///
	/// A behaviour difference is never listed here: it stays a failing test.
	/// </summary>
	public static class KnownSkips
	{
		const string HumanPrefix = "Needs a person (MAUI's device runner never runs InteractionType=Human tests): ";

		/// <summary>Why each human-interaction test needs a person, per test class.</summary>
		static readonly Dictionary<string, string> s_human = new(StringComparer.Ordinal)
		{
			["Contacts_Tests"] = "the contacts permission prompt has to be answered.",
			["Email_Tests"] = "opens the desktop's mail composer, which has to be closed.",
			["Geolocation_Tests"] = "the location permission (GeoClue agent) has to be granted and a position fix obtained.",
			["Launcher_Tests"] = "opens the URI in the desktop's handler (browser, mail client, dialer), which has to be closed.",
			["Maps_Tests"] = "opens the desktop's map application, which has to be closed.",
			["Microphone_Tests"] = "the microphone permission prompt has to be allowed or denied by hand.",
			["WebAuthenticator_Tests"] = "a browser sign-in round trip on a live web site that redirects back to the app.",
		};

		static readonly Dictionary<string, (Func<bool> Present, string Missing)> s_hardware = new(StringComparer.Ordinal)
		{
			[Traits.Hardware.Accelerometer] = (() => HardwareSupport.HasAccelerometer, "no accelerometer (no IIO in_accel channel under /sys/bus/iio/devices)"),
			[Traits.Hardware.Barometer] = (() => HardwareSupport.HasBarometer, "no barometer (no IIO in_pressure channel under /sys/bus/iio/devices)"),
			[Traits.Hardware.Compass] = (() => HardwareSupport.HasCompass, "no compass (no IIO magnetometer or heading channel under /sys/bus/iio/devices)"),
			[Traits.Hardware.Gyroscope] = (() => HardwareSupport.HasGyroscope, "no gyroscope (no IIO in_anglvel channel under /sys/bus/iio/devices)"),
			[Traits.Hardware.Magnetometer] = (() => HardwareSupport.HasMagnetometer, "no magnetometer (no IIO in_magn channel under /sys/bus/iio/devices)"),
			[Traits.Hardware.Battery] = (() => HardwareSupport.HasBattery, "no system battery (no power supply of type Battery under /sys/class/power_supply)"),
			[Traits.Hardware.Flash] = (() => HardwareSupport.HasFlash, "no camera torch/flash LED under /sys/class/leds"),
		};

		/// <summary>
		/// Tests without a MAUI trait that need hardware this machine lacks. Key: class.method.
		/// </summary>
		static readonly Dictionary<string, (Func<bool> Present, string Missing)> s_untaggedHardware = new(StringComparer.Ordinal)
		{
			// MAUI skips these on Windows ("Not supported on Windows? See Vibration
			// implementation"): Vibration.Vibrate throws FeatureNotSupportedException without
			// a vibration motor, as specified (VibrationImplementation, shared).
			["Vibration_Tests.Vibrate"] = (() => HardwareSupport.HasVibrator, "no vibration motor (/sys/class/leds/vibrator); Vibrate throws FeatureNotSupportedException without one, which is why MAUI skips this test on Windows"),
			["Vibration_Tests.Vibrate_Cancel"] = (() => HardwareSupport.HasVibrator, "no vibration motor (/sys/class/leds/vibrator); Vibrate throws FeatureNotSupportedException without one, which is why MAUI skips this test on Windows"),
		};

		/// <summary>
		/// Single theory rows to skip: Launcher.CanOpenAsync rows for a scheme no application on
		/// this machine handles (a CI container has no browser, mail client or dialer). Whether
		/// the desktop has a handler is asked of the desktop itself (gio, else xdg-mime), not of
		/// OpenMaui; where it has one, the row runs and OpenMaui must say true.
		/// </summary>
		public static string ReasonForCase(string testClassFullName, string method, string displayName)
		{
			if (!testClassFullName.EndsWith(".Launcher_Tests", StringComparison.Ordinal) || method is not ("CanOpen" or "CanOpenUri"))
				return null;
			var start = displayName.IndexOf('"');
			var end = displayName.LastIndexOf('"');
			if (start < 0 || end <= start)
				return null;
			var uri = displayName.Substring(start + 1, end - start - 1);
			var colon = uri.IndexOf(':');
			if (colon <= 0)
				return null;
			var scheme = uri.Substring(0, colon).ToLowerInvariant();
			return DesktopSchemeHandlers.Has(scheme)
				? null
				: $"No application on this machine handles {scheme}: URIs (x-scheme-handler/{scheme} has no handler according to {DesktopSchemeHandlers.Oracle}); CanOpenAsync is false there, as on any platform without one.";
		}

		/// <summary>MAUI-skipped tests that run on Linux (none in the Essentials suite).</summary>
		public static string UnskipReasonFor(string testClassFullName, string method) => null;

		public static string ReasonFor(string testClassFullName, string method)
		{
			// Discovery runs before any test: the OpenMaui main thread must be up by then.
			EssentialsTestHost.EnsureStarted();

			var simple = testClassFullName.Substring(testClassFullName.LastIndexOfAny(new[] { '.', '+' }) + 1);
			var traits = TraitsOf(testClassFullName, method);

			if (traits.Contains((Traits.InteractionType, Traits.InteractionTypes.Human)))
				return HumanPrefix + (s_human.TryGetValue(simple, out var why) ? why : "the test drives a platform UI a person completes.");

			foreach (var (name, value) in traits)
			{
				if (value == Traits.FeatureSupport.Supported && s_hardware.TryGetValue(name, out var hw) && !hw.Present())
					return $"This machine has {hw.Missing}; MAUI's device runner excludes {name}=Supported tests on such a device.";
			}

			if (s_untaggedHardware.TryGetValue(simple + "." + method, out var untagged) && !untagged.Present())
				return $"This machine has {untagged.Missing}.";

			// MAUI skips these two on Windows and Mac Catalyst. On Linux they run against the
			// session's Secret Service, or the encrypted-file store when there is none (CI).
			if (simple == "SecureStorage_Tests" && method is "Set_Get_Async_MultipleTimes" or "Set_Get_Remove_Async_MultipleTimes"
				&& SecretService.Daemon() == "ksecretd")
				return "The session's Secret Service is KDE's ksecretd, which under 100 concurrent store/lookup/clear calls acknowledges " +
					"writes before reads see them and briefly returns cleared items (one secret-tool call at a time from this process; " +
					"the same tests pass against the encrypted-file store). MAUI skips these two tests on Windows and Mac Catalyst as well.";

			// Geocoding is an online service on every platform (Bing Maps, Google, Apple; on
			// Linux Nominatim). Without network access to it, the tests cannot run.
			if (simple == "Geocoding_Tests" && GeocodingReachability.Unreachable() is string unreachable)
				return $"No network access to the geocoding service ({unreachable}).";

			return null;
		}

		static List<(string Name, string Value)> TraitsOf(string testClassFullName, string method)
		{
			var result = new List<(string, string)>();
			var type = typeof(KnownSkips).Assembly.GetType(testClassFullName);
			if (type is null)
				return result;
			var members = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(m => m.Name == method)
				.Cast<MemberInfo>()
				.Append(type);
			foreach (var member in members)
			{
				foreach (var data in member.CustomAttributes.Where(a => a.AttributeType == typeof(TraitAttribute)))
					result.Add(((string)data.ConstructorArguments[0].Value, (string)data.ConstructorArguments[1].Value));
			}
			return result;
		}
	}

	/// <summary>Whether the desktop has an application for a URI scheme, asked of the desktop's own tools.</summary>
	static class DesktopSchemeHandlers
	{
		static readonly Dictionary<string, bool> s_cache = new(StringComparer.Ordinal);

		public static string Oracle { get; private set; } = "gio mime";

		public static bool Has(string scheme)
		{
			lock (s_cache)
			{
				if (s_cache.TryGetValue(scheme, out var known))
					return known;
				var has = Query(scheme);
				s_cache[scheme] = has;
				return has;
			}
		}

		static bool Query(string scheme)
		{
			var mime = "x-scheme-handler/" + scheme;
			var gio = Run("gio", "mime", mime);
			if (gio != null)
			{
				Oracle = "gio mime";
				return gio.Contains("Default application for", StringComparison.Ordinal)
					|| gio.Contains("Registered applications:", StringComparison.Ordinal);
			}
			var xdg = Run("xdg-mime", "query", "default", mime);
			if (xdg != null)
			{
				Oracle = "xdg-mime";
				return xdg.Trim().Length > 0;
			}
			Oracle = "no gio or xdg-mime on this machine, so no desktop integration";
			return false;
		}

		static string Run(string file, params string[] args)
		{
			try
			{
				var psi = new System.Diagnostics.ProcessStartInfo(file)
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				};
				foreach (var arg in args)
					psi.ArgumentList.Add(arg);
				using var process = System.Diagnostics.Process.Start(psi);
				if (process is null)
					return null;
				var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
				process.WaitForExit(10000);
				return output;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	/// <summary>Reachability of the geocoding service the Linux Geocoding uses.</summary>
	static class GeocodingReachability
	{
		static string s_result;
		static bool s_checked;

		/// <summary>Null when a TCP connection to the service's host can be made, else why not.</summary>
		public static string Unreachable()
		{
			if (s_checked)
				return s_result;
			s_checked = true;
			var configured = Environment.GetEnvironmentVariable("OPENMAUI_GEOCODING_URL");
			var uri = new Uri(string.IsNullOrWhiteSpace(configured) ? "https://nominatim.openstreetmap.org/" : configured);
			try
			{
				using var client = new System.Net.Sockets.TcpClient();
				var connect = client.ConnectAsync(uri.Host, uri.Port);
				if (!connect.Wait(TimeSpan.FromSeconds(5)))
					return s_result = $"connecting to {uri.Host}:{uri.Port} timed out";
				return s_result = null;
			}
			catch (Exception ex)
			{
				return s_result = $"cannot connect to {uri.Host}:{uri.Port}: {ex.GetBaseException().Message}";
			}
		}
	}

	/// <summary>Which program owns org.freedesktop.secrets on the session bus (null: none, or unknown).</summary>
	static class SecretService
	{
		static string s_daemon;
		static bool s_checked;

		public static string Daemon()
		{
			if (s_checked)
				return s_daemon;
			s_checked = true;
			try
			{
				var psi = new System.Diagnostics.ProcessStartInfo("busctl")
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				};
				foreach (var arg in new[] { "--user", "call", "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "GetConnectionUnixProcessID", "s", "org.freedesktop.secrets" })
					psi.ArgumentList.Add(arg);
				using var process = System.Diagnostics.Process.Start(psi);
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(10000);
				// "u 1234"
				var parts = output.Trim().Split(' ');
				if (process.ExitCode != 0 || parts.Length != 2 || !int.TryParse(parts[1], out var pid))
					return s_daemon = null;
				return s_daemon = System.IO.File.ReadAllText($"/proc/{pid}/comm").Trim();
			}
			catch (Exception)
			{
				return s_daemon = null;
			}
		}
	}
}
