// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Linux handler for AbsoluteLayout: MAUI's AbsoluteLayoutManager positions
/// the children by their <c>AbsoluteLayout.LayoutBounds</c> /
/// <c>LayoutFlags</c> attached properties, read as they are at each layout
/// pass, as on every platform. Previously AbsoluteLayout resolved to the
/// generic layout handler and rendered as a vertical stack.
/// </summary>
public class AbsoluteLayoutHandler : LayoutHandler
{
    public new static IPropertyMapper<AbsoluteLayout, AbsoluteLayoutHandler> Mapper = new PropertyMapper<AbsoluteLayout, AbsoluteLayoutHandler>(LayoutHandler.Mapper);

    public new static CommandMapper<AbsoluteLayout, AbsoluteLayoutHandler> CommandMapper = new(LayoutHandler.CommandMapper);

    public AbsoluteLayoutHandler() : base(Mapper, CommandMapper)
    {
    }

    protected override SkiaLayoutView CreatePlatformView() => new SkiaCrossPlatformLayout();

    /// <summary>
    /// Re-lays the layout out after its children's bounds or flags changed.
    /// The layout's manager reads them from the children at every pass, so
    /// nothing is copied; this only invalidates the platform view.
    /// </summary>
    public void SyncAllChildBounds()
    {
        if (((IElementHandler)this).PlatformView is SkiaView platformView)
            platformView.InvalidateMeasure();
    }
}
