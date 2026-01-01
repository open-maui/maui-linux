using System;
using System.Collections.Generic;

namespace Microsoft.Maui.Platform.Linux.Services;

internal class NotificationContext
{
	public string? Tag { get; set; }

	public Dictionary<string, Action?>? ActionCallbacks { get; set; }
}
