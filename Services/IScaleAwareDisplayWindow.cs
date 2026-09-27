// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A window whose device scale (buffer pixels per logical pixel) can change
/// while it is open: moved to a monitor with a different scale, or the user
/// changed the display scale in the desktop settings.
/// </summary>
/// <remarks>
/// Ordering contract: <see cref="ScaleChanged"/> is raised before the
/// <see cref="IDisplayWindow.Resized"/> that carries the buffer size for the
/// new scale, so a listener that updates its rendering scale in
/// <see cref="ScaleChanged"/> lays out the resized frame at the right logical
/// size. A backend whose window keeps its pixel size across a scale change
/// (X11) raises only <see cref="ScaleChanged"/>.
/// </remarks>
public interface IScaleAwareDisplayWindow
{
    /// <summary>The window's current device scale (1.0, 1.25, 1.5, 2.0, ...).</summary>
    float Scale { get; }

    /// <summary>Raised with the new scale when it changes.</summary>
    event EventHandler<float>? ScaleChanged;
}
