// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Tab and Shift+Tab move keyboard focus between the presented page's tab
/// stops, as the platforms MAUI runs on do (WinUI's default tab navigation):
/// focusable views that are shown and enabled, in tree order, wrapping at
/// either end. A focused view that uses Tab itself (an Editor inserting a
/// tab) keeps it.
/// </summary>
public sealed partial class WindowContext
{
    /// <summary>
    /// Raised before Tab moves focus away from <c>FocusedView</c>, so an
    /// extension package can react to the key while focus is still where it
    /// was (a drop-down closing, as WinUI's controls see the Tab first).
    /// </summary>
    internal static event Action<WindowContext, SkiaView?>? TabNavigating;

    private bool TryTabNavigation(KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Tab || (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
            return false;
        try
        {
            TabNavigating?.Invoke(this, _focusedView);
            var stops = TabStops();
            if (stops.Count == 0)
                return false;
            bool backward = (e.Modifiers & KeyModifiers.Shift) != 0;
            int index = _focusedView == null ? -1 : stops.IndexOf(_focusedView);
            if (index < 0 && _focusedView != null)
            {
                // Focus inside a stop (an Entry's inner part): continue from that stop.
                for (var parent = _focusedView.Parent; parent != null && index < 0; parent = parent.Parent)
                    index = stops.IndexOf(parent);
            }
            int next = index < 0
                ? (backward ? stops.Count - 1 : 0)
                : (index + (backward ? stops.Count - 1 : 1)) % stops.Count;
            FocusedView = stops[next];
            e.Handled = true;
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("WindowContext", "Tab navigation failed", ex);
            return false;
        }
    }

    /// <summary>The presented page's tab stops in navigation order.</summary>
    internal List<SkiaView> TabStops()
    {
        var found = new List<SkiaView>();
        if (PresentedPage((MauiWindow as Microsoft.Maui.Controls.Window)?.Page) is { } page)
            Collect(page, found);
        return found;
    }

    private static void Collect(Element element, List<SkiaView> found)
    {
        if (element is VisualElement visual && (!visual.IsVisible || !visual.IsEnabled))
            return;
        if (element is VisualElement { Handler.PlatformView: SkiaView { IsFocusable: true, IsVisible: true, IsEnabled: true } view }
            && IsShown(view) && !found.Contains(view))
            found.Add(view);
        if (element is not IVisualTreeElement tree)
            return;
        foreach (var child in tree.GetVisualChildren())
        {
            if (child is Element childElement)
                Collect(childElement, found);
        }
    }

    /// <summary>True when the view is laid out in a shown tree (not a cached page or a collapsed row).</summary>
    private static bool IsShown(SkiaView view)
    {
        if (view.Bounds.Width <= 0 || view.Bounds.Height <= 0)
            return false;
        for (var current = view; current != null; current = current.Parent)
        {
            if (!current.IsVisible)
                return false;
        }
        return true;
    }
}
