using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Platform.Linux.Services;

public class PortalFilePickerService : IFilePicker
{
	private bool _portalAvailable = true;

	private string? _fallbackTool;

	public PortalFilePickerService()
	{
		DetectAvailableTools();
	}

	private void DetectAvailableTools()
	{
		_portalAvailable = CheckPortalAvailable();
		if (!_portalAvailable)
		{
			if (IsCommandAvailable("zenity"))
			{
				_fallbackTool = "zenity";
			}
			else if (IsCommandAvailable("kdialog"))
			{
				_fallbackTool = "kdialog";
			}
			else if (IsCommandAvailable("yad"))
			{
				_fallbackTool = "yad";
			}
		}
	}

	private bool CheckPortalAvailable()
	{
		try
		{
			return RunCommand("busctl", "--user list | grep -q org.freedesktop.portal.Desktop && echo yes").Trim() == "yes";
		}
		catch
		{
			return false;
		}
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

	public async Task<FileResult?> PickAsync(PickOptions? options = null)
	{
		if (options == null)
		{
			options = new PickOptions();
		}
		return (await PickFilesAsync(options, allowMultiple: false)).FirstOrDefault();
	}

	public async Task<IEnumerable<FileResult>> PickMultipleAsync(PickOptions? options = null)
	{
		if (options == null)
		{
			options = new PickOptions();
		}
		return await PickFilesAsync(options, allowMultiple: true);
	}

	private async Task<IEnumerable<FileResult>> PickFilesAsync(PickOptions options, bool allowMultiple)
	{
		if (_portalAvailable)
		{
			return await PickWithPortalAsync(options, allowMultiple);
		}
		if (_fallbackTool != null)
		{
			return await PickWithFallbackAsync(options, allowMultiple);
		}
		Console.WriteLine("[FilePickerService] No file picker available (install xdg-desktop-portal, zenity, or kdialog)");
		return Enumerable.Empty<FileResult>();
	}

	private async Task<IEnumerable<FileResult>> PickWithPortalAsync(PickOptions options, bool allowMultiple)
	{
		IEnumerable<FileResult> result = default(IEnumerable<FileResult>);
		object obj;
		int num;
		try
		{
			string text = BuildPortalFilterArgs(options.FileTypes);
			string value = (allowMultiple ? "true" : "false");
			string input = options.PickerTitle ?? "Open File";
			StringBuilder args = new StringBuilder();
			args.Append("call --session ");
			args.Append("--dest org.freedesktop.portal.Desktop ");
			args.Append("--object-path /org/freedesktop/portal/desktop ");
			args.Append("--method org.freedesktop.portal.FileChooser.OpenFile ");
			args.Append("\"\" ");
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
			handler.AppendLiteral("\"");
			handler.AppendFormatted(EscapeForShell(input));
			handler.AppendLiteral("\" ");
			stringBuilder2.Append(ref handler);
			args.Append("@a{sv} {");
			stringBuilder = args;
			StringBuilder stringBuilder3 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder);
			handler.AppendLiteral("'multiple': <");
			handler.AppendFormatted(value);
			handler.AppendLiteral(">");
			stringBuilder3.Append(ref handler);
			if (text != null)
			{
				stringBuilder = args;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
				handler.AppendLiteral(", 'filters': <");
				handler.AppendFormatted(text);
				handler.AppendLiteral(">");
				stringBuilder4.Append(ref handler);
			}
			args.Append("}");
			if (string.IsNullOrEmpty(ParseRequestPath(await Task.Run(() => RunCommand("gdbus", args.ToString())))))
			{
				result = Enumerable.Empty<FileResult>();
				return result;
			}
			await Task.Delay(100);
			if (_fallbackTool != null)
			{
				result = await PickWithFallbackAsync(options, allowMultiple);
				return result;
			}
			result = Enumerable.Empty<FileResult>();
			return result;
		}
		catch (Exception ex)
		{
			obj = ex;
			num = 1;
		}
		if (num != 1)
		{
			return result;
		}
		Exception ex2 = (Exception)obj;
		Console.WriteLine("[FilePickerService] Portal error: " + ex2.Message);
		if (_fallbackTool != null)
		{
			return await PickWithFallbackAsync(options, allowMultiple);
		}
		return Enumerable.Empty<FileResult>();
	}

	private async Task<IEnumerable<FileResult>> PickWithFallbackAsync(PickOptions options, bool allowMultiple)
	{
		return _fallbackTool switch
		{
			"zenity" => await PickWithZenityAsync(options, allowMultiple), 
			"kdialog" => await PickWithKdialogAsync(options, allowMultiple), 
			"yad" => await PickWithYadAsync(options, allowMultiple), 
			_ => Enumerable.Empty<FileResult>(), 
		};
	}

