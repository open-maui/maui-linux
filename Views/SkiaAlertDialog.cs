// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A modal alert dialog rendered with Skia.
/// Supports title, message, and up to two buttons (cancel/accept).
/// </summary>
public class SkiaAlertDialog : SkiaView
{
    private readonly string _title;
    private readonly string _message;
    private readonly string? _cancel;
    private readonly string? _accept;
    private readonly TaskCompletionSource<bool> _tcs;

    private SKRect _cancelButtonBounds;
    private SKRect _acceptButtonBounds;
    private bool _cancelHovered;
    private bool _acceptHovered;

    // Dialog styling
    private static readonly SKColor OverlayColor = new SKColor(0, 0, 0, 128);
    private static readonly SKColor DialogBackground = SKColors.White;
    private static readonly SKColor TitleColor = new SKColor(0x21, 0x21, 0x21);
    private static readonly SKColor MessageColor = new SKColor(0x61, 0x61, 0x61);
    private static readonly SKColor ButtonColor = new SKColor(0x21, 0x96, 0xF3);
    private static readonly SKColor ButtonHoverColor = new SKColor(0x19, 0x76, 0xD2);
    private static readonly SKColor ButtonTextColor = SKColors.White;
    private static readonly SKColor CancelButtonColor = new SKColor(0x9E, 0x9E, 0x9E);
    private static readonly SKColor CancelButtonHoverColor = new SKColor(0x75, 0x75, 0x75);
    private static readonly SKColor BorderColor = new SKColor(0xE0, 0xE0, 0xE0);

    private const float DialogWidth = 400;
    private const float DialogPadding = 24;
    private const float ButtonHeight = 44;
    private const float ButtonSpacing = 12;
    private const float CornerRadius = 12;

    /// <summary>
    /// Creates a new alert dialog.
    /// </summary>
    public SkiaAlertDialog(string title, string message, string? accept, string? cancel)
    {
        _title = title;
        _message = message;
        _accept = accept;
        _cancel = cancel;
        _tcs = new TaskCompletionSource<bool>();
        IsFocusable = true;
    }

    /// <summary>
    /// Gets the task that completes when the dialog is dismissed.
    /// Returns true if accept was clicked, false if cancel was clicked.
    /// </summary>
    public Task<bool> Result => _tcs.Task;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Draw semi-transparent overlay covering entire screen
        using var overlayPaint = new SKPaint
        {
            Color = OverlayColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(bounds, overlayPaint);

        // Calculate dialog dimensions
        var messageLines = WrapText(_message, DialogWidth - DialogPadding * 2, 16);
        var dialogHeight = CalculateDialogHeight(messageLines.Count);

        var dialogLeft = bounds.MidX - DialogWidth / 2;
        var dialogTop = bounds.MidY - dialogHeight / 2;
        var dialogBounds = new SKRect(dialogLeft, dialogTop, dialogLeft + DialogWidth, dialogTop + dialogHeight);

        // Draw dialog shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 60),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8),
            Style = SKPaintStyle.Fill
        };
        var shadowRect = new SKRect(dialogBounds.Left + 4, dialogBounds.Top + 4,
                                     dialogBounds.Right + 4, dialogBounds.Bottom + 4);
        canvas.DrawRoundRect(shadowRect, CornerRadius, CornerRadius, shadowPaint);

        // Draw dialog background
        using var bgPaint = new SKPaint
        {
            Color = DialogBackground,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(dialogBounds, CornerRadius, CornerRadius, bgPaint);

        // Draw title
        var yOffset = dialogBounds.Top + DialogPadding;
        if (!string.IsNullOrEmpty(_title))
        {
            using var titleFont = new SKFont(SKTypeface.Default, 20) { Embolden = true };
            using var titlePaint = new SKPaint(titleFont)
            {
                Color = TitleColor,
                IsAntialias = true
            };
            canvas.DrawText(_title, dialogBounds.Left + DialogPadding, yOffset + 20, titlePaint);
            yOffset += 36;
        }

        // Draw message
        if (!string.IsNullOrEmpty(_message))
        {
            using var messageFont = new SKFont(SKTypeface.Default, 16);
            using var messagePaint = new SKPaint(messageFont)
            {
                Color = MessageColor,
                IsAntialias = true
            };

            foreach (var line in messageLines)
            {
                canvas.DrawText(line, dialogBounds.Left + DialogPadding, yOffset + 16, messagePaint);
                yOffset += 22;
            }
            yOffset += 8;
        }

        // Draw buttons
        yOffset = dialogBounds.Bottom - DialogPadding - ButtonHeight;
        var buttonY = yOffset;

        var buttonCount = (_accept != null ? 1 : 0) + (_cancel != null ? 1 : 0);
        var totalButtonWidth = DialogWidth - DialogPadding * 2;

        if (buttonCount == 2)
        {
            var singleButtonWidth = (totalButtonWidth - ButtonSpacing) / 2;

            // Cancel button (left)
            _cancelButtonBounds = new SKRect(
                dialogBounds.Left + DialogPadding,
                buttonY,
                dialogBounds.Left + DialogPadding + singleButtonWidth,
                buttonY + ButtonHeight);
            DrawButton(canvas, _cancelButtonBounds, _cancel!,
                _cancelHovered ? CancelButtonHoverColor : CancelButtonColor);

            // Accept button (right)
            _acceptButtonBounds = new SKRect(
                dialogBounds.Right - DialogPadding - singleButtonWidth,
                buttonY,
                dialogBounds.Right - DialogPadding,
                buttonY + ButtonHeight);
            DrawButton(canvas, _acceptButtonBounds, _accept!,
                _acceptHovered ? ButtonHoverColor : ButtonColor);
        }
        else if (_accept != null)
        {
            _acceptButtonBounds = new SKRect(
                dialogBounds.Left + DialogPadding,
                buttonY,
                dialogBounds.Right - DialogPadding,
                buttonY + ButtonHeight);
            DrawButton(canvas, _acceptButtonBounds, _accept,
                _acceptHovered ? ButtonHoverColor : ButtonColor);
        }
        else if (_cancel != null)
        {
            _cancelButtonBounds = new SKRect(
                dialogBounds.Left + DialogPadding,
                buttonY,
                dialogBounds.Right - DialogPadding,
                buttonY + ButtonHeight);
            DrawButton(canvas, _cancelButtonBounds, _cancel,
                _cancelHovered ? CancelButtonHoverColor : CancelButtonColor);
        }
    }

