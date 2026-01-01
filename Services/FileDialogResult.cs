// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class FileDialogResult
{
    public bool Accepted { get; init; }
    public string[] SelectedFiles { get; init; } = [];
    public string? SelectedFile => SelectedFiles.Length > 0 ? SelectedFiles[0] : null;
}
