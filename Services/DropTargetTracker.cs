// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Tracks the current drop-target as a drag moves across a window and yields
/// the per-target enter/leave transitions the MAUI gesture layer expects:
/// leaving the old target when the target changes, and re-delivering DragOver
/// to the current target on every position update (MAUI fires DragOver
/// repeatedly on the same view). Factored out of the input router so the
/// transition state machine is unit-testable without a display server.
/// </summary>
public sealed class DropTargetTracker<T> where T : class
{
    private T? _current;

    /// <summary>The target currently under the drag, if any.</summary>
    public T? Current => _current;

    /// <summary>
    /// Record the target under the latest drag position.
    /// <c>Left</c> is the previous target when the target changed (send it
    /// DragLeave); <c>Target</c> is the target to send DragOver to — returned
    /// on every call, including repeats on an unchanged target.
    /// </summary>
    public (T? Left, T? Target) Update(T? hit)
    {
        var left = ReferenceEquals(_current, hit) ? null : _current;
        _current = hit;
        return (left, hit);
    }

    /// <summary>
    /// End tracking (window-level leave, drop delivered, drag cancelled, or
    /// window teardown). Returns the target that was current so the caller
    /// can deliver a final DragLeave/Drop to it.
    /// </summary>
    public T? Clear()
    {
        var left = _current;
        _current = null;
        return left;
    }
}
