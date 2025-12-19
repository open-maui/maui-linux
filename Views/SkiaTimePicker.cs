// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered time picker control with clock popup.
/// </summary>
public class SkiaTimePicker : SkiaView
{
    private TimeSpan _time = DateTime.Now.TimeOfDay;
    private bool _isOpen;
    private string _format = "t";
    private int _selectedHour;
    private int _selectedMinute;
    private bool _isSelectingHours = true;

    // Styling
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor BorderColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public SKColor ClockBackgroundColor { get; set; } = SKColors.White;
    public SKColor ClockFaceColor { get; set; } = new SKColor(0xF5, 0xF5, 0xF5);
    public SKColor SelectedColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor HeaderColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public float FontSize { get; set; } = 14;
    public float CornerRadius { get; set; } = 4;

    private const float ClockSize = 280;
    private const float ClockRadius = 100;
    private const float HeaderHeight = 80;

    public TimeSpan Time
    {
        get => _time;
        set
        {
            if (_time != value)
            {
                _time = value;
                _selectedHour = _time.Hours;
                _selectedMinute = _time.Minutes;
                TimeSelected?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
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

    public event EventHandler? TimeSelected;

    public SkiaTimePicker()
    {
        IsFocusable = true;
        _selectedHour = _time.Hours;
        _selectedMinute = _time.Minutes;
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);

        if (_isOpen)
        {
            DrawClockPopup(canvas, bounds);
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
            Color = IsFocused ? SelectedColor : BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, CornerRadius), borderPaint);

        // Draw time text
        using var font = new SKFont(SKTypeface.Default, FontSize);
        using var textPaint = new SKPaint(font)
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            IsAntialias = true
        };

        var timeText = DateTime.Today.Add(_time).ToString(_format);
        var textBounds = new SKRect();
        textPaint.MeasureText(timeText, ref textBounds);

        var textX = bounds.Left + 12;
        var textY = bounds.MidY - textBounds.MidY;
        canvas.DrawText(timeText, textX, textY, textPaint);

        // Draw clock icon
        DrawClockIcon(canvas, new SKRect(bounds.Right - 36, bounds.MidY - 10, bounds.Right - 12, bounds.MidY + 10));
    }

    private void DrawClockIcon(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            Color = IsEnabled ? TextColor : TextColor.WithAlpha(128),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        var centerX = bounds.MidX;
        var centerY = bounds.MidY;
        var radius = Math.Min(bounds.Width, bounds.Height) / 2 - 2;

        // Clock circle
        canvas.DrawCircle(centerX, centerY, radius, paint);

        // Hour hand
        canvas.DrawLine(centerX, centerY, centerX, centerY - radius * 0.5f, paint);

        // Minute hand
        canvas.DrawLine(centerX, centerY, centerX + radius * 0.4f, centerY, paint);

        // Center dot
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(centerX, centerY, 1.5f, paint);
    }

