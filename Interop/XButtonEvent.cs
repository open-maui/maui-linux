using System;

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XButtonEvent
{
	public int Type;

	public ulong Serial;

	public int SendEvent;

	public IntPtr Display;

	public IntPtr Window;

	public IntPtr Root;

	public IntPtr Subwindow;

	public ulong Time;

	public int X;

	public int Y;

	public int XRoot;

	public int YRoot;

	public uint State;

	public uint Button;

	public int SameScreen;
}
