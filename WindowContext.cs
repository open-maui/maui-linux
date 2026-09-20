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

    private readonly record struct ModalEntry(Page Page, SkiaView View);

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
    }

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
            }

            _mauiWindow = value;

            if (value is Microsoft.Maui.Controls.Window newWindow)
            {
                AttachMauiWindowHandler(newWindow);
                newWindow.ModalPushed += OnMauiModalPushed;
                newWindow.ModalPopped += OnMauiModalPopped;
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

    private float ToLogical(double physicalCoord) => (float)(physicalCoord / _app.DpiScale);

    /// <summary>
    /// When CSD is active on Wayland, the view tree is rendered translated down
    /// by the titlebar height; pointer events arrive in window-relative coords.
    /// This shift puts them in view-tree space. 0 on backends without CSD.
    /// </summary>
    internal float CsdPointerInsetLogical =>
        DisplayWindow is WaylandWindow w && w.UseCsd ? WaylandWindow.CsdTitlebarHeightLogical : 0f;

    private PointerEventArgs ScalePointerArgs(PointerEventArgs e)
    {
        float inset = CsdPointerInsetLogical;
        if (_app.DpiScale <= 1.0f && inset <= 0f) return e;
        return new PointerEventArgs(ToLogical(e.X), ToLogical(e.Y) - inset, e.Button);
    }

    private ScrollEventArgs ScaleScrollArgs(ScrollEventArgs e)
    {
        float inset = CsdPointerInsetLogical;
        if (_app.DpiScale <= 1.0f && inset <= 0f) return e;
        return new ScrollEventArgs(ToLogical(e.X), ToLogical(e.Y) - inset, e.DeltaX, e.DeltaY);
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
        if (_rootView != null)
        {
            // Re-measure with new available size, then arrange
            var availableSize = new Microsoft.Maui.Graphics.Size(size.Width, size.Height);
            _rootView.Measure(availableSize);
            _rootView.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, size.Width, size.Height));
        }
        for (int i = 0; i < _modalViews.Count; i++)
            LayoutModalLayer(_modalViews[i], size.Width, size.Height);
        RenderingEngine?.InvalidateAll();

        // Propagate to MAUI so Window.Width/Height and SizeChanged observers
        // stay accurate (secondary windows only; primary preserves the exact
        // historical single-window behavior of not reporting frames).
        if (RaisesMauiLifecycle && MauiWindow != null)
        {
            try
            {
                float scale = _app.DpiScale > 0 ? _app.DpiScale : 1f;
                MauiWindow.FrameChanged(new Microsoft.Maui.Graphics.Rect(
                    0, 0, size.Width / scale, size.Height / scale));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("WindowContext", "IWindow.FrameChanged threw", ex);
            }
        }
    }

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
        NotifyDeactivated();
    }

    /// <summary>
    /// Popup overlays are registered globally on SkiaView; when several
    /// windows are live, only popups whose owner belongs to THIS window's
    /// tree may be hit here. With a single window the filter root is null,
    /// preserving the historical unfiltered behavior bit-for-bit.
    /// </summary>
    private SkiaView? PopupFilterRoot => _app.WindowContexts.Count > 1 ? _rootView : null;

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

        if (_focusedView != null)
        {
            _focusedView.OnKeyDown(e);
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

        if (_focusedView != null)
        {
            _focusedView.OnKeyUp(e);
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
                CapturedView.OnPointerMoved(e);
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y, PopupFilterRoot);
            var hitView = popupOwner ?? inputRoot.HitTest(e.X, e.Y);

            // Track hover state changes
            if (hitView != HoveredView)
            {
                HoveredView?.OnPointerExited(e);
                HoveredView = hitView;
                HoveredView?.OnPointerEntered(e);

                // Update cursor based on view's cursor type
                CursorType cursor = hitView?.CursorType ?? CursorType.Arrow;
                DisplayWindow?.SetCursor(cursor);
            }

            hitView?.OnPointerMoved(e);
        }
    }

    internal void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        e = ScalePointerArgs(e);
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
            var hitView = popupOwner ?? inputRoot.HitTest(e.X, e.Y);
            DiagnosticLog.Debug("WindowContext", $"HitView: {hitView?.GetType().Name ?? "null"}, inputRoot: {inputRoot.GetType().Name}");

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

    internal void OnPointerReleased(object? sender, PointerEventArgs e)
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
                CapturedView.OnPointerReleased(e);
                CapturedView = null; // Release capture
                return;
            }

            // Check for popup overlay first
            var popupOwner = SkiaView.GetPopupOwnerAt(e.X, e.Y, PopupFilterRoot);
            var hitView = popupOwner ?? inputRoot.HitTest(e.X, e.Y);
            hitView?.OnPointerReleased(e);
        }
    }

    private void OnScroll(object? sender, ScrollEventArgs e)
    {
        e = ScaleScrollArgs(e);
        DiagnosticLog.Debug("WindowContext", $"OnScroll - X={e.X}, Y={e.Y}, DeltaX={e.DeltaX}, DeltaY={e.DeltaY}");
        if (LinuxDialogService.HasActiveDialog && !_app.IsDialogHost(this))
            return;
        var inputRoot = InputRoot;
        if (inputRoot != null)
        {
            var hitView = inputRoot.HitTest(e.X, e.Y);
            DiagnosticLog.Debug("WindowContext", $"HitView: {hitView?.GetType().Name ?? "null"}");
            // Bubble scroll events up to find a ScrollView
            var view = hitView;
            while (view != null)
            {
                DiagnosticLog.Debug("WindowContext", $"Bubbling to: {view.GetType().Name}");
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
    }

    /// <summary>
    /// Removes a presented modal page (normally the top-most) and its layer,
    /// disconnects the page's handler so its Skia tree can be collected, and
    /// returns input to whatever is now on top.
    /// </summary>
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

    private static void LayoutModalLayer(SkiaView view, int width, int height)
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

    internal void NotifyCreated()
    {
        if (!RaisesMauiLifecycle || _mauiCreatedSent || MauiWindow == null) return;
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
