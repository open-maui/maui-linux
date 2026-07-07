// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class DragEventArgs : EventArgs
{
    public DragData Data { get; }
    public int X { get; }
    public int Y { get; }
    // Default-accept: the drop is delivered unless a DragEnter/DragOver handler
    // explicitly sets this false. Consumers that only subscribe to Drop still
    // receive data.
    public bool Accepted { get; set; } = true;
    public DragAction AllowedAction { get; set; }
    public DragAction AcceptedAction { get; set; } = DragAction.Copy;

    public DragEventArgs(DragData data, int x, int y)
    {
        Data = data;
        X = x;
        Y = y;
    }
}
