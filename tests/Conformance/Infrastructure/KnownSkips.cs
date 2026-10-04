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
		// The container tests assert MAUI's wrapper mechanism itself: handler.ContainerView not
		// null and the platform view's parent a Microsoft.Maui.Platform.WrapperView. On the
		// platform-neutral build OpenMaui compiles against, ViewHandler.ContainerView has a
		// private protected setter (only handlers inside Microsoft.Maui.dll can set it) and its
		// SetupContainer/RemoveContainer do nothing, and WrapperView is a plain class that no
		// drawable view can be (a Skia view's parent is a SkiaView). So no Linux handler can
		// pass them. Clip and Shadow themselves work: Skia views apply them while drawing.
		const string NoWrapperView =
			"Asserts MAUI's ContainerView/WrapperView mechanism: ViewHandler.ContainerView is settable only inside " +
			"Microsoft.Maui.dll on the platform-neutral build and WrapperView is not a drawable view there. " +
			"Skia views apply Clip and Shadow while drawing, without a wrapper.";

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

		/// <summary>
		/// MAUI tests that MAUI itself skips, for a reason of its own platforms that does not
		/// hold on Linux (a missing root window, a bug of a platform view). They run here (their
		/// Skip is ignored). MAUI's "Shadow Initializes Correctly" tests stay skipped: MAUI calls
		/// them invalid (dotnet/maui#13692; the shadow is drawn outside the captured view), and
		/// they fail on Linux for that reason.
		/// </summary>
		static readonly Dictionary<string, string> s_unskips = new(StringComparer.Ordinal)
		{
			// MAUI: "iOS and Windows can't render elements to images from test runner. It's
			// missing the required root windows." (skipped except on Android). OpenMaui renders
			// a view through IView.CaptureAsync's capture hook without a window.
			["RendersAsImage"] = "View capture works without a root window on Linux",
			// MAUI disables these on every platform for bugs of its platform views
			// (dotnet/maui#1275: the Switch thumb colour; dotnet/maui#6415: a source that fails
			// to load). The behaviour exists on Linux and the tests pass, so they run.
			["SwitchHandlerTests.ThumbColorInitializesCorrectly"] = "MAUI's platform bug (dotnet/maui#1275) does not apply",
			["InvalidSourceFailsToLoad"] = "MAUI's platform bug (dotnet/maui#6415) does not apply",
		};

		/// <summary>Why a MAUI-skipped test runs on Linux, or null when MAUI's skip stands.</summary>
		public static string UnskipReasonFor(string testClassFullName, string method)
		{
			var simple = testClassFullName.Substring(testClassFullName.LastIndexOfAny(new[] { '.', '+' }) + 1);
			foreach (var key in new[] { simple + "." + method, method })
			{
				if (s_unskips.TryGetValue(key, out var reason))
					return reason;
			}
			return null;
		}

		/// <summary>Single theory rows to skip (none in the Core suite).</summary>
		public static string ReasonForCase(string testClassFullName, string method, string displayName) => null;

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
