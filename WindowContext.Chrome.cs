// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// The window's chrome around the page: MAUI's window overlays (<c>Window.AddOverlay</c>, the
/// visual diagnostics overlay), drawn over the page and given its taps, and the window's
/// TitleBar (<c>Window.TitleBar</c>), shown in a strip above the page.
/// </summary>
public sealed partial class WindowContext
{
    /// <summary>The window whose tree (page, modal or popup layer, or TitleBar) holds <paramref name="view"/>.</summary>
    internal static WindowContext? ForView(SkiaView view)
    {
        var app = LinuxApplication.Current;
        if (app == null)
            return null;
        var root = view;
        while (root.Parent != null)
            root = root.Parent;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, root) || context.ModalViews.Contains(root) || ReferenceEquals(context.TitleBarView, root))
                return context;
        }
        return null;
    }

    #region Window overlays

    private static readonly HarmonyLib.AccessTools.FieldRef<Microsoft.Maui.Controls.Window, HashSet<IWindowOverlay>>? s_windowOverlays =
        TryFieldRef();

    private static HarmonyLib.AccessTools.FieldRef<Microsoft.Maui.Controls.Window, HashSet<IWindowOverlay>>? TryFieldRef()
    {
        try
        {
            return HarmonyLib.AccessTools.FieldRefAccess<Microsoft.Maui.Controls.Window, HashSet<IWindowOverlay>>("_overlays");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private readonly List<IWindowOverlay> _activeOverlays = new();
    private int _overlaySignature;

    /// <summary>
    /// The window overlays to draw over the page now: the MAUI window's overlays
    /// (<c>Window.Overlays</c>) and its visual diagnostics overlay, each when initialized
    /// (MAUI's platform view exists), visible and holding elements.
    /// </summary>
    internal IReadOnlyList<IWindowOverlay> ActiveWindowOverlays()
    {
        _activeOverlays.Clear();
        if (MauiWindow is not { } window)
            return _activeOverlays;
        if (window is Microsoft.Maui.Controls.Window controls && s_windowOverlays != null)
        {
            foreach (var overlay in s_windowOverlays(controls))
                AddIfActive(overlay);
        }
        else
        {
            foreach (var overlay in window.Overlays)
                AddIfActive(overlay);
        }
        AddIfActive(window.VisualDiagnosticsOverlay);
        return _activeOverlays;

        void AddIfActive(IWindowOverlay? overlay)
        {
            if (overlay is { IsPlatformViewInitialized: true, IsVisible: true } && (overlay.WindowElements.Count > 0 || overlay is not WindowOverlay))
                _activeOverlays.Add(overlay);
        }
    }

    /// <summary>
    /// Hands the engine this frame's overlays and title bar. A change in which overlays are
    /// shown (one added, removed, hidden, or its elements changed) redraws the window, even
    /// when nothing else asked for a frame.
    /// </summary>
    private void PrepareChrome(Rendering.SkiaRenderingEngine engine)
    {
        var overlays = ActiveWindowOverlays();
        int signature = overlays.Count;
        foreach (var overlay in overlays)
            signature = HashCode.Combine(signature, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(overlay), overlay.WindowElements.Count);
        var titleBar = TitleBarView;
        signature = HashCode.Combine(signature, titleBar is { IsVisible: true } ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(titleBar) : 0);
        if (signature != _overlaySignature)
        {
            _overlaySignature = signature;
            engine.InvalidateAll();
        }
        engine.WindowOverlays = overlays.Count > 0 ? overlays.ToArray() : null;
        engine.TitleBarView = titleBar is { IsVisible: true } ? titleBar : null;
        if (titleBar != null && !ReferenceEquals(titleBar.RenderContext, engine))
            titleBar.RenderContext = engine;
    }

    // The overlay that took the current press (DisableUITouchEventPassthrough, or a touched
    // element with EnableDrawableTouchHandling), and where the press was.
    private bool _overlayCapturedPress;
    private Point? _overlayPressPoint;

    /// <summary>
    /// A press in the page: the window's overlays see it first, as Windows' overlay view sits
    /// over the page. True when an overlay takes it (the page does not get it): one with
    /// DisableUITouchEventPassthrough, or one with EnableDrawableTouchHandling over one of its
    /// elements.
    /// </summary>
    private bool OverlaysTakePress(PointerEventArgs e)
    {
        _overlayCapturedPress = false;
        _overlayPressPoint = null;
        var overlays = MauiWindow == null ? (IReadOnlyList<IWindowOverlay>)Array.Empty<IWindowOverlay>() : ActiveOverlaysForInput();
        if (overlays.Count == 0)
            return false;
        var point = new Point(e.X, e.Y);
        _overlayPressPoint = point;
        foreach (var overlay in overlays)
        {
            if (overlay.DisableUITouchEventPassthrough
                || (overlay.EnableDrawableTouchHandling && overlay.WindowElements.Any(element => element.Contains(point))))
            {
                _overlayCapturedPress = true;
            }
        }
        return _overlayCapturedPress;
    }

    /// <summary>
    /// The release that ends a press: a tap (no drag) is reported to every overlay's Tapped,
    /// as Windows raises it for taps on the page and on the overlay. True when the press was
    /// the overlay's (the page does not get the release).
    /// </summary>
    private bool OverlaysTakeRelease(PointerEventArgs e)
    {
        bool captured = _overlayCapturedPress;
        var pressedAt = _overlayPressPoint;
        _overlayCapturedPress = false;
        _overlayPressPoint = null;
        if (pressedAt is { } start && MauiWindow != null)
        {
            const double TapSlop = 10;
            var point = new Point(e.X, e.Y);
            if (Math.Abs(point.X - start.X) <= TapSlop && Math.Abs(point.Y - start.Y) <= TapSlop)
            {
                foreach (var overlay in ActiveOverlaysForInput())
                {
                    if (overlay is WindowOverlay windowOverlay)
                        WindowOverlayPatches.RaiseTapped(windowOverlay, point);
                }
            }
        }
        return captured;
    }

    /// <summary>Initialized, visible overlays (taps reach an overlay with no elements too).</summary>
    private List<IWindowOverlay> ActiveOverlaysForInput()
    {
        var result = new List<IWindowOverlay>();
        if (MauiWindow is not { } window)
            return result;
        IEnumerable<IWindowOverlay> all = window is Microsoft.Maui.Controls.Window controls && s_windowOverlays != null
            ? s_windowOverlays(controls)
            : window.Overlays;
        foreach (var overlay in all.Append(window.VisualDiagnosticsOverlay))
        {
            if (overlay is { IsPlatformViewInitialized: true, IsVisible: true })
                result.Add(overlay);
        }
        return result;
    }

    #endregion

    #region Title bar

    /// <summary>
    /// The platform view of the MAUI window's TitleBar (<c>Window.TitleBar</c>), realized by
    /// the window handler; null when the window has none.
    /// </summary>
    internal SkiaView? TitleBarView =>
        (MauiWindow?.Handler as WindowHandler)?.PlatformView?.TitleBar;

    /// <summary>
    /// The view of the window's TitleBar under a point of the strip above the page (negative
    /// Y in the page's coordinates); null when the point is not on the TitleBar.
    /// </summary>
    private SkiaView? HitTestTitleBar(float x, float y)
    {
        if (y >= 0 || TitleBarView is not { IsVisible: true } titleBar)
            return null;
        return titleBar.Bounds.Contains(x, y) ? titleBar.HitTestAt(x, y) : null;
    }

    /// <summary>
    /// For the client-side decoration: whether a press at a point of the titlebar (logical
    /// window coordinates) is on an interactive part of the window's TitleBar (MAUI's
    /// <c>ITitleBar.PassthroughElements</c>: its leading, main and trailing content), which
    /// takes the press; the rest of the bar moves the window, as on Windows.
    /// </summary>
    internal bool IsTitleBarPassthrough(float x, float y)
    {
        if (TitleBarView is not { IsVisible: true } || (MauiWindow as Microsoft.Maui.Controls.Window)?.TitleBar is not { } titleBar)
            return false;
        float pageY = y - CsdPointerInsetLogical;
        foreach (var element in titleBar.PassthroughElements)
        {
            if (element is not { Visibility: Visibility.Visible } || element.Handler?.PlatformView is not SkiaView)
                continue;
            var bounds = WindowOverlayPatches.GetBoundingBox(element);
            if (bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(x, pageY))
                return true;
        }
        return false;
    }

    #endregion
}