    private void DrawClockPopup(SKCanvas canvas, SKRect bounds)
    {
        var popupRect = new SKRect(
            bounds.Left,
            bounds.Bottom + 4,
            bounds.Left + ClockSize,
            bounds.Bottom + 4 + HeaderHeight + ClockSize);

        // Draw shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 40),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(popupRect.Left + 2, popupRect.Top + 2, popupRect.Right + 2, popupRect.Bottom + 2), CornerRadius), shadowPaint);

        // Draw background
        using var bgPaint = new SKPaint
        {
            Color = ClockBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, CornerRadius), bgPaint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, CornerRadius), borderPaint);

        // Draw header with time display
        DrawTimeHeader(canvas, new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + HeaderHeight));

        // Draw clock face
        DrawClockFace(canvas, new SKRect(popupRect.Left, popupRect.Top + HeaderHeight, popupRect.Right, popupRect.Bottom));
    }

    private void DrawTimeHeader(SKCanvas canvas, SKRect bounds)
    {
        // Draw header background
        using var headerPaint = new SKPaint
        {
            Color = HeaderColor,
            Style = SKPaintStyle.Fill
        };

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + CornerRadius * 2), CornerRadius));
        canvas.DrawRect(bounds, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top + CornerRadius, bounds.Right, bounds.Bottom), headerPaint);

        // Draw time display
        using var font = new SKFont(SKTypeface.Default, 32);
        using var selectedPaint = new SKPaint(font)
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        using var unselectedPaint = new SKPaint(font)
        {
            Color = new SKColor(255, 255, 255, 150),
            IsAntialias = true
        };

        var hourText = _selectedHour.ToString("D2");
        var minuteText = _selectedMinute.ToString("D2");
        var colonText = ":";

        var hourPaint = _isSelectingHours ? selectedPaint : unselectedPaint;
        var minutePaint = _isSelectingHours ? unselectedPaint : selectedPaint;

        var hourBounds = new SKRect();
        var colonBounds = new SKRect();
        var minuteBounds = new SKRect();
        hourPaint.MeasureText(hourText, ref hourBounds);
        selectedPaint.MeasureText(colonText, ref colonBounds);
        minutePaint.MeasureText(minuteText, ref minuteBounds);

        var totalWidth = hourBounds.Width + colonBounds.Width + minuteBounds.Width + 8;
        var startX = bounds.MidX - totalWidth / 2;
        var centerY = bounds.MidY - hourBounds.MidY;

        canvas.DrawText(hourText, startX, centerY, hourPaint);
        canvas.DrawText(colonText, startX + hourBounds.Width + 4, centerY, selectedPaint);
        canvas.DrawText(minuteText, startX + hourBounds.Width + colonBounds.Width + 8, centerY, minutePaint);
    }

    private void DrawClockFace(SKCanvas canvas, SKRect bounds)
    {
        var centerX = bounds.MidX;
        var centerY = bounds.MidY;

        // Draw clock face background
        using var facePaint = new SKPaint
        {
            Color = ClockFaceColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(centerX, centerY, ClockRadius + 20, facePaint);

        // Draw numbers
        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font)
        {
            Color = TextColor,
            IsAntialias = true
        };

        if (_isSelectingHours)
        {
            // Draw hour numbers (1-12)
            for (int i = 1; i <= 12; i++)
            {
                var angle = (i * 30 - 90) * Math.PI / 180;
                var x = centerX + (float)(ClockRadius * Math.Cos(angle));
                var y = centerY + (float)(ClockRadius * Math.Sin(angle));

                var numText = i.ToString();
                var textBounds = new SKRect();
                textPaint.MeasureText(numText, ref textBounds);

                var isSelected = (_selectedHour % 12 == i % 12);
                if (isSelected)
                {
                    using var selectedBgPaint = new SKPaint
                    {
                        Color = SelectedColor,
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    canvas.DrawCircle(x, y, 18, selectedBgPaint);
                    textPaint.Color = SKColors.White;
                }
                else
                {
                    textPaint.Color = TextColor;
                }

                canvas.DrawText(numText, x - textBounds.MidX, y - textBounds.MidY, textPaint);
            }

            // Draw center point and hand
            DrawClockHand(canvas, centerX, centerY, (_selectedHour % 12) * 30 - 90, ClockRadius - 18);
        }
        else
        {
            // Draw minute numbers (0, 5, 10, ... 55)
            for (int i = 0; i < 12; i++)
            {
                var minute = i * 5;
                var angle = (minute * 6 - 90) * Math.PI / 180;
                var x = centerX + (float)(ClockRadius * Math.Cos(angle));
                var y = centerY + (float)(ClockRadius * Math.Sin(angle));

                var numText = minute.ToString("D2");
                var textBounds = new SKRect();
                textPaint.MeasureText(numText, ref textBounds);

                var isSelected = (_selectedMinute / 5 == i);
                if (isSelected)
                {
                    using var selectedBgPaint = new SKPaint
                    {
                        Color = SelectedColor,
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    canvas.DrawCircle(x, y, 18, selectedBgPaint);
                    textPaint.Color = SKColors.White;
                }
                else
                {
                    textPaint.Color = TextColor;
                }

                canvas.DrawText(numText, x - textBounds.MidX, y - textBounds.MidY, textPaint);
            }

            // Draw center point and hand
            DrawClockHand(canvas, centerX, centerY, _selectedMinute * 6 - 90, ClockRadius - 18);
        }
    }

    private void DrawClockHand(SKCanvas canvas, float centerX, float centerY, float angleDegrees, float length)
    {
        var angle = angleDegrees * Math.PI / 180;
        var endX = centerX + (float)(length * Math.Cos(angle));
        var endY = centerY + (float)(length * Math.Sin(angle));

        using var handPaint = new SKPaint
        {
            Color = SelectedColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawLine(centerX, centerY, endX, endY, handPaint);

        // Center dot
        handPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(centerX, centerY, 6, handPaint);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (_isOpen)
        {
            var popupTop = Bounds.Bottom + 4;
            var popupLeft = Bounds.Left;

            // Check header click (toggle hours/minutes)
            if (e.Y >= popupTop && e.Y < popupTop + HeaderHeight)
            {
                var centerX = popupLeft + ClockSize / 2;
                if (e.X < centerX)
                {
                    _isSelectingHours = true;
                }
                else
                {
                    _isSelectingHours = false;
                }
                Invalidate();
                return;
            }

            // Check clock face click
            var clockCenterX = popupLeft + ClockSize / 2;
            var clockCenterY = popupTop + HeaderHeight + ClockSize / 2;

            var dx = e.X - clockCenterX;
            var dy = e.Y - clockCenterY;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance <= ClockRadius + 20)
            {
                var angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
                if (angle < 0) angle += 360;

                if (_isSelectingHours)
                {
                    _selectedHour = ((int)Math.Round(angle / 30) % 12);
                    if (_selectedHour == 0) _selectedHour = 12;
                    // Preserve AM/PM
                    if (_time.Hours >= 12 && _selectedHour != 12)
                        _selectedHour += 12;
                    else if (_time.Hours < 12 && _selectedHour == 12)
                        _selectedHour = 0;

                    _isSelectingHours = false; // Move to minutes
                }
                else
                {
                    _selectedMinute = ((int)Math.Round(angle / 6) % 60);
                    // Apply the time
                    Time = new TimeSpan(_selectedHour, _selectedMinute, 0);
                    _isOpen = false;
                }
                Invalidate();
                return;
            }

            // Click outside popup - close
            if (e.Y < popupTop)
            {
                _isOpen = false;
            }
        }
        else
        {
            _isOpen = true;
            _isSelectingHours = true;
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
                if (_isOpen)
                {
                    if (_isSelectingHours)
                    {
                        _isSelectingHours = false;
                    }
                    else
                    {
                        Time = new TimeSpan(_selectedHour, _selectedMinute, 0);
                        _isOpen = false;
                    }
                }
                else
                {
                    _isOpen = true;
                    _isSelectingHours = true;
                }
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isOpen)
                {
                    _isOpen = false;
                    e.Handled = true;
                }
                break;

            case Key.Up:
                if (_isSelectingHours)
                {
                    _selectedHour = (_selectedHour + 1) % 24;
                }
                else
                {
                    _selectedMinute = (_selectedMinute + 1) % 60;
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (_isSelectingHours)
                {
                    _selectedHour = (_selectedHour - 1 + 24) % 24;
                }
                else
                {
                    _selectedMinute = (_selectedMinute - 1 + 60) % 60;
                }
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Right:
                _isSelectingHours = !_isSelectingHours;
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
