// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Syncfusion's window overlay (<c>SfWindowOverlay</c>), which SfPopup shows
/// its popup in, and with it every control that opens an SfPopup: dialog-mode
/// Picker and Calendar, RichTextEditor dialogs, Toolbar overflow, Scheduler
/// and DataGrid dialogs. The platform-neutral build Linux apps get has every
/// member of <c>SfWindowOverlay</c> empty (<c>AddToOverlay</c> returns false,
/// so SfPopup deferred its open forever) and SfPopup's platform helpers
/// (<c>PopupExtension</c>: window size, anchor bounds, view offsets, overlay
/// blur; and the window-resize wiring) return zero or do nothing.
/// </summary>
/// <remarks>
/// Each <c>SfWindowOverlay</c> gets a <see cref="SkiaSfOverlay"/>, a
/// window-sized popup overlay holding its views at absolute positions, with
/// the semantics of the Windows build: <c>AddToOverlay</c> readies the
/// content view, <c>PositionOverlayContent</c> shows it at (x, y),
/// <c>AddOrUpdate</c> places a view absolutely or next to an anchor with the
/// alignments, <c>Remove</c>/<c>RemoveOverlay</c>/<c>RemoveFromWindow</c>
/// take views off. The overlay's container paints the overlay colour and
/// reports presses outside the views (SfPopup's dismiss on outside tap). The
/// SfPopup helpers are implemented from the window and view bounds (window
/// logical pixels). No compile-time reference to Syncfusion.Maui.Popup: it is
/// optional.
/// </remarks>
internal static class SfOverlayPatches
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly ConditionalWeakTable<object, OverlayState> s_overlays = new();
    private static readonly ConditionalWeakTable<object, EventHandler> s_resizeHandlers = new();
    private static int s_installed;

    private static FieldInfo? s_overlayStackView;
    private static MethodInfo? s_processTouchInteraction;

    // Syncfusion.Maui.Popup members.
    private static FieldInfo? s_popupOverlay;
    private static FieldInfo? s_popupView;
    private static PropertyInfo? s_popupStyle;
    private static PropertyInfo? s_isOpen;
    private static PropertyInfo? s_hasShadow;
    private static PropertyInfo? s_cornerRadius;
    private static MethodInfo? s_getBlurRadius;
    private static MethodInfo? s_getMainPage;
    private static MethodInfo? s_getMainWindowPage;
    private static MethodInfo? s_invalidateForceLayout;
    private static MethodInfo?[] s_onWindowResized = Array.Empty<MethodInfo?>();
    private static FieldInfo? s_popupOfView;
    private static PropertyInfo? s_popupBackground;
    private static FieldInfo? s_popupViewWidth;
    private static FieldInfo? s_popupViewHeight;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var core = typeof(SfDropdownEntry).Assembly;
            var overlayType = core.GetType("Syncfusion.Maui.Core.Internals.SfWindowOverlay");
            var containerType = core.GetType("Syncfusion.Maui.Core.Internals.WindowOverlayContainer");
            var addToOverlay = overlayType?.GetMethod("AddToOverlay", Any);
            if (overlayType == null || containerType == null || addToOverlay == null || !IsStub(addToOverlay))
                return; // a build with a working overlay needs nothing

            s_overlayStackView = overlayType.GetField("overlayStackView", Any);
            s_processTouchInteraction = containerType.GetMethod("ProcessTouchInteraction", Any, null, [typeof(float), typeof(float)], null);

            var harmony = new Harmony("com.openmaui.syncfusion.overlay");
            Patch(harmony, addToOverlay, nameof(AddToOverlay_Prefix));
            Patch(harmony, overlayType.GetMethod("PositionOverlayContent", Any), nameof(PositionOverlayContent_Prefix));
            Patch(harmony, overlayType.GetMethod("RemoveOverlay", Any), nameof(RemoveOverlay_Prefix));
            Patch(harmony, overlayType.GetMethod("Dispose", Any), nameof(Dispose_Prefix));
            Patch(harmony, overlayType.GetMethod("Remove", Any), nameof(Remove_Prefix));
            Patch(harmony, overlayType.GetMethod("RemoveFromWindow", Any), nameof(RemoveFromWindow_Prefix));
            foreach (var addOrUpdate in overlayType.GetMethods(Any).Where(m => m.Name == "AddOrUpdate"))
                Patch(harmony, addOrUpdate, nameof(AddOrUpdate_Prefix));

            InstallPopup(harmony);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching Syncfusion's window overlay failed", ex);
        }
    }

    /// <summary>The platform-neutral stub: a body that only returns (false).</summary>
    private static bool IsStub(MethodInfo method) => method.GetMethodBody() is { } body && body.GetILAsByteArray()?.Length <= 2;

    private static void Patch(Harmony harmony, MethodBase? original, string prefix)
    {
        if (original == null)
            return;
        harmony.Patch(original, new HarmonyMethod(typeof(SfOverlayPatches).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    private static void Postfix(Harmony harmony, MethodBase? original, string postfix)
    {
        if (original == null)
            return;
        harmony.Patch(original, postfix: new HarmonyMethod(typeof(SfOverlayPatches).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)));
    }

    private static void InstallPopup(Harmony harmony)
    {
        var extension = Type.GetType("Syncfusion.Maui.Popup.PopupExtension, Syncfusion.Maui.Popup");
        var popup = Type.GetType("Syncfusion.Maui.Popup.SfPopup, Syncfusion.Maui.Popup");
        if (extension == null || popup == null)
            return; // SfPopup is not part of the app

        s_popupOverlay = popup.GetField("PopupOverlay", Any);
        s_popupView = popup.GetField("PopupView", Any);
        s_popupStyle = popup.GetProperty("PopupStyle", Any);
        s_isOpen = popup.GetProperty("IsOpen", Any);
        s_hasShadow = s_popupStyle?.PropertyType.GetProperty("HasShadow", Any);
        s_cornerRadius = s_popupStyle?.PropertyType.GetProperty("CornerRadius", Any);
        s_getBlurRadius = extension.GetMethod("GetBlurRadius", Any);
        s_getMainPage = extension.GetMethod("GetMainPage", Any);
        s_getMainWindowPage = extension.GetMethod("GetMainWindowPage", Any);
        s_invalidateForceLayout = s_popupView?.FieldType.GetMethod("InvalidateForceLayout", Any, null, Type.EmptyTypes, null);
        // The Windows build's OnPlatformWindowSizeChanged, in order.
        s_onWindowResized = new[] { "AbortPopupViewAnimation", "ResetAnimatedProperties", "SyncPopupDimensionFields", "ResetPopupWidthHeight" }
            .Select(name => popup.GetMethod(name, Any, null, Type.EmptyTypes, null))
            .ToArray();

        Patch(harmony, extension.GetMethod("GetScreenWidth", Any), nameof(GetScreenWidth_Prefix));
        Patch(harmony, extension.GetMethod("GetScreenHeight", Any), nameof(GetScreenHeight_Prefix));
        Patch(harmony, extension.GetMethod("GetActionBarHeight", Any), nameof(GetActionBarHeight_Prefix));
        Patch(harmony, extension.GetMethod("GetX", Any), nameof(GetX_Prefix));
        Patch(harmony, extension.GetMethod("GetY", Any), nameof(GetY_Prefix));
        Patch(harmony, extension.GetMethod("GetRelativeViewBounds", Any), nameof(GetRelativeViewBounds_Prefix));
        Patch(harmony, extension.GetMethod("Blur", Any), nameof(Blur_Prefix));
        Patch(harmony, extension.GetMethod("ClearBlurViews", Any), nameof(ClearBlurViews_Prefix));
        s_popupOfView = s_popupView?.FieldType.GetField("Popup", Any);
        s_popupBackground = s_popupStyle?.PropertyType.GetProperty("PopupBackground", Any);
        s_popupViewWidth = popup.GetField("PopupViewWidth", Any);
        s_popupViewHeight = popup.GetField("PopupViewHeight", Any);
        Postfix(harmony, popup.GetMethod("UpdatePopupStyles", Any, null, Type.EmptyTypes, null), nameof(UpdatePopupStyles_Postfix));
        Postfix(harmony, s_popupView?.FieldType.GetMethod("ApplyShadowAndCornerRadius", Any, null, Type.EmptyTypes, null), nameof(ApplyShadowAndCornerRadius_Postfix));

        Patch(harmony, popup.GetMethod("WirePlatformSpecificEvents", Any, null, Type.EmptyTypes, null), nameof(WirePlatformSpecificEvents_Prefix));
        Patch(harmony, popup.GetMethod("UnWirePlatformSpecificEvents", Any, null, Type.EmptyTypes, null), nameof(UnWirePlatformSpecificEvents_Prefix));
    }

    /// <summary>One <c>SfWindowOverlay</c>'s overlay and the content view <c>AddToOverlay</c> readied.</summary>
    private sealed class OverlayState
    {
        internal SkiaSfOverlay Overlay { get; } = new();
        internal View? Content { get; set; }
    }

    private static OverlayState StateOf(object overlay)
    {
        var state = s_overlays.GetValue(overlay, _ => new OverlayState());
        state.Overlay.SetContainer(s_overlayStackView?.GetValue(overlay) as View);
        return state;
    }

    /// <summary>The overlay SfPopup's PopupOverlay shows in, if it has been created.</summary>
    internal static SkiaSfOverlay? OverlayOf(object? popup) =>
        popup != null && s_popupOverlay?.GetValue(popup) is { } overlay && s_overlays.TryGetValue(overlay, out var state) ? state.Overlay : null;

    /// <summary>Every shown window overlay (tests, diagnostics).</summary>
    internal static IEnumerable<SkiaSfOverlay> ShownOverlays => s_overlays.Select(p => p.Value.Overlay).Where(o => o.IsShown);

    /// <summary>A press on the overlay outside its views: the native container's pointer handler.</summary>
    internal static void ProcessTouchInteraction(View container, float x, float y)
    {
        try
        {
            s_processTouchInteraction?.Invoke(container, [x, y]);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "The window overlay's press handling failed", ex);
        }
    }

    /// <summary>
    /// The window Syncfusion's overlay shows in (its <c>WindowOverlayHelper</c>:
    /// the activated window, else the first), as an OpenMaui window context.
    /// </summary>
    internal static WindowContext? ActiveWindow()
    {
        if (LinuxApplication.Current is not { } app)
            return null;
        var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
        var window = windows?.FirstOrDefault(w => w.IsActivated) ?? windows?.FirstOrDefault();
        if (window != null)
        {
            foreach (var context in app.WindowContexts)
            {
                if (ReferenceEquals(context.MauiWindow, window) && context.RootView != null)
                    return context;
            }
        }
        return app.PrimaryContext is { RootView: not null } primary ? primary : app.WindowContexts.FirstOrDefault(c => c.RootView != null);
    }

    private static IMauiContext? MauiContextOf(WindowContext window) =>
        (window.MauiWindow as Microsoft.Maui.Controls.Window)?.Handler?.MauiContext ?? LinuxApplication.Current?.MauiContext;

    private static SkiaView? PlatformViewOf(View view, IMauiContext context)
    {
        if (view.Handler == null)
            view.Handler = view.ToViewHandler(context);
        return view.Handler?.PlatformView as SkiaView;
    }

    // Parameter names match SfWindowOverlay's.

    private static bool AddToOverlay_Prefix(object __instance, View childView, ref bool __result)
    {
        __result = false;
        try
        {
            if (childView == null || ActiveWindow() is not { } window || MauiContextOf(window) is not { } context)
                return false;
            if (PlatformViewOf(childView, context) == null)
            {
                DiagnosticLog.Warn("Syncfusion", $"{childView.GetType().Name} has no Skia view; it cannot be shown in the window overlay.");
                return false;
            }
            StateOf(__instance).Content = childView;
            __result = true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Adding a view to the window overlay failed", ex);
        }
        return false;
    }

    private static bool PositionOverlayContent_Prefix(object __instance, double x, double y)
    {
        try
        {
            var state = StateOf(__instance);
            if (state.Content is not { } content || ActiveWindow() is not { } window || MauiContextOf(window) is not { } context
                || PlatformViewOf(content, context) is not { } view)
                return false;
            state.Overlay.Place(window, content, view, new SkiaSfOverlay.Placement(view, x, y));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Showing the window overlay failed", ex);
        }
        return false;
    }

    private static bool AddOrUpdate_Prefix(object __instance, object[] __args)
    {
        try
        {
            if (__args.Length < 5 || __args[0] is not View child || ActiveWindow() is not { } window || MauiContextOf(window) is not { } context)
                return false;
            var relative = __args[1] as View;
            int offset = relative != null ? 2 : 1;
            if (__args.Length < offset + 4)
                return false;
            double x = Convert.ToDouble(__args[offset]);
            double y = Convert.ToDouble(__args[offset + 1]);
            var horizontal = (OverlayAlignment)Convert.ToInt32(__args[offset + 2]);
            var vertical = (OverlayAlignment)Convert.ToInt32(__args[offset + 3]);
            if (relative != null && (relative.Width < 0 || relative.Height < 0 || relative.Handler == null))
                return false;
            if (PlatformViewOf(child, context) is not { } view)
                return false;
            StateOf(__instance).Overlay.Place(window, child, view, new SkiaSfOverlay.Placement(view, x, y, horizontal, vertical, relative));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Placing a view on the window overlay failed", ex);
        }
        return false;
    }

    private static bool Remove_Prefix(object __instance, View view)
    {
        if (view?.Handler?.PlatformView is SkiaView platform && s_overlays.TryGetValue(__instance, out var state))
            state.Overlay.Remove(platform);
        return false;
    }

    /// <summary>Closes the overlay; the content view stays readied for the next show, as natively.</summary>
    private static bool RemoveOverlay_Prefix(object __instance)
    {
        if (s_overlays.TryGetValue(__instance, out var state))
            state.Overlay.Clear();
        return false;
    }

    private static bool RemoveFromWindow_Prefix(object __instance)
    {
        if (s_overlays.TryGetValue(__instance, out var state))
            state.Overlay.Clear();
        return false;
    }

    private static bool Dispose_Prefix(object __instance)
    {
        if (s_overlays.TryGetValue(__instance, out var state))
        {
            state.Overlay.Clear();
            state.Content = null;
        }
        return false;
    }

    // PopupExtension. The Windows build reads the platform window's bounds
    // (the client area its overlay covers); here the view tree's logical size.

    private static bool GetScreenWidth_Prefix(ref int __result)
    {
        __result = (int)SkiaSfOverlay.WindowSizeOf(ActiveWindow()).Width;
        return false;
    }

    private static bool GetScreenHeight_Prefix(ref int __result)
    {
        __result = (int)SkiaSfOverlay.WindowSizeOf(ActiveWindow()).Height;
        return false;
    }

    /// <summary>
    /// The navigation bar above the current page (NavigationPage or Shell with
    /// its bar shown): the page's top in the window, as the Windows build
    /// measures it. SfPopup positions below it.
    /// </summary>
    private static bool GetActionBarHeight_Prefix(ref int __result)
    {
        __result = 0;
        try
        {
            var page = s_getMainPage?.Invoke(null, [false]) as Page;
            var windowPage = s_getMainWindowPage?.Invoke(null, null) as Page;
            if (page?.Handler?.PlatformView is SkiaView view
                && ((windowPage is NavigationPage && NavigationPage.GetHasNavigationBar(page))
                    || (windowPage is Shell && Shell.GetNavBarIsVisible(page))))
                __result = Math.Max(0, (int)view.ScreenBounds.Y);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Reading the navigation bar height failed", ex);
        }
        return false;
    }

    /// <summary>The view's offset in its parent (WinUI ActualOffset).</summary>
    private static bool GetX_Prefix(View view, ref int __result)
    {
        __result = OffsetInParent(view) is { } offset ? (int)offset.X : 0;
        return false;
    }

    private static bool GetY_Prefix(View view, ref int __result)
    {
        __result = OffsetInParent(view) is { } offset ? (int)offset.Y : 0;
        return false;
    }

    private static Point? OffsetInParent(View? view)
    {
        if (view?.Handler?.PlatformView is not SkiaView platform)
            return null;
        var bounds = platform.Bounds;
        var parent = platform.Parent?.Bounds ?? Rect.Zero;
        return new Point(bounds.X - parent.X, bounds.Y - parent.Y);
    }

    /// <summary>The anchor's bounds in the window (ShowRelativeToView, RelativeView).</summary>
    private static bool GetRelativeViewBounds_Prefix(View relativeView, ref Rect __result)
    {
        __result = relativeView?.Handler?.PlatformView is SkiaView platform ? platform.ScreenBounds : Rect.Zero;
        return false;
    }

    /// <summary>OverlayMode Blur: the page beneath is blurred by the popup's BlurIntensity.</summary>
    private static bool Blur_Prefix(object popup, bool isopen)
    {
        if (!isopen || OverlayOf(popup) is not { } overlay)
            return false;
        try
        {
            overlay.BackdropBlurRadius = s_getBlurRadius?.Invoke(null, [popup]) is float radius ? radius : 7.5f;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Reading the popup's blur radius failed", ex);
        }
        return false;
    }

    private static bool ClearBlurViews_Prefix(object popup)
    {
        if (OverlayOf(popup) is { } overlay)
            overlay.BackdropBlurRadius = 0;
        return false;
    }

    // SfPopup. The Windows build follows the window's size (re-centring and
    // re-sizing the open popup) and gives the popup view a drop shadow.

    private static bool WirePlatformSpecificEvents_Prefix(object __instance)
    {
        if (OverlayOf(__instance) is not { } overlay)
            return false;
        var popup = new WeakReference<object>(__instance);
        var handler = s_resizeHandlers.GetValue(__instance, _ => (_, _) =>
        {
            if (popup.TryGetTarget(out var target))
                OnPopupWindowResized(target);
        });
        overlay.WindowSizeChanged -= handler;
        overlay.WindowSizeChanged += handler;
        overlay.ShadowOf = view => ShadowOf(popup, view);
        return false;
    }

    private static bool UnWirePlatformSpecificEvents_Prefix(object __instance)
    {
        if (OverlayOf(__instance) is { } overlay && s_resizeHandlers.TryGetValue(__instance, out var handler))
            overlay.WindowSizeChanged -= handler;
        return false;
    }

    /// <summary>
    /// The popup's background. SfPopup sets it on its popup view only when
    /// running on WinUI; the other native builds paint it on the native view,
    /// which the platform-neutral build has none of.
    /// </summary>
    private static void UpdatePopupStyles_Postfix(object __instance)
    {
        try
        {
            if (s_popupView?.GetValue(__instance) is View popupView && s_popupStyle?.GetValue(__instance) is { } style
                && s_popupBackground?.GetValue(style) is Brush background)
                popupView.Background = background;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Applying the popup's background failed", ex);
        }
    }

    /// <summary>
    /// The popup's rounded corners and shadow, as SfPopup applies them on
    /// WinUI: the corner radius clips the popup view (the other builds round
    /// the native view), and the shadow is the overlay's rounded drop shadow
    /// (see <see cref="ShadowOf"/>) rather than a MAUI Shadow, which draws
    /// square under the rounded corners.
    /// </summary>
    private static void ApplyShadowAndCornerRadius_Postfix(View __instance)
    {
        try
        {
            if (s_popupOfView?.GetValue(__instance) is not { } popup || s_popupStyle?.GetValue(popup) is not { } style)
                return;
            __instance.Shadow = null!;
            var radius = s_cornerRadius?.GetValue(style) is CornerRadius r ? r : default;
            double width = s_popupViewWidth?.GetValue(popup) is double w ? w : 0;
            double height = s_popupViewHeight?.GetValue(popup) is double h ? h : 0;
            __instance.Clip = radius.TopLeft == 0 && radius.TopRight == 0 && radius.BottomLeft == 0 && radius.BottomRight == 0
                ? null
                : new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(radius, new Rect(0, 0, width, height));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Applying the popup's corner radius failed", ex);
        }
    }

    private static void OnPopupWindowResized(object popup)
    {
        try
        {
            if (s_isOpen?.GetValue(popup) is not true)
                return;
            foreach (var method in s_onWindowResized)
                method?.Invoke(popup, null);
            if (s_popupView?.GetValue(popup) is { } popupView)
                s_invalidateForceLayout?.Invoke(popupView, null);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Re-laying out the popup after a window resize failed", ex);
        }
    }

    private static float? ShadowOf(WeakReference<object> popupReference, SkiaView view)
    {
        if (!popupReference.TryGetTarget(out var popup) || s_popupView?.GetValue(popup) is not View popupView
            || !ReferenceEquals(view.MauiView, popupView) || s_popupStyle?.GetValue(popup) is not { } style
            || s_hasShadow?.GetValue(style) is not true)
            return null;
        return s_cornerRadius?.GetValue(style) is CornerRadius radius ? (float)radius.TopLeft : 0f;
    }
}
