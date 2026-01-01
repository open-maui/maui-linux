using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

public class AppActionsService : IAppActions
{
	private readonly List<AppAction> _actions = new List<AppAction>();

	private static readonly string DesktopFilesPath;

	public bool IsSupported => true;

	public event EventHandler<AppActionEventArgs>? AppActionActivated;

	static AppActionsService()
	{
		DesktopFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
	}

	public Task<IEnumerable<AppAction>> GetAsync()
	{
		return Task.FromResult((IEnumerable<AppAction>)_actions.AsReadOnly());
	}

	public Task SetAsync(IEnumerable<AppAction> actions)
	{
		_actions.Clear();
		_actions.AddRange(actions);
		UpdateDesktopActions();
		return Task.CompletedTask;
	}

	private void UpdateDesktopActions()
	{
	}

	public void HandleActionArgument(string actionId)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		AppAction val = ((IEnumerable<AppAction>)_actions).FirstOrDefault((Func<AppAction, bool>)((AppAction a) => a.Id == actionId));
		if (val != null)
		{
			this.AppActionActivated?.Invoke(this, new AppActionEventArgs(val));
		}
	}

	public void CreateDesktopFile(string appName, string execPath, string? iconPath = null)
	{
		try
		{
			if (!Directory.Exists(DesktopFilesPath))
			{
				Directory.CreateDirectory(DesktopFilesPath);
			}
			string contents = GenerateDesktopFileContent(appName, execPath, iconPath);
			string path = Path.Combine(DesktopFilesPath, appName.ToLowerInvariant().Replace(" ", "-") + ".desktop");
			File.WriteAllText(path, contents);
			File.SetUnixFileMode(path, UnixFileMode.OtherRead | UnixFileMode.GroupRead | UnixFileMode.UserExecute | UnixFileMode.UserWrite | UnixFileMode.UserRead);
		}
		catch
		{
		}
	}

	private string GenerateDesktopFileContent(string appName, string execPath, string? iconPath)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("[Desktop Entry]");
		stringBuilder.AppendLine("Type=Application");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
		handler.AppendLiteral("Name=");
		handler.AppendFormatted(appName);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
		handler.AppendLiteral("Exec=");
		handler.AppendFormatted(execPath);
		handler.AppendLiteral(" %U");
		stringBuilder4.AppendLine(ref handler);
		if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
			handler.AppendLiteral("Icon=");
			handler.AppendFormatted(iconPath);
			stringBuilder5.AppendLine(ref handler);
		}
		stringBuilder.AppendLine("Terminal=false");
		stringBuilder.AppendLine("Categories=Utility;");
		if (_actions.Count > 0)
		{
			string value = string.Join(";", _actions.Select((AppAction a) => a.Id));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("Actions=");
			handler.AppendFormatted(value);
			handler.AppendLiteral(";");
			stringBuilder6.AppendLine(ref handler);
			stringBuilder.AppendLine();
			foreach (AppAction action in _actions)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder7 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
				handler.AppendLiteral("[Desktop Action ");
				handler.AppendFormatted(action.Id);
				handler.AppendLiteral("]");
				stringBuilder7.AppendLine(ref handler);
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder8 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
				handler.AppendLiteral("Name=");
				handler.AppendFormatted(action.Title);
				stringBuilder8.AppendLine(ref handler);
				if (!string.IsNullOrEmpty(action.Subtitle))
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder9 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
					handler.AppendLiteral("Comment=");
					handler.AppendFormatted(action.Subtitle);
					stringBuilder9.AppendLine(ref handler);
				}
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder10 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder2);
				handler.AppendLiteral("Exec=");
				handler.AppendFormatted(execPath);
				handler.AppendLiteral(" --action=");
				handler.AppendFormatted(action.Id);
				stringBuilder10.AppendLine(ref handler);
				stringBuilder.AppendLine();
			}
		}
		return stringBuilder.ToString();
	}
}
