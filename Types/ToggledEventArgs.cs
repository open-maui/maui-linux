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

[Obsolete("Use SkiaToggledEventArgs; this type is removed in OpenMaui 11.0.")]
public class ToggledEventArgs : SkiaToggledEventArgs
{
    public ToggledEventArgs(bool value) : base(value) { }
}
