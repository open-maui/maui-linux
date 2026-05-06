// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class DragData
{
    public nint SourceWindow { get; set; }
    public nint[] SupportedTypes { get; set; } = [];
    public string? Text { get; set; }
    public string[]? FilePaths { get; set; }
    public object? Data { get; set; }
}
