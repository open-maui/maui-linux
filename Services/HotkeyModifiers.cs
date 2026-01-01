using System;

namespace Microsoft.Maui.Platform.Linux.Services;

[Flags]
public enum HotkeyModifiers
{
	None = 0,
	Shift = 1,
	Control = 2,
	Alt = 4,
	Super = 8
}
