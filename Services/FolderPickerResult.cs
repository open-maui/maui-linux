namespace Microsoft.Maui.Platform.Linux.Services;

public class FolderPickerResult
{
	public FolderResult? Folder { get; }

	public bool WasSuccessful => Folder != null;

	public FolderPickerResult(FolderResult? folder)
	{
		Folder = folder;
	}
}
