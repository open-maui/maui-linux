// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

public class MenuItemClickedEventArgs : EventArgs
{
    public MenuItem Item { get; }

    public MenuItemClickedEventArgs(MenuItem item)
    {
        Item = item;
    }
}
