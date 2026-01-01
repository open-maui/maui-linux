using System;

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XConfigureEvent
{
	public int Type;

	public ulong Serial;

	public int SendEvent;

	public IntPtr Display;

	public IntPtr Event;

	public IntPtr Window;

	public int X;

	public int Y;

	public int Width;

	public int Height;

	public int BorderWidth;

	public IntPtr Above;

	public int OverrideRedirect;
}
