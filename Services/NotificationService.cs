using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Maui.Platform.Linux.Services;

public class NotificationService
{
	private readonly string _appName;

	private readonly string? _defaultIconPath;

	private readonly ConcurrentDictionary<uint, NotificationContext> _activeNotifications = new ConcurrentDictionary<uint, NotificationContext>();

	private static uint _notificationIdCounter = 1u;

	private Process? _dBusMonitor;

	private bool _monitoringActions;

	public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

	public event EventHandler<NotificationClosedEventArgs>? NotificationClosed;

	public NotificationService(string appName = "MAUI Application", string? defaultIconPath = null)
	{
		_appName = appName;
		_defaultIconPath = defaultIconPath;
	}

	public void StartActionMonitoring()
	{
		if (!_monitoringActions)
		{
			_monitoringActions = true;
			Task.Run((Func<Task?>)MonitorNotificationSignals);
		}
	}

	public void StopActionMonitoring()
	{
		_monitoringActions = false;
		try
		{
			_dBusMonitor?.Kill();
			_dBusMonitor?.Dispose();
			_dBusMonitor = null;
		}
		catch
		{
		}
	}

	private async Task MonitorNotificationSignals()
	{
		_ = 2;
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "dbus-monitor",
				Arguments = "--session \"interface='org.freedesktop.Notifications'\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			_dBusMonitor = Process.Start(startInfo);
			if (_dBusMonitor == null)
			{
				return;
			}
			StreamReader reader = _dBusMonitor.StandardOutput;
			StringBuilder buffer = new StringBuilder();
			while (_monitoringActions && !_dBusMonitor.HasExited)
			{
				string text = await reader.ReadLineAsync();
				if (text != null)
				{
					buffer.AppendLine(text);
					if (text.Contains("ActionInvoked"))
					{
						await ProcessActionInvoked(reader);
					}
					else if (text.Contains("NotificationClosed"))
					{
						await ProcessNotificationClosed(reader);
					}
					continue;
				}
				break;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[NotificationService] D-Bus monitor error: " + ex.Message);
		}
	}

	private async Task ProcessActionInvoked(StreamReader reader)
	{
		try
		{
			uint notificationId = 0u;
			string actionKey = null;
			for (int i = 0; i < 10; i++)
			{
				string text = await reader.ReadLineAsync();
				if (text == null)
				{
					break;
				}
				if (text.Contains("uint32"))
				{
					Match match = Regex.Match(text, "uint32\\s+(\\d+)");
					if (match.Success)
					{
						notificationId = uint.Parse(match.Groups[1].Value);
					}
				}
				else if (text.Contains("string"))
				{
					Match match2 = Regex.Match(text, "string\\s+\"([^\"]*)\"");
					if (match2.Success && actionKey == null)
					{
						actionKey = match2.Groups[1].Value;
					}
				}
				if (notificationId != 0 && actionKey != null)
				{
					break;
				}
			}
			if (notificationId != 0 && actionKey != null && _activeNotifications.TryGetValue(notificationId, out NotificationContext value))
			{
				Action value2 = default(Action);
				if (value.ActionCallbacks?.TryGetValue(actionKey, out value2) ?? false)
				{
					value2?.Invoke();
				}
				this.ActionInvoked?.Invoke(this, new NotificationActionEventArgs(notificationId, actionKey, value.Tag));
			}
		}
		catch
		{
		}
	}

	private async Task ProcessNotificationClosed(StreamReader reader)
	{
		try
		{
			uint notificationId = 0u;
			uint reason = 0u;
			for (int i = 0; i < 5; i++)
			{
				string text = await reader.ReadLineAsync();
				if (text == null)
				{
					break;
				}
				if (!text.Contains("uint32"))
				{
					continue;
				}
				Match match = Regex.Match(text, "uint32\\s+(\\d+)");
				if (match.Success)
				{
					if (notificationId == 0)
					{
						notificationId = uint.Parse(match.Groups[1].Value);
					}
					else
					{
						reason = uint.Parse(match.Groups[1].Value);
					}
				}
			}
			if (notificationId != 0)
			{
				_activeNotifications.TryRemove(notificationId, out NotificationContext value);
				this.NotificationClosed?.Invoke(this, new NotificationClosedEventArgs(notificationId, (NotificationCloseReason)reason, value?.Tag));
			}
		}
		catch
		{
		}
	}

