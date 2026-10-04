// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Controls-suite skip list (the Core suite has its own,
	/// ../../Infrastructure/KnownSkips.cs; LinuxTestFramework reads whichever the
	/// assembly compiles). Only tests of a mechanism Linux cannot have, or that
	/// would hang, belong here; a behaviour difference stays a failing test.
	///
	/// Key: test method name, optionally "TestClassSimpleName.Method".
	/// </summary>
	public static class KnownSkips
	{
		static readonly Dictionary<string, string> s_skips = new(StringComparer.Ordinal)
		{
			// Puts every Controls type (WebView included) in one page.
			["ValidateIsImportantForAccessibility"] = NoWebViewHost + " The test puts every control, WebView included, in one page.",
			// Converts a FormattedString to the platform's native attributed string
			// (NSAttributedString, SpannableString, WinUI Runs); Skia draws spans itself and has no such type.
			["NativeFormattedStringContainsSpan"] = "tests the conversion to a platform-native attributed string type (Spannable, NSAttributedString, WinUI Runs); OpenMaui draws spans itself and has none.",
		};

		/// <summary>Tests that would hang (reported as BLOCKED, counted with the gaps).</summary>
		static readonly Dictionary<string, string> s_blocked = new(StringComparer.Ordinal)
		{
		};

		const string NoWebViewHost =
			"WebView renders through OpenMaui's out-of-process WebKit host (WPE/WebKitGTK), which a headless " +
			"test process cannot start: creating the handler takes the test host down.";

		/// <summary>
		/// Single theory rows (matched on a substring of the row's display name) that
		/// need a mechanism Linux lacks while the theory's other rows run.
		/// </summary>
		static readonly (string Method, string RowContains, string Reason)[] s_rowSkips =
		{
			(null, "typeof(Microsoft.Maui.Controls.WebView)", NoWebViewHost),
			(null, "typeof(Microsoft.Maui.Controls.HybridWebView)", NoWebViewHost),
		};

		public static string ReasonForCase(string testClassFullName, string method, string displayName)
		{
			foreach (var (m, contains, reason) in s_rowSkips)
				if ((m is null || m == method) && displayName.Contains(contains, StringComparison.Ordinal))
					return reason;
			return null;
		}

		/// <summary>MAUI-skipped tests that run on Linux (none in the Controls suite).</summary>
		public static string UnskipReasonFor(string testClassFullName, string method) => null;

		public static string ReasonFor(string testClassFullName, string method)
		{
			var simple = testClassFullName.Substring(testClassFullName.LastIndexOfAny(new[] { '.', '+' }) + 1);
			foreach (var key in new[] { simple + "." + method, method })
			{
				if (s_skips.TryGetValue(key, out var reason))
					return reason;
				if (s_blocked.TryGetValue(key, out reason))
					return "BLOCKED: " + reason;
			}
			return null;
		}
	}
}