	private async Task<IEnumerable<FileResult>> PickWithZenityAsync(PickOptions options, bool allowMultiple)
	{
		StringBuilder args = new StringBuilder();
		args.Append("--file-selection ");
		if (!string.IsNullOrEmpty(options.PickerTitle))
		{
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
			handler.AppendLiteral("--title=\"");
			handler.AppendFormatted(EscapeForShell(options.PickerTitle));
			handler.AppendLiteral("\" ");
			stringBuilder2.Append(ref handler);
		}
		if (allowMultiple)
		{
			args.Append("--multiple --separator=\"|\" ");
		}
		List<string> extensionsFromFileType = GetExtensionsFromFileType(options.FileTypes);
		if (extensionsFromFileType.Count > 0)
		{
			string value = string.Join(" ", extensionsFromFileType.Select((string e) => "*" + e));
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder3 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder);
			handler.AppendLiteral("--file-filter=\"Files | ");
			handler.AppendFormatted(value);
			handler.AppendLiteral("\" ");
			stringBuilder3.Append(ref handler);
		}
		string text = await Task.Run(() => RunCommand("zenity", args.ToString()));
		if (string.IsNullOrWhiteSpace(text))
		{
			return Enumerable.Empty<FileResult>();
		}
		return ((IEnumerable<string>)text.Trim().Split('|', StringSplitOptions.RemoveEmptyEntries)).Select((Func<string, FileResult>)((string f) => new FileResult(f.Trim()))).ToList();
	}

	private async Task<IEnumerable<FileResult>> PickWithKdialogAsync(PickOptions options, bool allowMultiple)
	{
		StringBuilder args = new StringBuilder();
		args.Append("--getopenfilename ");
		args.Append(". ");
		List<string> extensionsFromFileType = GetExtensionsFromFileType(options.FileTypes);
		if (extensionsFromFileType.Count > 0)
		{
			string value = string.Join(" ", extensionsFromFileType.Select((string e) => "*" + e));
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
			handler.AppendLiteral("\"Files (");
			handler.AppendFormatted(value);
			handler.AppendLiteral(")\" ");
			stringBuilder2.Append(ref handler);
		}
		if (!string.IsNullOrEmpty(options.PickerTitle))
		{
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder3 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
			handler.AppendLiteral("--title \"");
			handler.AppendFormatted(EscapeForShell(options.PickerTitle));
			handler.AppendLiteral("\" ");
			stringBuilder3.Append(ref handler);
		}
		if (allowMultiple)
		{
			args.Append("--multiple --separate-output ");
		}
		string text = await Task.Run(() => RunCommand("kdialog", args.ToString()));
		if (string.IsNullOrWhiteSpace(text))
		{
			return Enumerable.Empty<FileResult>();
		}
		return ((IEnumerable<string>)text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)).Select((Func<string, FileResult>)((string f) => new FileResult(f.Trim()))).ToList();
	}

	private async Task<IEnumerable<FileResult>> PickWithYadAsync(PickOptions options, bool allowMultiple)
	{
		StringBuilder args = new StringBuilder();
		args.Append("--file ");
		if (!string.IsNullOrEmpty(options.PickerTitle))
		{
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
			handler.AppendLiteral("--title=\"");
			handler.AppendFormatted(EscapeForShell(options.PickerTitle));
			handler.AppendLiteral("\" ");
			stringBuilder2.Append(ref handler);
		}
		if (allowMultiple)
		{
			args.Append("--multiple --separator=\"|\" ");
		}
		List<string> extensionsFromFileType = GetExtensionsFromFileType(options.FileTypes);
		if (extensionsFromFileType.Count > 0)
		{
			string value = string.Join(" ", extensionsFromFileType.Select((string e) => "*" + e));
			StringBuilder stringBuilder = args;
			StringBuilder stringBuilder3 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder);
			handler.AppendLiteral("--file-filter=\"Files | ");
			handler.AppendFormatted(value);
			handler.AppendLiteral("\" ");
			stringBuilder3.Append(ref handler);
		}
		string text = await Task.Run(() => RunCommand("yad", args.ToString()));
		if (string.IsNullOrWhiteSpace(text))
		{
			return Enumerable.Empty<FileResult>();
		}
		return ((IEnumerable<string>)text.Trim().Split('|', StringSplitOptions.RemoveEmptyEntries)).Select((Func<string, FileResult>)((string f) => new FileResult(f.Trim()))).ToList();
	}

	private List<string> GetExtensionsFromFileType(FilePickerFileType? fileType)
	{
		List<string> list = new List<string>();
		if (fileType == null)
		{
			return list;
		}
		try
		{
			IEnumerable<string> value = fileType.Value;
			if (value == null)
			{
				return list;
			}
			foreach (string item2 in value)
			{
				if (item2.StartsWith(".") || (!item2.Contains('/') && !item2.Contains('*')))
				{
					string item = (item2.StartsWith(".") ? item2 : ("." + item2));
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private string? BuildPortalFilterArgs(FilePickerFileType? fileType)
	{
		List<string> extensionsFromFileType = GetExtensionsFromFileType(fileType);
		if (extensionsFromFileType.Count == 0)
		{
			return null;
		}
		string text = string.Join(", ", extensionsFromFileType.Select((string e) => "(uint32 0, '*" + e + "')"));
		return "[('Files', [" + text + "])]";
	}

	private string? ParseRequestPath(string output)
	{
		int num = output.IndexOf("'/");
		int num2 = output.IndexOf("',", num);
		if (num >= 0 && num2 > num)
		{
			return output.Substring(num + 1, num2 - num - 1);
		}
		return null;
	}

	private string EscapeForShell(string input)
	{
		return input.Replace("\"", "\\\"").Replace("'", "\\'");
	}

	private string RunCommand(string command, string arguments)
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
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit(30000);
			return result;
		}
		catch (Exception ex)
		{
			Console.WriteLine("[FilePickerService] Command error: " + ex.Message);
			return "";
		}
	}
}
