// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

/// <summary>Raised by <see cref="SkiaSwipeView.SwipeEnded"/> when a swipe ends.</summary>
public class SkiaSwipeEndedEventArgs : EventArgs
{
    public SkiaSwipeEndedEventArgs(Microsoft.Maui.SwipeDirection swipeDirection, bool isOpen)
    {
        SwipeDirection = swipeDirection;
        IsOpen = isOpen;
    }

    /// <summary>The direction of the swipe.</summary>
    public Microsoft.Maui.SwipeDirection SwipeDirection { get; }

    /// <summary>Whether the swipe left the items open.</summary>
    public bool IsOpen { get; }
}
