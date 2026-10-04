// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfTextInputLayout draws the field (outline or filled box, hint, icons)
/// around its content, so the content's own box must go: the Windows build's
/// <c>TextInputView_HandlerChanged</c> takes the inner TextBox's border,
/// background and padding away (and those of a ComboBox, CalendarDatePicker
/// or TimePicker content). The platform-neutral build leaves the handler
/// empty, so on Linux an Entry inside a text input layout drew its own white
/// box and border inside the layout's. The same is done for OpenMaui's
/// views: the entry and editor lose their background, border and padding;
/// the pickers their background and border.
/// </summary>
internal static class SfTextInputLayoutPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var original = typeof(SfTextInputLayout).GetMethod("TextInputView_HandlerChanged",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(object), typeof(EventArgs)], null);
            if (original == null)
            {
                DiagnosticLog.Warn("Syncfusion", "This Syncfusion.Maui.Core release lacks SfTextInputLayout.TextInputView_HandlerChanged; the inner field keeps its own box.");
                return;
            }
            new Harmony("com.openmaui.syncfusion.text-input-layout").Patch(original,
                postfix: new HarmonyMethod(typeof(SfTextInputLayoutPatches).GetMethod(nameof(HandlerChanged_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfTextInputLayout failed", ex);
        }
    }

    private static void HandlerChanged_Postfix(object? sender)
    {
        try
        {
            if (sender is VisualElement { Handler.PlatformView: SkiaView view })
                StripChrome(view);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Removing the text input layout content's box failed", ex);
        }
    }

    /// <summary>Removes the box a text field draws around itself (Windows: border, background and padding).</summary>
    internal static void StripChrome(SkiaView view)
    {
        switch (view)
        {
            case SkiaEntry entry:
                entry.EntryBackgroundColor = Colors.Transparent;
                entry.BackgroundColor = Colors.Transparent;
                entry.BorderWidth = 0;
                entry.Padding = new Thickness(0);
                break;
            case SkiaEditor editor:
                editor.EditorBackgroundColor = Colors.Transparent;
                editor.BackgroundColor = Colors.Transparent;
                editor.BorderColor = Colors.Transparent;
                editor.Padding = new Thickness(0);
                break;
            case SkiaPicker picker:
                picker.BackgroundColor = Colors.Transparent;
                picker.BorderColor = Colors.Transparent;
                break;
            case SkiaDatePicker datePicker:
                datePicker.BackgroundColor = Colors.Transparent;
                datePicker.BorderColor = Colors.Transparent;
                break;
            case SkiaTimePicker timePicker:
                timePicker.BackgroundColor = Colors.Transparent;
                timePicker.BorderColor = Colors.Transparent;
                break;
        }
        view.Invalidate();
    }
}
