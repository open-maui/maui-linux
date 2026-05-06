using System;

namespace Microsoft.Maui.Platform;

public class ContextMenuItem
{
    public string Text { get; }

    public Action? Action { get; }

    public bool IsEnabled { get; }

    public bool IsSeparator { get; }

    public static ContextMenuItem Separator => new ContextMenuItem();

    public ContextMenuItem(string text, Action? action, bool isEnabled = true)
    {
        Text = text;
        Action = action;
        IsEnabled = isEnabled;
        IsSeparator = false;
    }

    private ContextMenuItem()
    {
        Text = "";
        Action = null;
        IsEnabled = false;
        IsSeparator = true;
    }
}
