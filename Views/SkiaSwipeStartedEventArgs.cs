// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

/// <summary>Raised by <see cref="SkiaSwipeView.SwipeStarted"/> when a swipe starts.</summary>
public class SkiaSwipeStartedEventArgs : EventArgs
{
    public SkiaSwipeStartedEventArgs(Microsoft.Maui.SwipeDirection swipeDirection)
    {
        SwipeDirection = swipeDirection;
    }

    /// <summary>The direction of the swipe.</summary>
    public Microsoft.Maui.SwipeDirection SwipeDirection { get; }
}
