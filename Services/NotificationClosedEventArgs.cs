// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class NotificationClosedEventArgs : EventArgs
{
    public uint NotificationId { get; }

    public NotificationCloseReason Reason { get; }

    public string? Tag { get; }

    public NotificationClosedEventArgs(uint notificationId, NotificationCloseReason reason, string? tag)
    {
        NotificationId = notificationId;
        Reason = reason;
        Tag = tag;
    }
}
