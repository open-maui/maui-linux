// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Base class for all Skia-rendered views on Linux.
/// </summary>
public abstract class SkiaView : IDisposable
{
    // Popup overlay system for dropdowns, calendars, etc.
    private static readonly List<(SkiaView Owner, Action<SKCanvas> Draw)> _popupOverlays = new();

    public static void RegisterPopupOverlay(SkiaView owner, Action<SKCanvas> drawAction)
    {
        _popupOverlays.RemoveAll(p => p.Owner == owner);
        _popupOverlays.Add((owner, drawAction));
    }

    public static void UnregisterPopupOverlay(SkiaView owner)
    {
        _popupOverlays.RemoveAll(p => p.Owner == owner);
    }

    public static void DrawPopupOverlays(SKCanvas canvas)
    {
        // Restore canvas to clean state for overlay drawing
        // Save count tells us how many unmatched Saves there are
        while (canvas.SaveCount > 1)
        {
            canvas.Restore();
        }
        
        foreach (var (_, draw) in _popupOverlays)
        {
            canvas.Save();
            draw(canvas);
            canvas.Restore();
        }
    }

    /// <summary>
    /// Gets the absolute bounds of this view in screen coordinates.
    /// </summary>
    public SKRect GetAbsoluteBounds()
    {
        var bounds = Bounds;
        var current = Parent;
        while (current != null)
        {
            // Adjust for scroll offset if parent is a ScrollView
            if (current is SkiaScrollView scrollView)
            {
                bounds = new SKRect(
                    bounds.Left - scrollView.ScrollX,
                    bounds.Top - scrollView.ScrollY,
                    bounds.Right - scrollView.ScrollX,
                    bounds.Bottom - scrollView.ScrollY);
            }
            current = current.Parent;
        }
        return bounds;
    }

    private bool _disposed;
    private SKRect _bounds;
    private bool _isVisible = true;
    private bool _isEnabled = true;
    private float _opacity = 1.0f;
    private SKColor _backgroundColor = SKColors.Transparent;
    private SkiaView? _parent;
    private readonly List<SkiaView> _children = new();

