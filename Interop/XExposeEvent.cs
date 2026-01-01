using System;

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XExposeEvent
{
	public int Type;

	public ulong Serial;

	public int SendEvent;

	public IntPtr Display;

	public IntPtr Window;

	public int X;

	public int Y;

	public int Width;

	public int Height;

	public int Count;
}
