using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Microsoft.Maui.Platform.Linux.Services;

public class ClipboardService : IClipboard
{
	private string? _lastSetText;

	public bool HasText
	{
		get
		{
			try
			{
				return !string.IsNullOrEmpty(GetTextAsync().GetAwaiter().GetResult());
			}
			catch
			{
				return false;
			}
		}
	}

	public event EventHandler<EventArgs>? ClipboardContentChanged;

	public async Task<string?> GetTextAsync()
	{
		string text = await TryGetWithXclip();
		if (text != null)
		{
			return text;
		}
		return await TryGetWithXsel();
	}

	public async Task SetTextAsync(string? text)
	{
		_lastSetText = text;
		if (string.IsNullOrEmpty(text))
		{
			await ClearClipboard();
			return;
		}
		if (!(await TrySetWithXclip(text)))
		{
			await TrySetWithXsel(text);
		}
		this.ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
	}

	private async Task<string?> TryGetWithXclip()
	{
		_ = 1;
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xclip",
				Arguments = "-selection clipboard -o",
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
			string output = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
			return (process.ExitCode == 0) ? output : null;
		}
		catch
		{
			return null;
		}
	}

	private async Task<string?> TryGetWithXsel()
	{
		_ = 1;
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xsel",
				Arguments = "--clipboard --output",
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
			string output = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
			return (process.ExitCode == 0) ? output : null;
		}
		catch
		{
			return null;
		}
	}

	private async Task<bool> TrySetWithXclip(string text)
	{
		_ = 1;
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xclip",
				Arguments = "-selection clipboard",
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				return false;
			}
			await process.StandardInput.WriteAsync(text);
			process.StandardInput.Close();
			await process.WaitForExitAsync();
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> TrySetWithXsel(string text)
	{
		_ = 1;
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xsel",
				Arguments = "--clipboard --input",
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process == null)
			{
				return false;
			}
			await process.StandardInput.WriteAsync(text);
			process.StandardInput.Close();
			await process.WaitForExitAsync();
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	private async Task ClearClipboard()
	{
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "xclip",
				Arguments = "-selection clipboard",
				UseShellExecute = false,
				RedirectStandardInput = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				process.StandardInput.Close();
				await process.WaitForExitAsync();
			}
		}
		catch
		{
		}
	}
}
