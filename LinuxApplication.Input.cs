// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Diagnostics;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux;

// NOTE (multi-window): the per-window X11/Wayland input routing that used to
// live here (OnKeyDown/OnPointerMoved/... with the _focusedView/_hoveredView/
// _capturedView singletons) moved to WindowContext so each native window
// routes its own input into its own tree with its own focus/capture/hover
// state — all still through the Guarded wrapper (hard invariant: a view
// exception must never unwind into a native callback). This file keeps the
// GTK-mode handlers (GTK stays single-window; they operate on the primary
// context's state) and the app-level native drag-and-drop routing (primary
// window only).
public partial class LinuxApplication
{
    /// <summary>
    /// Converts physical pixel coordinates to logical pixel coordinates for HiDPI support.
    /// </summary>
    private float ToLogical(double physicalCoord) => (float)(physicalCoord / DpiScale);

    /// <summary>
    /// CSD titlebar inset of the PRIMARY window (drag-and-drop is wired to the
    /// primary window only). See WindowContext.CsdPointerInsetLogical.
    /// </summary>
    private float CsdPointerInsetLogical => PrimaryContext?.CsdPointerInsetLogical ?? 0f;

    private void UpdateAnimations()
    {
        // Fire MAUI's animation ticker(s) (FadeTo, Animation.Commit, ...) for
        // the native X11/Wayland loop; in GTK mode the ticker runs off a GLib
        // timeout instead. Runs before Render so this frame picks up the new
        // property values.
        Hosting.LinuxTicker.PumpAll();

        // Update cursor blink for text input controls in every window.
        for (int i = 0; i < WindowContexts.Count; i++)
            WindowContexts[i].UpdateAnimations();
    }

