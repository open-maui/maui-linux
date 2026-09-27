// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// A modal action sheet rendered with Skia: a card with an optional title,
/// the options as a list of rows, and the cancel option (when given) as a
/// button pinned at the foot. The card fits the window; a list longer than
/// the room scrolls (wheel, or the keyboard highlight). The destruction option
/// (when given) is first and drawn in the error colour. Backs
/// <c>Page.DisplayActionSheet</c> on Linux.
/// </summary>
/// <remarks>
/// Keyboard: Escape dismisses with the cancel text (null when there is no
/// cancel option); Up/Down move a highlight and Enter picks the highlighted
/// option. Pointer: clicking a row or button picks it; clicking outside the
/// card dismisses with the cancel text, as an action sheet does on iOS and
/// Mac Catalyst.
/// </remarks>
public class SkiaActionSheetDialog : SkiaModalDialog
{
    private readonly string? _title;
    private readonly string? _cancel;
    private readonly string? _destruction;
    private readonly List<string> _options;
    private readonly TaskCompletionSource<string?> _tcs = new();

    private SKRect[] _buttonBounds = Array.Empty<SKRect>();
    private SKRect _cardBounds;
    private SKRect _listBounds;
    private float _scrollY;
    private float _maxScroll;
    private int _hoveredIndex = -1;
    private int _selectedIndex = -1;

    private const float RowHeight = 40;
    private const float WindowMargin = 24;
    private const float SectionGap = 12;
    private const float RowFontSize = 15;

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

    /// <summary>The options in the scrolling list: everything but cancel.</summary>
    private int ListCount => _cancel != null ? _options.Count - 1 : _options.Count;

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawOverlay(canvas, bounds);

        var titleLines = string.IsNullOrEmpty(_title)
            ? new List<string>()
            : WrapText(_title!, DialogWidth - DialogPadding * 2, 18);
        float titleHeight = titleLines.Count > 0 ? titleLines.Count * 26 + SectionGap : 0;
        float cancelHeight = _cancel != null ? ButtonHeight + SectionGap : 0;
        float listHeight = ListCount * RowHeight;

        // The card fits the window; the list takes what is left and scrolls.
        float chrome = DialogPadding * 2 + titleHeight + cancelHeight;
        float maxCard = Math.Max(chrome + RowHeight, bounds.Height - WindowMargin * 2);
        float visibleList = Math.Min(listHeight, maxCard - chrome);
        float cardHeight = chrome + visibleList;
        float cardWidth = Math.Min(DialogWidth, bounds.Width - WindowMargin * 2);

        var left = bounds.MidX - cardWidth / 2;
        var top = bounds.MidY - cardHeight / 2;
        _cardBounds = new SKRect(left, top, left + cardWidth, top + cardHeight);
        DrawCard(canvas, _cardBounds);

        var y = _cardBounds.Top + DialogPadding;
        if (titleLines.Count > 0)
        {
            using var titleFont = SkiaFontFactory.Create(18);
            titleFont.Embolden = true;
            using var titlePaint = new SKPaint { Color = TitleColor, IsAntialias = true };
            foreach (var line in titleLines)
            {
                canvas.DrawText(line, _cardBounds.Left + DialogPadding, y + 18, titleFont, titlePaint);
                y += 26;
            }
            y += SectionGap;
        }

        if (_buttonBounds.Length != _options.Count)
            _buttonBounds = new SKRect[_options.Count];

