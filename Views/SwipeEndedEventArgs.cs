// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

public class SwipeEndedEventArgs : EventArgs
{
    public SwipeDirection Direction { get; }

    public bool IsOpen { get; }

    public SwipeEndedEventArgs(SwipeDirection direction, bool isOpen)
    {
        Direction = direction;
        IsOpen = isOpen;
    }
}
