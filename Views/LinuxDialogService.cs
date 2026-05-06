using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public static class LinuxDialogService
{
    private static readonly List<SkiaAlertDialog> _activeDialogs = new List<SkiaAlertDialog>();

    private static Action? _invalidateCallback;

    private static SkiaContextMenu? _activeContextMenu;

    private static Action? _showPopupCallback;

    private static Action? _hidePopupCallback;

    public static bool HasActiveDialog => _activeDialogs.Count > 0;

    public static SkiaAlertDialog? TopDialog
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
        _activeDialogs.Add(dialog);
        _invalidateCallback?.Invoke();
        return dialog.Result;
    }

    internal static void HideDialog(SkiaAlertDialog dialog)
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
