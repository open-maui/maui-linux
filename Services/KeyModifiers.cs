using System;

namespace Microsoft.Maui.Platform.Linux.Services;

[Flags]
public enum KeyModifiers
{
	None = 0,
	Shift = 1,
	Control = 2,
	Alt = 4,
	Super = 8,
	CapsLock = 0x10,
	NumLock = 0x20
}
