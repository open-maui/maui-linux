using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class DropEventArgs : EventArgs
{
	public DragData Data { get; }

	public string? DroppedData { get; }

	public bool Handled { get; set; }

	public DropEventArgs(DragData data, string? droppedData)
	{
		Data = data;
		DroppedData = droppedData;
	}
}
