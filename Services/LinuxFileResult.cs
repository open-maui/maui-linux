// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

internal class LinuxFileResult : FileResult
{
    public LinuxFileResult(string fullPath)
        : base(fullPath)
    {
    }
}
