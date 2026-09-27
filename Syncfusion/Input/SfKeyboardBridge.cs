// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Core.Internals;
using SfKeyEventArgs = Syncfusion.Maui.Core.Internals.KeyEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Keyboard input for Syncfusion's keyboard detectors, as the Windows build
/// raises it. A control that listens to the keyboard (SfListView, SfTreeView,
/// SfRating, SfSwitch, the DataGrid, PanZoomListener hosts, ...) keeps a
/// <see cref="KeyboardDetector"/>; the platform-neutral build never subscribes
/// it to key events, and never makes the control a tab stop.
/// <list type="bullet">
/// <item>Focus: pressing such a control focuses its platform view (Windows
/// sets IsTabStop on it), unless focus is already inside it (an Entry in a
/// row keeps it).</item>
/// <item>Keys: WinUI tunnels PreviewKeyDown from the root to the focused
/// element, then bubbles KeyDown and KeyUp back up, and each detector on the
/// route hears it until one marks it handled. The same route is walked over
/// the focused view's MAUI ancestors, around the focused view's own key
/// handling (<see cref="SkiaView.KeyRouted"/>).</item>
/// </list>
/// </summary>
internal static class SfKeyboardBridge
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static readonly BindableProperty? s_keyboardDetectorProperty =
        typeof(KeyboardListenerExtension).GetField("KeyboardDetectorProperty", Any)?.GetValue(null) as BindableProperty;

    private static readonly MethodInfo? s_onKeyAction =
        typeof(KeyboardDetector).GetMethod("OnKeyAction", Any, null, new[] { typeof(SfKeyEventArgs) }, null);

    private static readonly PropertyInfo? s_keyAction = typeof(SfKeyEventArgs).GetProperty("KeyAction", Any);
    private static readonly PropertyInfo? s_isShift = typeof(SfKeyEventArgs).GetProperty(nameof(SfKeyEventArgs.IsShiftKeyPressed), Any);
    private static readonly PropertyInfo? s_isCtrl = typeof(SfKeyEventArgs).GetProperty(nameof(SfKeyEventArgs.IsCtrlKeyPressed), Any);
    private static readonly PropertyInfo? s_isAlt = typeof(SfKeyEventArgs).GetProperty(nameof(SfKeyEventArgs.IsAltKeyPressed), Any);
    private static readonly PropertyInfo? s_isCapsLockOn = typeof(SfKeyEventArgs).GetProperty("IsCapsLockOn", Any);
    private static readonly PropertyInfo? s_isNumLockOn = typeof(SfKeyEventArgs).GetProperty("IsNumLockOn", Any);
    private static readonly PropertyInfo? s_isEntryFocused = typeof(SfKeyEventArgs).GetProperty("IsEntryFocused", Any);
    private static readonly PropertyInfo? s_isEditorFocused = typeof(SfKeyEventArgs).GetProperty("IsEditorFocused", Any);

    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        if (s_keyboardDetectorProperty == null || s_onKeyAction == null || s_keyAction == null)
        {
            DiagnosticLog.Warn("Syncfusion", "This Syncfusion.Maui.Core release lacks the keyboard entry points; keyboard navigation is disabled.");
            return;
        }
        SkiaView.PointerRouted += OnPointerRouted;
        SkiaView.KeyRouted += OnKeyRouted;
    }

    internal static KeyboardDetector? KeyboardDetectorOf(View view) =>
        s_keyboardDetectorProperty == null ? null : view.GetValue(s_keyboardDetectorProperty) as KeyboardDetector;

    private static void OnPointerRouted(View view, SkiaView.RoutedPointerKind kind, PointerEventArgs e)
    {
        if (kind != SkiaView.RoutedPointerKind.Pressed || e.Button != PointerButton.Left)
            return;
        if (KeyboardDetectorOf(view) is not { } detector || !detector.HasListener())
            return;
        if (view.Handler?.PlatformView is not SkiaView { IsVisible: true, IsEnabled: true } skia)
            return;
        // A drop-down's list and text field belong to the combo box, whose
        // text field keeps focus while the list is open.
        if (HasDropdownAncestor(view))
            return;
        if (ContextOf(skia) is not { } context)
            return;
        if (context.FocusedView is { } focused && IsWithin(focused, skia))
            return;
        skia.IsFocusable = true;
        context.FocusedView = skia;
    }

    private static void OnKeyRouted(SkiaView focused, SkiaView.RoutedKeyKind kind, KeyEventArgs e)
    {
        var key = ToKeyboardKey(e.Key);
        if (key == KeyboardKey.None || focused.MauiView is not Element start)
            return;

        var route = new List<(View View, KeyboardDetector Detector)>();
        for (var element = start; element != null; element = element.Parent)
        {
            if (element is View view && KeyboardDetectorOf(view) is { } detector && detector.HasListener())
                route.Add((view, detector));
        }
        if (route.Count == 0)
            return;
        if (kind == SkiaView.RoutedKeyKind.PreviewDown)
            route.Reverse();

        var action = kind switch
        {
            SkiaView.RoutedKeyKind.PreviewDown => 0,
            SkiaView.RoutedKeyKind.Down => 1,
            _ => 2,
        };
        foreach (var (view, detector) in route)
        {
            if (IsOpenDropdownKey(view, key))
                continue;
            var args = CreateArgs(key, action, e, focused);
            try
            {
                s_onKeyAction!.Invoke(detector, new object[] { args });
            }
            catch (TargetInvocationException ex)
            {
                DiagnosticLog.Error("Syncfusion", $"A keyboard listener on {view.GetType().Name} failed", ex.InnerException ?? ex);
            }
            if (args.Handled)
            {
                e.Handled = true;
                break;
            }
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    private static SfKeyEventArgs CreateArgs(KeyboardKey key, int action, KeyEventArgs e, SkiaView focused)
    {
        var args = new SfKeyEventArgs(key);
        s_keyAction!.SetValue(args, Enum.ToObject(s_keyAction.PropertyType, action));
        bool down = action != 2;
        // A modifier's own event reports the state before it (X11, Wayland);
        // Windows reports the state the key leaves, which PanZoomListener
        // tracks to know when ctrl is held.
        s_isShift?.SetValue(args, key == KeyboardKey.Shift ? down : (e.Modifiers & KeyModifiers.Shift) != 0);
        s_isCtrl?.SetValue(args, key == KeyboardKey.Ctrl ? down : (e.Modifiers & KeyModifiers.Control) != 0);
        s_isAlt?.SetValue(args, key == KeyboardKey.Alt ? down : (e.Modifiers & KeyModifiers.Alt) != 0);
        s_isCapsLockOn?.SetValue(args, (e.Modifiers & KeyModifiers.CapsLock) != 0);
        s_isNumLockOn?.SetValue(args, (e.Modifiers & KeyModifiers.NumLock) != 0);
        // As the Apple build reports: a text field that has focus keeps the
        // arrows and editing keys (a list does not move under a row's Entry).
        s_isEntryFocused?.SetValue(args, focused.MauiView is Entry or SearchBar);
        s_isEditorFocused?.SetValue(args, focused.MauiView is Editor);
        return args;
    }

    /// <summary>
    /// The keys an open drop-down's popup takes over (see
    /// SfDropdownPatches): its combo box must not also hear them.
    /// </summary>
    private static bool IsOpenDropdownKey(View view, KeyboardKey key)
    {
        if (key is not (KeyboardKey.Up or KeyboardKey.Down or KeyboardKey.Enter or KeyboardKey.Escape or KeyboardKey.Tab))
            return false;
        for (Element? element = view; element != null; element = element.Parent)
        {
            if (element is SfDropdownEntry dropdown)
                return dropdown.IsDropDownOpen;
        }
        return false;
    }

    private static bool HasDropdownAncestor(View view)
    {
        for (var element = view.Parent; element != null; element = element.Parent)
        {
            if (element is SfDropdownEntry)
                return true;
        }
        return false;
    }

    private static bool IsWithin(SkiaView view, SkiaView ancestor)
    {
        for (var current = view; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    /// <summary>The window whose tree (page or modal layer) holds <paramref name="view"/>.</summary>
    private static WindowContext? ContextOf(SkiaView view)
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

    /// <summary>
    /// The Windows build's <c>ConvertToKeyboardKey</c> table over OpenMaui's
    /// keys. Keys it leaves unmapped (the Windows key, punctuation, Pause,
    /// Menu) are None there too; the keypad digits, which Windows reports as
    /// None, map to the digits so numeric controls take them.
    /// </summary>
    internal static KeyboardKey ToKeyboardKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => KeyboardKey.A + (key - Key.A),
        >= Key.D0 and <= Key.D9 => KeyboardKey.Num0 + (key - Key.D0),
        >= Key.NumPad0 and <= Key.NumPad9 => KeyboardKey.Num0 + (key - Key.NumPad0),
        >= Key.F1 and <= Key.F12 => KeyboardKey.F1 + (key - Key.F1),
        Key.NumPadMultiply => KeyboardKey.Multiply,
        Key.NumPadAdd => KeyboardKey.Add,
        Key.NumPadSubtract => KeyboardKey.Subtract,
        Key.NumPadDecimal => KeyboardKey.Decimal,
        Key.NumPadDivide => KeyboardKey.Divide,
        Key.NumPadEnter or Key.Enter => KeyboardKey.Enter,
        Key.Left => KeyboardKey.Left,
        Key.Up => KeyboardKey.Up,
        Key.Right => KeyboardKey.Right,
        Key.Down => KeyboardKey.Down,
        Key.Home => KeyboardKey.Home,
        Key.End => KeyboardKey.End,
        Key.PageUp => KeyboardKey.PageUp,
        Key.PageDown => KeyboardKey.PageDown,
        Key.Insert => KeyboardKey.Insert,
        Key.Delete => KeyboardKey.Delete,
        Key.Shift => KeyboardKey.Shift,
        Key.Control => KeyboardKey.Ctrl,
        Key.Alt => KeyboardKey.Alt,
        Key.CapsLock => KeyboardKey.CapsLock,
        Key.NumLock => KeyboardKey.NumLock,
        Key.ScrollLock => KeyboardKey.ScrollLock,
        Key.Backspace => KeyboardKey.Back,
        Key.Tab => KeyboardKey.Tab,
        Key.Escape => KeyboardKey.Escape,
        Key.Space => KeyboardKey.Space,
        Key.PrintScreen => KeyboardKey.Print,
        _ => KeyboardKey.None,
    };
}
