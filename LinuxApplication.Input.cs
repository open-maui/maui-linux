// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux;

public partial class LinuxApplication
{
    /// <summary>
    /// Converts physical pixel coordinates to logical pixel coordinates for HiDPI support.
    /// </summary>
    private float ToLogical(double physicalCoord) => (float)(physicalCoord / DpiScale);

    /// <summary>
    /// Creates a new PointerEventArgs with coordinates scaled to logical pixels.
    /// </summary>
    private PointerEventArgs ScalePointerArgs(PointerEventArgs e)
    {
        if (DpiScale <= 1.0f) return e;
        return new PointerEventArgs(ToLogical(e.X), ToLogical(e.Y), e.Button);
    }

    /// <summary>
    /// Creates a new ScrollEventArgs with coordinates scaled to logical pixels.
    /// </summary>
    private ScrollEventArgs ScaleScrollArgs(ScrollEventArgs e)
    {
        if (DpiScale <= 1.0f) return e;
        return new ScrollEventArgs(ToLogical(e.X), ToLogical(e.Y), e.DeltaX, e.DeltaY);
    }

    private void UpdateAnimations()
    {
        // Update cursor blink for text input controls
        if (_focusedView is SkiaEntry entry)
        {
            entry.UpdateCursorBlink();
        }
        else if (_focusedView is SkiaEditor editor)
        {
            editor.UpdateCursorBlink();
        }
    }

