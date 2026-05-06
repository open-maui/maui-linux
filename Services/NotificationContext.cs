// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

internal class NotificationContext
{
    public string? Tag { get; set; }

    public Dictionary<string, Action?>? ActionCallbacks { get; set; }
}
