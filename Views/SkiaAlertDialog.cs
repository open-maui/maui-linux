// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A modal alert dialog rendered with Skia.
/// Supports title, message, and up to two buttons (cancel/accept).
/// </summary>
public class SkiaAlertDialog : SkiaModalDialog
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

    // Optional single-line input (prompt dialogs). Null = no field.
    private string? _input;
    private SKRect _inputBounds;
    private const float InputHeight = 40;

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
    }

    /// <summary>
    /// Creates a prompt dialog: an alert with a single-line text field whose
    /// value is read from <see cref="Input"/> after <see cref="Result"/> completes.
    /// </summary>
    public SkiaAlertDialog(string title, string message, string? accept, string? cancel, string initialInput)
        : this(title, message, accept, cancel)
    {
        _input = initialInput ?? string.Empty;
    }

    /// <summary>The text field's current value (prompt dialogs), else null.</summary>
    public string? Input => _input;

    /// <summary>
    /// Maximum number of characters the prompt field accepts; -1 (default)
    /// for unlimited. Typed and pasted text beyond the limit is dropped.
    /// Mirrors <c>Page.DisplayPromptAsync(maxLength)</c>.
    /// </summary>
    public int MaxLength { get; set; } = -1;

    /// <summary>
    /// Hint text drawn in the prompt field while it is empty.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Gets the task that completes when the dialog is dismissed.
    /// Returns true if accept was clicked, false if cancel was clicked.
    /// </summary>
    public Task<bool> Result => _tcs.Task;

    /// <summary>Bounds of the accept button as of the last draw (tests).</summary>
    internal SKRect AcceptButtonBounds => _acceptButtonBounds;

    /// <summary>Bounds of the cancel button as of the last draw (tests).</summary>
    internal SKRect CancelButtonBounds => _cancelButtonBounds;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var app = Application.Current;
        DiagnosticLog.Debug("SkiaAlertDialog", $"OnDraw: app={app != null}, UserAppTheme={app?.UserAppTheme}, RequestedTheme={app?.RequestedTheme}, IsDarkMode={SkiaTheme.IsDarkMode}, DialogBg={DialogBackground}");

        DrawOverlay(canvas, bounds);

        // Calculate dialog dimensions
        var messageLines = WrapText(_message, DialogWidth - DialogPadding * 2, 16);
        var dialogHeight = CalculateDialogHeight(messageLines.Count);

        var dialogLeft = bounds.MidX - DialogWidth / 2;
        var dialogTop = bounds.MidY - dialogHeight / 2;
        var dialogBounds = new SKRect(dialogLeft, dialogTop, dialogLeft + DialogWidth, dialogTop + dialogHeight);

        DrawCard(canvas, dialogBounds);

        // Draw title
        var yOffset = dialogBounds.Top + DialogPadding;
        if (!string.IsNullOrEmpty(_title))
        {
            using var titleFont = SkiaFontFactory.Create(20);
            titleFont.Embolden = true;
            using var titlePaint = new SKPaint
            {
                Color = TitleColor,
                IsAntialias = true
            };
            canvas.DrawText(_title, dialogBounds.Left + DialogPadding, yOffset + 20, titleFont, titlePaint);
            yOffset += 36;
        }

        // Draw message
        if (!string.IsNullOrEmpty(_message))
        {
            using var messageFont = SkiaFontFactory.Create(16);
            using var messagePaint = new SKPaint
            {
                Color = MessageColor,
                IsAntialias = true
            };

            foreach (var line in messageLines)
            {
                canvas.DrawText(line, dialogBounds.Left + DialogPadding, yOffset + 16, messageFont, messagePaint);
                yOffset += 22;
            }
            yOffset += 8;
        }

        // Draw input field (prompt)
        if (_input != null)
        {
            _inputBounds = new SKRect(dialogBounds.Left + DialogPadding, yOffset,
                dialogBounds.Right - DialogPadding, yOffset + InputHeight);
            using var fieldBg = new SKPaint { Color = SkiaTheme.IsDarkMode ? SkiaTheme.Gray700SK : SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var fieldBorder = new SKPaint { Color = SkiaTheme.PrimarySK, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            canvas.DrawRoundRect(_inputBounds, 6, 6, fieldBg);
            canvas.DrawRoundRect(_inputBounds, 6, 6, fieldBorder);

            using var inputFont = SkiaFontFactory.Create(16);
            using var inputPaint = new SKPaint { Color = TitleColor, IsAntialias = true };
            float textX = _inputBounds.Left + 10;
            float baseline = TextRenderingHelper.BaselineForVerticalCenter(inputFont, _inputBounds.MidY);
            // Keep the caret (end of text) visible when the value is wider than the field.
            float textWidth = inputFont.MeasureText(_input);
            float maxWidth = _inputBounds.Width - 20;
            canvas.Save();
            canvas.ClipRect(new SKRect(_inputBounds.Left + 4, _inputBounds.Top, _inputBounds.Right - 4, _inputBounds.Bottom));
            if (textWidth > maxWidth) textX -= textWidth - maxWidth;
            if (_input.Length == 0 && !string.IsNullOrEmpty(Placeholder))
            {
                using var placeholderPaint = new SKPaint { Color = MessageColor, IsAntialias = true };
                canvas.DrawText(Placeholder, textX, baseline, inputFont, placeholderPaint);
            }
            else
            {
                canvas.DrawText(_input, textX, baseline, inputFont, inputPaint);
            }
            using var caretPaint = new SKPaint { Color = TitleColor, StrokeWidth = 1.5f, IsAntialias = true };
            float caretX = textX + textWidth + 1;
            canvas.DrawLine(caretX, _inputBounds.MidY - 10, caretX, _inputBounds.MidY + 10, caretPaint);
            canvas.Restore();
            yOffset += InputHeight + 12;
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

    private float CalculateDialogHeight(int messageLineCount)
    {
        var height = DialogPadding * 2; // Top and bottom padding

        if (!string.IsNullOrEmpty(_title))
            height += 36; // Title height

        if (!string.IsNullOrEmpty(_message))
            height += messageLineCount * 22 + 8; // Message lines + spacing

        if (_input != null)
            height += InputHeight + 12; // Prompt field

        height += ButtonHeight; // Buttons

        return Math.Max(height, 180); // Minimum height
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

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (_input == null || string.IsNullOrEmpty(e.Text)) return;
        AppendInput(e.Text);
        e.Handled = true;
        Invalidate();
    }

    /// <summary>
    /// Appends text to the prompt field, truncating at <see cref="MaxLength"/>
    /// when a limit is set.
    /// </summary>
    private void AppendInput(string text)
    {
        if (_input == null) return;
        if (MaxLength >= 0)
        {
            int room = MaxLength - _input.Length;
            if (room <= 0) return;
            if (text.Length > room)
                text = text.Substring(0, room);
        }
        _input += text;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        // Prompt field editing: backspace (Ctrl clears), paste.
        if (_input != null)
        {
            bool ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
            if (e.Key == Key.Backspace)
            {
                if (_input.Length > 0)
                    _input = ctrl ? string.Empty : _input[..^1];
                e.Handled = true;
                Invalidate();
                return;
            }
            if (ctrl && e.Key == Key.V)
            {
                var text = SystemClipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                    AppendInput(text.Replace("\n", " ").Replace("\r", string.Empty));
                e.Handled = true;
                Invalidate();
                return;
            }
        }

        // Handle Escape to cancel
        if (e.Key == Key.Escape && _cancel != null)
        {
            Dismiss(false);
            e.Handled = true;
            return;
        }

        // Handle Enter to accept; on a single-button alert (cancel only, the
        // DisplayAlert(title, message, cancel) form) Enter dismisses it.
        if (e.Key == Key.Enter)
        {
            if (_accept != null)
            {
                Dismiss(true);
                e.Handled = true;
            }
            else if (_cancel != null)
            {
                Dismiss(false);
                e.Handled = true;
            }
            return;
        }
    }

    private void Dismiss(bool result)
    {
        // Remove from dialog system
        Hide();
        _tcs.TrySetResult(result);
    }
}
