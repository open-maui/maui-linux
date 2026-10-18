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

[Obsolete("Use SkiaNavigationEventArgs; this type is removed in OpenMaui 11.0.")]
public class NavigationEventArgs : SkiaNavigationEventArgs
{
    public NavigationEventArgs(SkiaPage page) : base(page) { }
}
