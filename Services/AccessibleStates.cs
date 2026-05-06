// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

[Flags]
public enum AccessibleStates : long
{
    None = 0L,
    Active = 1L,
    Armed = 2L,
    Busy = 4L,
    Checked = 8L,
    Collapsed = 0x10L,
    Defunct = 0x20L,
    Editable = 0x40L,
    Enabled = 0x80L,
    Expandable = 0x100L,
    Expanded = 0x200L,
    Focusable = 0x400L,
    Focused = 0x800L,
    HasToolTip = 0x1000L,
    Horizontal = 0x2000L,
    Iconified = 0x4000L,
    Modal = 0x8000L,
    MultiLine = 0x10000L,
    MultiSelectable = 0x20000L,
    Opaque = 0x40000L,
    Pressed = 0x80000L,
    Resizable = 0x100000L,
    Selectable = 0x200000L,
    Selected = 0x400000L,
    Sensitive = 0x800000L,
    Showing = 0x1000000L,
    SingleLine = 0x2000000L,
    Stale = 0x4000000L,
    Transient = 0x8000000L,
    Vertical = 0x10000000L,
    Visible = 0x20000000L,
    ManagesDescendants = 0x40000000L,
    Indeterminate = 0x80000000L,
    Required = 0x100000000L,
    Truncated = 0x200000000L,
    Animated = 0x400000000L,
    InvalidEntry = 0x800000000L,
    SupportsAutocompletion = 0x1000000000L,
    SelectableText = 0x2000000000L,
    IsDefault = 0x4000000000L,
    Visited = 0x8000000000L,
    ReadOnly = 0x10000000000L
}