        _listBounds = new SKRect(_cardBounds.Left + DialogPadding / 2, y, _cardBounds.Right - DialogPadding / 2, y + visibleList);
        _maxScroll = Math.Max(0, listHeight - visibleList);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);

        canvas.Save();
        canvas.ClipRect(_listBounds);
        using (var rowFont = SkiaFontFactory.Create(RowFontSize))
        using (var textPaint = new SKPaint { IsAntialias = true })
        using (var hoverPaint = new SKPaint { Color = SkiaTheme.IsDarkMode ? SkiaTheme.DarkHoverSK : SkiaTheme.Gray200SK, IsAntialias = true })
        {
            for (int i = 0; i < ListCount; i++)
            {
                var row = new SKRect(_listBounds.Left, _listBounds.Top + i * RowHeight - _scrollY,
                                     _listBounds.Right, _listBounds.Top + (i + 1) * RowHeight - _scrollY);
                _buttonBounds[i] = row;
                if (row.Bottom < _listBounds.Top || row.Top > _listBounds.Bottom)
                    continue;

                if (i == _hoveredIndex || i == _selectedIndex)
                    canvas.DrawRoundRect(row, 6, 6, hoverPaint);
                textPaint.Color = IsDestruction(i) ? DestructiveButtonColor : TitleColor;
                var text = TextRenderingHelper.Ellipsize(rowFont, _options[i], row.Width - 24);
                canvas.DrawText(text, row.Left + 12, TextRenderingHelper.BaselineForVerticalCenter(rowFont, row.MidY), rowFont, textPaint);
            }
        }
        canvas.Restore();

        // A scroll bar when the list is longer than the room.
        if (_maxScroll > 0)
        {
            float trackHeight = _listBounds.Height;
            float thumb = Math.Max(24, trackHeight * trackHeight / listHeight);
            float thumbTop = _listBounds.Top + (trackHeight - thumb) * (_scrollY / _maxScroll);
            using var barPaint = new SKPaint { Color = SkiaTheme.IsDarkMode ? SkiaTheme.Gray600SK : SkiaTheme.Gray400SK, IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(_listBounds.Right + 2, thumbTop, _listBounds.Right + 6, thumbTop + thumb), 2, 2, barPaint);
        }

        if (_cancel != null)
        {
            int index = _options.Count - 1;
            var rect = new SKRect(_cardBounds.Left + DialogPadding, _listBounds.Bottom + SectionGap,
                                  _cardBounds.Right - DialogPadding, _listBounds.Bottom + SectionGap + ButtonHeight);
            _buttonBounds[index] = rect;
            bool highlighted = index == _hoveredIndex || index == _selectedIndex;
            DrawButton(canvas, rect, _cancel, highlighted ? CancelButtonHoverColor : CancelButtonColor);
        }
    }

    private int IndexAt(float x, float y)
    {
        for (int i = 0; i < _buttonBounds.Length; i++)
        {
            // A list row counts only where the list shows it.
            if (i < ListCount && !_listBounds.Contains(x, y))
                continue;
            if (_buttonBounds[i].Contains(x, y))
                return i;
        }
        return -1;
    }

    /// <summary>Scrolls the list so row <paramref name="index"/> is in view.</summary>
    private void ScrollIntoView(int index)
    {
        if (index < 0 || index >= ListCount || _listBounds.Height <= 0)
            return;
        float top = index * RowHeight;
        if (top < _scrollY)
            _scrollY = top;
        else if (top + RowHeight > _scrollY + _listBounds.Height)
            _scrollY = top + RowHeight - _listBounds.Height;
    }

    public override void OnScroll(ScrollEventArgs e)
    {
        if (_maxScroll <= 0)
            return;
        _scrollY = Math.Clamp(_scrollY + e.DeltaY * RowHeight, 0, _maxScroll);
        _hoveredIndex = IndexAt(e.X, e.Y);
        Invalidate();
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

        // Outside the card: dismissed, as cancel (iOS and Mac Catalyst do the same).
        if (!_cardBounds.IsEmpty && !_cardBounds.Contains(e.X, e.Y))
            Dismiss(_cancel);
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
                    ScrollIntoView(_selectedIndex);
                    Invalidate();
                }
                e.Handled = true;
                return;

            case Key.Up:
                if (_options.Count > 0)
                {
                    _selectedIndex = _selectedIndex <= 0 ? _options.Count - 1 : _selectedIndex - 1;
                    ScrollIntoView(_selectedIndex);
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
