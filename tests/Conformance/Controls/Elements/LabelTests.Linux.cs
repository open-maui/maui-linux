// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of LabelTests.Windows.cs: values read back from SkiaLabel.
	public partial class LabelTests
	{
		SkiaLabel GetPlatformLabel(LabelHandler labelHandler) =>
			labelHandler.PlatformView;

		// SkiaLabel takes MAUI's LineBreakMode as is (wrapping and truncation in one).
		LineBreakMode GetPlatformLineBreakMode(LabelHandler labelHandler) =>
			GetPlatformLabel(labelHandler).LineBreakMode;

		int GetPlatformMaxLines(LabelHandler labelHandler) =>
			GetPlatformLabel(labelHandler).MaxLines;

		Task<float> GetPlatformOpacity(LabelHandler labelHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.Opacity(labelHandler));

		Task<bool> GetPlatformIsVisible(LabelHandler labelHandler) =>
			InvokeOnMainThreadAsync(() => LinuxPlatform.IsVisible(labelHandler));

		// The #else branches the MAUI file lacks (MauiPatches.props routes them here).
		static string LinuxTextForHandler(LabelHandler handler) =>
			LinuxPlatform.LabelText(handler.PlatformView);

		// A null TextColor is the theme's text colour, as a native label's default foreground.
		static Color LinuxTextColor(LabelHandler handler) =>
			handler.PlatformView.TextColor ?? throw LinuxPlatform.NoCounterpart(handler.PlatformView, "resolved default text colour (TextColor is null)");
	}
}

namespace Microsoft.Maui.DeviceTests
{
	public static class LinuxLabelExtensions
	{
		/// <summary>
		/// MAUI's platform LabelExtensions.UpdateLineBreakMode(platformLabel, label)
		/// (Android TextView / iOS UILabel). OpenMaui has no public platform
		/// extension for it; the same update runs through the label handler's
		/// mapper, which is where OpenMaui applies LineBreakMode.
		/// </summary>
		public static void UpdateLineBreakMode(this SkiaLabel platformLabel, Label label) =>
			label.Handler?.UpdateValue(nameof(Label.LineBreakMode));
	}
}
