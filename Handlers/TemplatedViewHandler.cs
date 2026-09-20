// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for <see cref="TemplatedView"/> subclasses that are not
/// <see cref="ContentView"/>s (custom templated controls) on Linux.
/// </summary>
/// <remarks>
/// MAUI materialises a <see cref="ControlTemplate"/> into the logical tree on
/// the Controls side: <c>IContentView.PresentedContent</c> returns the
/// template root (or the plain Content when no template is set). The base
/// <see cref="ContentViewHandler"/> already renders <c>PresentedContent</c>
/// and re-maps on <c>ControlTemplate</c> changes, so this handler only exists
/// so <c>TemplatedView</c> resolves to a Skia handler instead of MAUI's
/// placeholder (a <c>ContentView</c> registration does not match its base type).
/// </remarks>
public partial class TemplatedViewHandler : ContentViewHandler
{
    public static new IPropertyMapper<IContentView, ContentViewHandler> Mapper =
        new PropertyMapper<IContentView, ContentViewHandler>(ContentViewHandler.Mapper)
        {
        };

    public TemplatedViewHandler() : base(Mapper, CommandMapper) { }

    public TemplatedViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper) { }
}
