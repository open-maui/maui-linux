// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

public class MenuItem
{
    public string Text { get; set; } = string.Empty;

    public string? Shortcut { get; set; }

    public bool IsSeparator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsChecked { get; set; }

    public string? IconSource { get; set; }

    public List<MenuItem> SubItems { get; } = new List<MenuItem>();

    public event EventHandler? Clicked;

    internal void OnClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
