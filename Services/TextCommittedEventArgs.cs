// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class TextCommittedEventArgs : EventArgs
{
    public string Text { get; }

    public TextCommittedEventArgs(string text)
    {
        Text = text;
    }
}
