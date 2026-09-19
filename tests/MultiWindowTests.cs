// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using FluentAssertions;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Moq;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests;

// The test namespace lives under Microsoft.Maui.Controls, whose own
// PointerEventArgs would otherwise shadow the platform's input args during
// namespace-walk resolution; these aliases sit INSIDE the namespace so they
// win over the enclosing Microsoft.Maui.Controls types.
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;
using Key = Microsoft.Maui.Platform.Key;

/// <summary>
/// Headless tests for the multi-window registry: WindowContext lifecycle,
/// primary promotion, last-window-close semantics, per-window input routing
/// (through the Guarded wrapper), and MAUI IWindow lifecycle notification.
/// Uses a fake IDisplayWindow — no display server required.
/// </summary>
/// <remarks>
/// In the "LinuxApplication.Current" collection because these tests set and
/// clear the LinuxApplication.Current static, which VisualTreeInspectorTests
/// also observes; the collection serializes the two classes.
/// </remarks>
[Collection("LinuxApplication.Current")]
public class MultiWindowTests : IDisposable
{
    private readonly LinuxApplication _app = new();

    public void Dispose()
    {
        // Clears LinuxApplication.Current so unrelated tests see a clean slate.
        _app.Dispose();
    }

    #region Fakes

    /// <summary>
    /// Minimal IDisplayWindow: tracks running state and exposes Raise helpers
    /// so tests can inject native events synchronously (as X11/Wayland do).
    /// </summary>
    private sealed class FakeDisplayWindow : IDisplayWindow
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public bool IsRunning { get; private set; } = true;
        public bool Disposed { get; private set; }
        public CursorType? LastCursor { get; private set; }

        public event EventHandler<KeyEventArgs>? KeyDown;
        public event EventHandler<KeyEventArgs>? KeyUp;
        public event EventHandler<TextInputEventArgs>? TextInput;
        public event EventHandler<PointerEventArgs>? PointerMoved;
        public event EventHandler<PointerEventArgs>? PointerPressed;
        public event EventHandler<PointerEventArgs>? PointerReleased;
        public event EventHandler<ScrollEventArgs>? Scroll;
        public event EventHandler? Exposed;
        public event EventHandler<(int Width, int Height)>? Resized;
        public event EventHandler? CloseRequested;
        public event EventHandler? FocusGained;
        public event EventHandler? FocusLost;

        public void Show() => IsRunning = true;
        public void Hide() { }
        public void SetTitle(string title) { }
        public void Resize(int width, int height) { Width = width; Height = height; }
        public void SetCursor(CursorType cursorType) => LastCursor = cursorType;
        public void SetIcon(string iconPath) { }
        public void SetWMClass(string resName, string resClass) { }
        public void ProcessEvents() { }
        public void Stop() => IsRunning = false;
        public int GetFileDescriptor() => -1;
        public void Present(IntPtr pixels, int width, int height, int stride) { }
        public void FlushDeferredResize() { }
        public void AcknowledgeSync() { }
        public void Dispose() => Disposed = true;

