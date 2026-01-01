using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

public class BrowserService : IBrowser
{
	public async Task<bool> OpenAsync(string uri)
	{
		return await OpenAsync(new Uri(uri), (BrowserLaunchMode)0);
	}

	public async Task<bool> OpenAsync(string uri, BrowserLaunchMode launchMode)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		return await OpenAsync(new Uri(uri), launchMode);
	}

	public async Task<bool> OpenAsync(Uri uri)
	{
		return await OpenAsync(uri, (BrowserLaunchMode)0);
	}

	public async Task<bool> OpenAsync(Uri uri, BrowserLaunchMode launchMode)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		return await this.OpenAsync(uri, new BrowserLaunchOptions
		{
			LaunchMode = launchMode
		});
	}

	public async Task<bool> OpenAsync(Uri uri, BrowserLaunchOptions options)
	{
		if (uri == null)
		{
			throw new ArgumentNullException("uri");
		}
		try
		{
			string absoluteUri = uri.AbsoluteUri;
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xdg-open",
				Arguments = "\"" + absoluteUri + "\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				return false;
			}
			await process.WaitForExitAsync();
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}
}
