// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SfKeyboardKey = Syncfusion.Maui.Core.Internals.KeyboardKey;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The text box of SfNumericEntry and SfMaskedEntry (Syncfusion.Maui.Inputs),
/// as their Windows builds drive it from the native TextBox's events. The
/// platform-neutral build has the editing logic (number insertion, the mask
/// engine) but nothing calls it: typing went into the box unchecked.
/// <list type="bullet">
/// <item>SfNumericEntry (<c>OnTextBoxPreviewKeyDown</c>): Up/Down step by
/// SmallChange and PageUp/PageDown by LargeChange; digits, the decimal
/// separator and the minus sign go through the control's insertion rules and
/// any other character is refused; Backspace and Delete keep the number
/// valid; Enter commits, Escape reverts, Ctrl+Z/Ctrl+Y undo and redo; paste
/// and cut keep the number valid (<c>OnTextBoxPaste</c>,
/// <c>OnTextBoxCuttingToClipboard</c>); the caret cannot go before the minus
/// sign (<c>OnTextBoxSelectionChanging</c>); the wheel steps the value while
/// the box has focus (<c>OnPointerWheelChanged</c>).</item>
/// <item>SfMaskedEntry (<c>WindowEntry_PreviewKeyDown</c>): typed characters
/// go through the mask; Backspace and Delete clear mask positions; End goes
/// to the first prompt; paste and cut go through the mask
/// (<c>OnTextBoxPaste</c>, <c>OnTextBoxCuttingToClipboard</c>); focus selects
/// everything with SelectAllOnFocus (<c>WindowEntry_GettingFocus</c>); the
/// text-changed handling of the native box (<c>OnTextBoxTextChanged</c>).
/// Characters come from the keyboard layout (Shift, Caps Lock and Num Lock
/// applied), which the Windows build rebuilds from virtual keys
/// (<c>GetCharFromKey</c>, <c>IsKeyOn</c>).</item>
/// <item>Hover, for both and for SfComboBox, SfAutocomplete and
/// SfTextInputLayout: entering the text box removes the control's own hover
/// highlight, and the numeric and masked entries go to their PointerOver
/// state while the box is not focused (<c>OnPointerEntered</c>,
/// <c>OnPointerExited</c>, <c>Textbox_PointerEntered</c>).</item>
/// </list>
/// Characters arrive through the text box's text input (layout-correct, and
/// through the input method), keys through the window's key route.
/// </summary>
internal static class SfTextBoxBridge
{
    private const string NumericEntryType = "Syncfusion.Maui.Inputs.SfNumericEntry";
    private const string MaskedEntryType = "Syncfusion.Maui.Inputs.SfMaskedEntry";
    private const string DropdownEntryType = "Syncfusion.Maui.Core.SfDropdownEntry";
    private const string TextInputLayoutType = "Syncfusion.Maui.Core.SfTextInputLayout";

    /// <summary>The clipboard the bridge pastes from and cuts to (a seam for tests).</summary>
    internal static Func<string?> GetClipboardText { get; set; } = SystemClipboard.GetText;

    /// <summary>Writes the clipboard (a seam for tests).</summary>
    internal static Action<string?> SetClipboardText { get; set; } = SystemClipboard.SetText;

    private sealed class Box
    {
        public Box(View control, Entry textBox, bool numeric)
        {
            Control = new WeakReference<View>(control);
            TextBox = new WeakReference<Entry>(textBox);
            Numeric = numeric;
        }

        public WeakReference<View> Control { get; }
        public WeakReference<Entry> TextBox { get; }
        public bool Numeric { get; }

        /// <summary>The text Ctrl+Z replaced, for Ctrl+Y (the Windows build's redoText).</summary>
        public string? RedoText;
        public bool InTextChanged;

        /// <summary>The window whose pointer releases the focused box follows up.</summary>
        public Microsoft.Maui.Platform.Linux.Services.IDisplayWindow? Window;
        public EventHandler<PointerEventArgs>? Released;
        public bool JustFocused;
    }

    private static readonly ConditionalWeakTable<SkiaEntry, Box> s_boxes = new();
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        SkiaView.KeyRouted += OnKeyRouted;
        SkiaView.PointerRoutedAny += OnPointerRouted;
        SkiaView.ScrollRouted += OnScrollRouted;

