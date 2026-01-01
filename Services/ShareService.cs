using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

public class ShareService : IShare
{
	public async Task RequestAsync(ShareTextRequest request)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		if (!string.IsNullOrEmpty(request.Uri))
		{
			await OpenUrlAsync(request.Uri);
		}
		else if (!string.IsNullOrEmpty(request.Text))
		{
			string text = Uri.EscapeDataString(request.Subject ?? "");
			string text2 = Uri.EscapeDataString(request.Text ?? "");
			string url = "mailto:?subject=" + text + "&body=" + text2;
			await OpenUrlAsync(url);
		}
	}

	public async Task RequestAsync(ShareFileRequest request)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		if (request.File == null)
		{
			throw new ArgumentException("File is required", "request");
		}
		await ShareFileAsync(((FileBase)request.File).FullPath);
	}

	public async Task RequestAsync(ShareMultipleFilesRequest request)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		if (request.Files == null || !request.Files.Any())
		{
			throw new ArgumentException("Files are required", "request");
		}
		foreach (ShareFile file in request.Files)
		{
			await ShareFileAsync(((FileBase)file).FullPath);
		}
	}

	private async Task OpenUrlAsync(string url)
	{
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xdg-open",
				Arguments = "\"" + url + "\"",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Failed to open URL for sharing", innerException);
		}
	}

	private async Task ShareFileAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException("File not found for sharing", filePath);
		}
		try
		{
			if (await TryPortalShareAsync(filePath))
			{
				return;
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xdg-open",
				Arguments = "\"" + Path.GetDirectoryName(filePath) + "\"",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Failed to share file", innerException);
		}
	}

	private async Task<bool> TryPortalShareAsync(string filePath)
	{
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "zenity",
				Arguments = $"--info --text=\"File ready to share:\\n{Path.GetFileName(filePath)}\\n\\nPath: {filePath}\" --title=\"Share File\"",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}
}
