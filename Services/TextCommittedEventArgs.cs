using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class TextCommittedEventArgs : EventArgs
{
	public string Text { get; }

	public TextCommittedEventArgs(string text)
	{
		Text = text;
	}
}
