// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class ColorDialogResult
{
    public bool Accepted { get; init; }
    public float Red { get; init; }
    public float Green { get; init; }
    public float Blue { get; init; }
    public float Alpha { get; init; }
}
