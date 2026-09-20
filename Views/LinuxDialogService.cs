using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public static class LinuxDialogService
{
    private static readonly List<SkiaModalDialog> _activeDialogs = new List<SkiaModalDialog>();

    private static Action? _invalidateCallback;

    private static SkiaContextMenu? _activeContextMenu;

    private static Action? _showPopupCallback;

    private static Action? _hidePopupCallback;

    public static bool HasActiveDialog => _activeDialogs.Count > 0;

    /// <summary>
    /// The dialog currently receiving input (the most recently shown one):
    /// a <see cref="SkiaAlertDialog"/> for alerts and prompts, a
    /// <see cref="SkiaActionSheetDialog"/> for action sheets.
    /// </summary>
    public static SkiaModalDialog? TopDialog
    {
        get
        {
            if (_activeDialogs.Count <= 0)
            {
                return null;
            }
            return _activeDialogs[_activeDialogs.Count - 1];
        }
    }

    public static SkiaContextMenu? ActiveContextMenu => _activeContextMenu;

    public static bool HasContextMenu => _activeContextMenu != null;

    public static void SetInvalidateCallback(Action callback)
    {
        _invalidateCallback = callback;
    }

    public static Task<bool> ShowAlertAsync(string title, string message, string? accept, string? cancel)
    {
        var dialog = new SkiaAlertDialog(title, message, accept, cancel);
        Show(dialog);
        return dialog.Result;
    }

    /// <summary>
    /// Shows a modal prompt (alert with a text field). Completes with the
    /// entered text when accepted, or null when cancelled.
    /// <paramref name="maxLength"/> caps the entered text (-1 = unlimited);
    /// <paramref name="placeholder"/> is the hint shown while the field is empty.
    /// </summary>
    public static async Task<string?> ShowPromptAsync(string title, string message, string? accept, string? cancel, string initialValue = "", int maxLength = -1, string? placeholder = null)
    {
        var dialog = new SkiaAlertDialog(title, message, accept, cancel, initialValue)
        {
            MaxLength = maxLength,
            Placeholder = placeholder,
        };
        Show(dialog);
        bool accepted = await dialog.Result;
        return accepted ? dialog.Input : null;
    }

    /// <summary>
    /// Shows a modal action sheet with one button per option. Completes with
    /// the chosen option's text, the cancel text when dismissed with Escape,
    /// or null when dismissed without a cancel option.
    /// </summary>
    public static Task<string?> ShowActionSheetAsync(string? title, string? cancel, string? destruction, IEnumerable<string>? buttons)
    {
        var dialog = new SkiaActionSheetDialog(title, cancel, destruction, buttons);
        Show(dialog);
        return dialog.Result;
    }

    /// <summary>
    /// Registers an already-constructed dialog as the top-most modal and
    /// requests a redraw. Exposed for custom dialog types deriving from
    /// <see cref="SkiaModalDialog"/>.
    /// </summary>
    public static void Show(SkiaModalDialog dialog)
    {
        if (dialog == null) throw new ArgumentNullException(nameof(dialog));
        _activeDialogs.Add(dialog);
        _invalidateCallback?.Invoke();
    }

    internal static void HideDialog(SkiaModalDialog dialog)
    {
        _activeDialogs.Remove(dialog);
        _invalidateCallback?.Invoke();
    }

    public static void DrawDialogs(SKCanvas canvas, SKRect bounds)
    {
        DrawDialogsOnly(canvas, bounds);
        DrawContextMenuOnly(canvas, bounds);
    }

    public static void DrawDialogsOnly(SKCanvas canvas, SKRect bounds)
    {
        DiagnosticLog.Debug("LinuxDialogService", $"DrawDialogsOnly: {_activeDialogs.Count} dialogs, IsDarkMode={SkiaTheme.IsDarkMode}");
        foreach (var dialog in _activeDialogs)
        {
            DiagnosticLog.Debug("LinuxDialogService", $"Drawing dialog: IsVisible={dialog.IsVisible}, Opacity={dialog.Opacity}");
            dialog.Measure(new Size(bounds.Width, bounds.Height));
            dialog.Arrange(new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            dialog.Draw(canvas);
        }
    }

    public static void DrawContextMenuOnly(SKCanvas canvas, SKRect bounds)
    {
        if (_activeContextMenu != null)
        {
            _activeContextMenu.Draw(canvas);
        }
    }

    public static void SetPopupCallbacks(Action showPopup, Action hidePopup)
    {
        _showPopupCallback = showPopup;
        _hidePopupCallback = hidePopup;
    }

    public static void ShowContextMenu(SkiaContextMenu menu)
    {
        DiagnosticLog.Debug("LinuxDialogService", "ShowContextMenu called");
        _activeContextMenu = menu;
        _showPopupCallback?.Invoke();
        _invalidateCallback?.Invoke();
    }

    public static void HideContextMenu()
    {
        _activeContextMenu = null;
        _hidePopupCallback?.Invoke();
        _invalidateCallback?.Invoke();
    }
}
