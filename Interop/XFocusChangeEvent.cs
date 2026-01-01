using System;

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XFocusChangeEvent
{
	public int Type;

	public ulong Serial;

	public int SendEvent;

	public IntPtr Display;

	public IntPtr Window;

	public int Mode;

	public int Detail;
}
