// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's visual-tree platform lookups (<c>VisualTreeElementExtensions</c>), carried out
/// on the Skia tree. Both are platform code in MAUI, and its platform-neutral build
/// cannot do them:
/// <list type="bullet">
/// <item><c>GetVisualTreeElements(point)</c> / <c>(rect)</c> test each view's bounds on
/// screen (<c>GetBoundingBox</c>); the platform-neutral build uses the view's Frame,
/// which is relative to its parent, so a point in the window found nothing below the
/// top level. Here a view's bounds are its Skia view's window bounds.</item>
/// <item><c>GetVisualTreeElement(platformView)</c> walks up the platform tree to the
/// nearest platform view that knows its element, then back down the element tree along
/// that path; the platform-neutral build has no platform parents and found nothing.
/// Here the platform tree is the Skia tree: a Skia view knows its element
/// (<see cref="SkiaView.MauiView"/>), and a window's root view its window.</item>
/// </list>
/// </summary>
internal static class VisualTreeElementPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var extensions = typeof(VisualTreeElementExtensions);
            var harmony = new Harmony("com.openmaui.visualtree");

            var elementsInternal = extensions.GetMethod("GetVisualTreeElementsInternal", Static, null,
                new[] { typeof(IVisualTreeElement), typeof(Predicate<Rect>) }, null);
            if (elementsInternal != null)
                harmony.Patch(elementsInternal, new HarmonyMethod(typeof(VisualTreeElementPatches).GetMethod(nameof(GetVisualTreeElementsInternal_Prefix), Static)));
            else
                DiagnosticLog.Warn("VisualTreeElementPatches", "VisualTreeElementExtensions.GetVisualTreeElementsInternal not found; GetVisualTreeElements(point) finds only top-level views");

            var element = extensions.GetMethod("GetVisualTreeElement", Static, null,
                new[] { typeof(object), typeof(bool) }, null);
            if (element != null)
                harmony.Patch(element, new HarmonyMethod(typeof(VisualTreeElementPatches).GetMethod(nameof(GetVisualTreeElement_Prefix), Static)));
            else
                DiagnosticLog.Warn("VisualTreeElementPatches", "VisualTreeElementExtensions.GetVisualTreeElement not found; a platform view's element cannot be looked up");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("VisualTreeElementPatches", "Patching MAUI's visual-tree lookups failed", ex);
        }
    }

    // Parameter names match VisualTreeElementExtensions.GetVisualTreeElementsInternal.
    private static bool GetVisualTreeElementsInternal_Prefix(IVisualTreeElement visualElement, Predicate<Rect> intersectElementBounds, ref List<IVisualTreeElement> __result)
    {
        __result = GetVisualTreeElements(visualElement, intersectElementBounds);
        return false;
    }

    // Parameter names match VisualTreeElementExtensions.GetVisualTreeElement(platformView, searchAncestors).
    private static bool GetVisualTreeElement_Prefix(object platformView, bool searchAncestors, ref IVisualTreeElement? __result)
    {
        if (platformView is not SkiaView view)
            return true;
        __result = GetVisualTreeElement(view, searchAncestors);
        return false;
    }

    /// <summary>
    /// The elements under <paramref name="visualElement"/> (itself included) whose bounds in
    /// the window pass <paramref name="intersectElementBounds"/>, topmost first: MAUI's
    /// algorithm with a view's bounds on screen.
    /// </summary>
    internal static List<IVisualTreeElement> GetVisualTreeElements(IVisualTreeElement visualElement, Predicate<Rect> intersectElementBounds)
    {
        var elements = new List<IVisualTreeElement>();
        Collect(visualElement, intersectElementBounds, elements);
        elements.Reverse();
        return elements;

        static void Collect(IVisualTreeElement visualElement, Predicate<Rect> intersectElementBounds, List<IVisualTreeElement> elements)
        {
            if (visualElement is IView view && view.Handler is not null && intersectElementBounds(GetBoundingBox(view)))
                elements.Add(visualElement);
            foreach (var child in visualElement.GetVisualChildren())
                Collect(child, intersectElementBounds, elements);
        }
    }

    /// <summary>A view's bounds on screen (the window's coordinates): its Skia view's.</summary>
    internal static Rect GetBoundingBox(IView view) =>
        view.Handler?.PlatformView is SkiaView skia ? skia.ScreenBounds : view.Frame;

    /// <summary>
    /// The element that is the best fit for <paramref name="platformView"/>: its own
    /// element, else (with <paramref name="searchAncestors"/>) the nearest element above it.
    /// MAUI's VisualTreeElementExtensions.GetVisualTreeElement on the Skia tree.
    /// </summary>
    internal static IVisualTreeElement? GetVisualTreeElement(SkiaView platformView, bool searchAncestors)
    {
        // A Skia view that knows its own element is that element's (MAUI looks only at the
        // ancestors, and finds the element again by its platform view on the way down; an
        // element shown by a view that is not its logical parent, a list's item, is found
        // only this way).
        if (platformView.MauiView is IVisualTreeElement own && IsThisMyPlatformView(own, platformView))
            return own;

        var platformParentPath = new List<SkiaView>();
        IVisualTreeElement? foundParent = null;

        // The first platform view up the tree that can give its element.
        for (var parent = platformView.Parent; parent != null; parent = parent.Parent)
        {
            platformParentPath.Add(parent);
            foundParent = ElementOf(parent);
            if (foundParent != null)
                break;
        }

        platformParentPath.Reverse();

        if (IsThisMyPlatformView(foundParent, platformView))
            return foundParent;

        if (foundParent is null)
            return null;

        // Down the element tree along the path the platform tree went up.
        var returnValue = FindNextChild(foundParent, platformView, platformParentPath);

        if (!searchAncestors && returnValue != null && !IsThisMyPlatformView(returnValue, platformView))
            return null;

        return returnValue;

        static IVisualTreeElement? FindNextChild(IVisualTreeElement parent, SkiaView platformView, List<SkiaView> platformParentPath)
        {
            IVisualTreeElement? childMatch = null;
            foreach (var child in parent.GetVisualChildren())
            {
                if (IsThisMyPlatformView(child, platformView))
                    return child;

                // Only children whose platform view is realized.
                if (child is IElement { Handler: IViewHandler { PlatformView: SkiaView childView } })
                {
                    var index = platformParentPath.IndexOf(childView);
                    if (index < 0)
                        continue;

                    childMatch = child;
                    platformParentPath.RemoveRange(0, index + 1);
                    break;
                }
            }

            // Out of children: the parent is the furthest down element matched.
            if (childMatch is null)
                return parent;

            return FindNextChild(childMatch, platformView, platformParentPath);
        }
    }

    private static bool IsThisMyPlatformView(IVisualTreeElement? element, SkiaView platformView) =>
        element is IElement { Handler: IViewHandler handler }
        && (ReferenceEquals(handler.PlatformView, platformView) || ReferenceEquals(handler.ContainerView, platformView));

    /// <summary>
    /// The element a Skia view knows (MAUI's IVisualTreeElementProvidable): the view's
    /// own element, or for a window's root view (a page or a modal layer) its window.
    /// </summary>
    private static IVisualTreeElement? ElementOf(SkiaView view)
    {
        if (view.MauiView is IVisualTreeElement element)
            return element;
        if (view.Parent != null || LinuxApplication.Current is not { } app)
            return null;

        foreach (var context in app.WindowContexts)
        {
            if (context.MauiWindow is not IVisualTreeElement window)
                continue;
            if (ReferenceEquals(context.RootView, view))
                return window;
            // A modal layer's root: the window still leads down to it.
            foreach (var descendant in window.GetVisualTreeDescendants())
            {
                if (descendant is IElement { Handler: IViewHandler { PlatformView: var platformView } } && ReferenceEquals(platformView, view))
                    return window;
            }
        }
        return null;
    }
}
