// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit.Sdk;

// Linux counterparts of the Entry / Editor / SearchBar *.Windows.cs partials.
// Where the Skia view has no counterpart of the native property MAUI checks,
// the helper fails with "exposes no X" rather than inventing a value, so the
// test reports the missing mapping instead of passing by accident.
namespace Microsoft.Maui.DeviceTests
{
	static class Missing
	{
		public static XunitException Property(object view, string property) =>
			new($"{view.GetType().Name} exposes no {property}: the handler has nowhere to map it on Linux.");
	}

	public partial class EntryHandlerTests
	{
		static SkiaEntry GetNativeEntry(EntryHandler handler) => handler.PlatformView;

		static string GetNativeText(EntryHandler handler) => GetNativeEntry(handler).Text;

		internal static void SetNativeText(EntryHandler handler, string text) => GetNativeEntry(handler).Text = text;

		internal static int GetCursorStartPosition(EntryHandler handler) => GetNativeEntry(handler).CursorPosition;

		internal static void UpdateCursorStartPosition(EntryHandler handler, int position) => GetNativeEntry(handler).CursorPosition = position;

		double GetNativeCharacterSpacing(EntryHandler handler) => GetNativeEntry(handler).CharacterSpacing;

		Color GetNativeTextColor(EntryHandler handler) => GetNativeEntry(handler).TextColor;

		bool GetNativeIsPassword(EntryHandler handler) => GetNativeEntry(handler).IsPassword;

		string GetNativePlaceholder(EntryHandler handler) => GetNativeEntry(handler).Placeholder;

		bool GetNativeIsTextPredictionEnabled(EntryHandler handler) => GetNativeEntry(handler).IsTextPredictionEnabled;

		bool GetNativeIsSpellCheckEnabled(EntryHandler handler) => GetNativeEntry(handler).IsSpellCheckEnabled;

		bool GetNativeIsReadOnly(EntryHandler handler) => GetNativeEntry(handler).IsReadOnly;

		bool GetNativeIsNumericKeyboard(EntryHandler handler) => GetNativeEntry(handler).Keyboard == Keyboard.Numeric;

		bool GetNativeIsEmailKeyboard(EntryHandler handler) => GetNativeEntry(handler).Keyboard == Keyboard.Email;

		bool GetNativeIsTelephoneKeyboard(EntryHandler handler) => GetNativeEntry(handler).Keyboard == Keyboard.Telephone;

		bool GetNativeIsUrlKeyboard(EntryHandler handler) => GetNativeEntry(handler).Keyboard == Keyboard.Url;

		bool GetNativeIsTextKeyboard(EntryHandler handler) => GetNativeEntry(handler).Keyboard == Keyboard.Text;

		bool GetNativeIsChatKeyboard(EntryHandler handler) => GetNativeEntry(handler).Keyboard == Keyboard.Chat;

		bool GetNativeClearButtonVisibility(EntryHandler handler) =>
			// The two settings SkiaEntry draws the clear button from (the handler writes ShowClearButton).
			GetNativeEntry(handler).ShowClearButton || GetNativeEntry(handler).ClearButtonVisibility == ClearButtonVisibility.WhileEditing;

		TextAlignment GetNativeHorizontalTextAlignment(EntryHandler handler) => GetNativeEntry(handler).HorizontalTextAlignment;

		TextAlignment GetNativeVerticalTextAlignment(EntryHandler handler) => GetNativeEntry(handler).VerticalTextAlignment;

		TextAlignment GetNativeVerticalTextAlignment(TextAlignment textAlignment) => textAlignment;

		int GetNativeCursorPosition(EntryHandler handler) => GetNativeEntry(handler).CursorPosition;

		int GetNativeSelectionLength(EntryHandler handler) => GetNativeEntry(handler).SelectionLength;
	}

	public partial class EditorHandlerTests
	{
		static SkiaEditor GetNativeEditor(EditorHandler handler) => handler.PlatformView;

		string GetNativeText(EditorHandler handler) => GetNativeEditor(handler).Text;

		internal static void SetNativeText(EditorHandler handler, string text) => GetNativeEditor(handler).Text = text;

		internal static int GetCursorStartPosition(EditorHandler handler) => GetNativeEditor(handler).CursorPosition;

		internal static void UpdateCursorStartPosition(EditorHandler handler, int position) => GetNativeEditor(handler).CursorPosition = position;

		string GetNativePlaceholderText(EditorHandler handler) => GetNativeEditor(handler).Placeholder;

		Color GetNativePlaceholderColor(EditorHandler handler) => GetNativeEditor(handler).PlaceholderColor;

