// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	public partial class PickerHandlerTests
	{
		SkiaPicker GetNativePicker(PickerHandler handler) => handler.PlatformView;

		string GetNativeTitle(PickerHandler handler) => GetNativePicker(handler).Title;

		Color GetNativeTitleColor(PickerHandler handler) => GetNativePicker(handler).TitleColor;

		Color GetNativeTextColor(PickerHandler handler) => GetNativePicker(handler).TextColor;

		double GetNativeCharacterSpacing(PickerHandler handler) => GetNativePicker(handler).CharacterSpacing;

		TextAlignment GetNativeHorizontalTextAlignment(PickerHandler handler) => GetNativePicker(handler).HorizontalTextAlignment;

		TextAlignment GetNativeVerticalTextAlignment(PickerHandler handler) => GetNativePicker(handler).VerticalTextAlignment;
	}

	public partial class DatePickerHandlerTests
	{
		SkiaDatePicker GetNativeDatePicker(DatePickerHandler handler) => handler.PlatformView;

		DateTime? GetNativeDate(DatePickerHandler handler) => GetNativeDatePicker(handler).Date;

		Color GetNativeTextColor(DatePickerHandler handler) => GetNativeDatePicker(handler).TextColor;

		double GetNativeCharacterSpacing(DatePickerHandler handler) => GetNativeDatePicker(handler).CharacterSpacing;
	}

	public partial class TimePickerHandlerTests
	{
		SkiaTimePicker GetNativeTimePicker(TimePickerHandler handler) => handler.PlatformView;

		/// <summary>
		/// The other platforms compare the native control's displayed text with
		/// MAUI's ToFormattedString(); the Skia picker displays Time in Format.
		/// </summary>
		async Task ValidateTime(ITimePicker timePickerStub, Action action = null)
		{
			var actual = await GetValueAsync(timePickerStub, handler =>
			{
				var native = GetNativeTimePicker(handler);
				action?.Invoke();
				return DateTime.Today.Add(native.Time).ToString(string.IsNullOrEmpty(native.Format) ? "t" : native.Format);
			});

			var expected = timePickerStub.ToFormattedString();

			Assert.Equal(expected, actual);
		}

		double GetNativeCharacterSpacing(TimePickerHandler handler) => GetNativeTimePicker(handler).CharacterSpacing;

		Color GetNativeTextColor(TimePickerHandler handler) => GetNativeTimePicker(handler).TextColor;
	}
}
