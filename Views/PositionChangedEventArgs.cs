// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Event args for position changed events in carousel views.
/// </summary>
public class SkiaPositionChangedEventArgs : EventArgs
{
    public int PreviousPosition { get; }
    public int CurrentPosition { get; }

    public SkiaPositionChangedEventArgs(int previousPosition, int currentPosition)
    {
        PreviousPosition = previousPosition;
        CurrentPosition = currentPosition;
    }
}

/// <summary>
/// Event args for position changed events in carousel views.
/// </summary>
[Obsolete("Use SkiaPositionChangedEventArgs; this type is removed in OpenMaui 11.0.")]
public class PositionChangedEventArgs : SkiaPositionChangedEventArgs
{
    public PositionChangedEventArgs(int previousPosition, int currentPosition) : base(previousPosition, currentPosition) { }
}
