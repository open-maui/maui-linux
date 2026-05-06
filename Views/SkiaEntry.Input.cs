// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public partial class SkiaEntry
{
    #region IInputContext Implementation

    /// <summary>
    /// Gets or sets the text for IME context.
    /// </summary>
    string IInputContext.Text
    {
        get => Text;
        set => Text = value;
    }

    /// <summary>
    /// Gets or sets the cursor position for IME context.
    /// </summary>
    int IInputContext.CursorPosition
    {
        get => _cursorPosition;
        set => CursorPosition = value;
    }

    /// <summary>
    /// Gets the selection start for IME context.
    /// </summary>
    int IInputContext.SelectionStart => _selectionStart;

    /// <summary>
    /// Gets the selection length for IME context.
    /// </summary>
    int IInputContext.SelectionLength => _selectionLength;

    /// <summary>
    /// Called when IME commits text.
    /// </summary>
    public void OnTextCommitted(string text)
    {
        if (IsReadOnly) return;

        // Delete selection if any
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Clear pre-edit text
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;

        // Check max length
        if (MaxLength > 0 && Text.Length + text.Length > MaxLength)
        {
            text = text.Substring(0, MaxLength - Text.Length);
        }

        // Insert committed text at cursor
        var newText = Text.Insert(_cursorPosition, text);
        var newPos = _cursorPosition + text.Length;
        Text = newText;
        _cursorPosition = newPos;

        ResetCursorBlink();
        Invalidate();
    }

    /// <summary>
    /// Called when IME pre-edit (composition) text changes.
    /// </summary>
    public void OnPreEditChanged(string preEditText, int cursorPosition)
    {
        _preEditText = preEditText ?? string.Empty;
        _preEditCursorPosition = cursorPosition;
        Invalidate();
    }

    /// <summary>
    /// Called when IME pre-edit ends (cancelled or committed).
    /// </summary>
    public void OnPreEditEnded()
    {
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
        Invalidate();
    }

    #endregion

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Ignore control characters (Ctrl+key combinations send ASCII control codes)
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < 32)
            return;

        // Delete selection if any
        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0 && Text.Length >= MaxLength)
            return;

        // Insert text at cursor
        var insertText = e.Text;
        if (MaxLength > 0)
        {
            var remaining = MaxLength - Text.Length;
            insertText = insertText.Substring(0, Math.Min(insertText.Length, remaining));
        }

        var newText = Text.Insert(_cursorPosition, insertText);
        var oldPos = _cursorPosition;
        Text = newText;
        _cursorPosition = oldPos + insertText.Length;

        ResetCursorBlink();
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Backspace:
                if (!IsReadOnly)
                {
                    if (_selectionLength > 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition > 0)
                    {
                        var newText = Text.Remove(_cursorPosition - 1, 1);
                        var newPos = _cursorPosition - 1;
                        Text = newText;
                        _cursorPosition = newPos;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    if (_selectionLength > 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition < Text.Length)
                    {
                        Text = Text.Remove(_cursorPosition, 1);
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Left:
                if (_cursorPosition > 0)
                {
                    if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        ExtendSelection(-1);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition--;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_cursorPosition < Text.Length)
                {
                    if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        ExtendSelection(1);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition++;
                    }
                    ResetCursorBlink();
                    Invalidate();
                }
                e.Handled = true;
                break;

            case Key.Home:
                if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExtendSelectionTo(0);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = 0;
                }
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                break;

            case Key.End:
                if (e.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    ExtendSelectionTo(Text.Length);
                }
                else
                {
                    ClearSelection();
                    _cursorPosition = Text.Length;
                }
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                break;

            case Key.A:
                if (e.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    SelectAll();
                    e.Handled = true;
                }
                break;

            case Key.C:
                if (e.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    CopyToClipboard();
                    e.Handled = true;
                }
                break;

            case Key.V:
                if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
                {
                    PasteFromClipboard();
                    e.Handled = true;
                }
                break;

            case Key.X:
                if (e.Modifiers.HasFlag(KeyModifiers.Control) && !IsReadOnly)
                {
                    CutToClipboard();
                    e.Handled = true;
                }
                break;

            case Key.Enter:
                Completed?.Invoke(this, EventArgs.Empty);
                // Execute ReturnCommand if set and can execute
                if (ReturnCommand?.CanExecute(ReturnCommandParameter) == true)
                {
                    ReturnCommand.Execute(ReturnCommandParameter);
                }
                e.Handled = true;
                break;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        // Handle right-click context menu
        if (e.Button == PointerButton.Right)
        {
            ShowContextMenu(e.X, e.Y);
            return;
        }

        // Check if clicked on clear button
        if (ShouldShowClearButton())
        {
            var clearButtonSize = 20f;
            var clearButtonMargin = 8f;
            var clearCenterX = (float)(Bounds.Left + Bounds.Width) - clearButtonMargin - clearButtonSize / 2;
            var clearCenterY = (float)(Bounds.Top + Bounds.Height / 2);

            var dx = e.X - clearCenterX;
            var dy = e.Y - clearCenterY;
            if (dx * dx + dy * dy < (clearButtonSize / 2) * (clearButtonSize / 2))
            {
                // Clear button clicked
                Text = "";
                _cursorPosition = 0;
                _selectionLength = 0;
                Invalidate();
                return;
            }
        }

        // Calculate cursor position from click using screen coordinates
        var screenBounds = ScreenBounds;
        var clickX = e.X - (float)screenBounds.Left - (float)Padding.Left + _scrollOffset;
        _cursorPosition = GetCharacterIndexAtX(clickX);

        // Check for double-click (select word or select all)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Abs(e.X - _lastClickX);

        if (timeSinceLastClick < DoubleClickThresholdMs && distanceFromLastClick < 10)
        {
            // Double-click: select all or select word based on property
            if (SelectAllOnDoubleClick)
            {
                SelectAll();
            }
            else
            {
                SelectWordAtCursor();
            }
            _lastClickTime = DateTime.MinValue; // Reset to prevent triple-click issues
            _isSelecting = false;
        }
        else
        {
            // Single click: start selection
            _selectionStart = _cursorPosition;
            _selectionLength = 0;
            _isSelecting = true;
            _lastClickTime = now;
            _lastClickX = e.X;
        }

        ResetCursorBlink();
        Invalidate();
    }

    private void SelectWordAtCursor()
    {
        if (string.IsNullOrEmpty(Text)) return;

        // Find word boundaries
        int start = _cursorPosition;
        int end = _cursorPosition;

        // Move start backwards to beginning of word
        while (start > 0 && IsWordChar(Text[start - 1]))
            start--;

        // Move end forwards to end of word
        while (end < Text.Length && IsWordChar(Text[end]))
            end++;

        _selectionStart = start;
        _cursorPosition = end;
        _selectionLength = end - start;
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isSelecting) return;

        // Extend selection to current mouse position
        var screenBounds = ScreenBounds;
        var clickX = e.X - (float)screenBounds.Left - (float)Padding.Left + _scrollOffset;
        var newPosition = GetCharacterIndexAtX(clickX);

        if (newPosition != _cursorPosition)
        {
            _cursorPosition = newPosition;
            _selectionLength = _cursorPosition - _selectionStart;
            ResetCursorBlink();
            Invalidate();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isSelecting = false;
    }

    private int GetCharacterIndexAtX(float x)
    {
        if (string.IsNullOrEmpty(Text)) return 0;

        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);
        using var paint = new SKPaint(font);

        var displayText = GetDisplayText();

        for (int i = 0; i <= displayText.Length; i++)
        {
            var substring = displayText.Substring(0, i);
            var width = paint.MeasureText(substring);

            if (width >= x)
            {
                // Check if closer to current or previous character
                if (i > 0)
                {
                    var prevWidth = paint.MeasureText(displayText.Substring(0, i - 1));
                    if (x - prevWidth < width - x)
                        return i - 1;
                }
                return i;
            }
        }

        return displayText.Length;
    }

    private void DeleteSelection()
    {
        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);

        Text = Text.Remove(start, length);
        _cursorPosition = start;
        _selectionLength = 0;
    }

    private void ClearSelection()
    {
        _selectionLength = 0;
    }

    private void ExtendSelection(int delta)
    {
        if (_selectionLength == 0)
        {
            _selectionStart = _cursorPosition;
        }

        _cursorPosition += delta;
        _selectionLength = _cursorPosition - _selectionStart;
    }

    private void ExtendSelectionTo(int position)
    {
        if (_selectionLength == 0)
        {
            _selectionStart = _cursorPosition;
        }

        _cursorPosition = position;
        _selectionLength = _cursorPosition - _selectionStart;
    }

    /// <summary>
    /// Selects all text.
    /// </summary>
    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = Text.Length;
        _selectionLength = Text.Length;
        Invalidate();
    }

    private void CopyToClipboard()
    {
        // Password fields should not allow copying
        if (IsPassword) return;
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        // Use system clipboard via xclip/xsel
        SystemClipboard.SetText(selectedText);
    }

    private void CutToClipboard()
    {
        // Password fields should not allow cutting
        if (IsPassword) return;

        CopyToClipboard();
        DeleteSelection();
        Invalidate();
    }

    private void PasteFromClipboard()
    {
        // Get from system clipboard
        var text = SystemClipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;

        if (_selectionLength != 0)
        {
            DeleteSelection();
        }

        // Check max length
        if (MaxLength > 0)
        {
            var remaining = MaxLength - Text.Length;
            text = text.Substring(0, Math.Min(text.Length, remaining));
        }

        var newText = Text.Insert(_cursorPosition, text);
        var newPos = _cursorPosition + text.Length;
        Text = newText;
        _cursorPosition = newPos;
        Invalidate();
    }

    private void ShowContextMenu(float x, float y)
    {
        DiagnosticLog.Debug("SkiaEntry", $"ShowContextMenu at ({x}, {y}), IsGtkMode={LinuxApplication.IsGtkMode}");
        bool hasSelection = _selectionLength != 0;
        bool hasText = !string.IsNullOrEmpty(Text);
        bool hasClipboard = !string.IsNullOrEmpty(SystemClipboard.GetText());

        if (LinuxApplication.IsGtkMode)
        {
            // Use GTK context menu when running in GTK mode (e.g., with WebView)
            GtkContextMenuService.ShowContextMenu(new List<GtkMenuItem>
            {
                new GtkMenuItem("Cut", () =>
                {
                    CutToClipboard();
                    Invalidate();
                }, hasSelection),
                new GtkMenuItem("Copy", () =>
                {
                    CopyToClipboard();
                }, hasSelection),
                new GtkMenuItem("Paste", () =>
                {
                    PasteFromClipboard();
                    Invalidate();
                }, hasClipboard),
                GtkMenuItem.Separator,
                new GtkMenuItem("Select All", () =>
                {
                    SelectAll();
                    Invalidate();
                }, hasText)
            });
        }
        else
        {
            // Use Skia-rendered context menu for pure Skia mode (Wayland/X11)
            bool isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            var items = new List<ContextMenuItem>
            {
                new ContextMenuItem("Cut", () =>
                {
                    CutToClipboard();
                    Invalidate();
                }, hasSelection),
                new ContextMenuItem("Copy", () =>
                {
                    CopyToClipboard();
                }, hasSelection),
                new ContextMenuItem("Paste", () =>
                {
                    PasteFromClipboard();
                    Invalidate();
                }, hasClipboard),
                ContextMenuItem.Separator,
                new ContextMenuItem("Select All", () =>
                {
                    SelectAll();
                    Invalidate();
                }, hasText)
            };
            var menu = new SkiaContextMenu(x, y, items, isDarkTheme);
            LinuxDialogService.ShowContextMenu(menu);
        }
    }

    public override void OnFocusGained()
    {
        base.OnFocusGained();
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Focused);

        // Connect to IME service
        _inputMethodService?.SetFocus(this);

        // Update cursor location for IME candidate window positioning
        UpdateImeCursorLocation();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Normal);

        // Disconnect from IME service and reset any composition
        _inputMethodService?.SetFocus(null);
        _preEditText = string.Empty;
        _preEditCursorPosition = 0;
    }

    /// <summary>
    /// Updates the IME cursor location for candidate window positioning.
    /// </summary>
    private void UpdateImeCursorLocation()
    {
        if (_inputMethodService == null) return;

        var screenBounds = ScreenBounds;
        var fontStyle = GetFontStyle();
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(GetEffectiveFontFamily(), fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, (float)FontSize);
        using var paint = new SKPaint(font);

        var displayText = GetDisplayText();
        var textToCursor = displayText.Substring(0, Math.Min(_cursorPosition, displayText.Length));
        var cursorX = paint.MeasureText(textToCursor);

        int x = (int)(screenBounds.Left + Padding.Left - _scrollOffset + cursorX);
        int y = (int)(screenBounds.Top + Padding.Top);
        int height = (int)FontSize;

        _inputMethodService.SetCursorLocation(x, y, 2, height);
    }
}
