// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered date picker control with calendar popup.
/// </summary>
public class SkiaDatePicker : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty DateProperty =
        BindableProperty.Create(nameof(Date), typeof(DateTime), typeof(SkiaDatePicker), DateTime.Today, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).OnDatePropertyChanged());

    public static readonly BindableProperty MinimumDateProperty =
        BindableProperty.Create(nameof(MinimumDate), typeof(DateTime), typeof(SkiaDatePicker), new DateTime(1900, 1, 1),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty MaximumDateProperty =
        BindableProperty.Create(nameof(MaximumDate), typeof(DateTime), typeof(SkiaDatePicker), new DateTime(2100, 12, 31),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty FormatProperty =
        BindableProperty.Create(nameof(Format), typeof(string), typeof(SkiaDatePicker), "d",
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(SKColor), typeof(SkiaDatePicker), SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(SKColor), typeof(SkiaDatePicker), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty CalendarBackgroundColorProperty =
        BindableProperty.Create(nameof(CalendarBackgroundColor), typeof(SKColor), typeof(SkiaDatePicker), SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty SelectedDayColorProperty =
        BindableProperty.Create(nameof(SelectedDayColor), typeof(SKColor), typeof(SkiaDatePicker), new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty TodayColorProperty =
        BindableProperty.Create(nameof(TodayColor), typeof(SKColor), typeof(SkiaDatePicker), new SKColor(0x21, 0x96, 0xF3, 0x40),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty HeaderColorProperty =
        BindableProperty.Create(nameof(HeaderColor), typeof(SKColor), typeof(SkiaDatePicker), new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty DisabledDayColorProperty =
        BindableProperty.Create(nameof(DisabledDayColor), typeof(SKColor), typeof(SkiaDatePicker), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(float), typeof(SkiaDatePicker), 14f,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).InvalidateMeasure());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(SkiaDatePicker), 4f,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    #endregion

    #region Properties

    public DateTime Date
    {
        get => (DateTime)GetValue(DateProperty);
        set => SetValue(DateProperty, ClampDate(value));
    }

    public DateTime MinimumDate
    {
        get => (DateTime)GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    public DateTime MaximumDate
    {
        get => (DateTime)GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public SKColor CalendarBackgroundColor
    {
        get => (SKColor)GetValue(CalendarBackgroundColorProperty);
        set => SetValue(CalendarBackgroundColorProperty, value);
    }

    public SKColor SelectedDayColor
    {
        get => (SKColor)GetValue(SelectedDayColorProperty);
        set => SetValue(SelectedDayColorProperty, value);
    }

    public SKColor TodayColor
    {
        get => (SKColor)GetValue(TodayColorProperty);
        set => SetValue(TodayColorProperty, value);
    }

    public SKColor HeaderColor
    {
        get => (SKColor)GetValue(HeaderColorProperty);
        set => SetValue(HeaderColorProperty, value);
    }

    public SKColor DisabledDayColor
    {
        get => (SKColor)GetValue(DisabledDayColorProperty);
        set => SetValue(DisabledDayColorProperty, value);
    }

    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen != value)
            {
                _isOpen = value;
                if (_isOpen)
                    RegisterPopupOverlay(this, DrawCalendarOverlay);
                else
                    UnregisterPopupOverlay(this);
                Invalidate();
            }
        }
    }

    #endregion

    private DateTime _displayMonth;
    private bool _isOpen;

    private const float CalendarWidth = 280;
    private const float CalendarHeight = 320;
    private const float HeaderHeight = 48;

    public event EventHandler? DateSelected;

    /// <summary>
    /// Gets the calendar popup rectangle with edge detection applied.
    /// </summary>
    private SKRect GetCalendarRect(SKRect pickerBounds)
    {
        // Get window dimensions for edge detection
        var windowWidth = LinuxApplication.Current?.MainWindow?.Width ?? 800;
        var windowHeight = LinuxApplication.Current?.MainWindow?.Height ?? 600;

        // Calculate default position (below the picker)
        var calendarLeft = pickerBounds.Left;
        var calendarTop = pickerBounds.Bottom + 4;

        // Edge detection: adjust horizontal position if popup would go off-screen
        if (calendarLeft + CalendarWidth > windowWidth)
        {
            calendarLeft = windowWidth - CalendarWidth - 4;
        }
        if (calendarLeft < 0) calendarLeft = 4;

        // Edge detection: show above if popup would go off-screen vertically
        if (calendarTop + CalendarHeight > windowHeight)
        {
            calendarTop = pickerBounds.Top - CalendarHeight - 4;
        }
        if (calendarTop < 0) calendarTop = 4;

        return new SKRect(calendarLeft, calendarTop, calendarLeft + CalendarWidth, calendarTop + CalendarHeight);
    }

    public SkiaDatePicker()
    {
        IsFocusable = true;
        _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    private void OnDatePropertyChanged()
    {
        _displayMonth = new DateTime(Date.Year, Date.Month, 1);
        DateSelected?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private DateTime ClampDate(DateTime date)
    {
        if (date < MinimumDate) return MinimumDate;
        if (date > MaximumDate) return MaximumDate;
        return date;
    }

    private void DrawCalendarOverlay(SKCanvas canvas)
    {
        if (!_isOpen) return;
        // Use ScreenBounds for popup drawing (accounts for scroll offset)
        DrawCalendar(canvas, ScreenBounds);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);
    }

    private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
    {
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? BackgroundColor : new SKColor(0xF5, 0xF5, 0xF5),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? SelectedDayColor : BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), borderPaint);

        using var font = new SKFont(SKTypeface.Default, FontSize);
        using var textPaint = new SKPaint(font)
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            IsAntialias = true
        };

        var dateText = Date.ToString(Format);
        var textBounds = new SKRect();
        textPaint.MeasureText(dateText, ref textBounds);
        canvas.DrawText(dateText, bounds.Left + 12, bounds.MidY - textBounds.MidY, textPaint);

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

        var calRect = new SKRect(bounds.Left, bounds.Top + 3, bounds.Right, bounds.Bottom);
        canvas.DrawRoundRect(new SKRoundRect(calRect, 2), paint);
        canvas.DrawLine(bounds.Left + 5, bounds.Top, bounds.Left + 5, bounds.Top + 5, paint);
        canvas.DrawLine(bounds.Right - 5, bounds.Top, bounds.Right - 5, bounds.Top + 5, paint);
        canvas.DrawLine(bounds.Left, bounds.Top + 8, bounds.Right, bounds.Top + 8, paint);

        paint.Style = SKPaintStyle.Fill;
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
                canvas.DrawCircle(bounds.Left + 4 + col * 6, bounds.Top + 12 + row * 4, 1, paint);
    }

    private void DrawCalendar(SKCanvas canvas, SKRect bounds)
    {
        var calendarRect = GetCalendarRect(bounds);

        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(calendarRect.Left + 2, calendarRect.Top + 2, calendarRect.Right + 2, calendarRect.Bottom + 2), CornerRadius), shadowPaint);

        using var bgPaint = new SKPaint { Color = CalendarBackgroundColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(calendarRect, CornerRadius), bgPaint);

        using var borderPaint = new SKPaint { Color = BorderColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(calendarRect, CornerRadius), borderPaint);

        DrawCalendarHeader(canvas, new SKRect(calendarRect.Left, calendarRect.Top, calendarRect.Right, calendarRect.Top + HeaderHeight));
        DrawWeekdayHeaders(canvas, new SKRect(calendarRect.Left, calendarRect.Top + HeaderHeight, calendarRect.Right, calendarRect.Top + HeaderHeight + 30));
        DrawDays(canvas, new SKRect(calendarRect.Left, calendarRect.Top + HeaderHeight + 30, calendarRect.Right, calendarRect.Bottom));
    }

    private void DrawCalendarHeader(SKCanvas canvas, SKRect bounds)
    {
        using var headerPaint = new SKPaint { Color = HeaderColor, Style = SKPaintStyle.Fill };
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + CornerRadius * 2), CornerRadius));
        canvas.DrawRect(bounds, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top + CornerRadius, bounds.Right, bounds.Bottom), headerPaint);

        using var font = new SKFont(SKTypeface.Default, 16);
        using var textPaint = new SKPaint(font) { Color = SKColors.White, IsAntialias = true };
        var monthYear = _displayMonth.ToString("MMMM yyyy");
        var textBounds = new SKRect();
        textPaint.MeasureText(monthYear, ref textBounds);
        canvas.DrawText(monthYear, bounds.MidX - textBounds.MidX, bounds.MidY - textBounds.MidY, textPaint);

        using var arrowPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        using var leftPath = new SKPath();
        leftPath.MoveTo(bounds.Left + 26, bounds.MidY - 6);
        leftPath.LineTo(bounds.Left + 20, bounds.MidY);
        leftPath.LineTo(bounds.Left + 26, bounds.MidY + 6);
        canvas.DrawPath(leftPath, arrowPaint);

        using var rightPath = new SKPath();
        rightPath.MoveTo(bounds.Right - 26, bounds.MidY - 6);
        rightPath.LineTo(bounds.Right - 20, bounds.MidY);
        rightPath.LineTo(bounds.Right - 26, bounds.MidY + 6);
        canvas.DrawPath(rightPath, arrowPaint);
    }

    private void DrawWeekdayHeaders(SKCanvas canvas, SKRect bounds)
    {
        var dayNames = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        var cellWidth = bounds.Width / 7;
        using var font = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint(font) { Color = new SKColor(0x80, 0x80, 0x80), IsAntialias = true };
        for (int i = 0; i < 7; i++)
        {
            var textBounds = new SKRect();
            paint.MeasureText(dayNames[i], ref textBounds);
            canvas.DrawText(dayNames[i], bounds.Left + i * cellWidth + cellWidth / 2 - textBounds.MidX, bounds.MidY - textBounds.MidY, paint);
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
            var cellRect = new SKRect(bounds.Left + col * cellWidth + 2, bounds.Top + row * cellHeight + 2, bounds.Left + (col + 1) * cellWidth - 2, bounds.Top + (row + 1) * cellHeight - 2);

            var isSelected = dayDate.Date == Date.Date;
            var isToday = dayDate.Date == today;
            var isDisabled = dayDate < MinimumDate || dayDate > MaximumDate;

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

        if (IsOpen)
        {
            // Use ScreenBounds for popup coordinate calculations (accounts for scroll offset)
            var screenBounds = ScreenBounds;
            var calendarRect = GetCalendarRect(screenBounds);

            // Check if click is in header area (navigation arrows)
            var headerRect = new SKRect(calendarRect.Left, calendarRect.Top, calendarRect.Right, calendarRect.Top + HeaderHeight);
            if (headerRect.Contains(e.X, e.Y))
            {
                if (e.X < calendarRect.Left + 40) { _displayMonth = _displayMonth.AddMonths(-1); Invalidate(); return; }
                if (e.X > calendarRect.Right - 40) { _displayMonth = _displayMonth.AddMonths(1); Invalidate(); return; }
                return;
            }

            // Check if click is in days area
            var daysTop = calendarRect.Top + HeaderHeight + 30;
            var daysRect = new SKRect(calendarRect.Left, daysTop, calendarRect.Right, calendarRect.Bottom);
            if (daysRect.Contains(e.X, e.Y))
            {
                var cellWidth = CalendarWidth / 7;
                var cellHeight = (CalendarHeight - HeaderHeight - 40) / 6;
                var col = (int)((e.X - calendarRect.Left) / cellWidth);
                var row = (int)((e.Y - daysTop) / cellHeight);
                var firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
                var dayIndex = row * 7 + col - (int)firstDay.DayOfWeek + 1;
                var daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
                if (dayIndex >= 1 && dayIndex <= daysInMonth)
                {
                    var selectedDate = new DateTime(_displayMonth.Year, _displayMonth.Month, dayIndex);
                    if (selectedDate >= MinimumDate && selectedDate <= MaximumDate)
                    {
                        Date = selectedDate;
                        IsOpen = false;
                    }
                }
                return;
            }

            // Click is outside calendar - check if it's on the picker itself
            if (screenBounds.Contains(e.X, e.Y))
            {
                IsOpen = false;
            }
        }
        else IsOpen = true;
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;
        switch (e.Key)
        {
            case Key.Enter: case Key.Space: IsOpen = !IsOpen; e.Handled = true; break;
            case Key.Escape: if (IsOpen) { IsOpen = false; e.Handled = true; } break;
            case Key.Left: Date = Date.AddDays(-1); e.Handled = true; break;
            case Key.Right: Date = Date.AddDays(1); e.Handled = true; break;
            case Key.Up: Date = Date.AddDays(-7); e.Handled = true; break;
            case Key.Down: Date = Date.AddDays(7); e.Handled = true; break;
        }
        Invalidate();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        // Close popup when focus is lost (clicking outside)
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200, 40);
    }

    /// <summary>
    /// Override to include calendar popup area in hit testing.
    /// </summary>
    protected override bool HitTestPopupArea(float x, float y)
    {
        // Use ScreenBounds for hit testing (accounts for scroll offset)
        var screenBounds = ScreenBounds;

        // Always include the picker button itself
        if (screenBounds.Contains(x, y))
            return true;

        // When open, also include the calendar area (with edge detection)
        if (_isOpen)
        {
            var calendarRect = GetCalendarRect(screenBounds);
            return calendarRect.Contains(x, y);
        }

        return false;
    }
}
