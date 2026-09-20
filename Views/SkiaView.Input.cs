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
    private void BubblePointerEvent(double absX, double absY, Action<Microsoft.Maui.Controls.View, double, double> action)
    {
        var current = MauiView as Microsoft.Maui.Controls.Element;
        while (current != null)
        {
            if (current is Microsoft.Maui.Controls.View view
                && (view.Handler?.PlatformView is SkiaView || current == MauiView))
            {
                action(view, absX, absY);
            }
            current = current.Parent;
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
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerEntered);
    }

    public virtual void OnPointerExited(PointerEventArgs e)
    {
        PointerExited?.Invoke(this, e);
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerExited);
    }

    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerMove);
    }

    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        PointerPressed?.Invoke(this, e);
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerDown);
    }

    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        PointerReleased?.Invoke(this, e);
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerUp);
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

                foreach (var child in _children)
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
