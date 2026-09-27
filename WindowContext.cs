// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Platform.Linux.Diagnostics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Owns all per-window state for one native toplevel: the display window, its
/// rendering engine, the root Skia view tree, and the focus/hover/capture
/// pointers that input routing needs. <see cref="LinuxApplication"/> keeps a
/// registry of these; the first entry is the PRIMARY window and the legacy
/// single-window members on <see cref="LinuxApplication"/> (RootView,
/// MainWindow, RenderingEngine, FocusedView) forward to it so every existing
/// consumer keeps working unchanged.
///
/// In GTK mode there is a single context holding only view state
/// (<see cref="DisplayWindow"/> and <see cref="RenderingEngine"/> are null);
/// the GTK host window renders through its own path.
/// </summary>
public sealed class WindowContext : IDisposable
{
    private readonly LinuxApplication _app;
    private SkiaView? _rootView;
    private SkiaView? _focusedView;
    private IWindow? _mauiWindow;
    private bool _disposed;

    // Modal pages pushed through Navigation.PushModalAsync, bottom to top.
    // Each is a full-window layer rendered above the root (and above any
    // modal beneath it); input goes to the top-most entry while any exist.
    private readonly List<ModalEntry> _modals = new();
    private readonly List<SkiaView> _modalViews = new();

    private readonly record struct ModalEntry(Page Page, SkiaView View, bool IsPopup = false);

    // MAUI IWindow lifecycle bookkeeping. IWindow.Created/Activated/Deactivated/
    // Destroying THROW on double invocation, so each transition is latched here.
    private bool _mauiCreatedSent;
    private bool _mauiActivatedSent;
    private bool _mauiDestroyingSent;

    /// <summary>
    /// Whether MAUI window lifecycle events (Created/Activated/Deactivated)
    /// are raised for this context. False for the primary/startup window to
    /// preserve the exact single-window production behavior (the bootstrap
    /// never raised them); true for windows opened via Application.OpenWindow.
    /// Destroying is raised for every context that has a MauiWindow.
    /// </summary>
    internal bool RaisesMauiLifecycle { get; init; }