	public async Task ShowAsync(string title, string message)
	{
		await ShowAsync(new NotificationOptions
		{
			Title = title,
			Message = message
		});
	}

	public async Task<uint> ShowWithActionsAsync(string title, string message, IEnumerable<NotificationAction> actions, string? tag = null)
	{
		uint notificationId = _notificationIdCounter++;
		NotificationContext value = new NotificationContext
		{
			Tag = tag,
			ActionCallbacks = actions.ToDictionary((NotificationAction a) => a.Key, (NotificationAction a) => a.Callback)
		};
		_activeNotifications[notificationId] = value;
		Dictionary<string, string> actions2 = actions.ToDictionary((NotificationAction a) => a.Key, (NotificationAction a) => a.Label);
		await ShowAsync(new NotificationOptions
		{
			Title = title,
			Message = message,
			Actions = actions2
		});
		return notificationId;
	}

	public async Task CancelAsync(uint notificationId)
	{
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "gdbus",
				Arguments = $"call --session --dest org.freedesktop.Notifications --object-path /org/freedesktop/Notifications --method org.freedesktop.Notifications.CloseNotification {notificationId}",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
			_activeNotifications.TryRemove(notificationId, out NotificationContext _);
		}
		catch
		{
		}
	}

	public async Task ShowAsync(NotificationOptions options)
	{
		try
		{
			string arguments = BuildNotifyArgs(options);
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "notify-send",
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
		}
		catch (Exception ex)
		{
			_ = ex;
			await TryZenityNotification(options);
		}
	}

	private string BuildNotifyArgs(NotificationOptions options)
	{
		List<string> list = new List<string>();
		list.Add("--app-name=\"" + EscapeArg(_appName) + "\"");
		list.Add("--urgency=" + options.Urgency.ToString().ToLower());
		if (options.ExpireTimeMs > 0)
		{
			list.Add($"--expire-time={options.ExpireTimeMs}");
		}
		string text = options.IconPath ?? _defaultIconPath;
		if (!string.IsNullOrEmpty(text))
		{
			list.Add("--icon=\"" + EscapeArg(text) + "\"");
		}
		else if (!string.IsNullOrEmpty(options.IconName))
		{
			list.Add("--icon=" + options.IconName);
		}
		if (!string.IsNullOrEmpty(options.Category))
		{
			list.Add("--category=" + options.Category);
		}
		if (options.IsTransient)
		{
			list.Add("--hint=int:transient:1");
		}
		Dictionary<string, string>? actions = options.Actions;
		if (actions != null && actions.Count > 0)
		{
			foreach (KeyValuePair<string, string> action in options.Actions)
			{
				list.Add($"--action=\"{action.Key}={EscapeArg(action.Value)}\"");
			}
		}
		list.Add("\"" + EscapeArg(options.Title) + "\"");
		list.Add("\"" + EscapeArg(options.Message) + "\"");
		return string.Join(" ", list);
	}

	private async Task TryZenityNotification(NotificationOptions options)
	{
		try
		{
			string value = "";
			if (!string.IsNullOrEmpty(options.IconPath))
			{
				value = "--window-icon=\"" + options.IconPath + "\"";
			}
			string value2 = ((options.Urgency == NotificationUrgency.Critical) ? "--error" : "--info");
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "zenity",
				Arguments = $"{value2} {value} --title=\"{EscapeArg(options.Title)}\" --text=\"{EscapeArg(options.Message)}\" --timeout=5",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using Process process = Process.Start(startInfo);
			if (process != null)
			{
				await process.WaitForExitAsync();
			}
		}
		catch
		{
		}
	}

	public static bool IsAvailable()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "which",
				Arguments = "notify-send",
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
		return arg?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
	}
}
