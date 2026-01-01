using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class NotificationActionEventArgs : EventArgs
{
	public uint NotificationId { get; }

	public string ActionKey { get; }

	public string? Tag { get; }

	public NotificationActionEventArgs(uint notificationId, string actionKey, string? tag)
	{
		NotificationId = notificationId;
		ActionKey = actionKey;
		Tag = tag;
	}
}
