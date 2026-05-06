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
    /// Converts absolute window coordinates to view-relative coordinates.
    /// MAUI gesture recognizers expect coordinates relative to the view.
    /// </summary>
    private (double X, double Y) ToViewRelative(double absX, double absY)
    {
        return (absX - Bounds.Left, absY - Bounds.Top);
    }

    /// <summary>
    /// Bubbles a pointer event action up the MAUI visual tree from this view's MauiView.
    /// Each parent receives coordinates relative to itself.
    /// This enables controls like LiveCharts that handle pointer events at a parent level
    /// (e.g., PieChart is a grandparent of SKCanvasView).
    /// </summary>
    private void BubblePointerEvent(double absX, double absY, Action<Microsoft.Maui.Controls.View, double, double> action)
    {
        var current = MauiView as Microsoft.Maui.Controls.Element;
        while (current != null)
        {
            if (current is Microsoft.Maui.Controls.View view)
            {
                // Compute coordinates relative to this view's bounds
                var handler = view.Handler;
                if (handler?.PlatformView is SkiaView skiaView)
                {
                    var (rx, ry) = (absX - skiaView.Bounds.Left, absY - skiaView.Bounds.Top);
                    action(view, rx, ry);
                }
                else if (current == MauiView)
                {
                    // For the directly hit view, use our own bounds
                    var (rx, ry) = ToViewRelative(absX, absY);
                    action(view, rx, ry);
                }
            }
            current = current.Parent;
        }
    }

    public virtual void OnPointerEntered(PointerEventArgs e)
    {
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerEntered);
    }

    public virtual void OnPointerExited(PointerEventArgs e)
    {
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerExited);
    }

    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerMove);
    }

    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerDown);
    }

    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        BubblePointerEvent(e.X, e.Y, GestureManager.ProcessPointerUp);
    }

    public virtual void OnScroll(ScrollEventArgs e) { }
    public virtual void OnKeyDown(KeyEventArgs e) { }
    public virtual void OnKeyUp(KeyEventArgs e) { }
    public virtual void OnTextInput(TextInputEventArgs e) { }

    public virtual void OnFocusGained()
    {
        IsFocused = true;
        Invalidate();
    }

    public virtual void OnFocusLost()
    {
        IsFocused = false;
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
