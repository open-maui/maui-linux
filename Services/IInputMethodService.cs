using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public interface IInputMethodService
{
	bool IsActive { get; }

	string PreEditText { get; }

	int PreEditCursorPosition { get; }

	event EventHandler<TextCommittedEventArgs>? TextCommitted;

	event EventHandler<PreEditChangedEventArgs>? PreEditChanged;

	event EventHandler? PreEditEnded;

	void Initialize(IntPtr windowHandle);

	void SetFocus(IInputContext? context);

	void SetCursorLocation(int x, int y, int width, int height);

	bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown);

	void Reset();

	void Shutdown();
}
