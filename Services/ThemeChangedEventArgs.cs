using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class ThemeChangedEventArgs : EventArgs
{
	public SystemTheme NewTheme { get; }

	public ThemeChangedEventArgs(SystemTheme newTheme)
	{
		NewTheme = newTheme;
	}
}