    /// <summary>
    /// Gets or sets the bounds of this view in parent coordinates.
    /// </summary>
    public SKRect Bounds
    {
        get => _bounds;
        set
        {
            if (_bounds != value)
            {
                _bounds = value;
                OnBoundsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this view is visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this view is enabled for interaction.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the opacity of this view (0.0 to 1.0).
    /// </summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (_opacity != clamped)
            {
                _opacity = clamped;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public SKColor BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the requested width.
    /// </summary>
    public double RequestedWidth { get; set; } = -1;

    /// <summary>
    /// Gets or sets the requested height.
    /// </summary>
    public double RequestedHeight { get; set; } = -1;

    /// <summary>
    /// Gets or sets whether this view can receive keyboard focus.
    /// </summary>
    public bool IsFocusable { get; set; }

    /// <summary>
    /// Gets or sets whether this view currently has keyboard focus.
    /// </summary>
    public bool IsFocused { get; internal set; }

    /// <summary>
    /// Gets or sets the parent view.
    /// </summary>
    public SkiaView? Parent
    {
        get => _parent;
        internal set => _parent = value;
    }

    /// <summary>
    /// Gets the desired size calculated during measure.
    /// </summary>
    public SKSize DesiredSize { get; protected set; }

    /// <summary>
    /// Gets the child views.
    /// </summary>
    public IReadOnlyList<SkiaView> Children => _children;

    /// <summary>
    /// Event raised when this view needs to be redrawn.
    /// </summary>
    public event EventHandler? Invalidated;

    /// <summary>
    /// Adds a child view.
    /// </summary>
    public void AddChild(SkiaView child)
    {
        if (child._parent != null)
            throw new InvalidOperationException("View already has a parent");

        child._parent = this;
        _children.Add(child);
        Invalidate();
    }

    /// <summary>
    /// Removes a child view.
    /// </summary>
    public void RemoveChild(SkiaView child)
    {
        if (child._parent != this)
            return;

        child._parent = null;
        _children.Remove(child);
        Invalidate();
    }

    /// <summary>
    /// Inserts a child view at the specified index.
    /// </summary>
    public void InsertChild(int index, SkiaView child)
    {
        if (child._parent != null)
            throw new InvalidOperationException("View already has a parent");

        child._parent = this;
        _children.Insert(index, child);
        Invalidate();
    }

    /// <summary>
    /// Removes all child views.
    /// </summary>
    public void ClearChildren()
    {
        foreach (var child in _children)
        {
            child._parent = null;
        }
        _children.Clear();
        Invalidate();
    }

    /// <summary>
    /// Requests that this view be redrawn.
    /// </summary>
    public void Invalidate()
    {
        Invalidated?.Invoke(this, EventArgs.Empty);
        _parent?.Invalidate();
    }

    /// <summary>
    /// Invalidates the cached measurement.
    /// </summary>
    public void InvalidateMeasure()
    {
        DesiredSize = SKSize.Empty;
        _parent?.InvalidateMeasure();
        Invalidate();
    }

    /// <summary>
    /// Draws this view and its children to the canvas.
    /// </summary>
    public void Draw(SKCanvas canvas)
    {
        if (!IsVisible || Opacity <= 0)
            return;

        canvas.Save();

        // Apply opacity
        if (Opacity < 1.0f)
        {
            canvas.SaveLayer(new SKPaint { Color = SKColors.White.WithAlpha((byte)(Opacity * 255)) });
        }

        // Draw background at absolute bounds
        if (BackgroundColor != SKColors.Transparent)
        {
            using var paint = new SKPaint { Color = BackgroundColor };
            canvas.DrawRect(Bounds, paint);
        }

        // Draw content at absolute bounds
        OnDraw(canvas, Bounds);

        // Draw children - they draw at their own absolute bounds
        foreach (var child in _children)
        {
            child.Draw(canvas);
        }

        if (Opacity < 1.0f)
        {
            canvas.Restore();
        }

        canvas.Restore();
    }

    /// <summary>
    /// Override to draw custom content.
    /// </summary>
    protected virtual void OnDraw(SKCanvas canvas, SKRect bounds)
    {
    }

    /// <summary>
    /// Called when the bounds change.
    /// </summary>
    protected virtual void OnBoundsChanged()
    {
        Invalidate();
    }

    /// <summary>
    /// Measures the desired size of this view.
    /// </summary>
    public SKSize Measure(SKSize availableSize)
    {
        DesiredSize = MeasureOverride(availableSize);
        return DesiredSize;
    }

    /// <summary>
    /// Override to provide custom measurement.
    /// </summary>
    protected virtual SKSize MeasureOverride(SKSize availableSize)
    {
        var width = RequestedWidth >= 0 ? (float)RequestedWidth : 0;
        var height = RequestedHeight >= 0 ? (float)RequestedHeight : 0;
        return new SKSize(width, height);
    }

    /// <summary>
    /// Arranges this view within the given bounds.
    /// </summary>
    public void Arrange(SKRect bounds)
    {
        Bounds = ArrangeOverride(bounds);
    }

    /// <summary>
    /// Override to customize arrangement within the given bounds.
    /// </summary>
    protected virtual SKRect ArrangeOverride(SKRect bounds)
    {
        return bounds;
    }

    /// <summary>
    /// Performs hit testing to find the view at the given point.
    /// </summary>
    public virtual SkiaView? HitTest(SKPoint point)
    {
        return HitTest(point.X, point.Y);
    }

    /// <summary>
    /// Performs hit testing to find the view at the given coordinates.
    /// </summary>
    public virtual SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled)
            return null;

        if (!Bounds.Contains(x, y))
            return null;

        // Check children in reverse order (top-most first)
        var localX = x - Bounds.Left;
        var localY = y - Bounds.Top;
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var hit = _children[i].HitTest(localX, localY);
            if (hit != null)
                return hit;
        }

        return this;
    }

    #region Input Events

    public virtual void OnPointerEntered(PointerEventArgs e) { }
    public virtual void OnPointerExited(PointerEventArgs e) { }
    public virtual void OnPointerMoved(PointerEventArgs e) { }
    public virtual void OnPointerPressed(PointerEventArgs e) { }
    public virtual void OnPointerReleased(PointerEventArgs e) { }
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

/// <summary>
/// Event args for pointer events.
/// </summary>
public class PointerEventArgs : EventArgs
{
    public float X { get; }
    public float Y { get; }
    public PointerButton Button { get; }
    public bool Handled { get; set; }

    public PointerEventArgs(float x, float y, PointerButton button = PointerButton.None)
    {
        X = x;
        Y = y;
        Button = button;
    }
}

/// <summary>
/// Mouse button flags.
/// </summary>
[Flags]
public enum PointerButton
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 4,
    XButton1 = 8,
    XButton2 = 16
}

/// <summary>
/// Event args for scroll events.
/// </summary>
public class ScrollEventArgs : EventArgs
{
    public float X { get; }
    public float Y { get; }
    public float DeltaX { get; }
    public float DeltaY { get; }
    public bool Handled { get; set; }

    public ScrollEventArgs(float x, float y, float deltaX, float deltaY)
    {
        X = x;
        Y = y;
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}

/// <summary>
/// Event args for keyboard events.
/// </summary>
public class KeyEventArgs : EventArgs
{
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }
    public bool Handled { get; set; }

    public KeyEventArgs(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        Modifiers = modifiers;
    }
}

/// <summary>
/// Event args for text input events.
/// </summary>
public class TextInputEventArgs : EventArgs
{
    public string Text { get; }
    public bool Handled { get; set; }

    public TextInputEventArgs(string text)
    {
        Text = text;
    }
}

/// <summary>
/// Keyboard modifier flags.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Super = 8,
    CapsLock = 16,
    NumLock = 32
}