    private void DrawButton(SKCanvas canvas, SKRect bounds, string text, SKColor bgColor)
    {
        // Button background
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(bounds, 8, 8, bgPaint);

        // Button text
        using var font = new SKFont(SKTypeface.Default, 16) { Embolden = true };
        using var textPaint = new SKPaint(font)
        {
            Color = ButtonTextColor,
            IsAntialias = true
        };

        var textBounds = new SKRect();
        textPaint.MeasureText(text, ref textBounds);

        var x = bounds.MidX - textBounds.MidX;
        var y = bounds.MidY - textBounds.MidY;
        canvas.DrawText(text, x, y, textPaint);
    }

    private float CalculateDialogHeight(int messageLineCount)
    {
        var height = DialogPadding * 2; // Top and bottom padding

        if (!string.IsNullOrEmpty(_title))
            height += 36; // Title height

        if (!string.IsNullOrEmpty(_message))
            height += messageLineCount * 22 + 8; // Message lines + spacing

        height += ButtonHeight; // Buttons

        return Math.Max(height, 180); // Minimum height
    }

    private List<string> WrapText(string text, float maxWidth, float fontSize)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lines;

        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var paint = new SKPaint(font);

        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var width = paint.MeasureText(testLine);

            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        var wasHovered = _cancelHovered || _acceptHovered;

        _cancelHovered = _cancel != null && _cancelButtonBounds.Contains(e.X, e.Y);
        _acceptHovered = _accept != null && _acceptButtonBounds.Contains(e.X, e.Y);

        if (wasHovered != (_cancelHovered || _acceptHovered))
            Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        // Check if clicking on buttons
        if (_cancel != null && _cancelButtonBounds.Contains(e.X, e.Y))
        {
            Dismiss(false);
            return;
        }

        if (_accept != null && _acceptButtonBounds.Contains(e.X, e.Y))
        {
            Dismiss(true);
            return;
        }

        // Clicking outside dialog doesn't dismiss it (it's modal)
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        // Handle Escape to cancel
        if (e.Key == Key.Escape && _cancel != null)
        {
            Dismiss(false);
            e.Handled = true;
            return;
        }

        // Handle Enter to accept
        if (e.Key == Key.Enter && _accept != null)
        {
            Dismiss(true);
            e.Handled = true;
            return;
        }
    }

    private void Dismiss(bool result)
    {
        // Remove from dialog system
        LinuxDialogService.HideDialog(this);
        _tcs.TrySetResult(result);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        // Dialog takes full screen for the overlay
        return availableSize;
    }

    public override SkiaView? HitTest(float x, float y)
    {
        // Modal dialogs capture all input
        return this;
    }
}

/// <summary>
/// Service for showing modal dialogs in OpenMaui Linux.
/// </summary>
public static class LinuxDialogService
{
    private static readonly List<SkiaAlertDialog> _activeDialogs = new();
    private static Action? _invalidateCallback;

    /// <summary>
    /// Registers the invalidation callback (called by LinuxApplication).
    /// </summary>
    public static void SetInvalidateCallback(Action callback)
    {
        _invalidateCallback = callback;
    }

    /// <summary>
    /// Shows an alert dialog and returns when dismissed.
    /// </summary>
    public static Task<bool> ShowAlertAsync(string title, string message, string? accept, string? cancel)
    {
        var dialog = new SkiaAlertDialog(title, message, accept, cancel);
        _activeDialogs.Add(dialog);
        _invalidateCallback?.Invoke();
        return dialog.Result;
    }

    /// <summary>
    /// Hides a dialog.
    /// </summary>
    internal static void HideDialog(SkiaAlertDialog dialog)
    {
        _activeDialogs.Remove(dialog);
        _invalidateCallback?.Invoke();
    }

    /// <summary>
    /// Gets whether there are active dialogs.
    /// </summary>
    public static bool HasActiveDialog => _activeDialogs.Count > 0;

    /// <summary>
    /// Gets the topmost dialog.
    /// </summary>
    public static SkiaAlertDialog? TopDialog => _activeDialogs.Count > 0 ? _activeDialogs[^1] : null;

    /// <summary>
    /// Draws all active dialogs.
    /// </summary>
    public static void DrawDialogs(SKCanvas canvas, SKRect bounds)
    {
        foreach (var dialog in _activeDialogs)
        {
            dialog.Measure(new SKSize(bounds.Width, bounds.Height));
            dialog.Arrange(bounds);
            dialog.Draw(canvas);
        }
    }
}
