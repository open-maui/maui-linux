using System;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Represents a menu item for use with GtkContextMenuService.
/// </summary>
public class GtkMenuItem
{
    public string Text { get; }
    public Action? Action { get; }
    public bool IsEnabled { get; }
    public bool IsSeparator { get; }

    public static GtkMenuItem Separator => new GtkMenuItem();

    public GtkMenuItem(string text, Action? action, bool isEnabled = true)
    {
        Text = text;
        Action = action;
        IsEnabled = isEnabled;
        IsSeparator = false;
    }

    private GtkMenuItem()
    {
        Text = "";
        Action = null;
        IsEnabled = false;
        IsSeparator = true;
    }
}
