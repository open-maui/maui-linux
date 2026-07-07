// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class DropEventArgs : EventArgs
{
    public DragData Data { get; }
    public string? DroppedData { get; }

    /// <summary>
    /// Drop position in window-physical pixels — the same space the backends
    /// report pointer events in (captured at the last XdndPosition on X11,
    /// at the drop event on Wayland). Consumers hit-testing views should
    /// apply the same logical scaling the pointer path uses.
    /// </summary>
    public int X { get; }
    public int Y { get; }

    public bool Handled { get; set; }

    public DropEventArgs(DragData data, string? droppedData)
        : this(data, droppedData, 0, 0)
    {
    }

    public DropEventArgs(DragData data, string? droppedData, int x, int y)
    {
        Data = data;
        DroppedData = droppedData;
        X = x;
        Y = y;
    }
}
