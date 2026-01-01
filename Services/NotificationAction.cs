using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class NotificationAction
{
	public string Key { get; set; } = "";

	public string Label { get; set; } = "";

	public Action? Callback { get; set; }

	public NotificationAction()
	{
	}

	public NotificationAction(string key, string label, Action? callback = null)
	{
		Key = key;
		Label = label;
		Callback = callback;
	}
}
