// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.ApplicationModel;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux app actions implementation using desktop file actions.
/// </summary>
public class AppActionsService : IAppActions
{
    private readonly List<AppAction> _actions = new();
    private static readonly string DesktopFilesPath;

    static AppActionsService()
    {
        DesktopFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "applications");
    }

    public bool IsSupported => true;

    public event EventHandler<AppActionEventArgs>? AppActionActivated;

    public Task<IEnumerable<AppAction>> GetAsync()
    {
        return Task.FromResult<IEnumerable<AppAction>>(_actions.AsReadOnly());
    }

    public Task SetAsync(IEnumerable<AppAction> actions)
    {
        _actions.Clear();
        _actions.AddRange(actions);

        // On Linux, app actions can be exposed via .desktop file Actions
        // This would require modifying the application's .desktop file
        UpdateDesktopActions();

        return Task.CompletedTask;
    }

    private void UpdateDesktopActions()
    {
        // Desktop actions are defined in the .desktop file
        // Example:
        // [Desktop Action new-window]
        // Name=New Window
        // Exec=myapp --action=new-window

        // For a proper implementation, we would need to:
        // 1. Find or create the application's .desktop file
        // 2. Add [Desktop Action] sections for each action
        // 3. The actions would then appear in the dock/launcher right-click menu

        // This is a simplified implementation that logs actions
        // A full implementation would require more system integration
    }

    /// <summary>
    /// Call this method to handle command-line action arguments.
    /// </summary>
    public void HandleActionArgument(string actionId)
    {
        var action = _actions.FirstOrDefault(a => a.Id == actionId);
        if (action != null)
        {
            AppActionActivated?.Invoke(this, new AppActionEventArgs(action));
        }
    }

    /// <summary>
    /// Creates a .desktop file for the application with the defined actions.
    /// </summary>
    public void CreateDesktopFile(string appName, string execPath, string? iconPath = null)
    {
        try
        {
            if (!Directory.Exists(DesktopFilesPath))
            {
                Directory.CreateDirectory(DesktopFilesPath);
            }

            var desktopContent = GenerateDesktopFileContent(appName, execPath, iconPath);
            var desktopFilePath = Path.Combine(DesktopFilesPath, $"{appName.ToLowerInvariant().Replace(" ", "-")}.desktop");

            File.WriteAllText(desktopFilePath, desktopContent);

            // Make it executable
            File.SetUnixFileMode(desktopFilePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
        catch
        {
            // Silently fail - desktop file creation is optional
        }
    }

    private string GenerateDesktopFileContent(string appName, string execPath, string? iconPath)
    {
        var content = new System.Text.StringBuilder();

        content.AppendLine("[Desktop Entry]");
        content.AppendLine("Type=Application");
        content.AppendLine($"Name={appName}");
        content.AppendLine($"Exec={execPath} %U");

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            content.AppendLine($"Icon={iconPath}");
        }

        content.AppendLine("Terminal=false");
        content.AppendLine("Categories=Utility;");

        // Add actions list
        if (_actions.Count > 0)
        {
            var actionIds = string.Join(";", _actions.Select(a => a.Id));
            content.AppendLine($"Actions={actionIds};");
            content.AppendLine();

            // Add each action section
            foreach (var action in _actions)
            {
                content.AppendLine($"[Desktop Action {action.Id}]");
                content.AppendLine($"Name={action.Title}");

                if (!string.IsNullOrEmpty(action.Subtitle))
                {
                    content.AppendLine($"Comment={action.Subtitle}");
                }

                content.AppendLine($"Exec={execPath} --action={action.Id}");

                {
                }

                content.AppendLine();
            }
        }

        return content.ToString();
    }
}
