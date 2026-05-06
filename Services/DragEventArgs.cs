// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class DragEventArgs : EventArgs
{
    public DragData Data { get; }
    public int X { get; }
    public int Y { get; }
    public bool Accepted { get; set; }
    public DragAction AllowedAction { get; set; }
    public DragAction AcceptedAction { get; set; } = DragAction.Copy;

    public DragEventArgs(DragData data, int x, int y)
    {
        Data = data;
        X = x;
        Y = y;
    }
}
