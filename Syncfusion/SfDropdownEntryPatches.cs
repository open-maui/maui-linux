// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using SfKeyEventArgs = Syncfusion.Maui.Core.Internals.KeyEventArgs;
using SfKeyboardKey = Syncfusion.Maui.Core.Internals.KeyboardKey;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The parts of SfComboBox and SfAutocomplete (Syncfusion's
/// <c>SfDropdownEntry</c> and <c>DropDownListBase</c>) whose Windows bodies
/// the platform-neutral build leaves empty or leaves out:
/// <list type="bullet">
/// <item><c>SfDropdownEntry.OnPreviewKeyDown</c>: Backspace and Delete on
/// multi-selection chips (delete the highlighted chip; Backspace in an empty
/// editable box highlights the last one).</item>
/// <item><c>UpdateInputView</c> / <c>UpdateInputViewWidth</c>: the chip
/// area's minimum width (and margin) follow the control's width when the
/// multi-selection mode or the wrap mode changes.</item>
/// <item><c>SfComboBox_Unfocused</c>: a combo box inside a template closes
/// its drop-down when it loses focus.</item>
/// <item><c>DropDownListBase.OnSizeAllocated</c>: the drop-down is as wide
/// as the control unless DropdownWidth is set, and its content is padded by
/// the stroke.</item>
/// <item><c>Page_Disappearing</c>: navigating away from the page closes the
/// drop-down.</item>
/// <item><c>ListView_PropertyChanged</c>: a non-editable combo box's list
/// scrolls its selected item into view when it opens.</item>
/// <item><c>MoveSelectionToStart</c>: a picked item too long for the box
/// shows from its start.</item>
/// <item><c>SelectionTextHighlightColor</c> (<c>SfInputView.SelectionColor</c>):
/// the text box's selection colour.</item>
/// </list>
/// The controls are optional (Syncfusion.Maui.Inputs), so their types are
/// looked up by name; private members are reached through <see cref="SfMembers"/>.
/// </summary>
internal static class SfDropdownEntryPatches
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly ConditionalWeakTable<object, StrongBox<double>> s_previousWidths = new();
    private static readonly ConditionalWeakTable<object, WeakReference<Page>> s_pages = new();
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var harmony = new Harmony("com.openmaui.syncfusion.dropdown-entry");
            var entry = typeof(SfDropdownEntry);
            Prefix(harmony, entry.GetMethod(nameof(SfDropdownEntry.OnPreviewKeyDown), Any, null, [typeof(SfKeyEventArgs)], null), nameof(OnPreviewKeyDown_Prefix));
            Prefix(harmony, entry.GetMethod("UpdateInputView", Any, null, Type.EmptyTypes, null), nameof(UpdateInputView_Prefix));
            Prefix(harmony, entry.GetMethod("UpdateInputViewWidth", Any, null, Type.EmptyTypes, null), nameof(UpdateInputViewWidth_Prefix));
            foreach (var name in new[] { "IsMultiSelectionDelimiter", "IsMultiSelectionToken", "IsWrapMode" })
            {
                if (entry.GetProperty(name, Any)?.SetMethod is { } setter)
                    harmony.Patch(setter,
                        new HarmonyMethod(typeof(SfDropdownEntryPatches).GetMethod(nameof(ModeSetter_Prefix), BindingFlags.Static | BindingFlags.NonPublic)),
                        new HarmonyMethod(typeof(SfDropdownEntryPatches).GetMethod(nameof(ModeSetter_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
            }

            var comboBox = Type.GetType("Syncfusion.Maui.Inputs.SfComboBox, Syncfusion.Maui.Inputs");
            var autocomplete = Type.GetType("Syncfusion.Maui.Inputs.SfAutocomplete, Syncfusion.Maui.Inputs");
            var listBase = Type.GetType("Syncfusion.Maui.Inputs.DropDownControls.DropDownListBase, Syncfusion.Maui.Inputs");
            if (comboBox != null)
            {
                Prefix(harmony, comboBox.GetMethod("SfComboBox_Unfocused", Any), nameof(ComboBoxUnfocused_Prefix));
                Postfix(harmony, comboBox.GetMethod("UpdateTextFromSelectedItem", Any, null, Type.EmptyTypes, null), nameof(UpdateTextFromSelectedItem_Postfix));
            }
            if (autocomplete != null)
                Postfix(harmony, autocomplete.GetMethod("UpdateTextFromSelectedItem", Any, null, Type.EmptyTypes, null), nameof(UpdateTextFromSelectedItem_Postfix));
            if (listBase != null)
            {
                Postfix(harmony, listBase.GetMethod("OnSizeAllocated", Any, null, [typeof(double), typeof(double)], null), nameof(OnSizeAllocated_Postfix));
                Postfix(harmony, listBase.GetMethod("OnHandlerChanged", Any, null, Type.EmptyTypes, null), nameof(OnHandlerChanged_Postfix));
                Postfix(harmony, listBase.GetMethod("DropDownView_PopupOpened", Any), nameof(PopupOpened_Postfix));
                Postfix(harmony, listBase.GetMethod("UpdateSelectionTextHighlightColor", Any), nameof(SelectionHighlight_Postfix));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfComboBox and SfAutocomplete failed", ex);
        }
    }

    private static void Prefix(Harmony harmony, MethodInfo? original, string prefix)
    {
        if (original != null)
            harmony.Patch(original, new HarmonyMethod(typeof(SfDropdownEntryPatches).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    private static void Postfix(Harmony harmony, MethodInfo? original, string postfix)
    {
        if (original != null)
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(SfDropdownEntryPatches).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    /// <summary>The Windows build's <c>OnPreviewKeyDown</c>: Backspace and Delete on the chips.</summary>
    private static bool OnPreviewKeyDown_Prefix(SfDropdownEntry __instance, SfKeyEventArgs e)
    {
        try
        {
            if (e.Key is not (SfKeyboardKey.Delete or SfKeyboardKey.Back))
            {
                SfMembers.Set(__instance, "IsDeleteButtonClickEnabled", false);
                return false;
            }
            SfMembers.Set(__instance, "IsDeleteButtonClickEnabled", true);
            if ((SfMembers.Get(__instance, "ChipGroup") as SfChipGroup)?.ChipLayout is not { } chipLayout)
                return false;
            var chips = chipLayout.Children;
            bool chipHighlighted = chips.OfType<SfChip>().Any(chip => SfMembers.Get(chip, "IsKeyDown") is true);
            var input = InputOf(__instance);
            if (SfMembers.Get(__instance, "IsEditableMode") is true && !chipHighlighted && input != null)
            {
                SfMembers.Set(__instance, "IsDeleteButtonClickEnabled", false);
                if (input.Text == string.Empty && chips.Count > 1 && e.Key == SfKeyboardKey.Back)
                    SfMembers.Call(__instance, "OnLeftOrRightButtonPressed", string.Empty);
            }
            else
            {
                SfMembers.Call(__instance, "OnDeleteButtonPressed");
            }
            SfInvalidation.InvalidateAll(drawingOnly: false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Deleting a chip failed", ex);
        }
        return false;
    }

    /// <summary>The Windows build's <c>UpdateInputView</c>: the chip area spans the control.</summary>
    private static bool UpdateInputView_Prefix(SfDropdownEntry __instance)
    {
        if (SfMembers.Get(__instance, "isMultiSelection") is true && SfMembers.Get(__instance, "scrollView") is ScrollView scrollView)
        {
            double buttonSpace = SfMembers.Get(__instance, "ButtonSpace") is double space ? space : 0;
            scrollView.MinimumWidthRequest = __instance.Width - (SfMembers.Get(__instance, "IsTextInputLayout") is true ? 0.0 : buttonSpace);
        }
        return false;
    }

    /// <summary>The Windows build's <c>UpdateInputViewWidth</c>: the chip area leaves room for the buttons.</summary>
    private static bool UpdateInputViewWidth_Prefix(SfDropdownEntry __instance)
    {
        if (SfMembers.Get(__instance, "isMultiSelection") is true && SfMembers.Get(__instance, "scrollView") is ScrollView scrollView)
        {
            double buttonSpace = SfMembers.Get(__instance, "ButtonSpace") is double space ? space : 0;
            scrollView.MinimumWidthRequest = __instance.Width - buttonSpace;
            scrollView.Margin = new Thickness(0.0, 0.0, buttonSpace, 0.0);
        }
        return false;
    }

    private static void ModeSetter_Prefix(SfDropdownEntry __instance, MethodBase __originalMethod, out bool __state)
    {
        __state = SfMembers.Get(__instance, __originalMethod.Name.Substring(4)) is true;
    }

    /// <summary>
    /// The Windows setters of IsMultiSelectionDelimiter, IsMultiSelectionToken
    /// (when turned on) and IsWrapMode (on any change) size the chip area once
    /// the control has a width.
    /// </summary>
    private static void ModeSetter_Postfix(SfDropdownEntry __instance, MethodBase __originalMethod, bool value, bool __state)
    {
        if (__state == value)
            return;
        if (!value && __originalMethod.Name != "set_IsWrapMode")
            return;
        if (__instance.Width > 0.0 && !double.IsPositiveInfinity(__instance.Width))
            UpdateInputView_Prefix(__instance);
    }

    /// <summary>The Windows build's <c>SfComboBox_Unfocused</c>.</summary>
    private static bool ComboBoxUnfocused_Prefix(SfDropdownEntry __instance)
    {
        try
        {
            if (SfMembers.Get(__instance, "isWithinTemplate") is true && __instance.IsDropDownOpen
                && SfMembers.Call(__instance, "RaisePopupClosingEvent", false) is true)
                __instance.IsDropDownOpen = false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Closing a templated drop-down failed", ex);
        }
        return false;
    }

    /// <summary>The Windows build's <c>DropDownListBase.OnSizeAllocated</c>.</summary>
    private static void OnSizeAllocated_Postfix(SfDropdownEntry __instance, double width)
    {
        try
        {
            var previous = s_previousWidths.GetValue(__instance, _ => new StrongBox<double>(-1.0));
            if (width != previous.Value && SfMembers.Get(__instance, "IsTextInputLayout") is not true && SfMembers.Get(__instance, "DropDownView") is { } dropDownView
                && SfMembers.Get(__instance, "DropdownWidth") is double dropdownWidth && dropdownWidth == 0.0)
            {
                SfMembers.Set(dropDownView, "PopupWidth", width);
                previous.Value = width;
            }
            if (SfMembers.Get(__instance, "DropDownStrokeThickness") is Thickness stroke
                && SfMembers.Get(__instance, "dropdownContentLayout") is Layout content)
            {
                double right = stroke.Right * 2.0 + (stroke.Right % 2.0 != 0.0 ? 1 : 0);
                content.Padding = new Thickness(0.0, 0.0, right, 0.0);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Sizing a drop-down failed", ex);
        }
    }

    /// <summary>
    /// The Windows build's handler hook-up: the drop-down closes when the page
    /// it is on disappears (<c>Page_Disappearing</c>).
    /// </summary>
    private static void OnHandlerChanged_Postfix(SfDropdownEntry __instance)
    {
        if (s_pages.TryGetValue(__instance, out var previous))
        {
            if (previous.TryGetTarget(out var old))
                old.Disappearing -= OnPageDisappearing;
            s_pages.Remove(__instance);
        }
        if (__instance.Handler == null)
            return;
        SfTextBoxBridge.FollowFlowDirection(__instance, () => InputOf(__instance));
        Element? parent = __instance.Parent;
        while (parent != null && parent is not Page)
            parent = parent.Parent;
        if (parent is Page page)
        {
            page.Disappearing += OnPageDisappearing;
            s_pages.Add(__instance, new WeakReference<Page>(page));
        }
    }

    private static void OnPageDisappearing(object? sender, EventArgs e)
    {
        if (sender is not Page page)
            return;
        foreach (var dropdown in page.GetVisualTreeDescendants().OfType<SfDropdownEntry>())
        {
            if (dropdown.IsDropDownOpen && s_pages.TryGetValue(dropdown, out var reference)
                && reference.TryGetTarget(out var owner) && ReferenceEquals(owner, page))
                dropdown.IsDropDownOpen = false;
        }
    }

    /// <summary>
    /// The Windows build focuses a non-editable combo box's list when the
    /// drop-down opens, and the list's focus change
    /// (<c>ListView_PropertyChanged</c>) scrolls the selected item into view
    /// shortly after.
    /// </summary>
    private static void PopupOpened_Postfix(SfDropdownEntry __instance)
    {
        if (SfMembers.Get(__instance, "IsEditableMode") is true || SfMembers.Get(__instance, "IsMultiSelection") is true)
            return;
        var dispatcher = __instance.Dispatcher;
        void Scroll()
        {
            try
            {
                SfMembers.Call(__instance, "ScrollingAnimation");
                SfInvalidation.InvalidateAll(drawingOnly: false);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Syncfusion", "Scrolling the drop-down to its selection failed", ex);
            }
        }
        if (dispatcher == null || !dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(6), Scroll))
            Scroll();
    }

    /// <summary>
    /// The Windows build's <c>MoveSelectionToStart</c> after a picked item's
    /// text is shown: text wider than the box shows from its start, still
    /// selected when the box is editable.
    /// </summary>
    private static void UpdateTextFromSelectedItem_Postfix(SfDropdownEntry __instance)
    {
        try
        {
            if (SfMembers.Get(__instance, "IsMultiSelection") is true || SfMembers.Get(__instance, "IsFiltering") is true
                || InputOf(__instance) is not { Text: { Length: > 0 } text } input
                || input.Handler?.PlatformView is not SkiaEntry skia)
                return;
            var width = new SkiaTextMeasurer().MeasureText(text, (float)input.FontSize).Width;
            if (width <= input.Width)
                return;
            int length = skia.Selection.Length;
            skia.CursorPosition = 0;
            input.CursorPosition = 0;
            if (!input.IsReadOnly && length > 0)
            {
                skia.SelectionLength = length;
                input.SelectionLength = length;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Showing a long item from its start failed", ex);
        }
    }

    /// <summary>
    /// SelectionTextHighlightColor, which the Windows build hands to the text
    /// box as its selection colour (<c>SfInputView.SelectionColor</c>).
    /// </summary>
    private static void SelectionHighlight_Postfix(object[] __args)
    {
        if (__args.FirstOrDefault(a => a is SfDropdownEntry) is not SfDropdownEntry control
            || SfMembers.Get(control, "SelectionTextHighlightColor") is not Microsoft.Maui.Graphics.Color color
            || InputOf(control) is not { } input)
            return;
        ApplySelectionColor(input, color);
    }

    private static Entry? InputOf(SfDropdownEntry control) => SfMembers.Get(control, "InputView") as Entry;

    internal static void ApplySelectionColor(Entry input, Microsoft.Maui.Graphics.Color color)
    {
        if (input.Handler?.PlatformView is SkiaEntry skia)
        {
            skia.SelectionColor = color;
            return;
        }
        void OnHandlerChanged(object? sender, EventArgs e)
        {
            if (input.Handler?.PlatformView is not SkiaEntry created)
                return;
            input.HandlerChanged -= OnHandlerChanged;
            created.SelectionColor = color;
        }
        input.HandlerChanged += OnHandlerChanged;
    }
}
