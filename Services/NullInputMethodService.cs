// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class NullInputMethodService : IInputMethodService
{
    public bool IsActive => false;

    public string PreEditText => string.Empty;

    public int PreEditCursorPosition => 0;

    public event EventHandler<TextCommittedEventArgs>? TextCommitted;

    public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;

    public event EventHandler? PreEditEnded;

    public void Initialize(IntPtr windowHandle)
    {
    }

    public void SetFocus(IInputContext? context)
    {
    }

    public void SetCursorLocation(int x, int y, int width, int height)
    {
    }

    public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
    {
        return false;
    }

    public void Reset()
    {
    }

    public void Shutdown()
    {
    }
}
