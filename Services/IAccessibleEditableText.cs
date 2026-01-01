namespace Microsoft.Maui.Platform.Linux.Services;

public interface IAccessibleEditableText : IAccessibleText, IAccessible
{
	bool SetText(string text);

	bool InsertText(int position, string text);

	bool DeleteText(int start, int end);

	bool CopyText(int start, int end);

	bool CutText(int start, int end);

	bool PasteText(int position);
}
