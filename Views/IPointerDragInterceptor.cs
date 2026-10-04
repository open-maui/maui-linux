// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

/// <summary>
/// A container that takes over a press on one of its descendants once the pointer moves as the
/// container's own drag (a SwipeView's swipe over its content's buttons and labels), as a native
/// platform's container intercepts a touch it recognizes. The window tells it about the press and
/// each move while the descendant has the pointer; when it intercepts, the descendant's press is
/// cancelled (no click, no tap) and the container gets the pointer until release.
/// </summary>
internal interface IPointerDragInterceptor
{
    /// <summary>A descendant was pressed (the point is in this view's space).</summary>
    void OnDescendantPointerPressed(PointerEventArgs e);

    /// <summary>The pointer moved while the descendant has it: true takes it over.</summary>
    bool ShouldInterceptDrag(PointerEventArgs e);
}
