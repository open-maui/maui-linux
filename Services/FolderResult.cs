using System.IO;

namespace Microsoft.Maui.Platform.Linux.Services;

public class FolderResult
{
	public string Path { get; }

	public string Name => System.IO.Path.GetFileName(Path) ?? Path;

	public FolderResult(string path)
	{
		Path = path;
	}
}
