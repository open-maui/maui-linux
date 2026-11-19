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
    private static readonly Lock s_gate = new();
    private static volatile bool s_registered;

    /// <summary>
    /// Adds the mappings once. A caller returns only when they are in: a second thread that
    /// returned as soon as the first had started connected handlers without them (parallel tests
    /// building hosts at once lost a view's Semantics mapping).
    /// </summary>
    internal static void Register()
    {
        if (s_registered)
            return;
        lock (s_gate)
        {
            if (s_registered)
                return;
            AddMappings();
            s_registered = true;
        }
    }

    /// <summary>
    /// Registers the mappings when the assembly loads, before any handler of it exists: a handler's
    /// mapper merges its chain on first use and keeps that merge, so a Label handler used before a
    /// later <see cref="Register"/> kept MAUI's Semantics mapping without OpenMaui's (a test that
    /// created handlers without a host lost the heading level of every later Label).
    /// </summary>
#pragma warning disable CA2255 // the mappings must be in ViewMapper before any of this assembly's handlers merges its mapper
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterOnLoad() => Register();
#pragma warning restore CA2255

    private static void AddMappings()
    {
        var mapper = ViewHandler.ViewMapper;
        mapper.AppendToMapping(nameof(IView.Opacity), MapOpacity);
        mapper.AppendToMapping(nameof(IView.Visibility), MapVisibility);
        mapper.AppendToMapping(nameof(IView.FlowDirection), MapFlowDirection);
        mapper.AppendToMapping(nameof(IView.AutomationId), MapAutomationId);
        mapper.AppendToMapping(nameof(IView.Semantics), MapSemantics);
        mapper.AppendToMapping(nameof(IView.InputTransparent), MapInputTransparent);
        mapper.AppendToMapping(nameof(IView.ZIndex), MapZIndex);
        mapper.AppendToMapping(nameof(IToolTipElement.ToolTip), MapToolTip);
    }

    /// <summary>
    /// The tooltip's content, as text, on the platform view (MAUI's platforms show a tooltip's
    /// string content; other content is shown by its ToString as WinUI does for a non-element).
    /// </summary>
    internal static void MapToolTip(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is SkiaView skia)
            skia.ToolTipText = (view as IToolTipElement)?.ToolTip?.Content?.ToString();
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
