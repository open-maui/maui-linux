// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered navigation page with back stack support.
/// </summary>
public class SkiaNavigationPage : SkiaView
{
    private readonly Stack<SkiaPage> _navigationStack = new();
    private SkiaPage? _currentPage;
    private bool _isAnimating;
    private float _animationProgress;
    private SkiaPage? _incomingPage;
    private bool _isPushAnimation;

    // Navigation bar styling
    private SKColor _barBackgroundColor = new SKColor(0x21, 0x96, 0xF3);
    private SKColor _barTextColor = SKColors.White;
    private float _navigationBarHeight = 56;
    private bool _showBackButton = true;

    public SKColor BarBackgroundColor
    {
        get => _barBackgroundColor;
        set
        {
            _barBackgroundColor = value;
            UpdatePageNavigationBar();
            Invalidate();
        }
    }

    public SKColor BarTextColor
    {
        get => _barTextColor;
        set
        {
            _barTextColor = value;
            UpdatePageNavigationBar();
            Invalidate();
        }
    }

    public float NavigationBarHeight
    {
        get => _navigationBarHeight;
        set
        {
            _navigationBarHeight = value;
            UpdatePageNavigationBar();
            Invalidate();
        }
    }

    public SkiaPage? CurrentPage => _currentPage;
    public SkiaPage? RootPage => _navigationStack.Count > 0 ? _navigationStack.Last() : _currentPage;
    public int StackDepth => _navigationStack.Count + (_currentPage != null ? 1 : 0);

    public event EventHandler<NavigationEventArgs>? Pushed;
    public event EventHandler<NavigationEventArgs>? Popped;
    public event EventHandler<NavigationEventArgs>? PoppedToRoot;

    public SkiaNavigationPage()
    {
    }

    public SkiaNavigationPage(SkiaPage rootPage)
    {
        SetRootPage(rootPage);
    }

    public void SetRootPage(SkiaPage page)
    {
        _navigationStack.Clear();
        _currentPage?.OnDisappearing();
        _currentPage = page;
        _currentPage.Parent = this;
        ConfigurePage(_currentPage, false);
        _currentPage.OnAppearing();
        Invalidate();
    }

    public void Push(SkiaPage page, bool animated = true)
    {
        if (_isAnimating) return;

        if (_currentPage != null)
        {
            _currentPage.OnDisappearing();
            _navigationStack.Push(_currentPage);
        }

        ConfigurePage(page, true);
        page.Parent = this;

        if (animated)
        {
            _incomingPage = page;
            _isPushAnimation = true;
            _animationProgress = 0;
            _isAnimating = true;
            AnimatePush();
        }
        else
        {
            _currentPage = page;
            _currentPage.OnAppearing();
            Invalidate();
        }

        Pushed?.Invoke(this, new NavigationEventArgs(page));
    }

    public SkiaPage? Pop(bool animated = true)
    {
        if (_isAnimating || _navigationStack.Count == 0) return null;

        var poppedPage = _currentPage;
        poppedPage?.OnDisappearing();

        var previousPage = _navigationStack.Pop();

        if (animated && poppedPage != null)
        {
            _incomingPage = previousPage;
            _isPushAnimation = false;
            _animationProgress = 0;
            _isAnimating = true;
            AnimatePop(poppedPage);
        }
        else
        {
            _currentPage = previousPage;
            _currentPage?.OnAppearing();
            Invalidate();
        }

        if (poppedPage != null)
        {
            Popped?.Invoke(this, new NavigationEventArgs(poppedPage));
        }

        return poppedPage;
    }

    public void PopToRoot(bool animated = true)
    {
        if (_isAnimating || _navigationStack.Count == 0) return;

        _currentPage?.OnDisappearing();

        // Get root page
        SkiaPage? rootPage = null;
        while (_navigationStack.Count > 0)
        {
            rootPage = _navigationStack.Pop();
        }

        if (rootPage != null)
        {
            _currentPage = rootPage;
            ConfigurePage(_currentPage, false);
            _currentPage.OnAppearing();
            Invalidate();
        }

        PoppedToRoot?.Invoke(this, new NavigationEventArgs(_currentPage!));
    }

    private void ConfigurePage(SkiaPage page, bool showBackButton)
    {
        page.ShowNavigationBar = true;
        page.TitleBarColor = _barBackgroundColor;
        page.TitleTextColor = _barTextColor;
        page.NavigationBarHeight = _navigationBarHeight;
        _showBackButton = showBackButton && _navigationStack.Count > 0;
    }

    private void UpdatePageNavigationBar()
    {
        if (_currentPage != null)
        {
            _currentPage.TitleBarColor = _barBackgroundColor;
            _currentPage.TitleTextColor = _barTextColor;
            _currentPage.NavigationBarHeight = _navigationBarHeight;
        }
    }

    private async void AnimatePush()
    {
        const int durationMs = 250;
        const int frameMs = 16;
        var startTime = DateTime.Now;

        while (_animationProgress < 1)
        {
            await Task.Delay(frameMs);
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            _animationProgress = Math.Min(1, (float)(elapsed / durationMs));
            Invalidate();
        }

        _currentPage = _incomingPage;
        _incomingPage = null;
        _isAnimating = false;
        _currentPage?.OnAppearing();
        Invalidate();
    }