        // Native-event injection (synchronous, like the real backends).
        public void RaisePointerPressed(float x, float y) =>
            PointerPressed?.Invoke(this, new PointerEventArgs(x, y, PointerButton.Left));
        public void RaisePointerMoved(float x, float y) =>
            PointerMoved?.Invoke(this, new PointerEventArgs(x, y));
        public void RaiseKeyDown(Key key) =>
            KeyDown?.Invoke(this, new KeyEventArgs(key, Microsoft.Maui.Platform.KeyModifiers.None));
        public void RaiseFocusGained() => FocusGained?.Invoke(this, EventArgs.Empty);
        public void RaiseFocusLost() => FocusLost?.Invoke(this, EventArgs.Empty);
        public void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);
        public void RaiseResized(int w, int h) { Width = w; Height = h; Resized?.Invoke(this, (w, h)); }

        // Suppress unused-event warnings for events tests don't raise.
        private void Touch()
        {
            _ = KeyUp; _ = TextInput; _ = PointerReleased; _ = Scroll; _ = Exposed;
        }
    }

    /// <summary>Concrete view; base MeasureOverride, records input.</summary>
    private class RecordingView : SkiaView
    {
        public int Pressed;
        public int KeyDowns;
        protected override void OnDraw(SkiaSharp.SKCanvas canvas, SkiaSharp.SKRect bounds) { }
        public override void OnPointerPressed(PointerEventArgs e) { Pressed++; base.OnPointerPressed(e); }
        public override void OnKeyDown(KeyEventArgs e) { KeyDowns++; base.OnKeyDown(e); }
    }

    /// <summary>View whose press handler throws — Guarded must swallow it.</summary>
    private sealed class ThrowingView : RecordingView
    {
        public override void OnPointerPressed(PointerEventArgs e)
        {
            Pressed++;
            throw new InvalidOperationException("view exploded");
        }
    }

    private (WindowContext ctx, FakeDisplayWindow win) AddWindow(bool raisesLifecycle = false)
    {
        var win = new FakeDisplayWindow();
        var ctx = _app.AttachWindowContext(win, null, raisesMauiLifecycle: raisesLifecycle);
        ctx.WireInput();
        return (ctx, win);
    }

    private static RecordingView MakeRoot(bool focusable = true)
    {
        var root = new RecordingView { IsFocusable = focusable };
        root.Arrange(new Rect(0, 0, 800, 600));
        return root;
    }

    #endregion

    #region Registry / primary promotion

    [Fact]
    public void FirstAttachedContext_IsPrimary()
    {
        var (ctx1, _) = AddWindow();
        var (ctx2, _) = AddWindow();

        ctx1.IsPrimary.Should().BeTrue();
        ctx2.IsPrimary.Should().BeFalse();
        _app.PrimaryContext.Should().BeSameAs(ctx1);
        _app.WindowContexts.Should().HaveCount(2);
    }

    [Fact]
    public void LegacyForwarders_TargetThePrimaryContext()
    {
        var (ctx1, win1) = AddWindow();
        AddWindow();

        var root = MakeRoot();
        _app.RootView = root;

        ctx1.RootView.Should().BeSameAs(root);
        _app.RootView.Should().BeSameAs(root);
        _app.MainWindow.Should().BeSameAs(win1);
    }

    [Fact]
    public void ClosingPrimary_PromotesNextContext_AndKeepsRunning()
    {
        var (ctx1, win1) = AddWindow();
        var (ctx2, win2) = AddWindow();
        ctx2.RootView = MakeRoot();

        win1.RaiseCloseRequested(); // handler calls Stop()
        int reaped = _app.ReapClosedContexts();

        reaped.Should().Be(1);
        win1.Disposed.Should().BeTrue();
        _app.WindowContexts.Should().HaveCount(1);

        // The survivor is promoted; the app-exit condition (empty registry)
        // is NOT met — closing the primary must not kill the process.
        _app.PrimaryContext.Should().BeSameAs(ctx2);
        ctx2.IsPrimary.Should().BeTrue();
        _app.MainWindow.Should().BeSameAs(win2);
        _app.RootView.Should().BeSameAs(ctx2.RootView);
    }

    [Fact]
    public void LastWindowClose_EmptiesRegistry_WhichExitsTheRunLoop()
    {
        var (_, win1) = AddWindow();
        var (_, win2) = AddWindow();

        win1.RaiseCloseRequested();
        win2.RaiseCloseRequested();
        _app.ReapClosedContexts();

        // The run loop's condition is WindowContexts.Count > 0 — an empty
        // registry means the loop (and process) ends.
        _app.WindowContexts.Should().BeEmpty();
        _app.PrimaryContext.Should().BeNull();
    }

    [Fact]
    public void Reap_LeavesRunningWindowsAlone()
    {
        var (_, win1) = AddWindow();
        AddWindow();

        win1.Stop();
        _app.ReapClosedContexts().Should().Be(1);
        _app.ReapClosedContexts().Should().Be(0);
        _app.WindowContexts.Should().HaveCount(1);
    }

    #endregion

    #region MAUI IWindow lifecycle

    [Fact]
    public void Reap_NotifiesIWindowDestroying_ExactlyOnce()
    {
        var (ctx, win) = AddWindow(raisesLifecycle: true);
        var mauiWindow = new Mock<IWindow>();
        ctx.MauiWindow = mauiWindow.Object;

        win.Stop();
        _app.ReapClosedContexts();

        mauiWindow.Verify(w => w.Destroying(), Times.Once);

        // Idempotent even if teardown paths overlap.
        ctx.NotifyDestroying();
        mauiWindow.Verify(w => w.Destroying(), Times.Once);
    }

    [Fact]
    public void PrimaryWindow_GetsDestroying_ButNoCreatedOrActivated()
    {
        // The primary/startup window preserves historical single-window
        // behavior: no Created/Activated, but Destroying on close.
        var (ctx, win) = AddWindow(raisesLifecycle: false);
        var mauiWindow = new Mock<IWindow>();
        ctx.MauiWindow = mauiWindow.Object;

        ctx.NotifyCreated();
        win.RaiseFocusGained();
        win.Stop();
        _app.ReapClosedContexts();

        mauiWindow.Verify(w => w.Created(), Times.Never);
        mauiWindow.Verify(w => w.Activated(), Times.Never);
        mauiWindow.Verify(w => w.Destroying(), Times.Once);
    }

    [Fact]
    public void SecondaryWindow_LifecycleIsLatched()
    {
        var (ctx, win) = AddWindow(raisesLifecycle: true);
        var mauiWindow = new Mock<IWindow>();
        ctx.MauiWindow = mauiWindow.Object;

        ctx.NotifyCreated();
        ctx.NotifyCreated(); // double-create must not double-fire (IWindow throws on repeats)
        mauiWindow.Verify(w => w.Created(), Times.Once);

        win.RaiseFocusGained();
        win.RaiseFocusGained(); // repeated native focus events
        mauiWindow.Verify(w => w.Activated(), Times.Once);

        win.RaiseFocusLost();
        win.RaiseFocusLost();
        mauiWindow.Verify(w => w.Deactivated(), Times.Once);

        win.RaiseFocusGained();
        mauiWindow.Verify(w => w.Activated(), Times.Exactly(2));

        // Destroying deactivates first (window was active).
        win.Stop();
        _app.ReapClosedContexts();
        mauiWindow.Verify(w => w.Deactivated(), Times.Exactly(2));
        mauiWindow.Verify(w => w.Destroying(), Times.Once);
    }

    [Fact]
    public void SecondaryWindow_Resize_ReportsFrameToMaui()
    {
        var (ctx, win) = AddWindow(raisesLifecycle: true);
        var mauiWindow = new Mock<IWindow>();
        ctx.MauiWindow = mauiWindow.Object;
        ctx.RootView = MakeRoot();

        win.RaiseResized(1024, 768);

        mauiWindow.Verify(w => w.FrameChanged(It.Is<Rect>(r => r.Width == 1024 && r.Height == 768)), Times.Once);
    }

    #endregion

    #region Per-window input routing

    [Fact]
    public void PointerPress_RoutesToOwnWindowsTree_WithPerWindowFocusAndCapture()
    {
        var (ctx1, win1) = AddWindow();
        var (ctx2, win2) = AddWindow();
        var root1 = MakeRoot();
        var root2 = MakeRoot();
        ctx1.RootView = root1;
        ctx2.RootView = root2;

        win2.RaisePointerPressed(10, 10);

        root2.Pressed.Should().Be(1);
        root1.Pressed.Should().Be(0);
        ctx2.FocusedView.Should().BeSameAs(root2);
        ctx2.CapturedView.Should().BeSameAs(root2);
        ctx1.FocusedView.Should().BeNull();
        ctx1.CapturedView.Should().BeNull();
    }

    [Fact]
    public void KeyDown_RoutesToTheWindowsOwnFocusedView()
    {
        var (ctx1, win1) = AddWindow();
        var (ctx2, win2) = AddWindow();
        var root1 = MakeRoot();
        var root2 = MakeRoot();
        ctx1.RootView = root1;
        ctx2.RootView = root2;

        // Focus a view in each window via a click, then type into window 1.
        win1.RaisePointerPressed(5, 5);
        win2.RaisePointerPressed(5, 5);
        win1.RaiseKeyDown(Key.A);

        root1.KeyDowns.Should().Be(1);
        root2.KeyDowns.Should().Be(0);
    }

    [Fact]
    public void HoverTracking_IsPerWindow()
    {
        var (ctx1, win1) = AddWindow();
        var (ctx2, win2) = AddWindow();
        ctx1.RootView = MakeRoot();
        ctx2.RootView = MakeRoot();

        win1.RaisePointerMoved(20, 20);

        ctx1.HoveredView.Should().BeSameAs(ctx1.RootView);
        ctx2.HoveredView.Should().BeNull();
        win1.LastCursor.Should().NotBeNull("hover transitions update the window cursor");
    }

    [Fact]
    public void Guarded_SwallowsViewExceptions_FromNativeInputCallbacks()
    {
        // Hard invariant: a view exception unwinding into a native callback
        // aborts the process. The Guarded wrapper must contain it.
        var (ctx, win) = AddWindow();
        var root = new ThrowingView { IsFocusable = true };
        root.Arrange(new Rect(0, 0, 800, 600));
        ctx.RootView = root;

        var act = () => win.RaisePointerPressed(10, 10);

        act.Should().NotThrow();
        root.Pressed.Should().Be(1, "the handler ran before throwing");
    }

    [Fact]
    public void OsFocus_TracksFocusedContext_AndFocusedViewForwarderFollowsIt()
    {
        var (ctx1, win1) = AddWindow();
        var (ctx2, win2) = AddWindow();
        var root1 = MakeRoot();
        var root2 = MakeRoot();
        ctx1.RootView = root1;
        ctx2.RootView = root2;

        _app.FocusedContext.Should().BeSameAs(ctx1, "defaults to primary before any OS focus event");

        win2.RaiseFocusGained();
        _app.FocusedContext.Should().BeSameAs(ctx2);

        win2.RaisePointerPressed(1, 1);
        _app.FocusedView.Should().BeSameAs(root2, "the app-level FocusedView follows the OS-focused window");

        win1.RaiseFocusGained();
        _app.FocusedContext.Should().BeSameAs(ctx1);
    }

    [Fact]
    public void FocusedContextReset_WhenFocusedWindowCloses()
    {
        var (ctx1, _) = AddWindow();
        var (ctx2, win2) = AddWindow();

        win2.RaiseFocusGained();
        _app.FocusedContext.Should().BeSameAs(ctx2);

        win2.RaiseCloseRequested();
        _app.ReapClosedContexts();

        _app.FocusedContext.Should().BeSameAs(ctx1, "falls back to primary once the focused window is gone");
    }

    #endregion

    #region Dialog routing

    [Fact]
    public void IsDialogHost_SingleWindow_AlwaysTrue()
    {
        var (ctx, _) = AddWindow();
        _app.IsDialogHost(ctx).Should().BeTrue();
    }

    [Fact]
    public void IsDialogHost_MultiWindow_TrueForAllWhileNoDialogIsActive()
    {
        var (ctx1, _) = AddWindow();
        var (ctx2, _) = AddWindow();

        // No modal dialog active: routing gate must not block normal input.
        _app.IsDialogHost(ctx1).Should().BeTrue();
        _app.IsDialogHost(ctx2).Should().BeTrue();
    }

    #endregion
}