		bool GetNativeIsReadOnly(EditorHandler handler) => GetNativeEditor(handler).IsReadOnly;

		bool GetNativeIsTextPredictionEnabled(EditorHandler handler) => GetNativeEditor(handler).IsTextPredictionEnabled;

		bool GetNativeIsSpellCheckEnabled(EditorHandler handler) => GetNativeEditor(handler).IsSpellCheckEnabled;

		Color GetNativeTextColor(EditorHandler handler) => GetNativeEditor(handler).TextColor;

		TextAlignment GetNativeVerticalTextAlignment(EditorHandler handler) => GetNativeEditor(handler).VerticalTextAlignment;

		TextAlignment GetNativeVerticalTextAlignment(TextAlignment textAlignment) => textAlignment;

		bool GetNativeIsNumericKeyboard(EditorHandler handler) => GetNativeEditor(handler).Keyboard == Keyboard.Numeric;

		bool GetNativeIsEmailKeyboard(EditorHandler handler) => GetNativeEditor(handler).Keyboard == Keyboard.Email;

		bool GetNativeIsTelephoneKeyboard(EditorHandler handler) => GetNativeEditor(handler).Keyboard == Keyboard.Telephone;

		bool GetNativeIsUrlKeyboard(EditorHandler handler) => GetNativeEditor(handler).Keyboard == Keyboard.Url;

		bool GetNativeIsTextKeyboard(EditorHandler handler) => GetNativeEditor(handler).Keyboard == Keyboard.Text;

		bool GetNativeIsChatKeyboard(EditorHandler handler) => GetNativeEditor(handler).Keyboard == Keyboard.Chat;

		int GetNativeCursorPosition(EditorHandler handler) => GetNativeEditor(handler).CursorPosition;

		int GetNativeSelectionLength(EditorHandler handler) => GetNativeEditor(handler).SelectionLength;
	}

	public partial class SearchBarHandlerTests
	{
		static SkiaSearchBar GetNativeSearchBar(SearchBarHandler handler) => handler.PlatformView;

		string GetNativeText(SearchBarHandler handler) => GetNativeSearchBar(handler).Text;

		static void SetNativeText(SearchBarHandler handler, string value) => GetNativeSearchBar(handler).Text = value;

		static int GetCursorStartPosition(SearchBarHandler handler) => GetNativeSearchBar(handler).CursorPosition;

		static void UpdateCursorStartPosition(SearchBarHandler handler, int position) => GetNativeSearchBar(handler).CursorPosition = position;

		Color GetNativeTextColor(SearchBarHandler handler) => GetNativeSearchBar(handler).TextColor;

		string GetNativePlaceholder(SearchBarHandler handler) => GetNativeSearchBar(handler).Placeholder;

		double GetInputFieldHeight(SearchBarHandler handler) => GetNativeSearchBar(handler).Bounds.Height;

		bool GetNativeIsTextPredictionEnabled(SearchBarHandler handler) => GetNativeSearchBar(handler).IsTextPredictionEnabled;

		bool GetNativeIsSpellCheckEnabled(SearchBarHandler handler) => GetNativeSearchBar(handler).IsSpellCheckEnabled;

		bool GetNativeIsReadOnly(SearchBarHandler handler) => GetNativeSearchBar(handler).IsReadOnly;

		Color GetNativeCancelButtonColor(SearchBarHandler handler) => GetNativeSearchBar(handler).ClearButtonColor;

		double GetNativeCharacterSpacing(SearchBarHandler handler) => GetNativeSearchBar(handler).CharacterSpacing;

		TextAlignment GetNativeHorizontalTextAlignment(SearchBarHandler handler) => GetNativeSearchBar(handler).HorizontalTextAlignment;

		bool GetNativeIsNumericKeyboard(SearchBarHandler handler) => GetNativeSearchBar(handler).Keyboard == Keyboard.Numeric;

		bool GetNativeIsEmailKeyboard(SearchBarHandler handler) => GetNativeSearchBar(handler).Keyboard == Keyboard.Email;

		bool GetNativeIsTelephoneKeyboard(SearchBarHandler handler) => GetNativeSearchBar(handler).Keyboard == Keyboard.Telephone;

		bool GetNativeIsUrlKeyboard(SearchBarHandler handler) => GetNativeSearchBar(handler).Keyboard == Keyboard.Url;

		bool GetNativeIsTextKeyboard(SearchBarHandler handler) => GetNativeSearchBar(handler).Keyboard == Keyboard.Text;

		bool GetNativeIsChatKeyboard(SearchBarHandler handler) => GetNativeSearchBar(handler).Keyboard == Keyboard.Chat;
	}
}
