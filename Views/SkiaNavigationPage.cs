// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered navigation page with back stack support.
/// </summary>
public class SkiaNavigationPage : SkiaView
{
    private readonly Stack<SkiaPage> _navigationStack = new();
    private SkiaPage? _currentPage;

    /// <summary>
    /// Theme-refresh walker hook: yield the current page and every page on the
    /// back stack. NavigationPage keeps these in private fields rather than
    /// the standard Children list, so the walker needs an explicit pointer.
    /// </summary>
    public override IEnumerable<SkiaView> ExtraContentRoots
    {
        get
        {
            if (_currentPage != null) yield return _currentPage;
            foreach (var page in _navigationStack)
                if (page != null) yield return page;
        }
    }
    private bool _isAnimating;
    private float _animationProgress;
    private SkiaPage? _incomingPage;
    private bool _isPushAnimation;

    // Navigation bar styling
    private SKColor _barBackgroundColor = SkiaTheme.PrimarySK;
    private SKColor _barTextColor = SKColors.White;
    private Color _barBackgroundColorMaui = Color.FromRgb(0x21, 0x96, 0xF3);
    private Color _barTextColorMaui = Colors.White;
    private float _navigationBarHeight = 56;
    private SkiaPage? _outgoingPage;
    private int _transitionId;

    public Color BarBackgroundColor
    {
        get => _barBackgroundColorMaui;
        set
        {
            _barBackgroundColorMaui = value;
            _barBackgroundColor = value.ToSKColor();
            UpdatePageNavigationBar();
            Invalidate();
        }
    }

