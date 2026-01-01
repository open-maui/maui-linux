using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Maui.Platform.Linux.Services;

public class SystemTrayService : IDisposable
{
	private Process? _trayProcess;

	private readonly string _appName;

	private string? _iconPath;

	private string? _tooltip;

	private readonly List<TrayMenuItem> _menuItems = new List<TrayMenuItem>();

	private bool _isVisible;

	private bool _disposed;

	public string? IconPath
	{
		get
		{
			return _iconPath;
		}
		set
		{
			_iconPath = value;
			if (_isVisible)
			{
				UpdateTray();
			}
		}
	}

	public string? Tooltip
	{
		get
		{
			return _tooltip;
		}
		set
		{
			_tooltip = value;
			if (_isVisible)
			{
				UpdateTray();
			}
		}
	}

	public IList<TrayMenuItem> MenuItems => _menuItems;

	public event EventHandler? Clicked;

	public event EventHandler<string>? MenuItemClicked;

	public SystemTrayService(string appName)
	{
		_appName = appName;
	}

	public async Task ShowAsync()
	{
		if (!_isVisible)
		{
			await TryYadTray();
			_isVisible = true;
		}
	}

	public void Hide()
	{
		if (_isVisible)
		{
			_trayProcess?.Kill();
			_trayProcess?.Dispose();
			_trayProcess = null;
			_isVisible = false;
		}
	}

	public void UpdateTray()
	{
		if (_isVisible)
		{
			Hide();
			ShowAsync();
		}
	}

	private async Task<bool> TryYadTray()
	{
		try
		{
			string arguments = BuildYadArgs();
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "yad",
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			_trayProcess = Process.Start(startInfo);
			if (_trayProcess == null)
			{
				return false;
			}
			Task.Run(async delegate
			{
				try
				{
					while (!_trayProcess.HasExited)
					{
						string text = await _trayProcess.StandardOutput.ReadLineAsync();
						if (!string.IsNullOrEmpty(text))
						{
							HandleTrayOutput(text);
						}
					}
				}
				catch
				{
				}
			});
			return true;
		}
		catch
		{
			return false;
		}
	}

	private string BuildYadArgs()
	{
		List<string> list = new List<string> { "--notification", "--listen" };
		if (!string.IsNullOrEmpty(_iconPath) && File.Exists(_iconPath))
		{
			list.Add("--image=\"" + _iconPath + "\"");
		}
		else
		{
			list.Add("--image=application-x-executable");
		}
		if (!string.IsNullOrEmpty(_tooltip))
		{
			list.Add("--text=\"" + EscapeArg(_tooltip) + "\"");
		}
		if (_menuItems.Count > 0)
		{
			string text = string.Join("!", _menuItems.Select(delegate(TrayMenuItem m)
			{
				object obj;
				if (!m.IsSeparator)
				{
					obj = EscapeArg(m.Text);
					if (obj == null)
					{
						return "";
					}
				}
				else
				{
					obj = "---";
				}
				return (string)obj;
			}));
			list.Add("--menu=\"" + text + "\"");
		}
		list.Add("--command=\"echo clicked\"");
		return string.Join(" ", list);
	}

	private void HandleTrayOutput(string output)
	{
		if (output == "clicked")
		{
			this.Clicked?.Invoke(this, EventArgs.Empty);
			return;
		}
		TrayMenuItem trayMenuItem = _menuItems.FirstOrDefault((TrayMenuItem m) => m.Text == output);
		if (trayMenuItem != null)
		{
			trayMenuItem.Action?.Invoke();
			this.MenuItemClicked?.Invoke(this, output);
		}
	}

	public void AddMenuItem(string text, Action? action = null)
	{
		_menuItems.Add(new TrayMenuItem
		{
			Text = text,
			Action = action
		});
	}

	public void AddSeparator()
	{
		_menuItems.Add(new TrayMenuItem
		{
			IsSeparator = true
		});
	}

	public void ClearMenuItems()
	{
		_menuItems.Clear();
	}

	public static bool IsAvailable()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "which",
				Arguments = "yad",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			});
			if (process == null)
			{
				return false;
			}
			process.WaitForExit();
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	private static string EscapeArg(string arg)
	{
		return arg?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("!", "\\!") ?? "";
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			Hide();
			GC.SuppressFinalize(this);
		}
	}

	~SystemTrayService()
	{
		Dispose();
	}
}
