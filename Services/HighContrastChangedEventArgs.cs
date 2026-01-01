using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class HighContrastChangedEventArgs : EventArgs
{
	public bool IsEnabled { get; }

	public HighContrastTheme Theme { get; }

	public HighContrastChangedEventArgs(bool isEnabled, HighContrastTheme theme)
	{
		IsEnabled = isEnabled;
		Theme = theme;
	}
}
