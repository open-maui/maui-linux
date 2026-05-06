// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class NotificationOptions
{
    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    public string? IconPath { get; set; }

    public string? IconName { get; set; }

    public NotificationUrgency Urgency { get; set; } = NotificationUrgency.Normal;

    public int ExpireTimeMs { get; set; } = 5000;

    public string? Category { get; set; }

    public bool IsTransient { get; set; }

    public Dictionary<string, string>? Actions { get; set; }
}