    private async void AnimatePop(SkiaPage outgoingPage)
    {
        const int durationMs = 250;
        const int frameMs = 16;
        var startTime = DateTime.Now;

        while (_animationProgress < 1)
        {
            await Task.Delay(frameMs);
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            _animationProgress = Math.Min(1, (float)(elapsed / durationMs));
            Invalidate();
        }

        _currentPage = _incomingPage;
        _incomingPage = null;
        _isAnimating = false;
        _currentPage?.OnAppearing();
        outgoingPage.Parent = null;
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        if (BackgroundColor != SKColors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = BackgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(bounds, bgPaint);
        }

        if (_isAnimating && _incomingPage != null)
        {
            // Draw animation
            var eased = EaseOutCubic(_animationProgress);

            if (_isPushAnimation)
            {
                // Push: current page slides left, incoming slides from right
                var currentOffset = -bounds.Width * eased;
                var incomingOffset = bounds.Width * (1 - eased);

                // Draw current page (sliding out)
                if (_currentPage != null)
                {
                    canvas.Save();
                    canvas.Translate(currentOffset, 0);
                    _currentPage.Bounds = bounds;
                    _currentPage.Draw(canvas);
                    canvas.Restore();
                }

                // Draw incoming page
                canvas.Save();
                canvas.Translate(incomingOffset, 0);
                _incomingPage.Bounds = bounds;
                _incomingPage.Draw(canvas);
                canvas.Restore();
            }
            else
            {
                // Pop: incoming slides from left, current slides right
                var incomingOffset = -bounds.Width * (1 - eased);
                var currentOffset = bounds.Width * eased;

                // Draw incoming page (sliding in)
                canvas.Save();
                canvas.Translate(incomingOffset, 0);
                _incomingPage.Bounds = bounds;
                _incomingPage.Draw(canvas);
                canvas.Restore();

                // Draw current page (sliding out)
                if (_currentPage != null)
                {
                    canvas.Save();
                    canvas.Translate(currentOffset, 0);
                    _currentPage.Bounds = bounds;
                    _currentPage.Draw(canvas);
                    canvas.Restore();
                }
            }
        }
        else if (_currentPage != null)
        {
            // Draw current page normally
            _currentPage.Bounds = bounds;
            _currentPage.Draw(canvas);

            // Draw back button if applicable
            if (_showBackButton && _navigationStack.Count > 0)
            {
                DrawBackButton(canvas, bounds);
            }
        }
    }

    private void DrawBackButton(SKCanvas canvas, SKRect bounds)
    {
        var buttonBounds = new SKRect(bounds.Left + 8, bounds.Top + 12, bounds.Left + 48, bounds.Top + _navigationBarHeight - 12);

        using var paint = new SKPaint
        {
            Color = _barTextColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Draw back arrow
        var centerY = buttonBounds.MidY;
        var arrowSize = 10f;
        var left = buttonBounds.Left + 8;

        using var path = new SKPath();
        path.MoveTo(left + arrowSize, centerY - arrowSize);
        path.LineTo(left, centerY);
        path.LineTo(left + arrowSize, centerY + arrowSize);
        canvas.DrawPath(path, paint);
    }

    private static float EaseOutCubic(float t)
    {
        return 1 - (float)Math.Pow(1 - t, 3);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return availableSize;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Console.WriteLine($"[SkiaNavigationPage] OnPointerPressed at ({e.X}, {e.Y}), _isAnimating={_isAnimating}");
        if (_isAnimating) return;

        // Check for back button click
        if (_showBackButton && _navigationStack.Count > 0)
        {
            if (e.X < 56 && e.Y < _navigationBarHeight)
            {
                Console.WriteLine($"[SkiaNavigationPage] Back button clicked");
                Pop();
                return;
            }
        }

        Console.WriteLine($"[SkiaNavigationPage] Forwarding to _currentPage: {_currentPage?.GetType().Name}");
        _currentPage?.OnPointerPressed(e);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (_isAnimating) return;
        _currentPage?.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (_isAnimating) return;
        _currentPage?.OnPointerReleased(e);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (_isAnimating) return;

        // Handle back navigation with Escape or Backspace
        if ((e.Key == Key.Escape || e.Key == Key.Backspace) && _navigationStack.Count > 0)
        {
            Pop();
            e.Handled = true;
            return;
        }

        _currentPage?.OnKeyDown(e);
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        if (_isAnimating) return;
        _currentPage?.OnKeyUp(e);
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        if (_isAnimating) return;
        _currentPage?.OnScroll(e);
    }

    public override SkiaView? HitTest(float x, float y)
    {
        if (!IsVisible)
            return null;

        // Back button area - return self so OnPointerPressed handles it
        if (_showBackButton && _navigationStack.Count > 0 && x < 56 && y < _navigationBarHeight)
        {
            return this;
        }

        // Check current page
        if (_currentPage != null)
        {
            try
            {
                var hit = _currentPage.HitTest(x, y);
                if (hit != null)
                    return hit;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkiaNavigationPage] HitTest error: {ex.Message}");
            }
        }

        return this;
    }
}

/// <summary>
/// Event args for navigation events.
/// </summary>
public class NavigationEventArgs : EventArgs
{
    public SkiaPage Page { get; }

    public NavigationEventArgs(SkiaPage page)
    {
        Page = page;
    }
}
