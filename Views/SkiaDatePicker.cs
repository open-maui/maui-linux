// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered date picker control with calendar popup.
/// Implements MAUI IDatePicker interface patterns.
/// </summary>
public class SkiaDatePicker : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty DateProperty =
        BindableProperty.Create(nameof(Date), typeof(DateTime), typeof(SkiaDatePicker), DateTime.Today, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).OnDatePropertyChanged((DateTime)o, (DateTime)n));

    public static readonly BindableProperty MinimumDateProperty =
        BindableProperty.Create(nameof(MinimumDate), typeof(DateTime), typeof(SkiaDatePicker), new DateTime(1900, 1, 1), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty MaximumDateProperty =
        BindableProperty.Create(nameof(MaximumDate), typeof(DateTime), typeof(SkiaDatePicker), new DateTime(2100, 12, 31), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty FormatProperty =
        BindableProperty.Create(nameof(Format), typeof(string), typeof(SkiaDatePicker), "d", BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkiaDatePicker), Colors.Black, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkiaDatePicker), Color.FromRgb(189, 189, 189), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty CalendarBackgroundColorProperty =
        BindableProperty.Create(nameof(CalendarBackgroundColor), typeof(Color), typeof(SkiaDatePicker), Colors.White, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty SelectedDayColorProperty =
        BindableProperty.Create(nameof(SelectedDayColor), typeof(Color), typeof(SkiaDatePicker), Color.FromRgb(33, 150, 243), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty TodayColorProperty =
        BindableProperty.Create(nameof(TodayColor), typeof(Color), typeof(SkiaDatePicker), Color.FromRgba(33, 150, 243, 64), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty HeaderColorProperty =
        BindableProperty.Create(nameof(HeaderColor), typeof(Color), typeof(SkiaDatePicker), Color.FromRgb(33, 150, 243), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty DisabledDayColorProperty =
        BindableProperty.Create(nameof(DisabledDayColor), typeof(Color), typeof(SkiaDatePicker), Color.FromRgb(189, 189, 189), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkiaDatePicker), string.Empty, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkiaDatePicker), 14.0, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).InvalidateMeasure());

    public static readonly BindableProperty FontAttributesProperty =
        BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkiaDatePicker), FontAttributes.None, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkiaDatePicker), 0.0, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkiaDatePicker), 4.0, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaDatePicker)b).Invalidate());

    #endregion

    #region Fields

    private DateTime _displayMonth;
    private bool _isOpen;

    private const float CalendarWidth = 280f;
    private const float CalendarHeight = 320f;
    private const float HeaderHeight = 48f;

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

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public Color CalendarBackgroundColor
    {
        get => (Color)GetValue(CalendarBackgroundColorProperty);
        set => SetValue(CalendarBackgroundColorProperty, value);
    }

    public Color SelectedDayColor
    {
        get => (Color)GetValue(SelectedDayColorProperty);
        set => SetValue(SelectedDayColorProperty, value);
    }

    public Color TodayColor
    {
        get => (Color)GetValue(TodayColorProperty);
        set => SetValue(TodayColorProperty, value);
    }

    public Color HeaderColor
    {
        get => (Color)GetValue(HeaderColorProperty);
        set => SetValue(HeaderColorProperty, value);
    }

    public Color DisabledDayColor
    {
        get => (Color)GetValue(DisabledDayColorProperty);
        set => SetValue(DisabledDayColorProperty, value);
    }

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    public double CharacterSpacing
    {
        get => (double)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
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
                    SkiaView.RegisterPopupOverlay(this, DrawCalendarOverlay);
                else
                    SkiaView.UnregisterPopupOverlay(this);
                Invalidate();
            }
        }
    }

    #endregion

    #region Events

    public event EventHandler<DateChangedEventArgs>? DateSelected;

    #endregion

    #region Constructor

    public SkiaDatePicker()
    {
        IsFocusable = true;
        _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor.
    /// </summary>
    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }

    /// <summary>
    /// Converts a MAUI Color to SKColor with modified alpha.
    /// </summary>
    private static SKColor ToSKColorWithAlpha(Color? color, byte alpha)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            alpha);
    }

    #endregion

    #region Private Methods

    private SKRect GetCalendarRect(SKRect pickerBounds)
    {
        int windowWidth = LinuxApplication.Current?.MainWindow?.Width ?? 800;
        int windowHeight = LinuxApplication.Current?.MainWindow?.Height ?? 600;

        float calendarLeft = pickerBounds.Left;
        float calendarTop = pickerBounds.Bottom + 4f;

        if (calendarLeft + CalendarWidth > windowWidth)
        {
            calendarLeft = windowWidth - CalendarWidth - 4f;
        }
        if (calendarLeft < 0f)
        {
            calendarLeft = 4f;
        }
        if (calendarTop + CalendarHeight > windowHeight)
        {
            calendarTop = pickerBounds.Top - CalendarHeight - 4f;
        }
        if (calendarTop < 0f)
        {
            calendarTop = 4f;
        }

        return new SKRect(calendarLeft, calendarTop, calendarLeft + CalendarWidth, calendarTop + CalendarHeight);
    }

    private void OnDatePropertyChanged(DateTime oldValue, DateTime newValue)
    {
        _displayMonth = new DateTime(newValue.Year, newValue.Month, 1);
        DateSelected?.Invoke(this, new DateChangedEventArgs(oldValue, newValue));
        Invalidate();
    }

    private DateTime ClampDate(DateTime date)
    {
        if (date < MinimumDate)
            return MinimumDate;
        if (date > MaximumDate)
            return MaximumDate;
        return date;
    }

    private void DrawCalendarOverlay(SKCanvas canvas)
    {
        if (_isOpen)
        {
            DrawCalendar(canvas, ScreenBounds);
        }
    }

    private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
    {
        float cornerRadius = (float)CornerRadius;
        float fontSize = (float)FontSize;

        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? BackgroundColor : new SKColor(245, 245, 245),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), bgPaint);

        SKColor borderColor = IsFocused ? ToSKColor(SelectedDayColor) : ToSKColor(BorderColor);
        using var borderPaint = new SKPaint
        {
            Color = borderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), borderPaint);

        SKColor textColor = ToSKColor(TextColor);

        // Get typeface based on FontFamily and FontAttributes
        SKTypeface typeface = SKTypeface.Default;
        if (!string.IsNullOrEmpty(FontFamily))
        {
            var style = FontAttributes switch
            {
                FontAttributes.Bold => SKFontStyle.Bold,
                FontAttributes.Italic => SKFontStyle.Italic,
                FontAttributes.Bold | FontAttributes.Italic => SKFontStyle.BoldItalic,
                _ => SKFontStyle.Normal
            };
            typeface = SKTypeface.FromFamilyName(FontFamily, style) ?? SKTypeface.Default;
        }
        else if (FontAttributes != FontAttributes.None)
        {
            var style = FontAttributes switch
            {
                FontAttributes.Bold => SKFontStyle.Bold,
                FontAttributes.Italic => SKFontStyle.Italic,
                FontAttributes.Bold | FontAttributes.Italic => SKFontStyle.BoldItalic,
                _ => SKFontStyle.Normal
            };
            typeface = SKTypeface.FromFamilyName(null, style) ?? SKTypeface.Default;
        }

        using var font = new SKFont(typeface, fontSize, 1f, 0f);
        using var textPaint = new SKPaint(font)
        {
            Color = IsEnabled ? textColor : textColor.WithAlpha(128),
            IsAntialias = true
        };

        string dateText = Date.ToString(Format);
        SKRect textBounds = default;
        textPaint.MeasureText(dateText, ref textBounds);
        canvas.DrawText(dateText, bounds.Left + 12f, bounds.MidY - textBounds.MidY, textPaint);

        DrawCalendarIcon(canvas, new SKRect(bounds.Right - 36f, bounds.MidY - 10f, bounds.Right - 12f, bounds.MidY + 10f));
    }

    private void DrawCalendarIcon(SKCanvas canvas, SKRect bounds)
    {
        SKColor textColor = ToSKColor(TextColor);
        using var paint = new SKPaint
        {
            Color = IsEnabled ? textColor : textColor.WithAlpha(128),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        SKRect calRect = new SKRect(bounds.Left, bounds.Top + 3f, bounds.Right, bounds.Bottom);
        canvas.DrawRoundRect(new SKRoundRect(calRect, 2f), paint);
        canvas.DrawLine(bounds.Left + 5f, bounds.Top, bounds.Left + 5f, bounds.Top + 5f, paint);
        canvas.DrawLine(bounds.Right - 5f, bounds.Top, bounds.Right - 5f, bounds.Top + 5f, paint);
        canvas.DrawLine(bounds.Left, bounds.Top + 8f, bounds.Right, bounds.Top + 8f, paint);

        paint.Style = SKPaintStyle.Fill;
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                canvas.DrawCircle(bounds.Left + 4f + j * 6, bounds.Top + 12f + i * 4, 1f, paint);
            }
        }
    }

    private void DrawCalendar(SKCanvas canvas, SKRect bounds)
    {
        SKRect calendarRect = GetCalendarRect(bounds);
        float cornerRadius = (float)CornerRadius;

        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(calendarRect.Left + 2f, calendarRect.Top + 2f, calendarRect.Right + 2f, calendarRect.Bottom + 2f), cornerRadius), shadowPaint);

        using var bgPaint = new SKPaint
        {
            Color = ToSKColor(CalendarBackgroundColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(calendarRect, cornerRadius), bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = ToSKColor(BorderColor),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(calendarRect, cornerRadius), borderPaint);

        DrawCalendarHeader(canvas, new SKRect(calendarRect.Left, calendarRect.Top, calendarRect.Right, calendarRect.Top + 48f));
        DrawWeekdayHeaders(canvas, new SKRect(calendarRect.Left, calendarRect.Top + 48f, calendarRect.Right, calendarRect.Top + 48f + 30f));
        DrawDays(canvas, new SKRect(calendarRect.Left, calendarRect.Top + 48f + 30f, calendarRect.Right, calendarRect.Bottom));
    }

    private void DrawCalendarHeader(SKCanvas canvas, SKRect bounds)
    {
        float cornerRadius = (float)CornerRadius;

        using var headerPaint = new SKPaint
        {
            Color = ToSKColor(HeaderColor),
            Style = SKPaintStyle.Fill
        };

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + cornerRadius * 2f), cornerRadius), SKClipOperation.Intersect, false);
        canvas.DrawRect(bounds, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top + cornerRadius, bounds.Right, bounds.Bottom), headerPaint);

        using var font = new SKFont(SKTypeface.Default, 16f, 1f, 0f);
        using var textPaint = new SKPaint(font)
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        string monthYear = _displayMonth.ToString("MMMM yyyy");
        SKRect textBounds = default;
        textPaint.MeasureText(monthYear, ref textBounds);
        canvas.DrawText(monthYear, bounds.MidX - textBounds.MidX, bounds.MidY - textBounds.MidY, textPaint);

        using var arrowPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        using var leftPath = new SKPath();
        leftPath.MoveTo(bounds.Left + 26f, bounds.MidY - 6f);
        leftPath.LineTo(bounds.Left + 20f, bounds.MidY);
        leftPath.LineTo(bounds.Left + 26f, bounds.MidY + 6f);
        canvas.DrawPath(leftPath, arrowPaint);

        using var rightPath = new SKPath();
        rightPath.MoveTo(bounds.Right - 26f, bounds.MidY - 6f);
        rightPath.LineTo(bounds.Right - 20f, bounds.MidY);
        rightPath.LineTo(bounds.Right - 26f, bounds.MidY + 6f);
        canvas.DrawPath(rightPath, arrowPaint);
    }

    private void DrawWeekdayHeaders(SKCanvas canvas, SKRect bounds)
    {
        string[] dayNames = new string[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        float cellWidth = bounds.Width / 7f;

        using var font = new SKFont(SKTypeface.Default, 12f, 1f, 0f);
        using var paint = new SKPaint(font)
        {
            Color = new SKColor(128, 128, 128),
            IsAntialias = true
        };

        for (int i = 0; i < 7; i++)
        {
            SKRect textBounds = default;
            paint.MeasureText(dayNames[i], ref textBounds);
            canvas.DrawText(dayNames[i], bounds.Left + i * cellWidth + cellWidth / 2f - textBounds.MidX, bounds.MidY - textBounds.MidY, paint);
        }
    }

    private void DrawDays(SKCanvas canvas, SKRect bounds)
    {
        DateTime firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
        int startDayOfWeek = (int)firstDay.DayOfWeek;
        float cellWidth = bounds.Width / 7f;
        float cellHeight = (bounds.Height - 10f) / 6f;

        using var font = new SKFont(SKTypeface.Default, 14f, 1f, 0f);
        using var textPaint = new SKPaint(font)
        {
            IsAntialias = true
        };
        using var bgPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        DateTime today = DateTime.Today;
        SKRect cellRect = default;

        SKColor textColor = ToSKColor(TextColor);
        SKColor selectedDayColor = ToSKColor(SelectedDayColor);
        SKColor todayColor = ToSKColor(TodayColor);
        SKColor disabledDayColor = ToSKColor(DisabledDayColor);

        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime dayDate = new DateTime(_displayMonth.Year, _displayMonth.Month, day);
            int cellIndex = startDayOfWeek + day - 1;
            int row = cellIndex / 7;
            int col = cellIndex % 7;

            cellRect = new SKRect(
                bounds.Left + col * cellWidth + 2f,
                bounds.Top + row * cellHeight + 2f,
                bounds.Left + (col + 1) * cellWidth - 2f,
                bounds.Top + (row + 1) * cellHeight - 2f);

            bool isSelected = dayDate.Date == Date.Date;
            bool isToday = dayDate.Date == today;
            bool isDisabled = dayDate < MinimumDate || dayDate > MaximumDate;

            if (isSelected)
            {
                bgPaint.Color = selectedDayColor;
                canvas.DrawCircle(cellRect.MidX, cellRect.MidY, Math.Min(cellRect.Width, cellRect.Height) / 2f - 2f, bgPaint);
            }
            else if (isToday)
            {
                bgPaint.Color = todayColor;
                canvas.DrawCircle(cellRect.MidX, cellRect.MidY, Math.Min(cellRect.Width, cellRect.Height) / 2f - 2f, bgPaint);
            }

            textPaint.Color = isSelected ? SKColors.White : (isDisabled ? disabledDayColor : textColor);
            string dayText = day.ToString();
            SKRect dayTextBounds = default;
            textPaint.MeasureText(dayText, ref dayTextBounds);
            canvas.DrawText(dayText, cellRect.MidX - dayTextBounds.MidX, cellRect.MidY - dayTextBounds.MidY, textPaint);
        }
    }

    #endregion

    #region Overrides

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled)
            return;

        if (IsOpen)
        {
            SKRect screenBounds = ScreenBounds;
            SKRect calendarRect = GetCalendarRect(screenBounds);

            SKRect headerRect = new SKRect(calendarRect.Left, calendarRect.Top, calendarRect.Right, calendarRect.Top + 48f);
            if (headerRect.Contains(e.X, e.Y))
            {
                if (e.X < calendarRect.Left + 40f)
                {
                    _displayMonth = _displayMonth.AddMonths(-1);
                    Invalidate();
                }
                else if (e.X > calendarRect.Right - 40f)
                {
                    _displayMonth = _displayMonth.AddMonths(1);
                    Invalidate();
                }
                return;
            }

            float daysTop = calendarRect.Top + 48f + 30f;
            SKRect daysRect = new SKRect(calendarRect.Left, daysTop, calendarRect.Right, calendarRect.Bottom);
            if (daysRect.Contains(e.X, e.Y))
            {
                float cellWidth = 40f;
                float cellHeight = 38.666668f;
                int col = (int)((e.X - calendarRect.Left) / cellWidth);
                int dayIndex = (int)((e.Y - daysTop) / cellHeight) * 7 + col - (int)new DateTime(_displayMonth.Year, _displayMonth.Month, 1).DayOfWeek + 1;
                int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);

                if (dayIndex >= 1 && dayIndex <= daysInMonth)
                {
                    DateTime selectedDate = new DateTime(_displayMonth.Year, _displayMonth.Month, dayIndex);
                    if (selectedDate >= MinimumDate && selectedDate <= MaximumDate)
                    {
                        Date = selectedDate;
                        IsOpen = false;
                    }
                }
                return;
            }

            if (screenBounds.Contains(e.X, e.Y))
            {
                IsOpen = false;
            }
        }
        else
        {
            IsOpen = true;
        }
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled)
            return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                IsOpen = !IsOpen;
                e.Handled = true;
                break;
            case Key.Escape:
                if (IsOpen)
                {
                    IsOpen = false;
                    e.Handled = true;
                }
                break;
            case Key.Left:
                Date = Date.AddDays(-1.0);
                e.Handled = true;
                break;
            case Key.Right:
                Date = Date.AddDays(1.0);
                e.Handled = true;
                break;
            case Key.Up:
                Date = Date.AddDays(-7.0);
                e.Handled = true;
                break;
            case Key.Down:
                Date = Date.AddDays(7.0);
                e.Handled = true;
                break;
        }
        Invalidate();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200f) : 200f, 40f);
    }

    protected override bool HitTestPopupArea(float x, float y)
    {
        SKRect screenBounds = ScreenBounds;
        if (screenBounds.Contains(x, y))
        {
            return true;
        }
        if (_isOpen)
        {
            SKRect calendarRect = GetCalendarRect(screenBounds);
            return calendarRect.Contains(x, y);
        }
        return false;
    }

    #endregion
}