        try
        {
            var harmony = new Harmony("com.openmaui.syncfusion.textbox");
            var numeric = Type.GetType(NumericEntryType + ", Syncfusion.Maui.Inputs");
            var masked = Type.GetType(MaskedEntryType + ", Syncfusion.Maui.Inputs");
            if (numeric != null)
                Postfix(harmony, numeric.GetMethod("TextBox_HandlerChanged", BindingFlags.Instance | BindingFlags.NonPublic), nameof(NumericHandlerChanged_Postfix));
            if (masked != null)
            {
                Postfix(harmony, masked.GetMethod("TextBox_HandlerChanged", BindingFlags.Instance | BindingFlags.NonPublic), nameof(MaskedHandlerChanged_Postfix));
                Postfix(harmony, masked.GetMethod("MauiEntry_TextChanged", BindingFlags.Instance | BindingFlags.NonPublic), nameof(MaskedTextChanged_Postfix));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching the numeric and masked entries failed", ex);
        }
    }

    private static void Postfix(Harmony harmony, MethodInfo? original, string postfix)
    {
        if (original == null)
            return;
        harmony.Patch(original, postfix: new HarmonyMethod(typeof(SfTextBoxBridge).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    // Runs when the inner text box gets (or loses) its platform view, where
    // the Windows build hooks the TextBox's events.
    private static void NumericHandlerChanged_Postfix(object __instance) => Attach(__instance as View, numeric: true);

    private static void MaskedHandlerChanged_Postfix(object __instance) => Attach(__instance as View, numeric: false);

    /// <summary>Hooks the text box of a numeric or masked entry once it has its Skia view.</summary>
    internal static void Attach(View? control, bool numeric)
    {
        if (control == null || SfMembers.Get(control, "textBox") is not Entry textBox)
            return;
        if (textBox.Handler?.PlatformView is not SkiaEntry skia || s_boxes.TryGetValue(skia, out _))
            return;
        s_boxes.Add(skia, new Box(control, textBox, numeric));
        skia.TextInputting += OnTextInputting;
        skia.Pasting += OnPasting;
        skia.Cutting += OnCutting;
        textBox.Focused += OnTextBoxFocused;
        textBox.Unfocused += OnTextBoxUnfocused;
        FollowFlowDirection(control, () => SfMembers.Get(control, "textBox") as InputView);
    }

    private static readonly ConditionalWeakTable<View, object> s_flowFollowers = new();

    /// <summary>
    /// The Windows builds set the text box's FlowDirection to the control's
    /// (<c>GetParentElement</c> and <c>SetFlowDirection</c>, on handler and
    /// FlowDirection changes): OpenMaui's text views read only their own
    /// FlowDirection, so the control's effective direction is set on the
    /// text box's view now and whenever the control's FlowDirection changes.
    /// </summary>
    internal static void FollowFlowDirection(View control, Func<InputView?> textBox)
    {
        EnsureFlowMapperPatched();
        if (textBox() is { } box)
            s_flowBoxes.AddOrUpdate(box, new WeakReference<View>(control));
        ApplyFlowDirection(control, textBox());
        if (s_flowFollowers.TryGetValue(control, out _))
            return;
        s_flowFollowers.Add(control, true);
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisualElement.FlowDirection))
                ApplyFlowDirection(control, textBox());
        };
    }

    private static readonly ConditionalWeakTable<InputView, WeakReference<View>> s_flowBoxes = new();
    private static int s_flowPatched;

