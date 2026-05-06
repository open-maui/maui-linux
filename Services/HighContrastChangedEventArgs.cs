// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class HighContrastChangedEventArgs : EventArgs
{
    public bool IsEnabled { get; }

    public HighContrastTheme Theme { get; }

    public HighContrastChangedEventArgs(bool isEnabled, HighContrastTheme theme)
    {
        IsEnabled = isEnabled;
        Theme = theme;
    }
}
