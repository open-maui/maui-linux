// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
