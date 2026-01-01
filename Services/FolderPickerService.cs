using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Maui.Platform.Linux.Services;

public class FolderPickerService
{
	public async Task<string?> PickFolderAsync(string? initialDirectory = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		_ = 1;
		try
		{
			string text = await TryZenityFolderPicker(initialDirectory, cancellationToken);
			if (text != null)
			{
				return text;
			}
			text = await TryKdialogFolderPicker(initialDirectory, cancellationToken);
			if (text != null)
			{
				return text;
			}
			return null;
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		catch
		{
			return null;
		}
	}

	private async Task<string?> TryZenityFolderPicker(string? initialDirectory, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			string text = "--file-selection --directory";
			if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
			{
				text = text + " --filename=\"" + initialDirectory + "/\"";
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "zenity",
				Arguments = text,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				return null;
			}
			string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
			await process.WaitForExitAsync(cancellationToken);
			if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
			{
				string text2 = output.Trim();
				if (Directory.Exists(text2))
				{
					return text2;
				}
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	private async Task<string?> TryKdialogFolderPicker(string? initialDirectory, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			string text = "--getexistingdirectory";
			if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
			{
				text = text + " \"" + initialDirectory + "\"";
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "kdialog",
				Arguments = text,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				return null;
			}
			string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
			await process.WaitForExitAsync(cancellationToken);
			if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
			{
				string text2 = output.Trim();
				if (Directory.Exists(text2))
				{
					return text2;
				}
			}
			return null;
		}
		catch
		{
			return null;
		}
	}
}
