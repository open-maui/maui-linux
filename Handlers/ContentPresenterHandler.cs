// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for <see cref="ContentPresenter"/> on Linux.
/// </summary>
/// <remarks>
/// A ContentPresenter is the placeholder inside a <see cref="ControlTemplate"/>
/// that shows the templated control's <c>Content</c>. MAUI's Controls layer
/// keeps <see cref="ContentPresenter.Content"/> bound to the templated parent
/// (a <c>TemplatedParent</c> binding installed by the ContentPresenter
/// constructor), so the platform only has to render whatever Content it is
/// handed and re-render when that binding updates. It derives from
/// <c>Compatibility.Layout</c>, so no Linux handler matched it by base type and
/// it fell through to MAUI's placeholder handler, which cannot create a
/// platform view on this TFM. The presenter renders as a plain
/// <see cref="SkiaContentView"/>: no chrome, its single child fills it.
/// </remarks>
public partial class ContentPresenterHandler : ContentViewHandler
{
    public static new IPropertyMapper<IContentView, ContentViewHandler> Mapper =
        new PropertyMapper<IContentView, ContentViewHandler>(ContentViewHandler.Mapper)
        {
        };

    public ContentPresenterHandler() : base(Mapper, CommandMapper) { }

    public ContentPresenterHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }
}
