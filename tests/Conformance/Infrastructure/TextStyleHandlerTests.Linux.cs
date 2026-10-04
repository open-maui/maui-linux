// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of TextStyleHandlerTests.Android.cs / .iOS.cs: font
	/// state read from the text-bearing Skia view. The Skia text views share
	/// the FontSize / FontAttributes property names but no base type, so the
	/// values are read by name (what is checked is the value, not the shape).
	/// </summary>
	public abstract partial class TextStyleHandlerTests<THandler, TStub>
	{
		static object TextView(THandler handler) =>
			handler.PlatformView ?? throw new XunitException("Handler has no platform view");

		static T Read<T>(THandler handler, string property)
		{
			var view = TextView(handler);
			var prop = view.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
				?? throw Missing.Property(view, property);
			return (T)prop.GetValue(view);
		}

		/// <summary>
		/// The size the view lays text out with, in device-independent units. Skia
		/// views render at 1:1 with the logical size (the window scale is applied
		/// to the whole canvas), so there is no per-view scale factor to undo.
		/// </summary>
		protected double GetNativeUnscaledFontSize(THandler handler, bool autoScalingEnabled) =>
			Read<double>(handler, "FontSize");

		protected bool GetNativeIsBold(THandler handler) =>
			Read<FontAttributes>(handler, "FontAttributes").HasFlag(FontAttributes.Bold);

		protected bool GetNativeIsItalic(THandler handler) =>
			Read<FontAttributes>(handler, "FontAttributes").HasFlag(FontAttributes.Italic);
	}
}
