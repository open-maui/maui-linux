using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Maui.Platform.Linux.Services;

public class PortalFolderPickerService
{
	public async Task<FolderPickerResult> PickAsync(FolderPickerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (options == null)
		{
			options = new FolderPickerOptions();
		}
		string text = null;
		if (IsCommandAvailable("zenity"))
		{
			string args = "--file-selection --directory --title=\"" + (options.Title ?? "Select Folder") + "\"";
			text = await Task.Run(() => RunCommand("zenity", args)?.Trim());
		}
		else if (IsCommandAvailable("kdialog"))
		{
			string args2 = "--getexistingdirectory . --title \"" + (options.Title ?? "Select Folder") + "\"";
			text = await Task.Run(() => RunCommand("kdialog", args2)?.Trim());
		}
		if (!string.IsNullOrEmpty(text) && Directory.Exists(text))
		{
			return new FolderPickerResult(new FolderResult(text));
		}
		return new FolderPickerResult(null);
	}

	public async Task<FolderPickerResult> PickAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return await PickAsync(null, cancellationToken);
	}

	private bool IsCommandAvailable(string command)
	{
		try
		{
			return !string.IsNullOrWhiteSpace(RunCommand("which", command));
		}
		catch
		{
			return false;
		}
	}

	private string? RunCommand(string command, string arguments)
	{
		try
		{
			using Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = command,
					Arguments = arguments,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit(30000);
			return result;
		}
		catch
		{
			return null;
		}
	}
}
