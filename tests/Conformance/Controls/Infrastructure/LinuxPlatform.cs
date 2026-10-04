// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Reading values back from OpenMaui's Skia platform views, for the per-element
	/// helper partials (Elements/*.Linux.cs). Where a Skia view has no counterpart
	/// of the native property a MAUI test reads, the helper fails with
	/// "SkiaX exposes no Y" rather than inventing a value.
	/// </summary>
	public static class LinuxPlatform
	{
		public static SkiaView View(IElementHandler handler) =>
			handler?.PlatformView as SkiaView ?? throw new XunitException(
				$"{handler?.GetType().FullName ?? "null handler"} has platform view {handler?.PlatformView?.GetType().FullName ?? "null"}, not a SkiaView " +
				"(no OpenMaui handler: the test bound MAUI's platform-neutral handler).");

		public static T View<T>(IElementHandler handler) where T : SkiaView =>
			View(handler) as T ?? throw new XunitException(
				$"{handler.GetType().Name}'s platform view is {handler.PlatformView?.GetType().Name}, expected {typeof(T).Name}.");

		public static float Opacity(IElementHandler handler) => View(handler).Opacity;

		/// <summary>What the Windows helpers read as Visibility == Visible.</summary>
		public static bool IsVisible(IElementHandler handler) => View(handler).Visibility == Visibility.Visible;

		public static Exception NoCounterpart(SkiaView view, string what) =>
			new XunitException($"{view.GetType().Name} exposes no {what}.");

		/// <summary>The text a SkiaLabel draws (its text after TextTransform, or its spans unless it is Html).</summary>
		public static string LabelText(SkiaLabel label)
		{
			if (label.TextType != TextType.Html && label.FormattedText is { Spans.Count: > 0 } formatted)
				return string.Concat(formatted.Spans.Select(s => SpanText(label, s)));
			return Invoke<string>(label, "GetDisplayText");
		}

		// A span's text as SkiaLabel lays it out (its own transform, or the label's).
		static string SpanText(SkiaLabel label, Span span) => Invoke<string>(label, "GetSpanDisplayText", span);

		/// <summary>The text a SkiaButton draws (Text after its TextTransform).</summary>
		public static string ButtonText(SkiaButton button) => Invoke<string>(button, "ApplyTextTransform", button.Text);

		/// <summary>MAUI's LineBreakMode.ToPlatform(): SkiaLabel and SkiaButton take MAUI's enum.</summary>
		public static LineBreakMode ToPlatform(this LineBreakMode mode) => mode;

		static T Invoke<T>(object target, string method, params object?[] args)
		{
			var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new XunitException($"{target.GetType().Name}.{method} not found (OpenMaui changed; update LinuxPlatform).");
			return (T)m.Invoke(target, args)!;
		}
	}
}
