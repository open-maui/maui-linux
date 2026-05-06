// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Event args for position changed events in carousel views.
/// </summary>
public class PositionChangedEventArgs : EventArgs
{
    public int PreviousPosition { get; }
    public int CurrentPosition { get; }

    public PositionChangedEventArgs(int previousPosition, int currentPosition)
    {
        PreviousPosition = previousPosition;
        CurrentPosition = currentPosition;
    }
}