    /// <summary>
    /// The view mapper sets a view's own FlowDirection (MatchParent for the
    /// text box) whenever MAUI maps it again; for a Syncfusion text box the
    /// control's direction is put back after it.
    /// </summary>
    private static void EnsureFlowMapperPatched()
    {
        if (Interlocked.Exchange(ref s_flowPatched, 1) == 1)
            return;
        try
        {
            var mapper = typeof(SkiaView).Assembly.GetType("Microsoft.Maui.Platform.Linux.Handlers.LinuxViewMappers")
                ?.GetMethod("MapFlowDirection", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (mapper != null)
                new Harmony("com.openmaui.syncfusion.textbox-flow").Patch(mapper,
                    postfix: new HarmonyMethod(typeof(SfTextBoxBridge).GetMethod(nameof(MapFlowDirection_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching the flow direction mapper failed", ex);
        }
    }

    private static void MapFlowDirection_Postfix(IViewHandler handler)
    {
        if (handler.VirtualView is InputView box && s_flowBoxes.TryGetValue(box, out var owner) && owner.TryGetTarget(out var control))
            ApplyFlowDirection(control, box);
    }

    private static void ApplyFlowDirection(View control, InputView? textBox)
    {
        if (textBox?.Handler?.PlatformView is not SkiaView view)
            return;
        bool rtl = (((IVisualElementController)control).EffectiveFlowDirection & EffectiveFlowDirection.RightToLeft) != 0;
        view.FlowDirection = rtl ? Microsoft.Maui.FlowDirection.RightToLeft : Microsoft.Maui.FlowDirection.LeftToRight;
        view.Invalidate();
    }

    private static bool TryGet(SkiaEntry skia, out Box box, out View control, out Entry textBox)
    {
        control = null!;
        textBox = null!;
        if (!s_boxes.TryGetValue(skia, out box!))
            return false;
        if (!box.Control.TryGetTarget(out var c) || !box.TextBox.TryGetTarget(out var t))
            return false;
        control = c;
        textBox = t;
        return true;
    }

    /// <summary>
    /// Copies the Skia box's caret and selection to the MAUI Entry the
    /// control's logic reads (the native builds' TextBox reports them live).
    /// </summary>
    private static void SyncSelection(Entry textBox, SkiaEntry skia)
    {
        var (start, length) = skia.Selection;
        var state = skia.SelectionState;
        if (textBox.CursorPosition != start)
            textBox.CursorPosition = start;
        if (textBox.SelectionLength != length)
            textBox.SelectionLength = length;
        // Writing the view's values maps them back to the platform view: keep
        // its own anchor and caret (a Shift+arrow selection keeps extending).
        skia.SelectionState = state;
    }

    #region Keys

    private static void OnKeyRouted(SkiaView focused, SkiaView.RoutedKeyKind kind, KeyEventArgs e)
    {
        if (kind != SkiaView.RoutedKeyKind.PreviewDown || focused is not SkiaEntry skia || !TryGet(skia, out var box, out var control, out var textBox))
            return;
        try
        {
            SyncSelection(textBox, skia);
            if (box.Numeric)
                NumericKey(box, control, textBox, skia, e);
            else
                MaskedKey(control, textBox, e);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Key {e.Key} in {control.GetType().Name} failed", ex);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    /// <summary>The Windows build's <c>OnTextBoxPreviewKeyDown</c> for the keys that are not characters.</summary>
    private static void NumericKey(Box box, View control, Entry textBox, SkiaEntry skia, KeyEventArgs e)
    {
        var key = e.Key;
        bool ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
        bool shift = (e.Modifiers & KeyModifiers.Shift) != 0;
        if (textBox.IsReadOnly && key is not (Key.Up or Key.Down or Key.PageUp or Key.PageDown))
            return;
        string text = textBox.Text ?? string.Empty;
        if (text.Length > 0 && text.Length < textBox.CursorPosition)
            textBox.CursorPosition = text.Length;
        int caret = textBox.CursorPosition;
        var format = SfMembers.Call(control, "GetNumberFormat") as NumberFormatInfo ?? CultureInfo.CurrentCulture.NumberFormat;
        bool isNegative = text.Length > 0 && SfMembers.Call(control, "IsNegative", text, format) is true;
        string separator = SfMembers.Call(control, "GetNumberDecimalSeparator", format) as string ?? format.NumberDecimalSeparator;

        switch (key)
        {
            case Key.Backspace:
                e.Handled = true;
                SfMembers.Call(control, "HandleBackspace", caret, separator);
                break;
            case Key.Delete when !shift:
                e.Handled = true;
                SfMembers.Call(control, "HandleDelete", caret, separator);
                break;
            case Key.Delete:
                // Shift+Delete is the text box's cut.
                e.Handled = true;
                NumericCut(control, textBox, skia);
                break;
            case Key.Insert when shift:
                e.Handled = true;
                NumericPaste(control, textBox, skia);
                break;
            case Key.Insert when ctrl:
                e.Handled = true;
                CopySelection(textBox, skia);
                break;
            case Key.Left or Key.Right or Key.Home or Key.End or Key.Up or Key.Down or Key.PageUp or Key.PageDown:
                e.Handled = SfMembers.Call(control, "HandleNavigation", NavigationKey(key), isNegative, caret) is true;
                break;
            case Key.Enter or Key.NumPadEnter:
                SfMembers.Call(control, "UpdateValue");
                break;
            case Key.Escape:
                SfMembers.Call(control, "UpdateDisplayText", SfMembers.Get(control, "Value"), false);
                break;
            case Key.Z when ctrl:
                SfMembers.Call(control, "UpdateDisplayText", SfMembers.Get(control, "Value"), true);
                box.RedoText = text != textBox.Text ? text : box.RedoText;
                e.Handled = true;
                break;
            case Key.Y when ctrl:
                e.Handled = true;
                if (box.RedoText != null)
                {
                    SfMembers.Call(control, "UpdateDisplayText", SfMembers.Call(control, "Parse", box.RedoText), true);
                    box.RedoText = null;
                }
                break;
        }
    }

    /// <summary>The Windows build's <c>ConvertToKeyboardKey</c>: End maps to None.</summary>
    private static SfKeyboardKey NavigationKey(Key key) => key switch
    {
        Key.PageUp => SfKeyboardKey.PageUp,
        Key.PageDown => SfKeyboardKey.PageDown,
        Key.Home => SfKeyboardKey.Home,
        Key.Left => SfKeyboardKey.Left,
        Key.Up => SfKeyboardKey.Up,
        Key.Right => SfKeyboardKey.Right,
        Key.Down => SfKeyboardKey.Down,
        _ => SfKeyboardKey.None,
    };

    /// <summary>The Windows build's <c>WindowEntry_PreviewKeyDown</c> for the keys that are not characters.</summary>
    private static void MaskedKey(View control, Entry textBox, KeyEventArgs e)
    {
        if (SfMembers.Get(control, "Mask") is not string { Length: > 0 })
            return;
        bool ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
        SfMembers.Set(control, "maskedTextSelectionStart", textBox.CursorPosition);
        string maskedText = SfMembers.Get(control, "maskedText") as string ?? string.Empty;
        switch (e.Key)
        {
            case Key.Delete when maskedText.Length > 0:
                e.Handled = true;
                int caret = textBox.CursorPosition;
                for (int i = caret + (textBox.SelectionLength == 0 ? 1 : textBox.SelectionLength) - 1; i >= caret; i--)
                {
                    SfMembers.Set(control, "maskedTextSelectionStart", i);
                    SfMembers.Call(control, "DeletePressed");
                    textBox.Text = SfMembers.Get(control, "maskedText") as string;
                    textBox.CursorPosition = SfMembers.Get(control, "maskedTextSelectionStart") is int at ? at : i;
                }
                break;
            case Key.Backspace when maskedText.Length > 0:
                e.Handled = true;
                SfMembers.Call(control, "BackSpacePressed");
                break;
            case Key.Y or Key.Z when ctrl:
                e.Handled = true;
                break;
            case Key.End when SfMembers.Get(control, "PromptChar") is char prompt && maskedText.IndexOf(prompt) is var first and > -1:
                textBox.CursorPosition = first;
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Text, paste and cut

    private static void OnTextInputting(object? sender, TextInputEventArgs e)
    {
        if (sender is not SkiaEntry skia || !TryGet(skia, out var box, out var control, out var textBox) || string.IsNullOrEmpty(e.Text))
            return;
        try
        {
            SyncSelection(textBox, skia);
            if (box.Numeric)
            {
                // Every character is the control's: the Windows build handles
                // each key and lets none reach the text box.
                e.Handled = true;
                foreach (char ch in e.Text)
                    NumericCharacter(control, textBox, ch);
            }
            else if (SfMembers.Get(control, "Mask") is string { Length: > 0 })
            {
                e.Handled = true;
                foreach (char ch in e.Text)
                {
                    SfMembers.Set(control, "maskedTextSelectionStart", textBox.CursorPosition);
                    SfMembers.Call(control, "CheckMaskWithTypedChar", ch, false);
                    textBox.Text = SfMembers.Get(control, "maskedText") as string;
                    if (SfMembers.Get(control, "maskedTextSelectionStart") is int caret)
                        textBox.CursorPosition = caret;
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Typing in {control.GetType().Name} failed", ex);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    /// <summary>One typed character in a numeric entry: a digit, the decimal separator or the minus sign.</summary>
    private static void NumericCharacter(View control, Entry textBox, char ch)
    {
        string text = textBox.Text ?? string.Empty;
        int caret = Math.Min(textBox.CursorPosition, text.Length);
        var format = SfMembers.Call(control, "GetNumberFormat") as NumberFormatInfo ?? CultureInfo.CurrentCulture.NumberFormat;
        string separator = SfMembers.Call(control, "GetNumberDecimalSeparator", format) as string ?? format.NumberDecimalSeparator;
        if (ch is >= '0' and <= '9')
        {
            SfMembers.Call(control, "HandleNumbers", ch.ToString(), caret, separator);
        }
        else if (ch.ToString() == separator || ch is '.' or ',')
        {
            // The decimal key, and the '.' and ',' keys, insert the culture's separator.
            bool negative = text.Contains('-');
            if ((!negative && SfMembers.Get(control, "maximumPositiveFractionDigit") is int positive && positive != 0)
                || (negative && SfMembers.Get(control, "maximumNegativeFractionDigit") is int negativeDigits && negativeDigits != 0))
                DecimalSeparator(control, textBox, separator, caret);
        }
        else if (ch == '-' || ch.ToString() == format.NegativeSign)
        {
            bool isNegative = text.Length > 0 && SfMembers.Call(control, "IsNegative", text, format) is true;
            NegativeKey(control, textBox, format, isNegative, caret);
        }
    }

    /// <summary>
    /// The Windows build's <c>HandleDecimalSeparator</c> (the neutral one
    /// expects the platform to have typed the separator already).
    /// </summary>
    private static void DecimalSeparator(View control, Entry textBox, string separator, int caret)
    {
        string text = textBox.Text ?? string.Empty;
        int start = Math.Min(textBox.CursorPosition, text.Length);
        string selected = text.Substring(start, Math.Min(textBox.SelectionLength, text.Length - start));
        if (!text.Contains(separator, StringComparison.CurrentCulture) || selected.Contains(separator, StringComparison.CurrentCulture))
        {
            text = text.Remove(caret, selected.Length).Insert(caret, separator);
            bool onKeyFocus = SfMembers.Get(control, "ValueChangeMode")?.ToString() == "OnKeyFocus";
            textBox.Text = !text.StartsWith(separator, StringComparison.CurrentCulture) || !onKeyFocus ? text : "0" + text;
        }
        if (textBox.SelectionLength >= 0)
            textBox.CursorPosition = (textBox.Text ?? string.Empty).IndexOf(separator, StringComparison.CurrentCulture) + 1;
    }

    /// <summary>The Windows build's <c>HandleNegativeKey</c>.</summary>
    private static void NegativeKey(View control, Entry textBox, NumberFormatInfo format, bool isNegative, int caret)
    {
        double minimum = SfMembers.Get(control, "Minimum") is double min ? min : double.MinValue;
        bool onKeyFocus = SfMembers.Get(control, "ValueChangeMode")?.ToString() == "OnKeyFocus";
        if (minimum >= 0.0 && onKeyFocus)
            return;
        int selectionLength = textBox.SelectionLength;
        string text = textBox.Text ?? string.Empty;
        string sign = SfMembers.Call(control, "GetNegativeSign", format) as string ?? format.NegativeSign;
        int start = Math.Min(textBox.CursorPosition, text.Length);
        if (text == text.Substring(start, Math.Min(selectionLength, text.Length - start)))
        {
            text = text.Remove(caret, Math.Min(selectionLength, text.Length - caret)).Insert(0, sign);
            var args = new object?[] { text, true, format };
            SfMembers.Call(control, "PrefixZeroIfNeeded", args);
            text = args[0] as string ?? text;
            textBox.Text = text;
            textBox.CursorPosition = Math.Min(caret + 2, text.Length);
        }
        else
        {
            textBox.Text = isNegative ? text.Remove(0, 1) : text.Insert(0, sign);
            textBox.CursorPosition = Math.Clamp(caret + (!isNegative ? 1 : -1), 0, (textBox.Text ?? string.Empty).Length);
            textBox.SelectionLength = selectionLength;
        }
    }

    /// <summary>The Windows build's <c>RemoveSelectedText</c> (it also drops a sign the selection leaves alone).</summary>
    private static string RemoveSelectedText(string text, string selected, int caret, string separator)
    {
        bool all = text.Length == selected.Length;
        bool hasSeparator = selected.Contains(separator, StringComparison.CurrentCulture);
        if (all || !hasSeparator)
            text = text.Remove(caret, selected.Length);
        else if (text.Length == selected.Length + 1 && !selected.Contains('-') && text.Contains('-'))
            text = text.Remove(0, selected.Length + 1);
        return text;
    }

    private static void OnPasting(object? sender, HandledEventArgs e)
    {
        if (sender is not SkiaEntry skia || !TryGet(skia, out var box, out var control, out var textBox))
            return;
        try
        {
            SyncSelection(textBox, skia);
            if (box.Numeric)
            {
                e.Handled = true;
                NumericPaste(control, textBox, skia);
            }
            else if (SfMembers.Get(control, "Mask") is string { Length: > 0 })
            {
                e.Handled = true;
                if (GetClipboardText() is { Length: > 0 } clip)
                {
                    SfMembers.Set(control, "maskedTextSelectionStart", textBox.CursorPosition);
                    SfMembers.Call(control, "PasteText", clip);
                    textBox.Text = SfMembers.Get(control, "maskedText") as string;
                    if (SfMembers.Get(control, "maskedTextSelectionStart") is int caret)
                        textBox.CursorPosition = caret;
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Pasting in {control.GetType().Name} failed", ex);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    private static void OnCutting(object? sender, HandledEventArgs e)
    {
        if (sender is not SkiaEntry skia || !TryGet(skia, out var box, out var control, out var textBox))
            return;
        try
        {
            SyncSelection(textBox, skia);
            if (box.Numeric)
            {
                e.Handled = true;
                NumericCut(control, textBox, skia);
            }
            else if (SfMembers.Get(control, "Mask") is string { Length: > 0 })
            {
                // The Windows build's Cut copies the selection, then clears its mask positions.
                e.Handled = true;
                CopySelection(textBox, skia);
                SfMembers.Call(control, "Cut");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Cutting in {control.GetType().Name} failed", ex);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    /// <summary>The Windows build's numeric <c>OnTextBoxPaste</c>.</summary>
    private static void NumericPaste(View control, Entry textBox, SkiaEntry skia)
    {
        if (GetClipboardText() is not { Length: > 0 } clip)
            return;
        var format = SfMembers.Call(control, "GetNumberFormat") as NumberFormatInfo ?? CultureInfo.CurrentCulture.NumberFormat;
        var pasted = SfMembers.Call(control, "ValidatePastedText", clip, format) as string;
        var (start, length) = skia.Selection;
        string text = skia.Text ?? string.Empty;
        string selected = text.Substring(start, length);
        if (pasted != null && SfMembers.Call(control, "CanPaste", pasted) is true)
            SfMembers.Call(control, "InsertNumbers", text, selected, pasted, start);
    }

    /// <summary>The Windows build's numeric <c>OnTextBoxCuttingToClipboard</c>.</summary>
    private static void NumericCut(View control, Entry textBox, SkiaEntry skia)
    {
        CopySelection(textBox, skia);
        var (start, length) = skia.Selection;
        string text = skia.Text ?? string.Empty;
        string selected = text.Substring(start, length);
        if (selected.Length == 0)
            return;
        var format = SfMembers.Call(control, "GetNumberFormat") as NumberFormatInfo ?? CultureInfo.CurrentCulture.NumberFormat;
        string separator = SfMembers.Call(control, "GetNumberDecimalSeparator", format) as string ?? format.NumberDecimalSeparator;
        text = RemoveSelectedText(text, selected, start, separator);
        var args = new object?[] { text, start, false, 0.0 };
        SfMembers.Call(control, "PrefixZeroAndUpdateCursor", args);
        text = args[0] as string ?? text;
        int caret = args[1] is int c ? c : start;
        textBox.SelectionLength = text != textBox.Text ? 0 : textBox.SelectionLength;
        textBox.Text = text;
        textBox.CursorPosition = Math.Clamp(caret, 0, text.Length);
    }

    private static void CopySelection(Entry textBox, SkiaEntry skia)
    {
        if (skia.IsPassword)
            return;
        var (start, length) = skia.Selection;
        if (length > 0)
            SetClipboardText((skia.Text ?? string.Empty).Substring(start, length));
    }

    /// <summary>
    /// Focus on a numeric or masked box. The masked entry's
    /// <c>WindowEntry_GettingFocus</c> puts the caret on the first prompt (or
    /// selects everything with SelectAllOnFocus); WinUI raises it after the
    /// press that focused the box placed the caret, so a click is followed up
    /// on its release. While the box has focus, each release also keeps a
    /// negative number's caret after its sign (<c>OnTextBoxSelectionChanging</c>).
    /// </summary>
    private static void OnTextBoxFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry textBox || textBox.Handler?.PlatformView is not SkiaEntry skia || !s_boxes.TryGetValue(skia, out var box))
            return;
        if (!box.Numeric)
            ApplyMaskedFocus(textBox);
        box.JustFocused = true;
        Unsubscribe(box);
        if (WindowOf(skia)?.DisplayWindow is not { } window)
            return;
        box.Window = window;
        box.Released = (_, _) => OnFocusedBoxReleased(textBox);
        window.PointerReleased += box.Released;
    }

    private static void OnTextBoxUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry { Handler.PlatformView: SkiaEntry skia } && s_boxes.TryGetValue(skia, out var box))
            Unsubscribe(box);
    }

    private static void Unsubscribe(Box box)
    {
        if (box.Window != null && box.Released != null)
            box.Window.PointerReleased -= box.Released;
        box.Window = null;
        box.Released = null;
    }

    /// <summary>A pointer release in the focused box's window, after the window dispatched it.</summary>
    private static void OnFocusedBoxReleased(Entry textBox)
    {
        try
        {
            if (textBox.Handler?.PlatformView is not SkiaEntry skia || !TryGet(skia, out var box, out var control, out _))
                return;
            bool first = box.JustFocused;
            box.JustFocused = false;
            if (box.Numeric)
                KeepCaretAfterSign(control, textBox);
            else if (first)
                ApplyMaskedFocus(textBox);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Following up a press in a text box failed", ex);
        }
    }

    private static WindowContext? WindowOf(SkiaView view)
    {
        if (LinuxApplication.Current is not { } app)
            return null;
        var root = view;
        while (root.Parent != null)
            root = root.Parent;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, root) || context.ModalViews.Contains(root))
                return context;
        }
        return null;
    }

    private static void ApplyMaskedFocus(Entry textBox)
    {
        if (textBox.Handler?.PlatformView is not SkiaEntry skia || !TryGet(skia, out _, out var control, out _))
            return;
        string text = skia.Text ?? string.Empty;
        if (SfMembers.Get(control, "PromptChar") is char prompt && text.IndexOf(prompt) is var first and > -1)
        {
            skia.CursorPosition = first;
            skia.SelectionLength = 0;
            textBox.CursorPosition = first;
        }
        if (SfMembers.Get(control, "SelectAllOnFocus") is true)
            skia.SelectAll();
    }

    /// <summary>
    /// The masked entry's text-changed handling of the Windows build's text box
    /// (<c>OnTextBoxTextChanged</c>): an emptied box shows the prompts again,
    /// the caret goes to the first prompt, and the value follows the text.
    /// </summary>
    private static void MaskedTextChanged_Postfix(object __instance)
    {
        if (__instance is not View control || SfMembers.Get(control, "textBox") is not Entry textBox
            || textBox.Handler?.PlatformView is not SkiaEntry skia || !s_boxes.TryGetValue(skia, out var box) || box.InTextChanged)
            return;
        box.InTextChanged = true;
        try
        {
            string? mask = SfMembers.Get(control, "Mask") as string;
            string? promptText = SfMembers.Get(control, "promptCharText") as string;
            if (mask != null && !string.IsNullOrEmpty(promptText) && string.IsNullOrEmpty(textBox.Text)
                && SfMembers.Get(control, "HidePromptOnLeave") is not true)
            {
                if (SfMembers.Get(control, "maskWrapper") is System.Collections.IList wrapper)
                    wrapper.Clear();
                SfMembers.Call(control, "CreateMaskExpression");
                promptText = SfMembers.Call(control, "SetPromptCharValue") as string;
                SfMembers.Set(control, "promptCharText", promptText);
                SfMembers.Set(control, "maskedText", promptText);
                textBox.Text = promptText;
                return;
            }
            if (mask != null && textBox.Text == promptText && SfMembers.Get(control, "PromptChar") is char prompt
                && textBox.Text?.IndexOf(prompt) is int first and > -1)
                skia.CursorPosition = first;
            SfMembers.Call(control, "SetValueFromText");
            SfMembers.Call(control, "ChangeVisualState");
            SfMembers.Call(control, "SetHiddenText");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Masked entry text change failed", ex);
        }
        finally
        {
            box.InTextChanged = false;
        }
    }

    #endregion

    #region Pointer: hover, wheel, caret

    private static void OnPointerRouted(View view, SkiaView.RoutedPointerKind kind, PointerEventArgs e)
    {
        if (kind is not (SkiaView.RoutedPointerKind.Entered or SkiaView.RoutedPointerKind.Exited))
            return;
        if (view is not InputView input || ControlOf(input) is not { } control)
            return;
        try
        {
            bool entry = SfMembers.Is(control.GetType(), NumericEntryType) || SfMembers.Is(control.GetType(), MaskedEntryType);
            switch (kind)
            {
                case SkiaView.RoutedPointerKind.Entered:
                    RemoveHighlight(control);
                    if (entry && !input.IsFocused)
                        VisualStateManager.GoToState(control, "PointerOver");
                    break;
                case SkiaView.RoutedPointerKind.Exited:
                    if (entry && !input.IsFocused)
                        VisualStateManager.GoToState(control, "Normal");
                    break;

            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Hover on {control.GetType().Name} failed", ex);
        }
    }

    /// <summary>
    /// The control a text box belongs to: the numeric or masked entry, the
    /// combo box or autocomplete, or the text input layout it fills.
    /// </summary>
    private static View? ControlOf(InputView input)
    {
        for (var parent = input.Parent; parent != null; parent = parent.Parent)
        {
            var type = parent.GetType();
            if (SfMembers.Is(type, NumericEntryType) || SfMembers.Is(type, MaskedEntryType) || SfMembers.Is(type, DropdownEntryType))
                return SfMembers.Get(parent, parent is global::Syncfusion.Maui.Core.SfDropdownEntry ? "InputView" : "textBox") == input ? (View)parent : null;
            if (SfMembers.Is(type, TextInputLayoutType))
                return ((global::Syncfusion.Maui.Core.SfTextInputLayout)parent).Content == input ? (View)parent : null;
        }
        return null;
    }

    /// <summary>The control's hover highlight goes when the pointer is over its text box (Windows' PointerEntered handlers).</summary>
    private static void RemoveHighlight(View control)
    {
        if (SfMembers.Get(control, "effectsRenderer") is not { } renderer)
            return;
        if (SfMembers.Get(renderer, "HighlightBounds") is RectF { Width: > 0, Height: > 0 })
        {
            SfMembers.Call(renderer, "RemoveHighlight");
            (control.Handler?.PlatformView as SkiaView)?.Invalidate();
        }
    }

    /// <summary>The Windows build's <c>OnTextBoxSelectionChanging</c>: a negative number's caret stays after the sign.</summary>
    private static void KeepCaretAfterSign(View control, InputView input)
    {
        if (input.Handler?.PlatformView is not SkiaEntry skia || input is not Entry textBox)
            return;
        var format = SfMembers.Call(control, "GetNumberFormat") as NumberFormatInfo ?? CultureInfo.CurrentCulture.NumberFormat;
        if (skia.Selection == (0, 0) && !string.IsNullOrEmpty(skia.Text) && SfMembers.Call(control, "IsNegative", skia.Text, format) is true)
        {
            skia.CursorPosition = 1;
            textBox.CursorPosition = 1;
        }
    }

    /// <summary>The Windows build's <c>OnPointerWheelChanged</c>: the wheel steps a focused numeric entry by SmallChange.</summary>
    private static void OnScrollRouted(View view, ScrollEventArgs e)
    {
        if (e.DeltaY == 0 || view is not Entry { IsFocused: true } textBox || ControlOf(textBox) is not { } control
            || !SfMembers.Is(control.GetType(), NumericEntryType))
            return;
        e.Handled = true;
        try
        {
            double small = SfMembers.Get(control, "SmallChange") is double s ? s : 1;
            // OpenMaui's DeltaY is positive towards the user (Windows' negative wheel delta).
            if (e.DeltaY < 0 && SfMembers.Call(control, "CanIncrease", (object?)null) is true)
            {
                SfMembers.Call(control, "IncreaseDisplayText", (decimal)small);
                SfMembers.Call(control, "UpdateValue");
            }
            else if (e.DeltaY > 0 && SfMembers.Call(control, "CanDecrease", (object?)null) is true)
            {
                SfMembers.Call(control, "IncreaseDisplayText", (decimal)(0.0 - small));
                SfMembers.Call(control, "UpdateValue");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Wheel on a numeric entry failed", ex);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    #endregion
}
