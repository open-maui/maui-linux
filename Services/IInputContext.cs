namespace Microsoft.Maui.Platform.Linux.Services;

public interface IInputContext
{
	string Text { get; set; }

	int CursorPosition { get; set; }

	int SelectionStart { get; }

	int SelectionLength { get; }

	void OnTextCommitted(string text);

	void OnPreEditChanged(string preEditText, int cursorPosition);

	void OnPreEditEnded();
}
