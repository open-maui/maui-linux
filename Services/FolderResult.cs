// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class FolderResult
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path) ?? Path;

    public FolderResult(string path)
    {
        Path = path;
    }
}
