// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform;

public abstract partial class SkiaView
{
    #region Input Events

    public virtual void OnPointerEntered(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerEntered(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerExited(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerExited(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerMove(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        if (MauiView != null)
        {
            GestureManager.ProcessPointerDown(MauiView, e.X, e.Y);
        }
    }

    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaView", $"OnPointerReleased on {GetType().Name}, MauiView={MauiView?.GetType().Name ?? "null"}");
        if (MauiView != null)
        {
            GestureManager.ProcessPointerUp(MauiView, e.X, e.Y);
        }
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
