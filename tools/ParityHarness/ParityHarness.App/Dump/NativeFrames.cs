namespace ParityHarness.Dump;

/// <summary>
/// The platform view's actual rectangle relative to the root page's platform view, in
/// device-independent units. This catches the case where the MAUI frame is right but the
/// platform draws the view somewhere else (and positions MAUI frames cannot express, such
/// as CollectionView cells and pages under a navigation bar).
/// </summary>
public static class NativeFrames
{
#if OPENMAUI_LINUX
    // OpenMaui: SkiaView.Bounds are window-space; ScreenBounds also applies scroll offsets.
    public static object? RootOf(Page root) => root.Handler?.PlatformView as Microsoft.Maui.Platform.SkiaView;

    public static Rect? Get(VisualElement ve, object? rootNative)
    {
        if (rootNative is not Microsoft.Maui.Platform.SkiaView root) return null;
        if (ve.Handler?.PlatformView is not Microsoft.Maui.Platform.SkiaView view) return null;
        var r = root.ScreenBounds;
        var b = view.ScreenBounds;
        return new Rect(b.X - r.X, b.Y - r.Y, b.Width, b.Height);
    }
#elif WINDOWS
    public static object? RootOf(Page root) => root.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;

    public static Rect? Get(VisualElement ve, object? rootNative)
    {
        if (rootNative is not Microsoft.UI.Xaml.FrameworkElement root) return null;
        // ContainerView (WrapperView for clip/shadow) is what the parent arranges.
        var fe = (ve.Handler as IViewHandler)?.ContainerView as Microsoft.UI.Xaml.FrameworkElement
                 ?? ve.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        if (fe == null || fe.XamlRoot == null) return null;
        try
        {
            var p = fe.TransformToVisual(root).TransformPoint(new global::Windows.Foundation.Point(0, 0));
            return new Rect(p.X, p.Y, fe.ActualWidth, fe.ActualHeight);
        }
        catch (Exception)
        {
            // Not in the same visual tree (e.g. an unrealised template).
            return null;
        }
    }
#else
    public static object? RootOf(Page root) => null;

    public static Rect? Get(VisualElement ve, object? rootNative) => null;
#endif
}
