using System;

namespace Microsoft.Maui.Platform.Linux.Services;

public class FileDialogResult
{
	public bool Accepted { get; init; }

	public string[] SelectedFiles { get; init; } = Array.Empty<string>();

	public string? SelectedFile
	{
		get
		{
			if (SelectedFiles.Length == 0)
			{
				return null;
			}
			return SelectedFiles[0];
		}
	}
}
