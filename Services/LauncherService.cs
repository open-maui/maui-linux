using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

public class LauncherService : ILauncher
{
	public Task<bool> CanOpenAsync(Uri uri)
	{
		return Task.FromResult(result: true);
	}

	public Task<bool> OpenAsync(Uri uri)
	{
		return Task.Run(delegate
		{
			try
			{
				using Process process = Process.Start(new ProcessStartInfo
				{
					FileName = "xdg-open",
					Arguments = uri.ToString(),
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				});
				if (process == null)
				{
					return false;
				}
				return true;
			}
			catch
			{
				return false;
			}
		});
	}

	public Task<bool> OpenAsync(OpenFileRequest request)
	{
		if (request.File == null)
		{
			return Task.FromResult(result: false);
		}
		return Task.Run(delegate
		{
			try
			{
				string fullPath = ((FileBase)request.File).FullPath;
				using Process process = Process.Start(new ProcessStartInfo
				{
					FileName = "xdg-open",
					Arguments = "\"" + fullPath + "\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				});
				return process != null;
			}
			catch
			{
				return false;
			}
		});
	}

	public Task<bool> TryOpenAsync(Uri uri)
	{
		return OpenAsync(uri);
	}
}
