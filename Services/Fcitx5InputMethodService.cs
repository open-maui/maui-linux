using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Maui.Platform.Linux.Services;

public class Fcitx5InputMethodService : IInputMethodService, IDisposable
{
	private IInputContext? _currentContext;

	private string _preEditText = string.Empty;

	private int _preEditCursorPosition;

	private bool _isActive;

	private bool _disposed;

	private Process? _dBusMonitor;

	private string? _inputContextPath;

	public bool IsActive => _isActive;

	public string PreEditText => _preEditText;

	public int PreEditCursorPosition => _preEditCursorPosition;

	public event EventHandler<TextCommittedEventArgs>? TextCommitted;

	public event EventHandler<PreEditChangedEventArgs>? PreEditChanged;

	public event EventHandler? PreEditEnded;

	public void Initialize(IntPtr windowHandle)
	{
		try
		{
			string text = RunDBusCommand("call --session --dest org.fcitx.Fcitx5 --object-path /org/freedesktop/portal/inputmethod --method org.fcitx.Fcitx.InputMethod1.CreateInputContext \"maui-linux\" \"\"");
			if (!string.IsNullOrEmpty(text) && text.Contains("/"))
			{
				int num = text.IndexOf("'/");
				int num2 = text.IndexOf("'", num + 1);
				if (num >= 0 && num2 > num)
				{
					_inputContextPath = text.Substring(num + 1, num2 - num - 1);
					Console.WriteLine("Fcitx5InputMethodService: Created context at " + _inputContextPath);
					StartMonitoring();
				}
			}
			else
			{
				Console.WriteLine("Fcitx5InputMethodService: Failed to create input context");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Fcitx5InputMethodService: Initialization failed - " + ex.Message);
		}
	}

	private void StartMonitoring()
	{
		if (string.IsNullOrEmpty(_inputContextPath))
		{
			return;
		}
		Task.Run(async delegate
		{
			_ = 2;
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					FileName = "dbus-monitor",
					Arguments = "--session \"path='" + _inputContextPath + "'\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				};
				_dBusMonitor = Process.Start(startInfo);
				if (_dBusMonitor != null)
				{
					StreamReader reader = _dBusMonitor.StandardOutput;
					while (!_disposed && !_dBusMonitor.HasExited)
					{
						string text = await reader.ReadLineAsync();
						if (text == null)
						{
							break;
						}
						if (text.Contains("CommitString"))
						{
							await ProcessCommitSignal(reader);
						}
						else if (text.Contains("UpdatePreedit"))
						{
							await ProcessPreeditSignal(reader);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Fcitx5InputMethodService: Monitor error - " + ex.Message);
			}
		});
	}

	private async Task ProcessCommitSignal(StreamReader reader)
	{
		try
		{
			for (int i = 0; i < 5; i++)
			{
				string text = await reader.ReadLineAsync();
				if (text == null)
				{
					break;
				}
				if (text.Contains("string"))
				{
					Match match = Regex.Match(text, "string\\s+\"([^\"]*)\"");
					if (match.Success)
					{
						string value = match.Groups[1].Value;
						_preEditText = string.Empty;
						_preEditCursorPosition = 0;
						_isActive = false;
						this.TextCommitted?.Invoke(this, new TextCommittedEventArgs(value));
						_currentContext?.OnTextCommitted(value);
						break;
					}
				}
			}
		}
		catch
		{
		}
	}

	private async Task ProcessPreeditSignal(StreamReader reader)
	{
		try
		{
			for (int i = 0; i < 10; i++)
			{
				string text = await reader.ReadLineAsync();
				if (text == null)
				{
					break;
				}
				if (text.Contains("string"))
				{
					Match match = Regex.Match(text, "string\\s+\"([^\"]*)\"");
					if (match.Success)
					{
						_preEditText = match.Groups[1].Value;
						_isActive = !string.IsNullOrEmpty(_preEditText);
						this.PreEditChanged?.Invoke(this, new PreEditChangedEventArgs(_preEditText, _preEditCursorPosition, new List<PreEditAttribute>()));
						_currentContext?.OnPreEditChanged(_preEditText, _preEditCursorPosition);
						break;
					}
				}
			}
		}
		catch
		{
		}
	}

	public void SetFocus(IInputContext? context)
	{
		_currentContext = context;
		if (!string.IsNullOrEmpty(_inputContextPath))
		{
			if (context != null)
			{
				RunDBusCommand($"call --session --dest org.fcitx.Fcitx5 --object-path {_inputContextPath} --method org.fcitx.Fcitx.InputContext1.FocusIn");
			}
			else
			{
				RunDBusCommand($"call --session --dest org.fcitx.Fcitx5 --object-path {_inputContextPath} --method org.fcitx.Fcitx.InputContext1.FocusOut");
			}
		}
	}

	public void SetCursorLocation(int x, int y, int width, int height)
	{
		if (!string.IsNullOrEmpty(_inputContextPath))
		{
			RunDBusCommand($"call --session --dest org.fcitx.Fcitx5 --object-path {_inputContextPath} --method org.fcitx.Fcitx.InputContext1.SetCursorRect {x} {y} {width} {height}");
		}
	}

	public bool ProcessKeyEvent(uint keyCode, KeyModifiers modifiers, bool isKeyDown)
	{
		if (string.IsNullOrEmpty(_inputContextPath))
		{
			return false;
		}
		uint num = ConvertModifiers(modifiers);
		if (!isKeyDown)
		{
			num |= 0x40000000;
		}
		return RunDBusCommand($"call --session --dest org.fcitx.Fcitx5 --object-path {_inputContextPath} --method org.fcitx.Fcitx.InputContext1.ProcessKeyEvent {keyCode} {keyCode} {num} {(isKeyDown ? "true" : "false")} 0")?.Contains("true") ?? false;
	}

	private uint ConvertModifiers(KeyModifiers modifiers)
	{
		uint num = 0u;
		if (modifiers.HasFlag(KeyModifiers.Shift))
		{
			num |= 1;
		}
		if (modifiers.HasFlag(KeyModifiers.CapsLock))
		{
			num |= 2;
		}
		if (modifiers.HasFlag(KeyModifiers.Control))
		{
			num |= 4;
		}
		if (modifiers.HasFlag(KeyModifiers.Alt))
		{
			num |= 8;
		}
		if (modifiers.HasFlag(KeyModifiers.Super))
		{
			num |= 0x40;
		}
		return num;
	}

	public void Reset()
	{
		if (!string.IsNullOrEmpty(_inputContextPath))
		{
			RunDBusCommand($"call --session --dest org.fcitx.Fcitx5 --object-path {_inputContextPath} --method org.fcitx.Fcitx.InputContext1.Reset");
		}
		_preEditText = string.Empty;
		_preEditCursorPosition = 0;
		_isActive = false;
		this.PreEditEnded?.Invoke(this, EventArgs.Empty);
		_currentContext?.OnPreEditEnded();
	}

	public void Shutdown()
	{
		Dispose();
	}

	private string? RunDBusCommand(string args)
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "gdbus",
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			});
			if (process == null)
			{
				return null;
			}
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit(1000);
			return result;
		}
		catch
		{
			return null;
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			try
			{
				_dBusMonitor?.Kill();
				_dBusMonitor?.Dispose();
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(_inputContextPath))
			{
				RunDBusCommand($"call --session --dest org.fcitx.Fcitx5 --object-path {_inputContextPath} --method org.fcitx.Fcitx.InputContext1.Destroy");
			}
		}
	}

	public static bool IsAvailable()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "gdbus",
				Arguments = "introspect --session --dest org.fcitx.Fcitx5 --object-path /org/freedesktop/portal/inputmethod",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			});
			if (process == null)
			{
				return false;
			}
			process.WaitForExit(1000);
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}
}
