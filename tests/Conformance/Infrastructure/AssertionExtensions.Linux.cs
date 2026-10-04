// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.DeviceTests.ImageAnalysis;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux half of MAUI's AssertionExtensions (the *.Windows.cs / *.Android.cs
	/// partials in src/TestUtils/src/DeviceTests). On the platform-neutral TFM
	/// MAUI's PlatformView is <see cref="object"/>, so these extend object and
	/// expect a <see cref="SkiaView"/>.
	///
	/// "Attached" means what it means on the other platforms: the view has been
	/// measured and arranged by a host before the action runs (each suite hosts
	/// views its own way: AssertionExtensions.Attach.Core.cs for the Core suite,
	/// Controls/Infrastructure for the Controls suite). Pixels come from
	/// SkiaView.Draw into a raster surface, the same path a window frame takes.
	/// </summary>
	public static partial class AssertionExtensions
	{
		public static SkiaView AsSkia(this object? platformView) =>
			platformView as SkiaView
			// MAUI's platform helpers also take the element itself (IView.GetBoundingBox(),
			// ...); C# binds those calls to the object overloads here (the closer
			// namespace), so an element means its platform view.
			?? (platformView as IElement)?.Handler?.PlatformView as SkiaView
			?? throw new XunitException(
				$"Platform view is {platformView?.GetType().FullName ?? "null"}, expected a SkiaView.");

		// ---------------- attach ----------------

		/// <summary>Size given to a view with no explicit size, as a window would.</summary>
		const double HostWidth = 800, HostHeight = 600;

		internal static void EnsureLaidOut(SkiaView view)
		{
			if (view.Bounds.Width > 0 && view.Bounds.Height > 0)
				return;
			var desired = view.Measure(new Size(HostWidth, HostHeight));
			var w = desired.Width > 0 ? Math.Min(desired.Width, HostWidth) : HostWidth;
			var h = desired.Height > 0 ? Math.Min(desired.Height, HostHeight) : HostHeight;
			if (view.MauiView is null && view.WidthRequest > 0) w = view.WidthRequest;
			if (view.MauiView is null && view.HeightRequest > 0) h = view.HeightRequest;
			view.Arrange(new Rect(0, 0, w, h));
		}

		// ---------------- accessibility ----------------

		public static bool IsAccessibilityElement(this object? platformView)
		{
			if (platformView is not SkiaView v)
				return false;
			if (v.IsInAccessibleTree is bool b)
				return b;
			return !string.IsNullOrEmpty(v.SemanticName) || !string.IsNullOrEmpty(v.SemanticHint);
		}

		public static bool IsExcludedWithChildren(this object? platformView) =>
			platformView is SkiaView { IsExcludedWithChildren: true };

		// ---------------- focus / keyboard ----------------

		public static Task WaitForFocused(this object view, int timeout = 1000) =>
			AssertHelpers.AssertEventually(() => view.AsSkia().IsFocused, timeout, message: "View did not become focused");

		public static Task WaitForUnFocused(this object view, int timeout = 1000) =>
			AssertHelpers.AssertEventually(() => !view.AsSkia().IsFocused, timeout, message: "View did not lose focus");

		public static Task FocusView(this object view, int timeout = 1000)
		{
			var v = view.AsSkia();
			if (v.MauiView is Microsoft.Maui.Controls.VisualElement ve)
				ve.Focus();
			return WaitForFocused(view, timeout);
		}

		// There is no on-screen keyboard on the desktop; MAUI's own Windows
		// runner treats these as "focus the view".
		public static Task ShowKeyboardForView(this object view, int timeout = 1000) => FocusView(view, timeout);
		public static Task HideKeyboardForView(this object view, int timeout = 1000, string? message = null) => Task.CompletedTask;
		public static Task WaitForKeyboardToShow(this object view, int timeout = 1000) => WaitForFocused(view, timeout);
		public static Task WaitForKeyboardToHide(this object view, int timeout = 1000) => Task.CompletedTask;

		public static Task SendValueToKeyboard(this object view, char value, int timeout = 1000)
		{
			view.AsSkia().OnTextInput(new TextInputEventArgs(value.ToString()));
			return Task.CompletedTask;
		}

		public static Task SendKeyboardReturnType(this object view, ReturnType returnType, int timeout = 1000)
		{
			view.AsSkia().OnKeyDown(new KeyEventArgs(Key.Enter, KeyModifiers.None));
			return Task.CompletedTask;
		}

		// ---------------- pixels ----------------

		/// <summary>
		/// Run before a view is captured (the Controls suite renders a frame of the
		/// window the view is in, so pending layout is applied as a platform would
		/// before a capture). Unset in the Core suite.
		/// </summary>
		internal static Action<SkiaView>? BeforeCapture;

		public static SKBitmap ToBitmap(this object platformView)
		{
			var view = platformView.AsSkia();
			BeforeCapture?.Invoke(view);
			EnsureLaidOut(view);
			var b = view.Bounds;
			int w = Math.Max(1, (int)Math.Ceiling(b.Width));
			int h = Math.Max(1, (int)Math.Ceiling(b.Height));
			var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
			using var canvas = new SKCanvas(bmp);
			canvas.Clear(SKColors.Transparent);
			canvas.Translate((float)-b.X, (float)-b.Y);
			view.Draw(canvas);
			canvas.Flush();
			return bmp;
		}

		public static Task<SKBitmap> ToBitmap(this object platformView, IMauiContext mauiContext) =>
			Task.FromResult(platformView.ToBitmap());

		static bool ColorsMatch(SKColor actual, Color expected, double tolerance)
		{
			// Same comparison the other platforms use: per-channel distance on
			// 0..1 components (alpha ignored when the expected color is opaque).
			double dr = Math.Abs(actual.Red / 255.0 - expected.Red);
			double dg = Math.Abs(actual.Green / 255.0 - expected.Green);
			double db = Math.Abs(actual.Blue / 255.0 - expected.Blue);
			return dr <= tolerance && dg <= tolerance && db <= tolerance && actual.Alpha > 0;
		}

		static string Describe(SKBitmap bmp)
		{
			var counts = new Dictionary<SKColor, int>();
			for (int y = 0; y < bmp.Height; y++)
				for (int x = 0; x < bmp.Width; x++)
				{
					var c = bmp.GetPixel(x, y);
					counts[c] = counts.TryGetValue(c, out var n) ? n + 1 : 1;
				}
			var top = counts.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} x{kv.Value}");
			return $"{bmp.Width}x{bmp.Height}, top colors: {string.Join(", ", top)}";
		}

		public static Task<SKBitmap> AssertContainsColor(this object view, Color expectedColor, IMauiContext mauiContext, double? tolerance = null)
		{
			var bmp = view.ToBitmap();
			return Task.FromResult(bmp.AssertContainsColor(expectedColor, tolerance: tolerance));
		}

		public static SKBitmap AssertContainsColor(this SKBitmap bmp, Color expectedColor, Func<RectF, RectF>? withinRectModifier = null, double? tolerance = null)
		{
			var tol = tolerance ?? 0.05;
			var rect = new RectF(0, 0, bmp.Width, bmp.Height);
			if (withinRectModifier is not null)
				rect = withinRectModifier(rect);
			for (int y = (int)rect.Top; y < (int)rect.Bottom; y++)
				for (int x = (int)rect.Left; x < (int)rect.Right; x++)
					if (ColorsMatch(bmp.GetPixel(x, y), expectedColor, tol))
						return bmp;
			throw new XunitException($"Color {expectedColor} not found. Rendered {Describe(bmp)}");
		}

		public static Task<SKBitmap> AssertDoesNotContainColor(this object view, Color unexpectedColor, IMauiContext mauiContext)
		{
			var bmp = view.ToBitmap();
			for (int y = 0; y < bmp.Height; y++)
				for (int x = 0; x < bmp.Width; x++)
					if (ColorsMatch(bmp.GetPixel(x, y), unexpectedColor, 0.05))
						throw new XunitException($"Color {unexpectedColor} was found at {x},{y}. Rendered {Describe(bmp)}");
			return Task.FromResult(bmp);
		}

		public static Task<SKBitmap> AssertColorAtPointAsync(this object view, Color expectedColor, int x, int y, IMauiContext mauiContext)
		{
			var bmp = view.ToBitmap();
			var actual = bmp.GetPixel(Math.Clamp(x, 0, bmp.Width - 1), Math.Clamp(y, 0, bmp.Height - 1));
			if (!ColorsMatch(actual, expectedColor, 0.05))
				throw new XunitException($"Expected {expectedColor} at {x},{y}, was {actual}. Rendered {Describe(bmp)}");
			return Task.FromResult(bmp);
		}

		public static Task<SKBitmap> AssertColorsAtPointsAsync(this object view, Color[] colors, Point[] points, IMauiContext mauiContext)
		{
			var bmp = view.ToBitmap();
			for (int i = 0; i < points.Length; i++)
			{
				var p = points[i];
				var actual = bmp.GetPixel(Math.Clamp((int)p.X, 0, bmp.Width - 1), Math.Clamp((int)p.Y, 0, bmp.Height - 1));
				if (!ColorsMatch(actual, colors[i], 0.05))
					throw new XunitException($"Expected {colors[i]} at {p}, was {actual}. Rendered {Describe(bmp)}");
			}
			return Task.FromResult(bmp);
		}

		public static Task<SKBitmap> AssertColorAtCenterAsync(this object view, Color expectedColor, IMauiContext mauiContext)
		{
			var bmp = view.ToBitmap();
			return view.AssertColorAtPointAsync(expectedColor, bmp.Width / 2, bmp.Height / 2, mauiContext);
		}

		// Same comparison as MAUI's ColorComparison.*.cs IsEquivalent: every channel,
		// alpha included, within 1 of the other.
		const int ColorPrecision = 1;

		static bool IsEquivalent(SKColor a, SKColor b) =>
			Math.Abs(a.Red - b.Red) <= ColorPrecision && Math.Abs(a.Green - b.Green) <= ColorPrecision &&
			Math.Abs(a.Blue - b.Blue) <= ColorPrecision && Math.Abs(a.Alpha - b.Alpha) <= ColorPrecision;

		static bool IsMatching(SKBitmap first, SKBitmap second)
		{
			for (int x = 0; x < first.Width; x++)
				for (int y = 0; y < first.Height; y++)
					if (!IsEquivalent(first.GetPixel(x, y), second.GetPixel(x, y)))
						return false;
			return true;
		}

		/// <summary>MAUI's AssertEqualAsync (pixel-identical rendering, within ColorPrecision).</summary>
		public static Task AssertEqualAsync(this SKBitmap bitmap, SKBitmap other)
		{
			Assert.NotNull(bitmap);
			Assert.NotNull(other);
			Assert.Equal(new Size(bitmap.Width, bitmap.Height), new Size(other.Width, other.Height));
			Assert.True(IsMatching(bitmap, other), $"Images did not match. Rendered {Describe(bitmap)} vs {Describe(other)}");
			return Task.CompletedTask;
		}

		public static Task AssertNotEqualAsync(this SKBitmap bitmap, SKBitmap other)
		{
			Assert.NotNull(bitmap);
			Assert.NotNull(other);
			Assert.False(bitmap.Width == other.Width && bitmap.Height == other.Height && IsMatching(bitmap, other),
				$"Images matched. Rendered {Describe(bitmap)}");
			return Task.CompletedTask;
		}

		/// <summary>The other platforms' <c>Color.ToPlatform()</c>; Skia assertions take MAUI colors.</summary>
		public static Color ToPlatform(this Color color) => color;

		/// <summary>MAUI's platform ElementExtensions.ToPlatform(): the element's native view.</summary>
		public static object ToPlatform(this IElement element) =>
			element.Handler?.PlatformView ?? throw new XunitException($"{element.GetType().Name} has no handler");

		/// <summary>MAUI's platform ElementExtensions.ToPlatform(context): realizes the handler first.</summary>
		public static object ToPlatform(this IView view, IMauiContext context) =>
			view.ToHandler(context).PlatformView!;
	}
}