    public WindowContext(LinuxApplication app, IDisplayWindow? displayWindow, SkiaRenderingEngine? renderingEngine)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        DisplayWindow = displayWindow;
        RenderingEngine = renderingEngine;
        ToolTips = new ToolTipController(() => RenderingEngine?.InvalidateAll());
        if (renderingEngine != null)
            renderingEngine.ToolTips = ToolTips;
    }

    /// <summary>This window's tooltips (MAUI <c>ToolTipProperties.Text</c>).</summary>
    internal ToolTipController ToolTips { get; }

    /// <summary>The native window (X11 or Wayland). Null in GTK mode.</summary>
    public IDisplayWindow? DisplayWindow { get; }

    /// <summary>The Skia engine rendering into <see cref="DisplayWindow"/>. Null in GTK mode.</summary>
    public SkiaRenderingEngine? RenderingEngine { get; }

    /// <summary>True when this context is the app's primary (first) window.</summary>
    public bool IsPrimary => _app.PrimaryContext == this;

    /// <summary>
    /// The MAUI IWindow this context presents, when known. Assigning a
    /// <see cref="Microsoft.Maui.Controls.Window"/> attaches the Linux
    /// WindowHandler to it (MAUI's AlertManager and ModalNavigationManager
    /// only engage once the window has a handler with a MauiContext) and
    /// subscribes to its modal push/pop events so modal pages render in this
    /// window.
    /// </summary>
    public IWindow? MauiWindow
    {
        get => _mauiWindow;
        set
        {
            if (ReferenceEquals(_mauiWindow, value))
                return;

            if (_mauiWindow is Microsoft.Maui.Controls.Window oldWindow)
            {
                oldWindow.ModalPushed -= OnMauiModalPushed;
                oldWindow.ModalPopped -= OnMauiModalPopped;
                oldWindow.PropertyChanged -= OnMauiWindowPropertyChanged;
            }

            _mauiWindow = value;

            if (value is Microsoft.Maui.Controls.Window newWindow)
            {
                AttachMauiWindowHandler(newWindow);
                newWindow.ModalPushed += OnMauiModalPushed;
                newWindow.ModalPopped += OnMauiModalPopped;
                newWindow.PropertyChanged += OnMauiWindowPropertyChanged;
                ApplyInitialGeometry(newWindow);
            }
        }
    }

    /// <summary>View that has captured pointer events during a drag.</summary>
    public SkiaView? CapturedView { get; set; }

    /// <summary>View currently under the pointer (hover tracking).</summary>
    public SkiaView? HoveredView { get; set; }

    /// <summary>
    /// Root of this window's Skia view tree. Setter mirrors the historical
    /// LinuxApplication.RootView behavior: attaches the render context and
    /// arranges to the native window size.
    /// </summary>
    public SkiaView? RootView
    {
        get => _rootView;
        set
        {
            _rootView = value;
            if (_rootView != null)
            {
                // Attach the render context so views in this tree can resolve
                // typefaces and request invalidation without a global lookup.
                if (RenderingEngine != null)
                    _rootView.RenderContext = RenderingEngine;

                if (DisplayWindow != null)
                {
                    _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(
                        0, 0,
                        DisplayWindow.Width,
                        DisplayWindow.Height));
                }
            }
        }
    }

    /// <summary>
    /// The focused view within this window's tree. Setter fires
    /// OnFocusLost/OnFocusGained exactly like the historical
    /// LinuxApplication.FocusedView property.
    /// </summary>
    public SkiaView? FocusedView
    {
        get => _focusedView;
        set
        {
            if (_focusedView != value)
            {
                var oldFocus = _focusedView;
                _focusedView = value;

                // Call OnFocusLost on the old view (this sets IsFocused = false and invalidates)
                oldFocus?.OnFocusLost();

                // Call OnFocusGained on the new view (this sets IsFocused = true and invalidates)
                _focusedView?.OnFocusGained();
            }
        }
    }

    #region Coordinate scaling (HiDPI + Wayland CSD)

    /// <summary>
    /// This window's device scale: its rendering engine's, which follows the
    /// native window across monitors of different scale; the application's
    /// startup scale when the context has no engine.
    /// </summary>
    internal float Scale => RenderingEngine is { DpiScale: > 0f } engine ? engine.DpiScale : (_app.DpiScale > 0f ? _app.DpiScale : 1f);

    private float ToLogical(double physicalCoord) => (float)(physicalCoord / Scale);

    /// <summary>
    /// When CSD is active on Wayland, the view tree is rendered translated down
    /// by the titlebar height; pointer events arrive in window-relative coords.
    /// This shift puts them in view-tree space. 0 on backends without CSD.
    /// </summary>
    internal float CsdPointerInsetLogical =>
        DisplayWindow is WaylandWindow w && w.UseCsd ? WaylandWindow.CsdTitlebarHeightLogical : 0f;

    /// <summary>Window-physical pixels to the logical view-tree space pointer dispatch uses.</summary>
    internal (float X, float Y) ToLogicalPoint(double physicalX, double physicalY)
        => (ToLogical(physicalX), ToLogical(physicalY) - CsdPointerInsetLogical);

    private PointerEventArgs ScalePointerArgs(PointerEventArgs e)
    {
        float inset = CsdPointerInsetLogical;
        if (Scale <= 1.0f && inset <= 0f) return e;
        return new PointerEventArgs(ToLogical(e.X), ToLogical(e.Y) - inset, e.Button);
    }

    private ScrollEventArgs ScaleScrollArgs(ScrollEventArgs e)
    {
        float inset = CsdPointerInsetLogical;
        if (Scale <= 1.0f && inset <= 0f) return e;
        return new ScrollEventArgs(ToLogical(e.X), ToLogical(e.Y) - inset, e.DeltaX, e.DeltaY, e.Modifiers);
    }

    #endregion

    #region Input wiring

    /// <summary>
    /// Subscribes this context to its native window's input events. All input
    /// handlers run synchronously inside native (Wayland/X11) event callbacks;
    /// route them through Guarded so a view exception is logged instead of
    /// aborting the process through the native frame. This is a hard invariant.
    /// </summary>
    internal void WireInput()
    {
        var window = DisplayWindow;
        if (window == null) return;

        window.Resized += OnWindowResized;
        window.Exposed += OnWindowExposed;
        if (window is IVisibilityAwareDisplayWindow visibility)
            visibility.SuspendedChanged += Guarded<bool>("suspended-changed", OnWindowSuspendedChanged);
        if (window is IScaleAwareDisplayWindow scaleAware)
        {
            scaleAware.ScaleChanged += Guarded<float>("scale-changed", OnWindowScaleChanged);
            // A scale the compositor reported before the engine existed.
            if (RenderingEngine != null && Math.Abs(scaleAware.Scale - RenderingEngine.DpiScale) > 0.01f)
                OnWindowScaleChanged(scaleAware, scaleAware.Scale);
        }
        window.KeyDown += Guarded<KeyEventArgs>("key-down", OnKeyDown);
        window.KeyUp += Guarded<KeyEventArgs>("key-up", OnKeyUp);
        window.TextInput += Guarded<TextInputEventArgs>("text-input", OnTextInput);
        window.PointerMoved += Guarded<PointerEventArgs>("pointer-moved", OnPointerMoved);
        window.PointerPressed += Guarded<PointerEventArgs>("pointer-pressed", OnPointerPressed);
        window.PointerReleased += Guarded<PointerEventArgs>("pointer-released", OnPointerReleased);
        window.Scroll += Guarded<ScrollEventArgs>("scroll", OnScroll);
        window.CloseRequested += OnCloseRequested;
        window.FocusGained += GuardedPlain("focus-gained", OnWindowFocusGained);
        window.FocusLost += GuardedPlain("focus-lost", OnWindowFocusLost);
    }

    /// <summary>
    /// Wraps a window input-event handler so an exception thrown by view code
    /// is logged instead of unwinding through the native (Wayland/X11) dispatch
    /// frame that invoked it — which would abort the whole process (SIGABRT).
    /// A misbehaving control must never crash the app via the input path.
    /// </summary>
    private EventHandler<T> Guarded<T>(string name, EventHandler<T> handler) => (s, e) =>
    {
        try { handler(s, e); }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", $"Unhandled exception in {name} handler", ex);
        }
    };

    private EventHandler GuardedPlain(string name, EventHandler handler) => (s, e) =>
    {
        try { handler(s, e); }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", $"Unhandled exception in {name} handler", ex);
        }
    };

    #endregion

    #region Input handlers (per-window routing)

    internal void UpdateAnimations()
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
        // The size is the buffer's, in physical pixels; the tree is laid out in logical
        // units below any client-drawn title bar, exactly as the renderer lays it out.
        // Laying it out at the physical size put every view through a pass at the
        // scale factor times its width on each resize step, then back: layouts that
        // react to their width (a toolbar folding its items) churned while resizing.
        float scale = Scale;
        double width = size.Width / scale;
        double height = (double)(size.Height / scale) - CsdPointerInsetLogical;
        if (_rootView != null)
        {
            _rootView.Measure(new Microsoft.Maui.Graphics.Size(width, height));
            _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, width, height));
        }
        for (int i = 0; i < _modalViews.Count; i++)
            LayoutModalLayer(_modalViews[i], width, height);
        RenderingEngine?.InvalidateAll();

        // Propagate to MAUI so Window.Width/Height and SizeChanged observers
        // stay accurate (secondary windows only; primary preserves the exact
        // historical single-window behavior of not reporting frames).
        if (RaisesMauiLifecycle && MauiWindow != null)
        {
            try
            {
                MauiWindow.FrameChanged(new Microsoft.Maui.Graphics.Rect(
                    0, 0, size.Width / scale, size.Height / scale));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WindowContext", "IWindow.FrameChanged threw", ex);
            }
        }
    }

    /// <summary>
    /// The native window moved to a monitor with a different scale (or the
    /// desktop scale changed). The engine renders at the new density from the
    /// next frame; on Wayland the Resized that follows carries the new buffer
    /// size, on X11 the window keeps its pixels and only the logical size
    /// changes, so the whole tree is invalidated here either way.
    /// </summary>
    private void OnWindowScaleChanged(object? sender, float scale)
    {
        if (scale <= 0f) return;
        var engine = RenderingEngine;
        if (engine != null)
        {
            if (Math.Abs(engine.DpiScale - scale) < 0.01f) return;
            engine.DpiScale = scale;
        }
        if (ReferenceEquals(_app.MainWindow, DisplayWindow))
            _app.UpdateDpiScale(scale);

        DiagnosticLog.Info("WindowContext", $"Window scale is now {scale:0.##}");
        _rootView?.InvalidateMeasure();
        for (int i = 0; i < _modalViews.Count; i++)
            _modalViews[i].InvalidateMeasure();
        engine?.InvalidateAll();
        ScaleChanged?.Invoke(this, scale);
    }

    /// <summary>Raised after this window's rendering scale changed.</summary>
    internal event EventHandler<float>? ScaleChanged;

    private void OnWindowExposed(object? sender, EventArgs e)
    {
        Render();
    }

    private void OnWindowFocusGained(object? sender, EventArgs e)
    {
        _app.NotifyContextFocused(this);
        NotifyActivated();
    }

    private void OnWindowFocusLost(object? sender, EventArgs e)
    {
        ToolTips.Reset();
        NotifyDeactivated();
    }

    /// <summary>
    /// Popup overlays are registered globally on SkiaView; when several
    /// windows are live, only popups whose owner belongs to THIS window's
    /// tree may be hit here. With a single window the filter root is null,
    /// preserving the historical unfiltered behavior bit-for-bit.
    /// </summary>
    internal SkiaView? PopupFilterRoot => _app.WindowContexts.Count > 1 ? _rootView : null;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Live Visual Tree inspector: consume Escape while picking (no-op
        // otherwise). Inspector targets the primary window only (v1).
        if (IsPrimary && VisualTreeInspector.Instance.HandleKeyDown(e.Key))
            return;

        // Route to dialog if one is active. Dialogs are app-modal; they render
        // in (and take input from) the dialog-host window — see
        // LinuxApplication.IsDialogHost for the routing rule.
        if (LinuxDialogService.HasActiveDialog && _app.IsDialogHost(this))
        {
            LinuxDialogService.TopDialog?.OnKeyDown(e);
            return;
        }

        if (_focusedView is { } focused)
        {
            // Observers (Syncfusion's keyboard detectors) see the key around the
            // focused view's own handling, as native preview and bubbling do.
            SkiaView.RaiseKeyRouted(focused, SkiaView.RoutedKeyKind.PreviewDown, e);
            if (e.Handled)
                return;
            focused.OnKeyDown(e);
            focused.RaiseKeyEvent(down: true, e);
            if (!e.Handled)
                SkiaView.RaiseKeyRouted(focused, SkiaView.RoutedKeyKind.Down, e);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog && _app.IsDialogHost(this))
        {
            LinuxDialogService.TopDialog?.OnKeyUp(e);
            return;
        }

        if (_focusedView is { } focused)
        {
            focused.OnKeyUp(e);
            focused.RaiseKeyEvent(down: false, e);
            if (!e.Handled)
                SkiaView.RaiseKeyRouted(focused, SkiaView.RoutedKeyKind.Up, e);
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (LinuxDialogService.HasActiveDialog && !_app.IsDialogHost(this))
            return;

        // Modal dialogs with an input field (prompt) take typed text.
        if (LinuxDialogService.HasActiveDialog && _app.IsDialogHost(this))
        {
            LinuxDialogService.TopDialog?.OnTextInput(e);
            return;
        }

        if (_focusedView != null)
        {
            _focusedView.OnTextInput(e);
        }
    }

    internal void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        e = ScalePointerArgs(e);

        // Live Visual Tree inspector pick mode: track the hovered node and
        // suppress normal routing while active (no-op when inactive).
        if (IsPrimary && VisualTreeInspector.Instance.HandlePointerMoved(e.X, e.Y))
            return;

        // Route to context menu if one is active
        if (LinuxDialogService.HasContextMenu && _app.IsDialogHost(this))
        {
            LinuxDialogService.ActiveContextMenu?.OnPointerMoved(e);
            return;
        }

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            if (_app.IsDialogHost(this))
                LinuxDialogService.TopDialog?.OnPointerMoved(e);
            return;
        }

        var inputRoot = InputRoot;
        if (inputRoot != null)
        {
            // If a view has captured the pointer, send all events to it
            if (CapturedView != null)
            {
                CapturedView.OnPointerMoved(InViewSpace(CapturedView, e));
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y, PopupFilterRoot);
            var hitView = popupOwner ?? HitTestLayers(e.X, e.Y, out _);
            ToolTips.OnPointerMoved(hitView, e.X, e.Y);

            // Track hover state changes
            if (hitView != HoveredView)
            {
                if (HoveredView != null) HoveredView.OnPointerExited(InViewSpace(HoveredView, e));
                HoveredView = hitView;
                if (HoveredView != null) HoveredView.OnPointerEntered(InViewSpace(HoveredView, e));

                // Update cursor based on view's cursor type
                CursorType cursor = hitView?.CursorType ?? CursorType.Arrow;
                DisplayWindow?.SetCursor(cursor);
            }

            if (hitView != null) hitView.OnPointerMoved(InViewSpace(hitView, e));
        }
    }

    internal void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        e = ScalePointerArgs(e);
        GestureManager.CurrentButton = ToButtonsMask(e.Button);
        ToolTips.Dismiss();
        DiagnosticLog.Debug("WindowContext", $"OnPointerPressed at ({e.X}, {e.Y}), Button={e.Button}");

        // Live Visual Tree inspector pick mode: commit the element under the
        // cursor as the selection and consume the press (no-op when inactive).
        if (IsPrimary && VisualTreeInspector.Instance.HandlePointerPressed(e.X, e.Y))
            return;

        // Route to context menu if one is active
        if (LinuxDialogService.HasContextMenu && _app.IsDialogHost(this))
        {
            LinuxDialogService.ActiveContextMenu?.OnPointerPressed(e);
            return;
        }

        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            if (_app.IsDialogHost(this))
                LinuxDialogService.TopDialog?.OnPointerPressed(e);
            return;
        }

        var inputRoot = InputRoot;
        if (inputRoot != null)
        {
            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y, PopupFilterRoot);
            Page? backdropOf = null;
            var hitView = popupOwner ?? HitTestLayers(e.X, e.Y, out backdropOf);
            DiagnosticLog.Debug("WindowContext", $"HitView: {hitView?.GetType().Name ?? "null"}, inputRoot: {inputRoot.GetType().Name}");

            // A press on a popup's backdrop is the popup's background click
            // (close on background click), as on the other platforms.
            if (popupOwner == null && backdropOf != null)
            {
                if (e.Button == PointerButton.Left)
                    MopupsBridge.SendBackgroundClick(backdropOf);
                return;
            }

            // An explicit FlyoutBase.ContextFlyout replaces the control's own
            // context menu: open it and swallow the press.
            if (e.Button == PointerButton.Right && ContextFlyoutBridge.TryShow(hitView, e.X, e.Y))
                return;

            if (hitView != null)
            {
                // Capture pointer to this view for drag operations
                CapturedView = hitView;

                // Update focus
                if (hitView.IsFocusable)
                {
                    FocusedView = hitView;
                }

                DiagnosticLog.Debug("WindowContext", $"Calling OnPointerPressed on {hitView.GetType().Name}");
                hitView.OnPointerPressed(InViewSpace(hitView, e));
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

    /// <summary>
    /// The event in <paramref name="view"/>'s untransformed space, where its Bounds are:
    /// under a translated, scaled or rotated ancestor (a tab slid into view) the window
    /// point is mapped back, so a caret or a slider thumb lands under the pointer.
    /// </summary>
    private static PointerEventArgs InViewSpace(SkiaView view, PointerEventArgs e)
    {
        var p = view.FromWindow(e.X, e.Y);
        return p.X == e.X && p.Y == e.Y ? e : new PointerEventArgs(p.X, p.Y, e.Button);
    }

    internal void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        try
        {
            DispatchPointerReleased(e);
        }
        finally
        {
            // The press is over: later hit tests (hover) use the default button.
            GestureManager.CurrentButton = default;
        }
    }

    private void DispatchPointerReleased(PointerEventArgs e)
    {
        e = ScalePointerArgs(e);
        // Route to dialog if one is active
        if (LinuxDialogService.HasActiveDialog)
        {
            if (_app.IsDialogHost(this))
                LinuxDialogService.TopDialog?.OnPointerReleased(e);
            return;
        }

        var inputRoot = InputRoot;
        if (inputRoot != null)
        {
            // If a view has captured the pointer, send release to it
            if (CapturedView != null)
            {
                CapturedView.OnPointerReleased(InViewSpace(CapturedView, e));
                CapturedView = null; // Release capture
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y, PopupFilterRoot);
            Page? backdropOf = null;
            var hitView = popupOwner ?? HitTestLayers(e.X, e.Y, out backdropOf);
            if (popupOwner == null && backdropOf != null)
                return;
            if (hitView != null) hitView.OnPointerReleased(InViewSpace(hitView, e));
        }
    }

    private void OnScroll(object? sender, ScrollEventArgs e)
    {
        e = ScaleScrollArgs(e);
        ToolTips.Dismiss();
        DiagnosticLog.Debug("WindowContext", $"OnScroll - X={e.X}, Y={e.Y}, DeltaX={e.DeltaX}, DeltaY={e.DeltaY}");
        if (LinuxDialogService.HasActiveDialog)
        {
            // The top dialog takes the wheel (an action sheet's long list scrolls).
            if (_app.IsDialogHost(this))
                LinuxDialogService.TopDialog?.OnScroll(e);
            return;
        }
        var inputRoot = InputRoot;
        if (inputRoot != null)
        {
            // An open popup takes the wheel over it (a drop-down's long list scrolls).
            var hitView = SkiaView.GetPopupOwnerAt(e.X, e.Y, PopupFilterRoot) ?? HitTestLayers(e.X, e.Y, out _);
            DiagnosticLog.Debug("WindowContext", $"HitView: {hitView?.GetType().Name ?? "null"}");
            // Bubble scroll events up to find a ScrollView
            var view = hitView;
            while (view != null)
            {
                DiagnosticLog.Debug("WindowContext", $"Bubbling to: {view.GetType().Name}");
                SkiaView.RaiseScrollRouted(view, e);
                if (e.Handled) return;
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
        _app.HandleContextCloseRequested(this);
        DisplayWindow?.Stop();
    }

    #endregion

    #region Modal navigation (Navigation.PushModalAsync / PopModalAsync)

    /// <summary>
    /// MAUI modal pages currently presented in this window, bottom to top.
    /// Mirrors <c>Window.Navigation.ModalStack</c> for the pages the platform
    /// managed to render.
    /// </summary>
    public IReadOnlyList<Page> ModalStack
    {
        get
        {
            var pages = new Page[_modals.Count];
            for (int i = 0; i < _modals.Count; i++)
                pages[i] = _modals[i].Page;
            return pages;
        }
    }

    /// <summary>
    /// The Skia views of the presented modal pages, bottom to top. Each is a
    /// full-window layer drawn above <see cref="RootView"/> (GTK draw path and
    /// <see cref="SkiaRenderingEngine.OverlayLayers"/>).
    /// </summary>
    public IReadOnlyList<SkiaView> ModalViews => _modalViews;

    /// <summary>True while at least one modal page is presented.</summary>
    public bool HasModal => _modals.Count > 0;

    /// <summary>
    /// The view tree that receives pointer/scroll input: the top-most modal
    /// while one is presented, else the root. Modal layers cover the whole
    /// window, so the page beneath is unreachable until the modal pops.
    /// </summary>
    public SkiaView? InputRoot => _modalViews.Count > 0 ? _modalViews[_modalViews.Count - 1] : _rootView;

    /// <summary>
    /// Attaches the Linux WindowHandler to a MAUI Window that has none.
    /// Without a window handler MAUI's AlertManager never subscribes (so
    /// Page.DisplayAlert silently no-ops) and ModalNavigationManager never
    /// reports the platform as ready. Failure is logged, not thrown: the
    /// window still renders, only dialogs/modal sync are lost.
    /// </summary>
    #region MAUI Window property sync (page swap, title, geometry)

    /// <summary>
    /// Live changes on the MAUI <see cref="Microsoft.Maui.Controls.Window"/>
    /// reach the native window: a replaced Page (the post-login
    /// <c>Window.Page = new AppShell()</c> / <c>MainPage =</c> swap) is
    /// rendered into this window, and Title, Width/Height, X/Y and the
    /// minimum/maximum sizes are applied. Geometry is logical (device-
    /// independent) as everywhere in MAUI; values equal to the current native
    /// state are ignored, which also absorbs the FrameChanged echo of a user
    /// resize.
    /// </summary>
    private void OnMauiWindowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Window window) return;
        try
        {
            switch (e.PropertyName)
            {
                case nameof(Microsoft.Maui.Controls.Window.Page):
                    ReplacePage(window);
                    break;
                case nameof(Microsoft.Maui.Controls.Window.Title):
                    if (!string.IsNullOrEmpty(window.Title))
                        DisplayWindow?.SetTitle(window.Title);
                    break;
                case nameof(Microsoft.Maui.Controls.Window.Width):
                case nameof(Microsoft.Maui.Controls.Window.Height):
                    ApplyRequestedSize(window.Width, window.Height);
                    break;
                case nameof(Microsoft.Maui.Controls.Window.X):
                case nameof(Microsoft.Maui.Controls.Window.Y):
                    ApplyRequestedPosition(window.X, window.Y);
                    break;
                case nameof(Microsoft.Maui.Controls.Window.MinimumWidth):
                case nameof(Microsoft.Maui.Controls.Window.MinimumHeight):
                case nameof(Microsoft.Maui.Controls.Window.MaximumWidth):
                case nameof(Microsoft.Maui.Controls.Window.MaximumHeight):
                    ApplySizeLimits(window);
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", $"Applying Window.{e.PropertyName} failed", ex);
        }
    }

    /// <summary>Renders the window's new Page as this window's root.</summary>
    private void ReplacePage(Microsoft.Maui.Controls.Window window)
    {
        var page = window.Page;
        if (page == null) return;
        if (page.Handler?.PlatformView is SkiaView current && ReferenceEquals(current, _rootView))
            return;

        var mauiContext = _app.MauiContext;
        if (mauiContext == null)
        {
            DiagnosticLog.Warn("WindowContext", "Window.Page changed but no MAUI context is available");
            return;
        }

        var renderer = new LinuxViewRenderer(mauiContext);
        var root = renderer.RenderPage(page);
        if (root == null) return;

        CapturedView = null;
        HoveredView = null;
        FocusedView = null;
        RootView = root;
        if (ReferenceEquals(_app.PrimaryContext, this))
            LinuxApplication.TrackRootForHotReload(renderer, window, page);
        if (string.IsNullOrEmpty(window.Title) && !string.IsNullOrEmpty(page.Title))
            DisplayWindow?.SetTitle(page.Title);
        RenderingEngine?.InvalidateAll();
        DiagnosticLog.Info("WindowContext", $"Window page replaced: {page.GetType().Name}");
    }

    /// <summary>
    /// Limits and position the app set before the window was shown (size is
    /// already used to create the native window).
    /// </summary>
    private void ApplyInitialGeometry(Microsoft.Maui.Controls.Window window)
    {
        try
        {
            if (IsSet(window.MinimumWidth) || IsSet(window.MinimumHeight) || IsSet(window.MaximumWidth) || IsSet(window.MaximumHeight))
                ApplySizeLimits(window);
            if (!double.IsNaN(window.X) && !double.IsNaN(window.Y))
                ApplyRequestedPosition(window.X, window.Y);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", "Applying initial window geometry failed", ex);
        }
    }

    private static bool IsSet(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

    private void ApplyRequestedSize(double logicalWidth, double logicalHeight)
    {
        var native = DisplayWindow;
        if (native == null || !IsSet(logicalWidth) || !IsSet(logicalHeight)) return;
        float scale = Scale;
        int w = (int)Math.Round(logicalWidth * scale);
        int h = (int)Math.Round(logicalHeight * scale);
        if (Math.Abs(w - native.Width) <= 1 && Math.Abs(h - native.Height) <= 1) return;
        if (native is IDesktopWindowControl control)
            control.RequestLogicalSize((int)Math.Round(logicalWidth), (int)Math.Round(logicalHeight));
        else
            native.Resize(w, h);
    }

    private void ApplyRequestedPosition(double logicalX, double logicalY)
    {
        if (double.IsNaN(logicalX) || double.IsNaN(logicalY)) return;
        if (DisplayWindow is IDesktopWindowControl control)
            control.RequestLogicalPosition((int)Math.Round(logicalX), (int)Math.Round(logicalY));
    }

    private void ApplySizeLimits(Microsoft.Maui.Controls.Window window)
    {
        if (DisplayWindow is not IDesktopWindowControl control) return;
        static int Limit(double v) => double.IsNaN(v) || double.IsInfinity(v) || v <= 0 ? 0 : (int)Math.Round(v);
        control.SetLogicalSizeLimits(
            Limit(window.MinimumWidth), Limit(window.MinimumHeight),
            Limit(window.MaximumWidth), Limit(window.MaximumHeight));
    }

    #endregion

    private void AttachMauiWindowHandler(Microsoft.Maui.Controls.Window window)
    {
        if (window.Handler != null)
            return;

        var mauiContext = _app.MauiContext;
        if (mauiContext == null)
        {
            DiagnosticLog.Debug("WindowContext", "No MAUI context yet; WindowHandler not attached");
            return;
        }

        try
        {
            window.ToHandler(mauiContext);
            DiagnosticLog.Debug("WindowContext", "Attached WindowHandler to MAUI window");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", "Attaching WindowHandler failed; DisplayAlert and modal sync are unavailable for this window", ex);
        }
    }

    private void OnMauiModalPushed(object? sender, ModalPushedEventArgs e)
    {
        try { PushModalView(e.Modal); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "PushModalAsync platform presentation failed", ex); }
    }

    private void OnMauiModalPopped(object? sender, ModalPoppedEventArgs e)
    {
        try { PopModalView(e.Modal); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "PopModalAsync platform teardown failed", ex); }
    }

    /// <summary>
    /// Presents a modal page: renders its Skia tree through
    /// <see cref="LinuxViewRenderer"/>, lays it out to the window, pushes it
    /// as the top input/render layer, and drops focus/hover/capture so the
    /// page beneath stops receiving input. Appearing/Disappearing are raised
    /// by MAUI's ModalNavigationManager before this runs.
    /// </summary>
    internal void PushModalView(Page page)
    {
        if (page == null) return;

        var mauiContext = _app.MauiContext
            ?? (_mauiWindow as Microsoft.Maui.Controls.Window)?.Handler?.MauiContext;
        if (mauiContext == null)
        {
            DiagnosticLog.Warn("WindowContext", $"PushModalAsync({page.GetType().Name}): no MAUI context; modal not presented");
            return;
        }

        var renderer = new LinuxViewRenderer(mauiContext);
        var view = renderer.RenderPage(page);
        if (view == null)
        {
            DiagnosticLog.Warn("WindowContext", $"PushModalAsync({page.GetType().Name}): page produced no SkiaView; modal not presented");
            return;
        }

        if (RenderingEngine != null)
            view.RenderContext = RenderingEngine;

        var (width, height) = CurrentLayoutSize();
        LayoutModalLayer(view, width, height);

        _modals.Add(new ModalEntry(page, view));
        _modalViews.Add(view);
        ResetInputState();
        RequestFullRedraw();
        DiagnosticLog.Debug("WindowContext", $"Modal pushed: {page.GetType().Name} (depth {_modals.Count})");
        Diagnostics.VisualTreeInspector.DumpAfterModalIfRequested();
    }

    /// <summary>
    /// Removes a presented modal page (normally the top-most) and its layer,
    /// disconnects the page's handler so its Skia tree can be collected, and
    /// returns input to whatever is now on top.
    /// </summary>
    /// <summary>
    /// Hit-tests the modal and popup layers from the top, then the root. A
    /// modal page takes every point inside it. A popup takes points on its
    /// content; its backdrop (transparent, non-interactive area) either lets
    /// the point through to the layer beneath, for a popup with
    /// BackgroundInputTransparent (toasts), or is reported through
    /// <paramref name="backdropOf"/> as that popup's background.
    /// </summary>
    internal SkiaView? HitTestLayers(float x, float y, out Page? backdropOf)
    {
        backdropOf = null;
        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            var layer = _modals[i];
            var hit = layer.View.HitTestAt(x, y);
            if (!layer.IsPopup || !IsBackdrop(hit, layer.View))
                return hit;
            if (!MopupsBridge.IsBackgroundInputTransparent(layer.Page))
            {
                backdropOf = layer.Page;
                return hit;
            }
        }
        return _rootView?.HitTestAt(x, y);
    }

    /// <summary>
    /// True when <paramref name="hit"/> is a popup's backdrop: nothing, or a
    /// container that paints no background and takes no input, as native
    /// hit-testing treats it (a transparent panel is not hit on WinUI, a view
    /// with no listeners does not consume the touch on Android).
    /// </summary>
    private static bool IsBackdrop(SkiaView? hit, SkiaView layer)
    {
        for (var view = hit; view != null; view = view.Parent)
        {
            if (view is not (SkiaLayoutView or SkiaPage))
                return false;
            if (view.IsFocusable || view.BackgroundColor is { Alpha: > 0 }
                || (view.MauiView is { } maui && (maui.GestureRecognizers.Count > 0 || !Microsoft.Maui.Controls.Brush.IsNullOrEmpty(maui.Background)
                    || maui.BackgroundColor is { Alpha: > 0 })))
                return false;
            if (ReferenceEquals(view, layer))
                break;
        }
        return true;
    }

    /// <summary>
    /// Presents a popup (Mopups' PopupPage) as a layer over the window without
    /// modal navigation: the page beneath gets no Disappearing/Appearing, as
    /// with the native popup overlays on the other platforms. Mopups raises
    /// the popup's own lifecycle.
    /// </summary>
    internal void PushPopupView(Page page)
    {
        var mauiContext = _app.MauiContext
            ?? (_mauiWindow as Microsoft.Maui.Controls.Window)?.Handler?.MauiContext;
        if (mauiContext == null)
        {
            DiagnosticLog.Warn("WindowContext", $"Popup {page.GetType().Name}: no MAUI context; not presented");
            return;
        }

        var view = new LinuxViewRenderer(mauiContext).RenderPage(page);
        if (view == null)
        {
            DiagnosticLog.Warn("WindowContext", $"Popup {page.GetType().Name} produced no view; not presented");
            return;
        }
        if (RenderingEngine != null)
            view.RenderContext = RenderingEngine;

        var (width, height) = CurrentLayoutSize();
        LayoutModalLayer(view, width, height);
        _modals.Add(new ModalEntry(page, view, IsPopup: true));
        _modalViews.Add(view);
        ResetInputState();
        RequestFullRedraw();
        DiagnosticLog.Debug("WindowContext", $"Popup shown: {page.GetType().Name}");
        Diagnostics.VisualTreeInspector.DumpAfterModalIfRequested();
    }

    /// <summary>Removes a popup presented with <see cref="PushPopupView"/>.</summary>
    internal bool PopPopupView(Page page)
    {
        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            if (_modals[i].IsPopup && ReferenceEquals(_modals[i].Page, page))
            {
                PopModalView(page);
                return true;
            }
        }
        return false;
    }

    private static ButtonsMask ToButtonsMask(PointerButton button) =>
        button == PointerButton.Right ? ButtonsMask.Secondary : ButtonsMask.Primary;

    internal void PopModalView(Page page)
    {
        if (page == null) return;

        int index = -1;
        for (int i = _modals.Count - 1; i >= 0; i--)
        {
            if (_modals[i].Page == page)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            DiagnosticLog.Debug("WindowContext", $"PopModalAsync({page.GetType().Name}): page was not presented here");
            return;
        }

        _modals.RemoveAt(index);
        _modalViews.RemoveAt(index);
        ResetInputState();

        try
        {
            page.Handler?.DisconnectHandler();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", "Disconnecting popped modal page handler failed", ex);
        }

        RequestFullRedraw();
        DiagnosticLog.Debug("WindowContext", $"Modal popped: {page.GetType().Name} (depth {_modals.Count})");
    }

    private (int Width, int Height) CurrentLayoutSize()
    {
        if (RenderingEngine != null)
            return ((int)RenderingEngine.LogicalWidth, (int)(RenderingEngine.LogicalHeight - CsdPointerInsetLogical));
        if (DisplayWindow != null)
            return (DisplayWindow.Width, DisplayWindow.Height);
        if (_rootView != null && _rootView.Bounds.Width > 0)
            return ((int)_rootView.Bounds.Width, (int)_rootView.Bounds.Height);
        return (800, 600);
    }

    private static void LayoutModalLayer(SkiaView view, double width, double height)
    {
        var size = new Microsoft.Maui.Graphics.Size(width, height);
        view.Measure(size);
        view.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, width, height));
    }

    /// <summary>
    /// Drops focus/hover/capture when the input root changes (modal push or
    /// pop) so no view under a modal keeps keyboard focus or a drag capture.
    /// Goes through the FocusedView property so the old view sees OnFocusLost.
    /// </summary>
    private void ResetInputState()
    {
        FocusedView = null;
        HoveredView = null;
        CapturedView = null;
    }

    private void RequestFullRedraw()
    {
        if (RenderingEngine != null)
            RenderingEngine.InvalidateAll();
        else
            LinuxApplication.RequestRedraw();
    }

    #endregion

    #region Rendering

    /// <summary>Renders this context's view tree through its engine.</summary>
    internal void Render()
    {
        if (RenderingEngine != null && _rootView != null)
        {
            // Only popups owned by this window's tree draw here (null filter
            // when a single window is live — historical behavior).
            RenderingEngine.PopupFilterRoot = PopupFilterRoot;
            RenderingEngine.OverlayLayers = _modalViews.Count > 0 ? _modalViews : null;
            RenderingEngine.Render(_rootView);
        }
    }

    #endregion

    #region MAUI IWindow lifecycle

    /// <summary>
    /// IWindow.Created, which MAUI turns into Window.Created and, for the first
    /// window, Application.OnStart. The primary window raises it too
    /// (<paramref name="primary"/>) although it raises no other lifecycle
    /// events: without it OnStart never ran, and an app that starts its work
    /// there (loading its data and swapping in its shell) stayed on its first page.
    /// </summary>
    internal void NotifyCreated(bool primary = false)
    {
        if ((!RaisesMauiLifecycle && !primary) || _mauiCreatedSent || MauiWindow == null) return;
        _mauiCreatedSent = true;
        try { MauiWindow.Created(); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Created threw", ex); }
    }

    internal void NotifyActivated()
    {
        if (!RaisesMauiLifecycle || _mauiActivatedSent || _mauiDestroyingSent || MauiWindow == null) return;
        _mauiActivatedSent = true;
        try { MauiWindow.Activated(); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Activated threw", ex); }
    }

    internal void NotifyDeactivated()
    {
        if (!RaisesMauiLifecycle || !_mauiActivatedSent || _mauiDestroyingSent || MauiWindow == null) return;
        _mauiActivatedSent = false;
        try { MauiWindow.Deactivated(); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Deactivated threw", ex); }
    }

    private bool _mauiStoppedSent;

    /// <summary>
    /// The native window was minimized or became fully hidden (xdg-shell
    /// <c>suspended</c> on Wayland, <c>_NET_WM_STATE_HIDDEN</c> on X11):
    /// IWindow.Stopped, which MAUI turns into Window.Stopped and
    /// Application.OnSleep. Raised for every context, the primary included,
    /// since the bootstrap raises no sleep/resume of its own.
    /// </summary>
    internal void NotifyStopped()
    {
        if (_mauiStoppedSent || _mauiDestroyingSent || MauiWindow == null) return;
        _mauiStoppedSent = true;
        try { MauiWindow.Stopped(); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Stopped threw", ex); }
    }

    /// <summary>The window is visible again: IWindow.Resumed (Application.OnResume).</summary>
    internal void NotifyResumed()
    {
        if (!_mauiStoppedSent || _mauiDestroyingSent || MauiWindow == null) return;
        _mauiStoppedSent = false;
        try { MauiWindow.Resumed(); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Resumed threw", ex); }
    }

    private void OnWindowSuspendedChanged(object? sender, bool suspended)
    {
        if (suspended) NotifyStopped();
        else NotifyResumed();
    }

    /// <summary>
    /// Raised for EVERY context (including the primary) when its native window
    /// closes, matching MAUI desktop semantics: Destroying fires page
    /// Disappearing, the window's Destroying event, and removes the window
    /// from Application.Windows.
    /// </summary>
    internal void NotifyDestroying()
    {
        if (_mauiDestroyingSent || MauiWindow == null) return;
        _mauiDestroyingSent = true;
        if (RaisesMauiLifecycle && _mauiActivatedSent)
        {
            _mauiActivatedSent = false;
            try { MauiWindow.Deactivated(); }
            catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Deactivated threw", ex); }
        }
        try { MauiWindow.Destroying(); }
        catch (Exception ex) { DiagnosticLog.Error("WindowContext", "IWindow.Destroying threw", ex); }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Pointers into this tree must not survive the context. Null the
        // focused-view FIELD directly: going through the property would fire
        // OnFocusLost -> Invalidate -> RequestRedraw on a tree that is being
        // torn down — in GTK mode after the host window was already destroyed,
        // which called gtk_widget_queue_draw on a freed widget (SIGSEGV on
        // WebViewDemo close).
        _focusedView = null;
        HoveredView = null;
        CapturedView = null;
        _rootView = null;
        _modals.Clear();
        _modalViews.Clear();

        if (_mauiWindow is Microsoft.Maui.Controls.Window window)
        {
            window.ModalPushed -= OnMauiModalPushed;
            window.ModalPopped -= OnMauiModalPopped;
        }

        RenderingEngine?.Dispose();
        DisplayWindow?.Dispose();
    }
}
