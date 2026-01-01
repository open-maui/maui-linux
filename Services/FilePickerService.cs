using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

public class FilePickerService : IFilePicker
{
	private enum DialogTool
	{
		None,
		Zenity,
		Kdialog
	}

	private static DialogTool? _availableTool;

	private static DialogTool GetAvailableTool()
	{
		if (_availableTool.HasValue)
		{
			return _availableTool.Value;
		}
		if (IsToolAvailable("zenity"))
		{
			_availableTool = DialogTool.Zenity;
			return DialogTool.Zenity;
		}
		if (IsToolAvailable("kdialog"))
		{
			_availableTool = DialogTool.Kdialog;
			return DialogTool.Kdialog;
		}
		_availableTool = DialogTool.None;
		return DialogTool.None;
	}

	private static bool IsToolAvailable(string tool)
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "which",
				Arguments = tool,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			});
			process?.WaitForExit(1000);
			return process != null && process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	public Task<FileResult?> PickAsync(PickOptions? options = null)
	{
		return PickInternalAsync(options, multiple: false);
	}

	public Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null)
	{
		return PickMultipleInternalAsync(options);
	}

	private async Task<FileResult?> PickInternalAsync(PickOptions? options, bool multiple)
	{
		return (await PickMultipleInternalAsync(options, multiple)).FirstOrDefault();
	}

	private Task<IEnumerable<FileResult>> PickMultipleInternalAsync(PickOptions? options, bool multiple = true)
	{
		return Task.Run(delegate
		{
			DialogTool availableTool = GetAvailableTool();
			string arguments;
			switch (availableTool)
			{
			case DialogTool.None:
			{
				Console.WriteLine("No file dialog available. Please enter file path:");
				string text = Console.ReadLine();
				if (!string.IsNullOrEmpty(text) && File.Exists(text))
				{
					return (IEnumerable<FileResult>)(object)new LinuxFileResult[1]
					{
						new LinuxFileResult(text)
					};
				}
				return Array.Empty<FileResult>();
			}
			case DialogTool.Zenity:
				arguments = BuildZenityArguments(options, multiple);
				break;
			default:
				arguments = BuildKdialogArguments(options, multiple);
				break;
			}
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = ((availableTool == DialogTool.Zenity) ? "zenity" : "kdialog"),
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			try
			{
				using Process process = Process.Start(startInfo);
				if (process == null)
				{
					return Array.Empty<FileResult>();
				}
				string text2 = process.StandardOutput.ReadToEnd().Trim();
				process.WaitForExit();
				if (process.ExitCode != 0 || string.IsNullOrEmpty(text2))
				{
					return Array.Empty<FileResult>();
				}
				char separator = ((availableTool == DialogTool.Zenity) ? '|' : '\n');
				return (from p in text2.Split(separator, StringSplitOptions.RemoveEmptyEntries).Where(File.Exists)
					select (FileResult)(object)new LinuxFileResult(p)).ToArray();
			}
			catch
			{
				return Array.Empty<FileResult>();
			}
		});
	}

	private string BuildZenityArguments(PickOptions? options, bool multiple)
	{
		StringBuilder stringBuilder = new StringBuilder("--file-selection");
		if (multiple)
		{
			stringBuilder.Append(" --multiple --separator='|'");
		}
		if (!string.IsNullOrEmpty((options != null) ? options.PickerTitle : null))
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
			handler.AppendLiteral(" --title=\"");
			handler.AppendFormatted(EscapeArgument(options.PickerTitle));
			handler.AppendLiteral("\"");
			stringBuilder3.Append(ref handler);
		}
		if (((options != null) ? options.FileTypes : null) != null)
		{
			foreach (string item in options.FileTypes.Value)
			{
				string value = (item.StartsWith(".") ? item : ("." + item));
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
				handler.AppendLiteral(" --file-filter='*");
				handler.AppendFormatted(value);
				handler.AppendLiteral("'");
				stringBuilder4.Append(ref handler);
			}
		}
		return stringBuilder.ToString();
	}

	private string BuildKdialogArguments(PickOptions? options, bool multiple)
	{
		StringBuilder stringBuilder = new StringBuilder("--getopenfilename");
		if (multiple)
		{
			stringBuilder.Insert(0, "--multiple ");
		}
		stringBuilder.Append(" .");
		if (((options != null) ? options.FileTypes : null) != null)
		{
			string value = string.Join(" ", options.FileTypes.Value.Select((string e) => (!e.StartsWith(".")) ? ("*." + e) : ("*" + e)));
			if (!string.IsNullOrEmpty(value))
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder3 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder2);
				handler.AppendLiteral(" \"");
				handler.AppendFormatted(value);
				handler.AppendLiteral("\"");
				stringBuilder3.Append(ref handler);
			}
		}
		if (!string.IsNullOrEmpty((options != null) ? options.PickerTitle : null))
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
			handler.AppendLiteral(" --title \"");
			handler.AppendFormatted(EscapeArgument(options.PickerTitle));
			handler.AppendLiteral("\"");
			stringBuilder4.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string EscapeArgument(string arg)
	{
		return arg.Replace("\"", "\\\"").Replace("'", "\\'");
	}
}
