using System;

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XClientMessageEvent
{
	public int Type;

	public ulong Serial;

	public int SendEvent;

	public IntPtr Display;

	public IntPtr Window;

	public IntPtr MessageType;

	public int Format;

	public ClientMessageData Data;
}