    private void OnWindowResized(object? sender, (int Width, int Height) size)
    {
        if (_rootView != null)
        {
            // Re-measure with new available size, then arrange
            var availableSize = new Microsoft.Maui.Graphics.Size(size.Width, size.Height);
            _rootView.Measure(availableSize);
            _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, size.Width, size.Height));
        }
        _renderingEngine?.InvalidateAll();
    }

    private void OnWindowExposed(object? sender, EventArgs e)
    {
        Render();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnKeyDown(e);
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnKeyDown(e);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnKeyUp(e);
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnKeyUp(e);
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_focusedView != null)
        {
            _focusedView.OnTextInput(e);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        e = ScalePointerArgs(e);

        // Route to context menu if one is active
        if (LinuxDialogService.HasContextMenu)
        {
            LinuxDialogService.ActiveContextMenu?.OnPointerMoved(e);
            return;
        }

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnPointerMoved(e);
            return;
        }

        if (_rootView != null)
        {
            // If a view has captured the pointer, send all events to it
            if (_capturedView != null)
            {
                _capturedView.OnPointerMoved(e);
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y);
            var hitView = popupOwner ?? _rootView.HitTest(e.X, e.Y);

            // Track hover state changes
            if (hitView != _hoveredView)
            {
                _hoveredView?.OnPointerExited(e);
                _hoveredView = hitView;
                _hoveredView?.OnPointerEntered(e);

                // Update cursor based on view's cursor type
                CursorType cursor = hitView?.CursorType ?? CursorType.Arrow;
                _mainWindow?.SetCursor(cursor);
            }

            hitView?.OnPointerMoved(e);
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        e = ScalePointerArgs(e);
        DiagnosticLog.Debug("LinuxApplication", $"OnPointerPressed at ({e.X}, {e.Y}), Button={e.Button}");

        // Route to context menu if one is active
        if (LinuxDialogService.HasContextMenu)
        {
            LinuxDialogService.ActiveContextMenu?.OnPointerPressed(e);
            return;
        }

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnPointerPressed(e);
            return;
        }

        if (_rootView != null)
        {
            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y);
            var hitView = popupOwner ?? _rootView.HitTest(e.X, e.Y);
            DiagnosticLog.Debug("LinuxApplication", $"HitView: {hitView?.GetType().Name ?? "null"}, rootView: {_rootView.GetType().Name}");

            if (hitView != null)
            {
                // Capture pointer to this view for drag operations
                _capturedView = hitView;

                // Update focus
                if (hitView.IsFocusable)
                {
                    FocusedView = hitView;
                }

                DiagnosticLog.Debug("LinuxApplication", $"Calling OnPointerPressed on {hitView.GetType().Name}");
                hitView.OnPointerPressed(e);
            }
            else
            {
                // Close any open popups when clicking outside
                if (SkiaView.HasActivePopup && _focusedView != null)
                {
                    _focusedView.OnFocusLost();
                }
                FocusedView = null;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        e = ScalePointerArgs(e);
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnPointerReleased(e);
            return;
        }

        if (_rootView != null)
        {
            // If a view has captured the pointer, send release to it
            if (_capturedView != null)
            {
                _capturedView.OnPointerReleased(e);
                _capturedView = null; // Release capture
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y);
            var hitView = popupOwner ?? _rootView.HitTest(e.X, e.Y);
            hitView?.OnPointerReleased(e);
        }
    }

    private void OnScroll(object? sender, ScrollEventArgs e)
    {
        e = ScaleScrollArgs(e);
        DiagnosticLog.Debug("LinuxApplication", $"OnScroll - X={e.X}, Y={e.Y}, DeltaX={e.DeltaX}, DeltaY={e.DeltaY}");
        if (_rootView != null)
        {
            var hitView = _rootView.HitTest(e.X, e.Y);
            DiagnosticLog.Debug("LinuxApplication", $"HitView: {hitView?.GetType().Name ?? "null"}");
            // Bubble scroll events up to find a ScrollView
            var view = hitView;
            while (view != null)
            {
                DiagnosticLog.Debug("LinuxApplication", $"Bubbling to: {view.GetType().Name}");
                if (view is SkiaScrollView scrollView)
                {
                    scrollView.OnScroll(e);
                    return;
                }
                view.OnScroll(e);
                if (e.Handled) return;
                view = view.Parent;
            }
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        _mainWindow?.Stop();
    }

    // GTK Event Handlers
    private void OnGtkDrawRequested(object? sender, EventArgs e)
    {
        DiagnosticLog.Debug("LinuxApplication", ">>> OnGtkDrawRequested ENTER");
        LogDraw();
        var surface = _gtkWindow?.SkiaSurface;
        if (surface?.Canvas != null && _rootView != null)
        {
            var bgColor = Application.Current?.UserAppTheme == AppTheme.Dark
                ? new SKColor(32, 33, 36)
                : SKColors.White;
            surface.Canvas.Clear(bgColor);

            // Apply DPI scaling for HiDPI displays
            surface.Canvas.Save();
            if (DpiScale > 1.0f)
            {
                surface.Canvas.Scale(DpiScale);
            }

            DiagnosticLog.Debug("LinuxApplication", "Drawing rootView...");
            _rootView.Draw(surface.Canvas);

            surface.Canvas.Restore();

            DiagnosticLog.Debug("LinuxApplication", "Drawing dialogs...");
            var bounds = new SKRect(0, 0, surface.Width, surface.Height);
            LinuxDialogService.DrawDialogs(surface.Canvas, bounds);
            DiagnosticLog.Debug("LinuxApplication", "<<< OnGtkDrawRequested EXIT");
        }
    }

    private void OnGtkResized(object? sender, (int Width, int Height) size)
    {
        PerformGtkLayout(size.Width, size.Height);
        _gtkWindow?.RequestRedraw();
    }

    private void OnGtkPointerPressed(object? sender, (double X, double Y, int Button) e)
    {
        // Convert physical to logical coordinates for HiDPI
        float lx = ToLogical(e.X), ly = ToLogical(e.Y);
        string buttonName = e.Button == 1 ? "Left" : e.Button == 2 ? "Middle" : e.Button == 3 ? "Right" : $"Unknown({e.Button})";
        DiagnosticLog.Debug("LinuxApplication", $"GTK PointerPressed at ({lx:F1}, {ly:F1}), Button={e.Button} ({buttonName})");

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs(lx, ly, button);
            LinuxDialogService.TopDialog?.OnPointerPressed(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (LinuxDialogService.HasContextMenu)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs(lx, ly, button);
            LinuxDialogService.ActiveContextMenu?.OnPointerPressed(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_rootView == null)
        {
            DiagnosticLog.Warn("LinuxApplication", "GTK _rootView is null!");
            return;
        }

        var hitView = _rootView.HitTest(lx, ly);
        DiagnosticLog.Debug("LinuxApplication", $"GTK HitView: {hitView?.GetType().Name ?? "null"}");

        if (hitView != null)
        {
            if (hitView.IsFocusable && _focusedView != hitView)
            {
                _focusedView?.OnFocusLost();
                _focusedView = hitView;
                _focusedView.OnFocusGained();
            }
            _capturedView = hitView;
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs(lx, ly, button);
            DiagnosticLog.Debug("LinuxApplication", ">>> Before OnPointerPressed");
            hitView.OnPointerPressed(args);
            DiagnosticLog.Debug("LinuxApplication", "<<< After OnPointerPressed, calling RequestRedraw");
            _gtkWindow?.RequestRedraw();
            DiagnosticLog.Debug("LinuxApplication", "<<< After RequestRedraw, returning from handler");
        }
    }

    private void OnGtkPointerReleased(object? sender, (double X, double Y, int Button) e)
    {
        float lx = ToLogical(e.X), ly = ToLogical(e.Y);
        DiagnosticLog.Debug("LinuxApplication", ">>> OnGtkPointerReleased ENTER");

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs(lx, ly, button);
            LinuxDialogService.TopDialog?.OnPointerReleased(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_rootView == null) return;

        if (_capturedView != null)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs(lx, ly, button);
            DiagnosticLog.Debug("LinuxApplication", $"Calling OnPointerReleased on {_capturedView.GetType().Name}");
            _capturedView.OnPointerReleased(args);
            DiagnosticLog.Debug("LinuxApplication", "OnPointerReleased returned");
            _capturedView = null;
            _gtkWindow?.RequestRedraw();
            DiagnosticLog.Debug("LinuxApplication", "<<< OnGtkPointerReleased EXIT (captured path)");
        }
        else
        {
            var hitView = _rootView.HitTest(lx, ly);
            if (hitView != null)
            {
                var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
                var args = new PointerEventArgs(lx, ly, button);
                hitView.OnPointerReleased(args);
                _gtkWindow?.RequestRedraw();
            }
        }
    }

    private void OnGtkPointerMoved(object? sender, (double X, double Y) e)
    {
        float lx = ToLogical(e.X), ly = ToLogical(e.Y);

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            var args = new PointerEventArgs(lx, ly);
            LinuxDialogService.TopDialog?.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (LinuxDialogService.HasContextMenu)
        {
            var args = new PointerEventArgs(lx, ly);
            LinuxDialogService.ActiveContextMenu?.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_rootView == null) return;

        if (_capturedView != null)
        {
            var args = new PointerEventArgs(lx, ly);
            _capturedView.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        var hitView = _rootView.HitTest(lx, ly);
        if (hitView != _hoveredView)
        {
            var args = new PointerEventArgs(lx, ly);
            _hoveredView?.OnPointerExited(args);
            _hoveredView = hitView;
            _hoveredView?.OnPointerEntered(args);
            _gtkWindow?.RequestRedraw();
        }

        if (hitView != null)
        {
            var args = new PointerEventArgs(lx, ly);
            hitView.OnPointerMoved(args);
        }
    }

    private void OnGtkKeyPressed(object? sender, (uint KeyVal, uint KeyCode, uint State) e)
    {
        var key = ConvertGdkKey(e.KeyVal);
        var modifiers = ConvertGdkModifiers(e.State);
        var args = new KeyEventArgs(key, modifiers);

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnKeyDown(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnKeyDown(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private void OnGtkKeyReleased(object? sender, (uint KeyVal, uint KeyCode, uint State) e)
    {
        var key = ConvertGdkKey(e.KeyVal);
        var modifiers = ConvertGdkModifiers(e.State);
        var args = new KeyEventArgs(key, modifiers);

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            LinuxDialogService.TopDialog?.OnKeyUp(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnKeyUp(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private void OnGtkScrolled(object? sender, (double X, double Y, double DeltaX, double DeltaY, uint State) e)
    {
        if (_rootView == null) return;

        // Convert GDK state to KeyModifiers
        var modifiers = ConvertGdkStateToModifiers(e.State);
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0;

        var hitView = _rootView.HitTest((float)e.X, (float)e.Y);

        // Check for pinch gesture (Ctrl+Scroll) first
        if (isCtrlPressed && hitView?.MauiView != null)
        {
            if (Handlers.GestureManager.ProcessScrollAsPinch(hitView.MauiView, e.X, e.Y, e.DeltaY, true))
            {
                _gtkWindow?.RequestRedraw();
                return;
            }
        }

        while (hitView != null)
        {
            if (hitView is SkiaScrollView scrollView)
            {
                var args = new ScrollEventArgs((float)e.X, (float)e.Y, (float)e.DeltaX, (float)e.DeltaY, modifiers);
                scrollView.OnScroll(args);
                _gtkWindow?.RequestRedraw();
                break;
            }
            hitView = hitView.Parent;
        }
    }

    private static KeyModifiers ConvertGdkStateToModifiers(uint state)
    {
        var modifiers = KeyModifiers.None;
        // GDK modifier masks
        const uint GDK_SHIFT_MASK = 1 << 0;
        const uint GDK_CONTROL_MASK = 1 << 2;
        const uint GDK_MOD1_MASK = 1 << 3;  // Alt
        const uint GDK_SUPER_MASK = 1 << 26;
        const uint GDK_LOCK_MASK = 1 << 1;  // Caps Lock

        if ((state & GDK_SHIFT_MASK) != 0) modifiers |= KeyModifiers.Shift;
        if ((state & GDK_CONTROL_MASK) != 0) modifiers |= KeyModifiers.Control;
        if ((state & GDK_MOD1_MASK) != 0) modifiers |= KeyModifiers.Alt;
        if ((state & GDK_SUPER_MASK) != 0) modifiers |= KeyModifiers.Super;
        if ((state & GDK_LOCK_MASK) != 0) modifiers |= KeyModifiers.CapsLock;

        return modifiers;
    }

    private void OnGtkTextInput(object? sender, string text)
    {
        if (_focusedView != null)
        {
            var args = new TextInputEventArgs(text);
            _focusedView.OnTextInput(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private static Key ConvertGdkKey(uint keyval)
    {
        return keyval switch
        {
            65288 => Key.Backspace,
            65289 => Key.Tab,
            65293 => Key.Enter,
            65307 => Key.Escape,
            65360 => Key.Home,
            65361 => Key.Left,
            65362 => Key.Up,
            65363 => Key.Right,
            65364 => Key.Down,
            65365 => Key.PageUp,
            65366 => Key.PageDown,
            65367 => Key.End,
            65535 => Key.Delete,
            >= 32 and <= 126 => (Key)keyval,
            _ => Key.Unknown
        };
    }

    private static KeyModifiers ConvertGdkModifiers(uint state)
    {
        var modifiers = KeyModifiers.None;
        if ((state & 1) != 0) modifiers |= KeyModifiers.Shift;
        if ((state & 4) != 0) modifiers |= KeyModifiers.Control;
        if ((state & 8) != 0) modifiers |= KeyModifiers.Alt;
        return modifiers;
    }
}
