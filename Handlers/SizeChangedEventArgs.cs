using System;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class SizeChangedEventArgs : EventArgs
{
	public int Width { get; }

	public int Height { get; }

	public SizeChangedEventArgs(int width, int height)
	{
		Width = width;
		Height = height;
	}
}
