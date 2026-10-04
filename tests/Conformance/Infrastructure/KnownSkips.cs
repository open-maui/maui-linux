// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// MAUI tests that check a platform mechanism Linux does not have (not a
	/// behaviour it gets wrong). Only genuine "cannot exist here" cases belong in
	/// this list; a behaviour difference stays a failing test and is reported in
	/// docs/CONFORMANCE.md as a parity gap.
	///
	/// Key: test method name, optionally "TestClassSimpleName.Method" to limit it
	/// to one handler.
	/// </summary>
	public static class KnownSkips
	{
		const string NoWrapperView =
			"Skia views apply Clip and Shadow while drawing; there is no native wrapper (ContainerView) to add. " +
			"MAUI's platform-neutral ViewHandler has no container implementation either.";

		static readonly Dictionary<string, string> s_skips = new(StringComparer.Ordinal)
		{
			["ContainerViewInitializesCorrectly"] = NoWrapperView,
			["ContainerViewRemainsIfShadowMapperRunsAgain"] = NoWrapperView,
			["ContainerViewAddsAndRemoves"] = NoWrapperView,
			["LayoutHandlerTests.ContainerViewAddedToLayout"] = NoWrapperView,
			["LayoutHandlerTests.ContainerViewDifferentThanPlatformView"] = NoWrapperView,
		};

		/// <summary>
		/// Tests that cannot be run because they would hang on a missing
		/// mechanism. Reported as BLOCKED (and counted with the gaps in the report).
		/// Empty since the image handlers load through IImageSourceService.
		/// </summary>
		static readonly Dictionary<string, string> s_blocked = new(StringComparer.Ordinal)
		{
		};

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