    /// <summary>
    /// Wraps a window input-event handler so an exception thrown by view code
    /// is logged instead of unwinding through the native (GTK) dispatch
    /// frame that invoked it — which would abort the whole process (SIGABRT). A
    /// misbehaving control must never crash the app via the input path. All
    /// input subscriptions in <c>LinuxApplication</c> route through this
    /// (X11/Wayland subscriptions route through WindowContext's equivalent).
    /// </summary>
    private EventHandler<T> Guarded<T>(string name, EventHandler<T> handler) => (s, e) =>
    {
        try { handler(s, e); }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LinuxApplication", $"Unhandled exception in {name} handler", ex);
        }
    };

    // GTK Event Handlers (GTK mode is single-window; all state lives on the
    // primary context)
    private void OnGtkDrawRequested(object? sender, EventArgs e)
    {
        DiagnosticLog.Debug("LinuxApplication", ">>> OnGtkDrawRequested ENTER");
        LogDraw();
        var surface = _gtkWindow?.SkiaSurface;
        var rootView = RootView;
        if (surface?.Canvas != null && rootView != null)
        {
            var bgColor = Application.Current?.UserAppTheme == AppTheme.Dark
                ? new SKColor(32, 33, 36)
                : SKColors.White;
            surface.Canvas.Clear(bgColor);
            DiagnosticLog.Debug("LinuxApplication", "Drawing rootView...");
            rootView.Draw(surface.Canvas);

            // Modal pages (Navigation.PushModalAsync) stack above the root.
            var modalViews = PrimaryContext?.ModalViews;
            if (modalViews != null)
            {
                for (int i = 0; i < modalViews.Count; i++)
                {
                    var layer = modalViews[i];
                    layer.Measure(new Microsoft.Maui.Graphics.Size(surface.Width, surface.Height));
                    layer.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, surface.Width, surface.Height));
                    layer.Draw(surface.Canvas);
                }
            }

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
        string buttonName = e.Button == 1 ? "Left" : e.Button == 2 ? "Middle" : e.Button == 3 ? "Right" : $"Unknown({e.Button})";
        DiagnosticLog.Debug("LinuxApplication", $"GTK PointerPressed at ({e.X:F1}, {e.Y:F1}), Button={e.Button} ({buttonName})");

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            LinuxDialogService.TopDialog?.OnPointerPressed(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (LinuxDialogService.HasContextMenu)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            LinuxDialogService.ActiveContextMenu?.OnPointerPressed(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        var ctx = PrimaryContext;
        if (ctx?.InputRoot == null)
        {
            DiagnosticLog.Warn("LinuxApplication", "GTK root view is null!");
            return;
        }

        var hitView = ctx.InputRoot.HitTest((float)e.X, (float)e.Y);
        DiagnosticLog.Debug("LinuxApplication", $"GTK HitView: {hitView?.GetType().Name ?? "null"}");

        if (hitView != null)
        {
            if (hitView.IsFocusable && ctx.FocusedView != hitView)
            {
                ctx.FocusedView = hitView;
            }
            ctx.CapturedView = hitView;
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            DiagnosticLog.Debug("LinuxApplication", ">>> Before OnPointerPressed");
            hitView.OnPointerPressed(args);
            DiagnosticLog.Debug("LinuxApplication", "<<< After OnPointerPressed, calling RequestRedraw");
            _gtkWindow?.RequestRedraw();
            DiagnosticLog.Debug("LinuxApplication", "<<< After RequestRedraw, returning from handler");
        }
    }

    private void OnGtkPointerReleased(object? sender, (double X, double Y, int Button) e)
    {
        DiagnosticLog.Debug("LinuxApplication", ">>> OnGtkPointerReleased ENTER");

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            LinuxDialogService.TopDialog?.OnPointerReleased(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        var ctx = PrimaryContext;
        if (ctx?.InputRoot == null) return;

        if (ctx.CapturedView != null)
        {
            var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
            var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
            DiagnosticLog.Debug("LinuxApplication", $"Calling OnPointerReleased on {ctx.CapturedView.GetType().Name}");
            ctx.CapturedView.OnPointerReleased(args);
            DiagnosticLog.Debug("LinuxApplication", "OnPointerReleased returned");
            ctx.CapturedView = null;
            _gtkWindow?.RequestRedraw();
            DiagnosticLog.Debug("LinuxApplication", "<<< OnGtkPointerReleased EXIT (captured path)");
        }
        else
        {
            var hitView = ctx.InputRoot.HitTest((float)e.X, (float)e.Y);
            if (hitView != null)
            {
                var button = e.Button == 1 ? PointerButton.Left : e.Button == 2 ? PointerButton.Middle : PointerButton.Right;
                var args = new PointerEventArgs((float)e.X, (float)e.Y, button);
                hitView.OnPointerReleased(args);
                _gtkWindow?.RequestRedraw();
            }
        }
    }

    private void OnGtkPointerMoved(object? sender, (double X, double Y) e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            LinuxDialogService.TopDialog?.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        if (LinuxDialogService.HasContextMenu)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            LinuxDialogService.ActiveContextMenu?.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        var ctx = PrimaryContext;
        if (ctx?.InputRoot == null) return;

        if (ctx.CapturedView != null)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            ctx.CapturedView.OnPointerMoved(args);
            _gtkWindow?.RequestRedraw();
            return;
        }

        var hitView = ctx.InputRoot.HitTest((float)e.X, (float)e.Y);
        if (hitView != ctx.HoveredView)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
            ctx.HoveredView?.OnPointerExited(args);
            ctx.HoveredView = hitView;
            ctx.HoveredView?.OnPointerEntered(args);
            _gtkWindow?.RequestRedraw();
        }

        if (hitView != null)
        {
            var args = new PointerEventArgs((float)e.X, (float)e.Y);
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

        var focused = PrimaryContext?.FocusedView;
        if (focused != null)
        {
            focused.OnKeyDown(args);
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

        var focused = PrimaryContext?.FocusedView;
        if (focused != null)
        {
            focused.OnKeyUp(args);
            _gtkWindow?.RequestRedraw();
        }
    }

    private void OnGtkScrolled(object? sender, (double X, double Y, double DeltaX, double DeltaY, uint State) e)
    {
        var rootView = PrimaryContext?.InputRoot;
        if (rootView == null) return;

        // Convert GDK state to KeyModifiers
        var modifiers = ConvertGdkStateToModifiers(e.State);
        bool isCtrlPressed = (modifiers & KeyModifiers.Control) != 0;

        var hitView = rootView.HitTest((float)e.X, (float)e.Y);

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
        var focused = PrimaryContext?.FocusedView;
        if (focused != null)
        {
            var args = new TextInputEventArgs(text);
            focused.OnTextInput(args);
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

    #region Native drag-and-drop → DropGestureRecognizer routing

    // Current MAUI-level drop target (the view owning the enabled
    // DropGestureRecognizer), tracked across positions so per-view
    // enter/leave transitions fire as the drag moves between views. This
    // routing is ADDITIVE: DragDropService.Default's own events keep firing
    // for direct subscribers (the samples use those) unchanged.
    //
    // Multi-window: drag-and-drop resolves against the PRIMARY window's tree
    // only. On X11 only the primary window announces XdndAware (the singleton
    // DragDropService binds first-wins to the primary display/window), so no
    // XDND traffic ever targets a secondary window. On Wayland the per-window
    // data devices raise into the same DragDropService.Default events without
    // window identity, so a drag over a secondary window would mis-resolve —
    // documented v1 limitation.
    private readonly DropTargetTracker<View> _dropTargetTracker = new();

    private void WireDragDropRouting()
    {
        DragDropService.Default.DragEnter += OnNativeDragEnter;
        DragDropService.Default.DragOver += OnNativeDragOver;
        DragDropService.Default.DragLeave += OnNativeDragLeave;
        DragDropService.Default.Drop += OnNativeDrop;
    }

    /// <summary>
    /// Convert a drag event's window-physical position to the logical space
    /// pointer dispatch hit-tests in (same scaling + CSD inset as the pointer
    /// path), and resolve the MAUI drop target under it. The shared HitTest
    /// entry point handles scroll-offset views internally, so coordinates stay
    /// consistent with regular pointer routing.
    /// </summary>
    private View? ResolveDropTarget(int physicalX, int physicalY)
    {
        var rootView = PrimaryContext?.InputRoot;
        if (rootView == null) return null;
        float x = ToLogical(physicalX);
        float y = ToLogical(physicalY) - CsdPointerInsetLogical;
        var hit = SkiaView.GetPopupOwnerAt(x, y) ?? rootView.HitTest(x, y);
        return Handlers.GestureManager.FindDropTarget(hit?.MauiView);
    }

    private void OnNativeDragEnter(object? sender, Services.DragEventArgs e)
    {
        // View targeting is driven from DragOver: X11 enter carries no
        // position (it only becomes known at the first XdndPosition), so a
        // window-level enter just resets any stale target.
        var left = _dropTargetTracker.Clear();
        if (left != null)
            Handlers.GestureManager.ProcessDragLeave(left);
    }

    private void OnNativeDragOver(object? sender, Services.DragEventArgs e)
    {
        var target = ResolveDropTarget(e.X, e.Y);

        var (leftView, current) = _dropTargetTracker.Update(target);
        if (leftView != null)
            Handlers.GestureManager.ProcessDragLeave(leftView);

        if (current != null)
        {
            float x = ToLogical(e.X);
            float y = ToLogical(e.Y) - CsdPointerInsetLogical;
            // MAUI fires DragOver repeatedly on the current target.
            var accepted = Handlers.GestureManager.ProcessDragOver(current, x, y);
            // A recognizer that set AcceptedOperation.None flips the native
            // accept; no participating recognizer (null) leaves the window's
            // default-accept intact for plain DragDropService consumers.
            if (accepted == false)
                e.Accepted = false;
        }
    }

    private void OnNativeDragLeave(object? sender, EventArgs e)
    {
        var left = _dropTargetTracker.Clear();
        if (left != null)
            Handlers.GestureManager.ProcessDragLeave(left);
    }

    private void OnNativeDrop(object? sender, Services.DropEventArgs e)
    {
        // Prefer the tracked target; fall back to hit-testing the drop
        // position (covers a drop with no preceding positioned DragOver).
        var target = _dropTargetTracker.Clear() ?? ResolveDropTarget(e.X, e.Y);
        if (target == null) return;

        float x = ToLogical(e.X);
        float y = ToLogical(e.Y) - CsdPointerInsetLogical;
        Handlers.GestureManager.ProcessDrop(target, x, y, e.DroppedData, e.Data.FilePaths);
    }

    #endregion
}
