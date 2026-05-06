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

public partial class SkiaEditor
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

        EnsureCursorVisible();
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

    private void UpdateLines()
    {
        _lines.Clear();
        var text = Text ?? "";
        if (string.IsNullOrEmpty(text))
        {
            _lines.Add("");
            return;
        }

        using var font = new SKFont(SKTypeface.Default, (float)FontSize);

        // Split by actual newlines first
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                _lines.Add("");
                continue;
            }

            // Word wrap this paragraph if we have a known width
            if (_wrapWidth > 0)
            {
                WrapParagraph(paragraph, font, _wrapWidth);
            }
            else
            {
                _lines.Add(paragraph);
            }
        }

        if (_lines.Count == 0)
        {
            _lines.Add("");
        }
    }

    private void WrapParagraph(string paragraph, SKFont font, float maxWidth)
    {
        var words = paragraph.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var lineWidth = MeasureText(testLine, font);

            if (lineWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                // Line too long, save current and start new
                _lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        // Add remaining text
        if (!string.IsNullOrEmpty(currentLine))
        {
            _lines.Add(currentLine);
        }
    }

    private (int line, int column) GetLineColumn(int position)
    {
        var pos = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            var lineLength = _lines[i].Length;
            if (pos + lineLength >= position || i == _lines.Count - 1)
            {
                return (i, position - pos);
            }
            pos += lineLength + 1;
        }
        return (_lines.Count - 1, _lines[^1].Length);
    }

    private int GetPosition(int line, int column)
    {
        var pos = 0;
        for (int i = 0; i < line && i < _lines.Count; i++)
        {
            pos += _lines[i].Length + 1;
        }
        if (line < _lines.Count)
        {
            pos += Math.Min(column, _lines[line].Length);
        }
        return Math.Min(pos, Text.Length);
    }

    private void EnsureCursorVisible()
    {
        var (line, col) = GetLineColumn(_cursorPosition);
        var fontSize = (float)FontSize;
        var lineHeight = (float)LineHeight;
        var lineSpacing = fontSize * lineHeight;
        var cursorY = line * lineSpacing;
        var viewHeight = Bounds.Height - (float)(Padding.Top + Padding.Bottom);

        if (cursorY < _scrollOffsetY)
        {
            _scrollOffsetY = cursorY;
        }
        else if (cursorY + lineSpacing > _scrollOffsetY + (float)viewHeight)
        {
            _scrollOffsetY = cursorY + lineSpacing - (float)viewHeight;
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        DiagnosticLog.Debug("SkiaEditor", $"OnPointerPressed: Button={e.Button}, IsEnabled={IsEnabled}");
        if (!IsEnabled) return;

        // Handle right-click context menu
        if (e.Button == PointerButton.Right)
        {
            DiagnosticLog.Debug("SkiaEditor", "Right-click detected, showing context menu");
            ShowContextMenu(e.X, e.Y);
            return;
        }

        IsFocused = true;

        // Use screen coordinates for proper hit detection
        var screenBounds = ScreenBounds;
        var paddingLeft = (float)Padding.Left;
        var paddingTop = (float)Padding.Top;
        var contentX = e.X - screenBounds.Left - paddingLeft;
        var contentY = e.Y - screenBounds.Top - paddingTop + _scrollOffsetY;

        var fontSize = (float)FontSize;
        var lineSpacing = fontSize * (float)LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

        for (int i = 0; i <= line.Length; i++)
        {
            var charX = MeasureText(line.Substring(0, i), font);
            if (charX > contentX)
            {
                clickedCol = i > 0 ? i - 1 : 0;
                break;
            }
            clickedCol = i;
        }

        _cursorPosition = GetPosition(clickedLine, clickedCol);

        // Check for double-click (select word)
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
        var distanceFromLastClick = Math.Sqrt(Math.Pow(e.X - _lastClickX, 2) + Math.Pow(e.Y - _lastClickY, 2));

        if (timeSinceLastClick < DoubleClickThresholdMs && distanceFromLastClick < 10)
        {
            // Double-click: select the word at cursor
            SelectWordAtCursor();
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
            _lastClickY = e.Y;
        }

        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled || !_isSelecting) return;

        // Calculate position from mouse coordinates
        var screenBounds = ScreenBounds;
        var paddingLeft = (float)Padding.Left;
        var paddingTop = (float)Padding.Top;
        var contentX = e.X - screenBounds.Left - paddingLeft;
        var contentY = e.Y - screenBounds.Top - paddingTop + _scrollOffsetY;

        var fontSize = (float)FontSize;
        var lineSpacing = fontSize * (float)LineHeight;
        var clickedLine = Math.Clamp((int)(contentY / lineSpacing), 0, _lines.Count - 1);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        var line = _lines[clickedLine];
        var clickedCol = 0;

        for (int i = 0; i <= line.Length; i++)
        {
            var charX = MeasureText(line.Substring(0, i), font);
            if (charX > contentX)
            {
                clickedCol = i > 0 ? i - 1 : 0;
                break;
            }
            clickedCol = i;
        }

        var newPosition = GetPosition(clickedLine, clickedCol);
        if (newPosition != _cursorPosition)
        {
            _cursorPosition = newPosition;
            _selectionLength = _cursorPosition - _selectionStart;
            _cursorVisible = true;
            _lastCursorBlink = DateTime.Now;
            Invalidate();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        _isSelecting = false;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        var (line, col) = GetLineColumn(_cursorPosition);
        _cursorVisible = true;
        _lastCursorBlink = DateTime.Now;

        switch (e.Key)
        {
            case Key.Left:
                if (_cursorPosition > 0)
                {
                    _cursorPosition--;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_cursorPosition < Text.Length)
                {
                    _cursorPosition++;
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (line > 0)
                {
                    _cursorPosition = GetPosition(line - 1, col);
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (line < _lines.Count - 1)
                {
                    _cursorPosition = GetPosition(line + 1, col);
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Home:
                _cursorPosition = GetPosition(line, 0);
                EnsureCursorVisible();
                e.Handled = true;
                break;

            case Key.End:
                _cursorPosition = GetPosition(line, _lines[line].Length);
                EnsureCursorVisible();
                e.Handled = true;
                break;

            case Key.Enter:
                if (!IsReadOnly)
                {
                    InsertText("\n");
                }
                e.Handled = true;
                break;

            case Key.Backspace:
                if (!IsReadOnly)
                {
                    if (_selectionLength != 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition > 0)
                    {
                        Text = Text.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    EnsureCursorVisible();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (!IsReadOnly)
                {
                    if (_selectionLength != 0)
                    {
                        DeleteSelection();
                    }
                    else if (_cursorPosition < Text.Length)
                    {
                        Text = Text.Remove(_cursorPosition, 1);
                    }
                }
                e.Handled = true;
                break;

            case Key.Tab:
                if (!IsReadOnly)
                {
                    InsertText("    ");
                }
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
        }

        Invalidate();
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return;

        // Ignore control characters (Ctrl+key combinations send ASCII control codes)
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length == 1 && e.Text[0] < 32)
            return;

        if (!string.IsNullOrEmpty(e.Text))
        {
            InsertText(e.Text);
            e.Handled = true;
        }
    }

    private void InsertText(string text)
    {
        if (_selectionLength > 0)
        {
            var currentText = Text;
            Text = currentText.Remove(_selectionStart, _selectionLength);
            _cursorPosition = _selectionStart;
            _selectionStart = -1;
            _selectionLength = 0;
        }

        if (MaxLength > 0 && Text.Length + text.Length > MaxLength)
        {
            text = text.Substring(0, Math.Max(0, MaxLength - Text.Length));
        }

        if (!string.IsNullOrEmpty(text))
        {
            Text = Text.Insert(_cursorPosition, text);
            _cursorPosition += text.Length;
            EnsureCursorVisible();
        }
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        var fontSize = (float)FontSize;
        var lineHeight = (float)LineHeight;
        var lineSpacing = fontSize * lineHeight;
        var totalHeight = _lines.Count * lineSpacing;
        var viewHeight = (float)Bounds.Height - (float)(Padding.Top + Padding.Bottom);
        var maxScroll = Math.Max(0, totalHeight - viewHeight);

        _scrollOffsetY = Math.Clamp(_scrollOffsetY - e.DeltaY * 3, 0, maxScroll);
        Invalidate();
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

        Completed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets the cursor blink timer (shows cursor immediately).
    /// </summary>
    private void ResetCursorBlink()
    {
        _lastCursorBlink = DateTime.Now;
        _cursorVisible = true;
    }

    /// <summary>
    /// Updates cursor blink animation. Called by the application's animation loop.
    /// </summary>
    public void UpdateCursorBlink()
    {
        if (!IsFocused) return;

        var elapsed = (DateTime.Now - _lastCursorBlink).TotalMilliseconds;
        var newVisible = ((int)(elapsed / 500) % 2) == 0;

        if (newVisible != _cursorVisible)
        {
            _cursorVisible = newVisible;
            Invalidate();
        }
    }

    #region Selection and Clipboard

    public void SelectAll()
    {
        _selectionStart = 0;
        _cursorPosition = Text.Length;
        _selectionLength = Text.Length;
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

    private void CopyToClipboard()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);
        var selectedText = Text.Substring(start, length);

        // Use system clipboard via xclip/xsel
        SystemClipboard.SetText(selectedText);
    }

    private void CutToClipboard()
    {
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

        InsertText(text);
    }

    private void DeleteSelection()
    {
        if (_selectionLength == 0) return;

        var start = Math.Min(_selectionStart, _selectionStart + _selectionLength);
        var length = Math.Abs(_selectionLength);

        Text = Text.Remove(start, length);
        _cursorPosition = start;
        _selectionStart = -1;
        _selectionLength = 0;
    }

    private void ShowContextMenu(float x, float y)
    {
        DiagnosticLog.Debug("SkiaEditor", $"ShowContextMenu at ({x}, {y}), IsGtkMode={LinuxApplication.IsGtkMode}");
        bool hasSelection = _selectionLength != 0;
        bool hasText = !string.IsNullOrEmpty(Text);
        bool hasClipboard = !string.IsNullOrEmpty(SystemClipboard.GetText());
        bool isEditable = !IsReadOnly;

        if (LinuxApplication.IsGtkMode)
        {
            // Use GTK context menu when running in GTK mode (e.g., with WebView)
            GtkContextMenuService.ShowContextMenu(new List<GtkMenuItem>
            {
                new GtkMenuItem("Cut", () =>
                {
                    CutToClipboard();
                    Invalidate();
                }, hasSelection && isEditable),
                new GtkMenuItem("Copy", () =>
                {
                    CopyToClipboard();
                }, hasSelection),
                new GtkMenuItem("Paste", () =>
                {
                    PasteFromClipboard();
                    Invalidate();
                }, hasClipboard && isEditable),
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
                }, hasSelection && isEditable),
                new ContextMenuItem("Copy", () =>
                {
                    CopyToClipboard();
                }, hasSelection),
                new ContextMenuItem("Paste", () =>
                {
                    PasteFromClipboard();
                    Invalidate();
                }, hasClipboard && isEditable),
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

    #endregion

    /// <summary>
    /// Updates the IME cursor location for candidate window positioning.
    /// </summary>
    private void UpdateImeCursorLocation()
    {
        if (_inputMethodService == null) return;

        var screenBounds = ScreenBounds;
        var fontSize = (float)FontSize;
        var lineSpacing = fontSize * (float)LineHeight;
        var (line, col) = GetLineColumn(_cursorPosition);

        using var font = new SKFont(SKTypeface.Default, fontSize);
        var lineText = line < _lines.Count ? _lines[line] : "";
        var textToCursor = lineText.Substring(0, Math.Min(col, lineText.Length));
        var cursorX = MeasureText(textToCursor, font);

        int x = (int)(screenBounds.Left + Padding.Left + cursorX);
        int y = (int)(screenBounds.Top + Padding.Top + line * lineSpacing - _scrollOffsetY);
        int height = (int)fontSize;

        _inputMethodService.SetCursorLocation(x, y, 2, height);
    }
}
