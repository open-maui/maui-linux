// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

public class SkiaVisualStateGroup
{
    public string Name { get; set; } = "";

    public List<SkiaVisualState> States { get; } = new List<SkiaVisualState>();

    public SkiaVisualState? CurrentState { get; set; }
}
