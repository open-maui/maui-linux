using System;

namespace Microsoft.Maui.Platform.Linux.Interop;

public struct XSetWindowAttributes
{
	public IntPtr BackgroundPixmap;

	public ulong BackgroundPixel;

	public IntPtr BorderPixmap;

	public ulong BorderPixel;

	public int BitGravity;

	public int WinGravity;

	public int BackingStore;

	public ulong BackingPlanes;

	public ulong BackingPixel;

	public int SaveUnder;

	public long EventMask;

	public long DoNotPropagateMask;

	public int OverrideRedirect;

	public IntPtr Colormap;

	public IntPtr Cursor;
}
