// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered date picker control with calendar popup.
/// </summary>
public class SkiaDatePicker : SkiaView
{
    private DateTime _date = DateTime.Today;
    private DateTime _minimumDate = new DateTime(1900, 1, 1);
    private DateTime _maximumDate = new DateTime(2100, 12, 31);
    private DateTime _displayMonth;
    private bool _isOpen;
    private string _format = "d";

    // Styling
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor BorderColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor CalendarBackgroundColor { get; set; } = SKColors.White;
    public SKColor SelectedDayColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor TodayColor { get; set; } = new SKColor(0x21, 0x96, 0xF3, 0x40);
    public SKColor HeaderColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor DisabledDayColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float FontSize { get; set; } = 14;
    public float CornerRadius { get; set; } = 4;

    private const float CalendarWidth = 280;
    private const float CalendarHeight = 320;
    private const float DayCellSize = 36;
    private const float HeaderHeight = 48;

    public DateTime Date
    {
        get => _date;
        set
        {
            var clamped = ClampDate(value);
            if (_date != clamped)
            {
                _date = clamped;
                _displayMonth = new DateTime(_date.Year, _date.Month, 1);
                DateSelected?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public DateTime MinimumDate
    {
        get => _minimumDate;
        set { _minimumDate = value; Invalidate(); }
    }

    public DateTime MaximumDate
    {
        get => _maximumDate;
        set { _maximumDate = value; Invalidate(); }
    }

    public string Format
    {
        get => _format;
        set { _format = value; Invalidate(); }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set { _isOpen = value; Invalidate(); }
    }

    public event EventHandler? DateSelected;

    public SkiaDatePicker()
    {
        IsFocusable = true;
        _displayMonth = new DateTime(_date.Year, _date.Month, 1);
    }

    private DateTime ClampDate(DateTime date)
    {
        if (date < _minimumDate) return _minimumDate;
        if (date > _maximumDate) return _maximumDate;
        return date;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);

        if (_isOpen)
        {
            DrawCalendar(canvas, bounds);
        }
    }

    private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
    {
        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? BackgroundColor : new SKColor(0xF5, 0xF5, 0xF5),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? SelectedDayColor : BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), borderPaint);

        // Draw date text
        using var font = new SKFont(SKTypeface.Default, FontSize);
        using var textPaint = new SKPaint(font)
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            IsAntialias = true
        };

        var dateText = _date.ToString(_format);
        var textBounds = new SKRect();
        textPaint.MeasureText(dateText, ref textBounds);

        var textX = bounds.Left + 12;
        var textY = bounds.MidY - textBounds.MidY;
        canvas.DrawText(dateText, textX, textY, textPaint);

