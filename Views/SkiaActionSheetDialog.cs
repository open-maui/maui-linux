// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A modal action sheet rendered with Skia: a card with an optional title and
/// one button per option, stacked vertically. The destruction option (when
/// given) is drawn in the error color; the cancel option (when given) is
/// always last and drawn as a neutral button. Backs
/// <c>Page.DisplayActionSheet</c> on Linux.
/// </summary>
/// <remarks>
/// Keyboard: Escape dismisses with the cancel text (null when there is no
/// cancel option); Up/Down move a highlight and Enter picks the highlighted
/// option. Pointer: clicking a button picks it; clicking outside the card
/// does nothing (the sheet is modal).
/// </remarks>
public class SkiaActionSheetDialog : SkiaModalDialog
{
    private readonly string? _title;
    private readonly string? _cancel;
    private readonly string? _destruction;
    private readonly List<string> _options;
    private readonly TaskCompletionSource<string?> _tcs = new();

    private SKRect[] _buttonBounds = Array.Empty<SKRect>();
    private int _hoveredIndex = -1;
    private int _selectedIndex = -1;

    private const float OptionSpacing = 8;

    /// <summary>
    /// Creates a new action sheet. <paramref name="buttons"/> are shown in
    /// order after the destruction option and before cancel; null or empty
    /// entries are skipped.
    /// </summary>
    public SkiaActionSheetDialog(string? title, string? cancel, string? destruction, IEnumerable<string>? buttons)
    {
        _title = title;
        _cancel = string.IsNullOrEmpty(cancel) ? null : cancel;
        _destruction = string.IsNullOrEmpty(destruction) ? null : destruction;

        _options = new List<string>();
        if (_destruction != null)
            _options.Add(_destruction);
        if (buttons != null)
        {
            foreach (var b in buttons)
            {
                if (!string.IsNullOrEmpty(b))
                    _options.Add(b);
            }
        }
        if (_cancel != null)
            _options.Add(_cancel);
    }

    /// <summary>
    /// Completes with the text of the chosen option, the cancel text when
    /// dismissed with Escape, or null when dismissed without a cancel option.
    /// </summary>
    public Task<string?> Result => _tcs.Task;

    /// <summary>All options in display order (destruction, buttons, cancel).</summary>
    public IReadOnlyList<string> Options => _options;

    /// <summary>Bounds of each option button as of the last draw (tests).</summary>
    internal IReadOnlyList<SKRect> ButtonBounds => _buttonBounds;

    private bool IsDestruction(int index) => _destruction != null && index == 0;

    private bool IsCancel(int index) => _cancel != null && index == _options.Count - 1;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawOverlay(canvas, bounds);

        var titleLines = string.IsNullOrEmpty(_title)
            ? new List<string>()
            : WrapText(_title!, DialogWidth - DialogPadding * 2, 18);
        var dialogHeight = CalculateDialogHeight(titleLines.Count);

        var dialogLeft = bounds.MidX - DialogWidth / 2;
        var dialogTop = bounds.MidY - dialogHeight / 2;
        var dialogBounds = new SKRect(dialogLeft, dialogTop, dialogLeft + DialogWidth, dialogTop + dialogHeight);

        DrawCard(canvas, dialogBounds);

        var yOffset = dialogBounds.Top + DialogPadding;
        if (titleLines.Count > 0)
        {
            using var titleFont = SkiaFontFactory.Create(18);
            titleFont.Embolden = true;
            using var titlePaint = new SKPaint
            {
                Color = TitleColor,
                IsAntialias = true
            };
            foreach (var line in titleLines)
            {
                canvas.DrawText(line, dialogBounds.Left + DialogPadding, yOffset + 18, titleFont, titlePaint);
                yOffset += 26;
            }
            yOffset += 10;
        }

        if (_buttonBounds.Length != _options.Count)
            _buttonBounds = new SKRect[_options.Count];

        for (int i = 0; i < _options.Count; i++)
        {
            if (IsCancel(i) && i > 0)
                yOffset += OptionSpacing; // extra gap before cancel

            var rect = new SKRect(
                dialogBounds.Left + DialogPadding,
                yOffset,
                dialogBounds.Right - DialogPadding,
                yOffset + ButtonHeight);
            _buttonBounds[i] = rect;

            bool highlighted = i == _hoveredIndex || i == _selectedIndex;
            SKColor color;
            if (IsDestruction(i))
                color = highlighted ? DestructiveButtonHoverColor : DestructiveButtonColor;
            else if (IsCancel(i))
                color = highlighted ? CancelButtonHoverColor : CancelButtonColor;
            else
                color = highlighted ? ButtonHoverColor : ButtonColor;

            DrawButton(canvas, rect, _options[i], color);
            yOffset += ButtonHeight + OptionSpacing;
        }
    }

    private float CalculateDialogHeight(int titleLineCount)
    {
        var height = DialogPadding * 2;
        if (titleLineCount > 0)
            height += titleLineCount * 26 + 10;
        if (_options.Count > 0)
        {
            height += _options.Count * ButtonHeight + (_options.Count - 1) * OptionSpacing;
            if (_cancel != null && _options.Count > 1)
                height += OptionSpacing;
        }
        return Math.Max(height, 120);
    }

    private int IndexAt(float x, float y)
    {
        for (int i = 0; i < _buttonBounds.Length; i++)
        {
            if (_buttonBounds[i].Contains(x, y))
                return i;
        }
        return -1;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        int hovered = IndexAt(e.X, e.Y);
        if (hovered != _hoveredIndex)
        {
            _hoveredIndex = hovered;
            Invalidate();
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            Invalidate();
        }
        base.OnPointerExited(e);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        int index = IndexAt(e.X, e.Y);
        if (index >= 0)
        {
            Dismiss(_options[index]);
            return;
        }

        // Clicking outside the card does not dismiss (modal).
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Dismiss(_cancel);
                e.Handled = true;
                return;

            case Key.Down:
                if (_options.Count > 0)
                {
                    _selectedIndex = (_selectedIndex + 1) % _options.Count;
                    Invalidate();
                }
                e.Handled = true;
                return;

            case Key.Up:
                if (_options.Count > 0)
                {
                    _selectedIndex = _selectedIndex <= 0 ? _options.Count - 1 : _selectedIndex - 1;
                    Invalidate();
                }
                e.Handled = true;
                return;

            case Key.Enter:
                if (_selectedIndex >= 0 && _selectedIndex < _options.Count)
                {
                    Dismiss(_options[_selectedIndex]);
                    e.Handled = true;
                }
                return;
        }
    }

    private void Dismiss(string? result)
    {
        Hide();
        _tcs.TrySetResult(result);
    }
}