    public Color BarTextColor
    {
        get => _barTextColorMaui;
        set
        {
            _barTextColorMaui = value;
            _barTextColor = value.ToSKColor();
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

    /// <summary>True while a push or pop is animating (the stack has already changed).</summary>
    public bool IsTransitioning => _isAnimating;

    /// <summary>Raised when an animated push or pop has finished and its page is current.</summary>
    public event EventHandler? TransitionCompleted;

    /// <summary>
    /// The back arrow is shown: a page is below the current one and the current page
    /// shows its navigation bar with a back button (NavigationPage.HasBackButton).
    /// </summary>
    public bool IsBackButtonVisible =>
        _navigationStack.Count > 0 && _currentPage is { ShowNavigationBar: true, HasBackButton: true };

    /// <summary>
    /// Asked when the user presses the back arrow (or Escape/Backspace): return true
    /// when the request was handled (a MAUI NavigationPage pops through its own
    /// navigation, which then drives this view). Unset or false: the view pops itself.
    /// </summary>
    public Func<bool>? BackRequested { get; set; }

    private void RequestBack()
    {
        if (BackRequested?.Invoke() == true)
            return;
        Pop();
    }

    /// <summary>
    /// Shows exactly <paramref name="pages"/> (bottom first): pushes or pops when the
    /// new stack extends or shortens the current one, replaces the pages beneath when
    /// only those changed, and otherwise swaps the whole stack.
    /// </summary>
    public void SetNavigationStack(IReadOnlyList<SkiaPage> pages, bool animated = true)
    {
        if (pages == null || pages.Count == 0)
            return;
        FinishTransition();

        var current = _navigationStack.Reverse().ToList();
        if (_currentPage != null)
            current.Add(_currentPage);
        if (current.Count == pages.Count && current.SequenceEqual(pages))
            return;

        var newTop = pages[pages.Count - 1];
        if (current.Count == 0)
        {
            SetRootPage(pages[0]);
            for (int i = 1; i < pages.Count; i++)
                Push(pages[i], false);
            return;
        }

        if (ReferenceEquals(current[current.Count - 1], newTop))
        {
            // Same page on screen; only the pages beneath it changed.
            ReplaceBackStack(pages);
            Invalidate();
            return;
        }

        if (pages.Count > current.Count && pages.Take(current.Count).SequenceEqual(current))
        {
            for (int i = current.Count; i < pages.Count; i++)
                Push(pages[i], animated && i == pages.Count - 1);
            return;
        }

        if (pages.Count < current.Count && current.Take(pages.Count).SequenceEqual(pages))
        {
            // Pages between the new top and the current one go without a transition.
            ReplaceBackStack(pages.Concat(new[] { _currentPage! }).ToList());
            Pop(animated);
            return;
        }

        // A different stack: swap to it.
        var old = _currentPage;
        old?.OnDisappearing();
        if (old != null && !pages.Contains(old))
            old.Parent = null;
        ReplaceBackStack(pages);
        _currentPage = newTop;
        newTop.Parent = this;
        ConfigurePage(newTop, _navigationStack.Count > 0);
        InvalidateMeasure();
        newTop.OnAppearing();
        Invalidate();
    }

    /// <summary>Puts every page of <paramref name="pages"/> but the last beneath the current page.</summary>
    private void ReplaceBackStack(IReadOnlyList<SkiaPage> pages)
    {
        foreach (var page in _navigationStack)
            if (!pages.Contains(page) && ReferenceEquals(page.Parent, this))
                page.Parent = null;
        _navigationStack.Clear();
        for (int i = 0; i < pages.Count - 1; i++)
        {
            pages[i].Parent = this;
            _navigationStack.Push(pages[i]);
        }
    }

    /// <summary>Ends a running push or pop animation at once (its page becomes current).</summary>
    private void FinishTransition()
    {
        if (!_isAnimating)
            return;
        _transitionId++;
        _animationProgress = 1;
        _currentPage = _incomingPage;
        _incomingPage = null;
        _isAnimating = false;
        if (_outgoingPage != null)
        {
            // A popped page leaves the tree; it is not on the back stack any more.
            if (!_navigationStack.Contains(_outgoingPage))
                _outgoingPage.Parent = null;
            _outgoingPage = null;
        }
        InvalidateMeasure();
        Invalidate();
        TransitionCompleted?.Invoke(this, EventArgs.Empty);
    }

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
        InvalidateMeasure(); // the new page is arranged (and gets its frame) next layout
        _currentPage.Parent = this;
        ConfigurePage(_currentPage, false);
        _currentPage.OnAppearing();
        Invalidate();
    }

    public void Push(SkiaPage page, bool animated = true)
    {
        // A push during a transition ends the transition first (it was dropped,
        // losing the page, when an app pushed again as soon as PushAsync returned).
        FinishTransition();

        // Disable animation in GTK mode
        if (LinuxApplication.IsGtkMode)
        {
            animated = false;
        }

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
            InvalidateMeasure(); // the new page is arranged (and gets its frame) next layout
            _isPushAnimation = true;
            _animationProgress = 0;
            _isAnimating = true;
            // Fire OnAppearing BEFORE the first frame that presents the page,
            // matching other platforms where OnAppearing completes before the
            // page becomes visible. Any state the handler/app sets up in
            // OnAppearing is then reflected from the first animation frame.
            _incomingPage.OnAppearing();
            AnimatePush();
        }
        else
        {
            DiagnosticLog.Debug("SkiaNavigationPage", "Push (no animation): setting _currentPage to " + page.Title);
            _currentPage = page;
            InvalidateMeasure(); // the new page is arranged (and gets its frame) next layout
            _currentPage.OnAppearing();
            DiagnosticLog.Debug("SkiaNavigationPage", "Push: calling Invalidate");
            Invalidate();
            DiagnosticLog.Debug("SkiaNavigationPage", "Push: Invalidate called, _currentPage is now " + _currentPage?.Title);
        }

        Pushed?.Invoke(this, new NavigationEventArgs(page));
    }

    public SkiaPage? Pop(bool animated = true)
    {
        FinishTransition();
        if (_navigationStack.Count == 0) return null;

        // Disable animation in GTK mode
        if (LinuxApplication.IsGtkMode)
        {
            animated = false;
        }

        var poppedPage = _currentPage;
        poppedPage?.OnDisappearing();

        var previousPage = _navigationStack.Pop();

        if (animated && poppedPage != null)
        {
            _incomingPage = previousPage;
            InvalidateMeasure(); // the new page is arranged (and gets its frame) next layout
            _isPushAnimation = false;
            _animationProgress = 0;
            _isAnimating = true;
            _outgoingPage = poppedPage;
            // Fire OnAppearing BEFORE the first frame that presents the restored
            // page (matching other platforms). Apps commonly refresh state here
            // (e.g. resetting a CollectionView's ItemsSource); doing it before the
            // animation means the page never paints stale content that is then
            // visibly replaced after the animation settles.
            _incomingPage.OnAppearing();
            AnimatePop(poppedPage);
        }
        else
        {
            _currentPage = previousPage;
            InvalidateMeasure(); // the new page is arranged (and gets its frame) next layout
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
        FinishTransition();
        if (_navigationStack.Count == 0) return;

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
            InvalidateMeasure(); // the new page is arranged (and gets its frame) next layout
            ConfigurePage(_currentPage, false);
            _currentPage.OnAppearing();
            Invalidate();
        }

        PoppedToRoot?.Invoke(this, new NavigationEventArgs(_currentPage!));
    }

