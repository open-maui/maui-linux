// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// MAUI's base view properties on Linux: Opacity, Visibility, FlowDirection, AutomationId,
/// Semantics, InputTransparent and ZIndex, mapped in <see cref="ViewHandler.ViewMapper"/>, which
/// every handler's mapper inherits. MAUI's own mappings for them call platform extensions that do
/// nothing on its platform-neutral build; OpenMaui used to copy the values from a Controls view
/// when it created the handler, so <c>handler.UpdateValue</c> did nothing and a view that is not
/// a Controls view (a library's core view) got none of them (MAUI's conformance tests). The
/// mappings read the <see cref="IView"/> interface and run after MAUI's own.
/// </summary>
internal static class LinuxViewMappers
{
    private static int s_registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref s_registered, 1) == 1)
            return;
        var mapper = ViewHandler.ViewMapper;
        mapper.AppendToMapping(nameof(IView.Opacity), MapOpacity);
        mapper.AppendToMapping(nameof(IView.Visibility), MapVisibility);
        mapper.AppendToMapping(nameof(IView.FlowDirection), MapFlowDirection);
        mapper.AppendToMapping(nameof(IView.AutomationId), MapAutomationId);
        mapper.AppendToMapping(nameof(IView.Semantics), MapSemantics);
        mapper.AppendToMapping(nameof(IView.InputTransparent), MapInputTransparent);
        mapper.AppendToMapping(nameof(IView.ZIndex), MapZIndex);
    }

    internal static void MapOpacity(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            skia.Opacity = (float)view.Opacity;
    }

    /// <summary>
    /// Hidden and Collapsed both draw nothing and take no input; MAUI's layout managers give a
    /// Hidden view its space and a Collapsed one none.
    /// </summary>
    internal static void MapVisibility(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            skia.Visibility = view.Visibility;
    }

    internal static void MapFlowDirection(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            skia.FlowDirection = view.FlowDirection;
    }

    internal static void MapAutomationId(IViewHandler handler, IView view)
    {
        // A Controls element's AutomationId can be set only once, and the platform view reads
        // it from the element: only a view that is not one needs the value copied.
        var id = view.AutomationId ?? string.Empty;
        if (handler.PlatformView is SkiaView skia && skia.AutomationId != id)
            skia.AutomationId = id;
    }

    internal static void MapSemantics(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            SemanticMapper.Apply(view, skia);
    }

    internal static void MapInputTransparent(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            skia.InputTransparent = view.InputTransparent;
    }

    internal static void MapZIndex(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            skia.ZIndex = view.ZIndex;
    }
}
