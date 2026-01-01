using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

internal class LinuxFileResult : FileResult
{
	public LinuxFileResult(string fullPath)
		: base(fullPath)
	{
	}
}
