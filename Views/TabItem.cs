// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

public class TabItem
{
    public string Title { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public SkiaView Content { get; set; } = null!;

    public string? Badge { get; set; }
}
