// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

public class SkiaNavigationEventArgs : EventArgs
{
    public SkiaPage Page { get; }

    public SkiaNavigationEventArgs(SkiaPage page)
    {
        Page = page;
    }
}
