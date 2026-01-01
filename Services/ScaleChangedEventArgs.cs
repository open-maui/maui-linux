using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class ScaleChangedEventArgs : EventArgs
{
	public float OldScale { get; }

	public float NewScale { get; }

	public float NewDpi { get; }

	public ScaleChangedEventArgs(float oldScale, float newScale, float newDpi)
	{
		OldScale = oldScale;
		NewScale = newScale;
		NewDpi = newDpi;
	}
}