    private void ConfigurePage(SkiaPage page, bool showBackButton)
    {
        page.ShowNavigationBar = true;
        page.TitleBarColor = _barBackgroundColorMaui;
        page.TitleTextColor = _barTextColorMaui;
        page.NavigationBarHeight = _navigationBarHeight;
    }

    private void UpdatePageNavigationBar()
    {
        if (_currentPage != null)
        {
            _currentPage.TitleBarColor = _barBackgroundColorMaui;
            _currentPage.TitleTextColor = _barTextColorMaui;
            _currentPage.NavigationBarHeight = _navigationBarHeight;
        }
    }

    private async void AnimatePush()
    {
        const int durationMs = 250;
        const int frameMs = 16;
        var startTime = DateTime.Now;
        var id = _transitionId;

        while (_animationProgress < 1 && id == _transitionId)
        {
            await Task.Delay(frameMs);
            if (id != _transitionId) return; // finished early by another navigation
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            _animationProgress = Math.Min(1, (float)(elapsed / durationMs));
            Invalidate();
        }

        // OnAppearing already fired in Push() before the animation started.
        if (id == _transitionId)
            FinishTransition();
    }

    private async void AnimatePop(SkiaPage outgoingPage)
    {
        const int durationMs = 250;
        const int frameMs = 16;
        var startTime = DateTime.Now;
        var id = _transitionId;

        while (_animationProgress < 1 && id == _transitionId)
        {
            await Task.Delay(frameMs);
            if (id != _transitionId) return; // finished early by another navigation
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            _animationProgress = Math.Min(1, (float)(elapsed / durationMs));
            Invalidate();
        }

        // OnAppearing already fired in Pop() before the animation started.
        if (id == _transitionId)
            FinishTransition();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint
            {
                Color = GetEffectiveBackgroundColor(),
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
                    _currentPage.Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                    _currentPage.Draw(canvas);
                    canvas.Restore();
                }

                // Draw incoming page
                canvas.Save();
                canvas.Translate(incomingOffset, 0);
                _incomingPage.Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
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
                _incomingPage.Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                _incomingPage.Draw(canvas);
                canvas.Restore();

                // Draw current page (sliding out)
                if (_currentPage != null)
                {
                    canvas.Save();
                    canvas.Translate(currentOffset, 0);
                    _currentPage.Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                    _currentPage.Draw(canvas);
                    canvas.Restore();
                }
            }
        }
        else if (_currentPage != null)
        {
            // Draw current page normally
            DiagnosticLog.Debug("SkiaNavigationPage", "OnDraw: drawing _currentPage=" + _currentPage.Title);
            _currentPage.Bounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            _currentPage.Draw(canvas);

            // Draw back button if applicable
            if (IsBackButtonVisible)
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

    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }

    /// <summary>
    /// The pages fill the navigation page, as on MAUI's platforms: arranging them
    /// (not only drawing them at its bounds) lays their content out and gives each
    /// MAUI page its Frame (SkiaPage.ArrangeOverride), so Width/Height and
    /// SizeChanged/OnSizeAllocated reach pages inside a NavigationPage.
    /// </summary>
    protected override Rect ArrangeOverride(Rect bounds)
    {
        var size = new Size(bounds.Width, bounds.Height);
        if (_currentPage != null)
        {
            _currentPage.Measure(size);
            _currentPage.Arrange(bounds);
        }
        if (_isAnimating && _incomingPage != null)
        {
            _incomingPage.Measure(size);
            _incomingPage.Arrange(bounds);
        }
        return bounds;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaNavigationPage", $"OnPointerPressed at ({e.X}, {e.Y}), _isAnimating={_isAnimating}");
        if (_isAnimating) return;

        // Check for back button click
        if (IsBackButtonVisible)
        {
            if (e.X < 56 && e.Y < _navigationBarHeight)
            {
                DiagnosticLog.Debug("SkiaNavigationPage", "Back button clicked");
                RequestBack();
                return;
            }
        }

        DiagnosticLog.Debug("SkiaNavigationPage", $"Forwarding to _currentPage: {_currentPage?.GetType().Name}");
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
            RequestBack();
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
        if (IsBackButtonVisible && x < 56 && y < _navigationBarHeight)
        {
            return this;
        }

        // Check current page
        if (_currentPage != null)
        {
            try
            {
                var hit = _currentPage.HitTestAt(x, y);
                if (hit != null)
                    return hit;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("SkiaNavigationPage", $"HitTest error: {ex.Message}", ex);
            }
        }

        return this;
    }
}
