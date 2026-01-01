using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class DragData
{
	public IntPtr SourceWindow { get; set; }

	public IntPtr[] SupportedTypes { get; set; } = Array.Empty<IntPtr>();

	public string? Text { get; set; }

	public string[]? FilePaths { get; set; }

	public object? Data { get; set; }
}
