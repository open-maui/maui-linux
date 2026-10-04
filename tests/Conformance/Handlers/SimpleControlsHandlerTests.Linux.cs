// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

// Linux counterparts of the *.Android.cs / *.Windows.cs partials of MAUI's
// handler tests for the value controls: each helper reads the value back from
// the Skia platform view, the way the other platforms read their native view.
namespace Microsoft.Maui.DeviceTests
{
	public partial class ButtonHandlerTests
	{
		SkiaButton GetNativeButton(ButtonHandler handler) => handler.PlatformView;

		string GetNativeText(ButtonHandler handler) => GetNativeButton(handler).Text;

		Color GetNativeTextColor(ButtonHandler handler) => GetNativeButton(handler).TextColor;

		Thickness GetNativePadding(ButtonHandler handler) => GetNativeButton(handler).Padding;

		double GetNativeCharacterSpacing(ButtonHandler handler) => GetNativeButton(handler).CharacterSpacing;

		Task PerformClick(IButton button) =>
			InvokeOnMainThreadAsync(() => LinuxInput.Click(GetNativeButton(CreateHandler(button))));
	}

	public partial class CheckBoxHandlerTests
	{
		SkiaCheckBox GetNativeCheckBox(CheckBoxHandler handler) => handler.PlatformView;

		bool GetNativeIsChecked(CheckBoxHandler handler) => GetNativeCheckBox(handler).IsChecked;

		Task ValidateColor(ICheckBox checkBoxStub, Color color, Action action = null) =>
			ValidateHasColor(checkBoxStub, color, action);
	}

	public partial class SwitchHandlerTests
	{
		SkiaSwitch GetNativeSwitch(SwitchHandler handler) => handler.PlatformView;

		void SetIsOn(SwitchHandler handler, bool value) => GetNativeSwitch(handler).IsOn = value;

		bool GetNativeIsOn(SwitchHandler handler) => GetNativeSwitch(handler).IsOn;

		Task ValidateTrackColor(ISwitch switchStub, Color color, Action action = null, string updatePropertyValue = null) =>
			ValidateHasColor(switchStub, color, action, updatePropertyValue: updatePropertyValue);

		Task ValidateThumbColor(ISwitch switchStub, Color color, Action action = null, string updatePropertyValue = null) =>
			ValidateHasColor(switchStub, color, action, updatePropertyValue: updatePropertyValue);
	}

	public partial class SliderHandlerTests
	{
		SkiaSlider GetNativeSlider(SliderHandler handler) => handler.PlatformView;

		double GetNativeProgress(SliderHandler handler) => GetNativeSlider(handler).Value;

		double GetNativeMinimum(SliderHandler handler) => GetNativeSlider(handler).Minimum;

		double GetNativeMaximum(SliderHandler handler) => GetNativeSlider(handler).Maximum;
	}

	public partial class StepperHandlerTests
	{
		SkiaStepper GetNativeStepper(StepperHandler handler) => handler.PlatformView;

		double GetPlatformValue(StepperHandler handler) => GetNativeStepper(handler).Value;

		double GetNativeMaximum(StepperHandler handler) => GetNativeStepper(handler).Maximum;

		double GetNativeMinimum(StepperHandler handler) => GetNativeStepper(handler).Minimum;
	}

	public partial class ProgressBarHandlerTests
	{
		SkiaProgressBar GetNativeProgressBar(ProgressBarHandler handler) => handler.PlatformView;

		double GetNativeProgress(ProgressBarHandler handler) => GetNativeProgressBar(handler).Progress;

		Task ValidateNativeProgressColor(IProgress progressBar, Color color, Action action = null) =>
			ValidateHasColor(progressBar, color, action);
	}

	public partial class ActivityIndicatorHandlerTests
	{
		SkiaActivityIndicator GetNativeActivityIndicator(ActivityIndicatorHandler handler) => handler.PlatformView;

		bool GetNativeIsRunning(ActivityIndicatorHandler handler) => GetNativeActivityIndicator(handler).IsRunning;
	}

	public partial class RadioButtonHandlerTests
	{
		SkiaRadioButton GetNativeRadioButton(RadioButtonHandler handler) => handler.PlatformView;

		bool GetNativeIsChecked(RadioButtonHandler handler) => GetNativeRadioButton(handler).IsChecked;
	}
}
