using System;
using System.Collections.Generic;

namespace Microsoft.Maui.Platform.Linux.Services;

public class PreEditChangedEventArgs : EventArgs
{
	public string PreEditText { get; }

	public int CursorPosition { get; }

	public IReadOnlyList<PreEditAttribute> Attributes { get; }

	public PreEditChangedEventArgs(string preEditText, int cursorPosition, IReadOnlyList<PreEditAttribute>? attributes = null)
	{
		PreEditText = preEditText;
		CursorPosition = cursorPosition;
		Attributes = attributes ?? Array.Empty<PreEditAttribute>();
	}
}
