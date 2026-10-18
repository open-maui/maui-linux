// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Maui.Platform;

public class SkiaToggledEventArgs : EventArgs
{
    public bool Value { get; }

    public SkiaToggledEventArgs(bool value)
    {
        Value = value;
    }
}
