// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

public class MenuItemClickedEventArgs : EventArgs
{
#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public MenuItem Item { get; }
#pragma warning restore CS0618

#pragma warning disable CS0618 // legacy type kept for binary compatibility until OpenMaui 11.0
    public MenuItemClickedEventArgs(MenuItem item)
#pragma warning restore CS0618
    {
        Item = item;
    }
}
