namespace Microsoft.Maui.Platform.Linux.Services;

public interface IAccessibleText : IAccessible
{
	string Text { get; }

	int CaretOffset { get; }

	int SelectionCount { get; }

	(int Start, int End) GetSelection(int index);

	bool SetSelection(int index, int start, int end);

	char GetCharacterAtOffset(int offset);

	string GetTextInRange(int start, int end);

	AccessibleRect GetCharacterBounds(int offset);
}
