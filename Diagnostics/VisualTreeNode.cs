// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>
/// Immutable, read-only snapshot of a single <see cref="SkiaView"/> in the live
/// visual tree, produced by <see cref="VisualTreeInspector.Snapshot"/>. Capturing
/// a plain data model (rather than handing out live views) lets callers inspect,
/// log, or serialize the hierarchy without risk of mutating UI state.
/// </summary>
public sealed class VisualTreeNode
{
    /// <summary>Runtime type name of the source view (e.g. "SkiaLabel").</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// Screen (absolute, scroll-adjusted) bounds in logical pixels, matching the
    /// coordinate space the overlay and pointer hit-testing use.
    /// </summary>
    public Rect Bounds { get; init; }

    /// <summary>
    /// Text content when the source view exposes a public <c>string Text</c>
    /// property (labels, buttons, entries, editors, search bars); otherwise null.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>Background color when set; otherwise null.</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>Whether the source view was visible at snapshot time.</summary>
    public bool IsVisible { get; init; }

    /// <summary>Number of captured children (includes extra content roots).</summary>
    public int ChildCount { get; init; }

    /// <summary>Captured child nodes.</summary>
    public IReadOnlyList<VisualTreeNode> Children { get; init; } = Array.Empty<VisualTreeNode>();

    /// <summary>
    /// The live view this node was captured from. Kept for tooling (overlay
    /// selection, pick mode) — it is a back-reference, not part of the snapshot's
    /// value; do not mutate it.
    /// </summary>
    public SkiaView? Source { get; init; }
}
