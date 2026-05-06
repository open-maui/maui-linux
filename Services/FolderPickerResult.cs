// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class FolderPickerResult
{
    public FolderResult? Folder { get; }
    public bool WasSuccessful => Folder != null;

    public FolderPickerResult(FolderResult? folder)
    {
        Folder = folder;
    }
}