        // Draw calendar icon
        DrawCalendarIcon(canvas, new SKRect(bounds.Right - 36, bounds.MidY - 10, bounds.Right - 12, bounds.MidY + 10));
    }

    private void DrawCalendarIcon(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        // Calendar outline
        var calRect = new SKRect(bounds.Left, bounds.Top + 3, bounds.Right, bounds.Bottom);
        canvas.DrawRoundRect(new SKRoundRect(calRect, 2), paint);

        // Top tabs
        canvas.DrawLine(bounds.Left + 5, bounds.Top, bounds.Left + 5, bounds.Top + 5, paint);
        canvas.DrawLine(bounds.Right - 5, bounds.Top, bounds.Right - 5, bounds.Top + 5, paint);

        // Header line
        canvas.DrawLine(bounds.Left, bounds.Top + 8, bounds.Right, bounds.Top + 8, paint);

        // Dots for days
        paint.Style = SKPaintStyle.Fill;
        paint.StrokeWidth = 0;
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var dotX = bounds.Left + 4 + col * 6;
                var dotY = bounds.Top + 12 + row * 4;
                canvas.DrawCircle(dotX, dotY, 1, paint);
            }
        }
    }

    private void DrawCalendar(SKCanvas canvas, SKRect bounds)
    {
        var calendarRect = new SKRect(
            bounds.Left,
            bounds.Bottom + 4,
            bounds.Left + CalendarWidth,
            bounds.Bottom + 4 + CalendarHeight);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(calendarRect.Left + 2, calendarRect.Top + 2, calendarRect.Right + 2, calendarRect.Bottom + 2), CornerRadius), shadowPaint);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = CalendarBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(calendarRect, CornerRadius), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(calendarRect, CornerRadius), borderPaint);

        // Draw header
        DrawCalendarHeader(canvas, new SKRect(calendarRect.Left, calendarRect.Top, calendarRect.Right, calendarRect.Top + HeaderHeight));

        // Draw weekday headers
        DrawWeekdayHeaders(canvas, new SKRect(calendarRect.Left, calendarRect.Top + HeaderHeight, calendarRect.Right, calendarRect.Top + HeaderHeight + 30));

        // Draw days
        DrawDays(canvas, new SKRect(calendarRect.Left, calendarRect.Top + HeaderHeight + 30, calendarRect.Right, calendarRect.Bottom));
    }

    private void DrawCalendarHeader(SKCanvas canvas, SKRect bounds)
    {
        // Draw header background
        using var headerPaint = new SKPaint
        {
            Color = HeaderColor,
            Style = SKPaintStyle.Fill
        };

        var headerRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + CornerRadius * 2), CornerRadius));
        canvas.DrawRect(headerRect, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top + CornerRadius, bounds.Right, bounds.Bottom), headerPaint);

        // Draw month/year text
        using var font = new SKFont(SKTypeface.Default, 16);
        using var textPaint = new SKPaint(font)
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        var monthYear = _displayMonth.ToString("MMMM yyyy");
        var textBounds = new SKRect();
        textPaint.MeasureText(monthYear, ref textBounds);
        canvas.DrawText(monthYear, bounds.MidX - textBounds.MidX, bounds.MidY - textBounds.MidY, textPaint);

        // Draw navigation arrows
        using var arrowPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Left arrow
        var leftArrowX = bounds.Left + 20;
        using var leftPath = new SKPath();
        leftPath.MoveTo(leftArrowX + 6, bounds.MidY - 6);
        leftPath.LineTo(leftArrowX, bounds.MidY);
        leftPath.LineTo(leftArrowX + 6, bounds.MidY + 6);
        canvas.DrawPath(leftPath, arrowPaint);

        // Right arrow
        var rightArrowX = bounds.Right - 20;
        using var rightPath = new SKPath();
        rightPath.MoveTo(rightArrowX - 6, bounds.MidY - 6);
        rightPath.LineTo(rightArrowX, bounds.MidY);
        rightPath.LineTo(rightArrowX - 6, bounds.MidY + 6);
        canvas.DrawPath(rightPath, arrowPaint);
    }

    private void DrawWeekdayHeaders(SKCanvas canvas, SKRect bounds)
    {
        var dayNames = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        var cellWidth = bounds.Width / 7;

        using var font = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint(font)
        {
            Color = new SKColor(0x80, 0x80, 0x80),
            IsAntialias = true
        };

        for (int i = 0; i < 7; i++)
        {
            var textBounds = new SKRect();
            paint.MeasureText(dayNames[i], ref textBounds);
            var x = bounds.Left + i * cellWidth + cellWidth / 2 - textBounds.MidX;
            var y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(dayNames[i], x, y, paint);
        }
    }

    private void DrawDays(SKCanvas canvas, SKRect bounds)
    {
        var firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
        var startDayOfWeek = (int)firstDay.DayOfWeek;

        var cellWidth = bounds.Width / 7;
        var cellHeight = (bounds.Height - 10) / 6;

        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font) { IsAntialias = true };
        using var bgPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

        var today = DateTime.Today;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var dayDate = new DateTime(_displayMonth.Year, _displayMonth.Month, day);
            var cellIndex = startDayOfWeek + day - 1;
            var row = cellIndex / 7;
            var col = cellIndex % 7;

            var cellX = bounds.Left + col * cellWidth;
            var cellY = bounds.Top + row * cellHeight;
            var cellRect = new SKRect(cellX + 2, cellY + 2, cellX + cellWidth - 2, cellY + cellHeight - 2);

            var isSelected = dayDate.Date == _date.Date;
            var isToday = dayDate.Date == today;
            var isDisabled = dayDate < _minimumDate || dayDate > _maximumDate;

            // Draw day background
            if (isSelected)
            {
                bgPaint.Color = SelectedDayColor;
                canvas.DrawCircle(cellRect.MidX, cellRect.MidY, Math.Min(cellRect.Width, cellRect.Height) / 2 - 2, bgPaint);
            }
            else if (isToday)
            {
                bgPaint.Color = TodayColor;
                canvas.DrawCircle(cellRect.MidX, cellRect.MidY, Math.Min(cellRect.Width, cellRect.Height) / 2 - 2, bgPaint);
            }

            // Draw day text
            textPaint.Color = isSelected ? SKColors.White : isDisabled ? DisabledDayColor : TextColor;
            var dayText = day.ToString();
            var textBounds = new SKRect();
            textPaint.MeasureText(dayText, ref textBounds);
            canvas.DrawText(dayText, cellRect.MidX - textBounds.MidX, cellRect.MidY - textBounds.MidY, textPaint);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (_isOpen)
        {
            var calendarTop = Bounds.Bottom + 4;

            // Check header navigation
            if (e.Y >= calendarTop && e.Y < calendarTop + HeaderHeight)
            {
                if (e.X < Bounds.Left + 40)
                {
                    // Previous month
                    _displayMonth = _displayMonth.AddMonths(-1);
                    Invalidate();
                    return;
                }
                else if (e.X > Bounds.Left + CalendarWidth - 40)
                {
                    // Next month
                    _displayMonth = _displayMonth.AddMonths(1);
                    Invalidate();
                    return;
                }
            }

            // Check day selection
            var daysTop = calendarTop + HeaderHeight + 30;
            if (e.Y >= daysTop && e.Y < calendarTop + CalendarHeight)
            {
                var cellWidth = CalendarWidth / 7;
                var cellHeight = (CalendarHeight - HeaderHeight - 40) / 6;

                var col = (int)((e.X - Bounds.Left) / cellWidth);
                var row = (int)((e.Y - daysTop) / cellHeight);

                var firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
                var startDayOfWeek = (int)firstDay.DayOfWeek;
                var dayIndex = row * 7 + col - startDayOfWeek + 1;
                var daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);

                if (dayIndex >= 1 && dayIndex <= daysInMonth)
                {
                    var selectedDate = new DateTime(_displayMonth.Year, _displayMonth.Month, dayIndex);
                    if (selectedDate >= _minimumDate && selectedDate <= _maximumDate)
                    {
                        Date = selectedDate;
                        _isOpen = false;
                    }
                }
            }
            else if (e.Y < calendarTop)
            {
                _isOpen = false;
            }
        }
        else
        {
            _isOpen = true;
        }

        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                _isOpen = !_isOpen;
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isOpen)
                {
                    _isOpen = false;
                    e.Handled = true;
                }
                break;

            case Key.Left:
                Date = _date.AddDays(-1);
                e.Handled = true;
                break;

            case Key.Right:
                Date = _date.AddDays(1);
                e.Handled = true;
                break;

            case Key.Up:
                Date = _date.AddDays(-7);
                e.Handled = true;
                break;

            case Key.Down:
                Date = _date.AddDays(7);
                e.Handled = true;
                break;
        }

        Invalidate();
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(
            availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200,
            40);
    }
}
