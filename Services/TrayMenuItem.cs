// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class TrayMenuItem
{
    public string Text { get; set; } = "";

    public Action? Action { get; set; }

    public bool IsSeparator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? IconPath { get; set; }
}
