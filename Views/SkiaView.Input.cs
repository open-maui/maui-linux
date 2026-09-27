// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform;

public abstract partial class SkiaView
{
    #region Input Events

    /// <summary>
    /// Bubbles a pointer event action up the MAUI visual tree from this view's MauiView.
    /// This enables controls like LiveCharts that handle pointer events at a parent level
    /// (e.g., PieChart is a grandparent of SKCanvasView).
    /// Coordinates stay in window-logical space — GestureManager's contract —
    /// so the GetPosition resolvers it hands to MAUI event args translate them
    /// once, against the requested element's ScreenBounds. (Passing
    /// view-relative points here made GetPosition(element) subtract the
    /// element origin twice and GetPosition(null) return a relative point.)
    /// </summary>
    private void BubblePointerEvent(PointerEventArgs e, RoutedPointerKind kind, Action<Microsoft.Maui.Controls.View, double, double> action)
    {
        var current = MauiView as Microsoft.Maui.Controls.Element;
        while (current != null)
        {
            if (current is Microsoft.Maui.Controls.View view
                && (view.Handler?.PlatformView is SkiaView || current == MauiView))
            {
                action(view, e.X, e.Y);
                RaisePointerRouted(view, kind, e);
            }
            current = current.Parent;
        }
    }

    internal enum RoutedPointerKind { Entered, Exited, Moved, Pressed, Released }

    /// <summary>
    /// Raised once for every MAUI view a pointer event reaches: the view under
    /// the pointer and each ancestor it bubbles to, in window-logical
    /// coordinates. Lets extension packages feed third-party input pipelines
    /// that expect native per-view touch events (Syncfusion's detectors).
    /// </summary>
    internal static event Action<Microsoft.Maui.Controls.View, RoutedPointerKind, PointerEventArgs>? PointerRouted;

    /// <summary>
    /// Raises <see cref="PointerRouted"/> for this view and its ancestors
    /// without running their gesture recognizers: for events a layout handles
    /// on its own surface (no child under the pointer), which do not bubble.
    /// </summary>
    private protected void RaisePointerRoutedChain(RoutedPointerKind kind, PointerEventArgs e)
    {
        if (PointerRouted == null)
            return;
        for (var current = MauiView as Microsoft.Maui.Controls.Element; current != null; current = current.Parent)
        {
            if (current is Microsoft.Maui.Controls.View view
                && (view.Handler?.PlatformView is SkiaView || current == MauiView))
                RaisePointerRouted(view, kind, e);
        }
    }

    internal static void RaisePointerRouted(Microsoft.Maui.Controls.View? view, RoutedPointerKind kind, PointerEventArgs e)
    {
        var handler = PointerRouted;
        if (handler == null || view == null)
            return;
        try
        {
            handler(view, kind, e);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("SkiaView", $"Pointer observer failed for {view.GetType().Name}", ex);
        }
    }

    /// <summary>
    /// Raised by the base pointer/focus handlers so external observers (the
    /// MAUI VisualStateManager bridge, tests) can follow interaction state
    /// without subclassing. Subclasses that override the On* methods without
    /// calling base do not raise these; interactive controls instead report
    /// their transitions through <see cref="VisualStateRequested"/>.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerEntered;
    public event EventHandler<PointerEventArgs>? PointerExited;
    public event EventHandler<PointerEventArgs>? PointerPressed;
    public event EventHandler<PointerEventArgs>? PointerReleased;
    public event EventHandler? FocusGained;
    public event EventHandler? FocusLost;

    /// <summary>
    /// Raised whenever a control asks <see cref="SkiaVisualStateManager"/> to
    /// enter a named state ("Normal", "PointerOver", "Pressed", "Focused",
    /// "Disabled", ...). Fires even when the view has no Skia-side visual
    /// state groups, so it doubles as the interaction feed for the MAUI
    /// VisualStateManager bridge.
    /// </summary>
    public event EventHandler<string>? VisualStateRequested;

    internal void RaiseVisualStateRequested(string stateName)
    {
        VisualStateRequested?.Invoke(this, stateName);
    }

    public virtual void OnPointerEntered(PointerEventArgs e)
    {
        PointerEntered?.Invoke(this, e);
        BubblePointerEvent(e, RoutedPointerKind.Entered, GestureManager.ProcessPointerEntered);
    }

    public virtual void OnPointerExited(PointerEventArgs e)
    {
        PointerExited?.Invoke(this, e);
        BubblePointerEvent(e, RoutedPointerKind.Exited, GestureManager.ProcessPointerExited);
    }

    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        BubblePointerEvent(e, RoutedPointerKind.Moved, GestureManager.ProcessPointerMove);
    }

    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        PointerPressed?.Invoke(this, e);
        BubblePointerEvent(e, RoutedPointerKind.Pressed, GestureManager.ProcessPointerDown);
    }

    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        PointerReleased?.Invoke(this, e);
        BubblePointerEvent(e, RoutedPointerKind.Released, GestureManager.ProcessPointerUp);
    }

    public virtual void OnScroll(ScrollEventArgs e) { }
    public virtual void OnKeyDown(KeyEventArgs e) { }
    public virtual void OnKeyUp(KeyEventArgs e) { }
    public virtual void OnTextInput(TextInputEventArgs e) { }

    public virtual void OnFocusGained()
    {
        IsFocused = true;
        FocusGained?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public virtual void OnFocusLost()
    {
        IsFocused = false;
        FocusLost?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up gesture tracking to prevent memory leaks
                if (MauiView != null)
                {
                    GestureManager.CleanupView(MauiView);
                }

                foreach (var child in _children.ToArray())
                {
                    child.Dispose();
                }
                _children.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
