// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's Focus and Unfocus view commands. <c>VisualElement.Focus()</c> asks
/// the handler for a result; with no Linux mapping none was set and every call
/// threw ("No result value was set"), taking down whatever called it (an
/// Entry focused from code; Syncfusion's ListViewItem focuses itself on a tap,
/// so SfListView's ItemTapped never fired). As on the other platforms, a view
/// that can take focus gets it and reports true; any other reports false.
/// </summary>
internal static class FocusCommands
{
    private static int s_registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref s_registered, 1) == 1)
            return;
        ViewHandler.ViewCommandMapper.Add(nameof(IView.Focus), MapFocus);
        ViewHandler.ViewCommandMapper.Add(nameof(IView.Unfocus), MapUnfocus);
    }

    internal static void MapFocus(IViewHandler handler, IView view, object? args)
    {
        if (args is not FocusRequest request)
            return;
        bool focused = false;
        if (handler.PlatformView is SkiaView { IsFocusable: true, IsVisible: true, IsEnabled: true } skia
            && ContextOf(skia) is { } context)
        {
            context.FocusedView = skia;
            focused = ReferenceEquals(context.FocusedView, skia);
        }
        request.TrySetResult(focused);
    }

    internal static void MapUnfocus(IViewHandler handler, IView view, object? args)
    {
        if (handler.PlatformView is SkiaView skia && ContextOf(skia) is { } context
            && ReferenceEquals(context.FocusedView, skia))
            context.FocusedView = null;
    }

    /// <summary>The window whose tree (page or modal/popup layer) holds <paramref name="view"/>.</summary>
    private static WindowContext? ContextOf(SkiaView view)
    {
        var app = LinuxApplication.Current;
        if (app == null)
            return null;
        var root = view;
        while (root.Parent != null)
            root = root.Parent;
        foreach (var context in app.WindowContexts)
        {
            if (ReferenceEquals(context.RootView, root) || context.ModalViews.Contains(root))
                return context;
        }
        return null;
    }
}
