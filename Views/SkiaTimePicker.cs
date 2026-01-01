// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered time picker control with clock popup.
/// </summary>
public class SkiaTimePicker : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty TimeProperty =
        BindableProperty.Create(nameof(Time), typeof(TimeSpan), typeof(SkiaTimePicker), DateTime.Now.TimeOfDay, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).OnTimePropertyChanged());

    public static readonly BindableProperty FormatProperty =
        BindableProperty.Create(nameof(Format), typeof(string), typeof(SkiaTimePicker), "t",
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(SKColor), typeof(SkiaTimePicker), SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(SKColor), typeof(SkiaTimePicker), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty ClockBackgroundColorProperty =
        BindableProperty.Create(nameof(ClockBackgroundColor), typeof(SKColor), typeof(SkiaTimePicker), SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty ClockFaceColorProperty =
        BindableProperty.Create(nameof(ClockFaceColor), typeof(SKColor), typeof(SkiaTimePicker), new SKColor(0xF5, 0xF5, 0xF5),
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty SelectedColorProperty =
        BindableProperty.Create(nameof(SelectedColor), typeof(SKColor), typeof(SkiaTimePicker), new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty HeaderColorProperty =
        BindableProperty.Create(nameof(HeaderColor), typeof(SKColor), typeof(SkiaTimePicker), new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(float), typeof(SkiaTimePicker), 14f,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).InvalidateMeasure());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(float), typeof(SkiaTimePicker), 4f,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    #endregion

    #region Properties

    public TimeSpan Time
    {
        get => (TimeSpan)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
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

    public SKColor ClockBackgroundColor
    {
        get => (SKColor)GetValue(ClockBackgroundColorProperty);
        set => SetValue(ClockBackgroundColorProperty, value);
    }

    public SKColor ClockFaceColor
    {
        get => (SKColor)GetValue(ClockFaceColorProperty);
        set => SetValue(ClockFaceColorProperty, value);
    }

    public SKColor SelectedColor
    {
        get => (SKColor)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public SKColor HeaderColor
    {
        get => (SKColor)GetValue(HeaderColorProperty);
        set => SetValue(HeaderColorProperty, value);
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
                    RegisterPopupOverlay(this, DrawClockOverlay);
                else
                    UnregisterPopupOverlay(this);
                Invalidate();
            }
        }
    }

    #endregion

    private bool _isOpen;
    private int _selectedHour;
    private int _selectedMinute;
    private bool _isSelectingHours = true;

    private const float ClockSize = 280;
    private const float ClockRadius = 100;
    private const float HeaderHeight = 80;
    private const float PopupHeight = ClockSize + HeaderHeight;

    public event EventHandler? TimeSelected;

    /// <summary>
    /// Gets the clock popup rectangle with edge detection applied.
    /// </summary>
    private SKRect GetPopupRect(SKRect pickerBounds)
    {
        // Get window dimensions for edge detection
        var windowWidth = LinuxApplication.Current?.MainWindow?.Width ?? 800;
        var windowHeight = LinuxApplication.Current?.MainWindow?.Height ?? 600;

        // Calculate default position (below the picker)
        var popupLeft = pickerBounds.Left;
        var popupTop = pickerBounds.Bottom + 4;

        // Edge detection: adjust horizontal position if popup would go off-screen
        if (popupLeft + ClockSize > windowWidth)
        {
            popupLeft = windowWidth - ClockSize - 4;
        }
        if (popupLeft < 0) popupLeft = 4;

        // Edge detection: show above if popup would go off-screen vertically
        if (popupTop + PopupHeight > windowHeight)
        {
            popupTop = pickerBounds.Top - PopupHeight - 4;
        }
        if (popupTop < 0) popupTop = 4;

        return new SKRect(popupLeft, popupTop, popupLeft + ClockSize, popupTop + PopupHeight);
    }

    public SkiaTimePicker()
    {
        IsFocusable = true;
        _selectedHour = DateTime.Now.Hour;
        _selectedMinute = DateTime.Now.Minute;
    }

    private void OnTimePropertyChanged()
    {
        _selectedHour = Time.Hours;
        _selectedMinute = Time.Minutes;
        TimeSelected?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void DrawClockOverlay(SKCanvas canvas)
    {
        if (!_isOpen) return;
        // Use ScreenBounds for popup drawing (accounts for scroll offset)
        DrawClockPopup(canvas, ScreenBounds);
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
            Color = IsFocused ? SelectedColor : BorderColor,
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
        var timeText = DateTime.Today.Add(Time).ToString(Format);
        var textBounds = new SKRect();
        textPaint.MeasureText(timeText, ref textBounds);
        canvas.DrawText(timeText, bounds.Left + 12, bounds.MidY - textBounds.MidY, textPaint);

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
        var radius = Math.Min(bounds.Width, bounds.Height) / 2 - 2;
        canvas.DrawCircle(bounds.MidX, bounds.MidY, radius, paint);
        canvas.DrawLine(bounds.MidX, bounds.MidY, bounds.MidX, bounds.MidY - radius * 0.5f, paint);
        canvas.DrawLine(bounds.MidX, bounds.MidY, bounds.MidX + radius * 0.4f, bounds.MidY, paint);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(bounds.MidX, bounds.MidY, 1.5f, paint);
    }

    private void DrawClockPopup(SKCanvas canvas, SKRect bounds)
    {
        var popupRect = GetPopupRect(bounds);

        using var shadowPaint = new SKPaint { Color = new SKColor(0, 0, 0, 40), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4), Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(popupRect.Left + 2, popupRect.Top + 2, popupRect.Right + 2, popupRect.Bottom + 2), CornerRadius), shadowPaint);

        using var bgPaint = new SKPaint { Color = ClockBackgroundColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, CornerRadius), bgPaint);

        using var borderPaint = new SKPaint { Color = BorderColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, CornerRadius), borderPaint);

        DrawTimeHeader(canvas, new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + HeaderHeight));
        DrawClockFace(canvas, new SKRect(popupRect.Left, popupRect.Top + HeaderHeight, popupRect.Right, popupRect.Bottom));
    }

    private void DrawTimeHeader(SKCanvas canvas, SKRect bounds)
    {
        using var headerPaint = new SKPaint { Color = HeaderColor, Style = SKPaintStyle.Fill };
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + CornerRadius * 2), CornerRadius));
        canvas.DrawRect(bounds, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top + CornerRadius, bounds.Right, bounds.Bottom), headerPaint);

        using var font = new SKFont(SKTypeface.Default, 32);
        using var selectedPaint = new SKPaint(font) { Color = SKColors.White, IsAntialias = true };
        using var unselectedPaint = new SKPaint(font) { Color = new SKColor(255, 255, 255, 150), IsAntialias = true };

        var hourText = _selectedHour.ToString("D2");
        var minuteText = _selectedMinute.ToString("D2");
        var hourPaint = _isSelectingHours ? selectedPaint : unselectedPaint;
        var minutePaint = _isSelectingHours ? unselectedPaint : selectedPaint;

        var hourBounds = new SKRect(); var colonBounds = new SKRect(); var minuteBounds = new SKRect();
        hourPaint.MeasureText(hourText, ref hourBounds);
        selectedPaint.MeasureText(":", ref colonBounds);
        minutePaint.MeasureText(minuteText, ref minuteBounds);

        var totalWidth = hourBounds.Width + colonBounds.Width + minuteBounds.Width + 8;
        var startX = bounds.MidX - totalWidth / 2;
        var centerY = bounds.MidY - hourBounds.MidY;

        canvas.DrawText(hourText, startX, centerY, hourPaint);
        canvas.DrawText(":", startX + hourBounds.Width + 4, centerY, selectedPaint);
        canvas.DrawText(minuteText, startX + hourBounds.Width + colonBounds.Width + 8, centerY, minutePaint);
    }

    private void DrawClockFace(SKCanvas canvas, SKRect bounds)
    {
        var centerX = bounds.MidX;
        var centerY = bounds.MidY;

        using var facePaint = new SKPaint { Color = ClockFaceColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(centerX, centerY, ClockRadius + 20, facePaint);

        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font) { Color = TextColor, IsAntialias = true };

        if (_isSelectingHours)
        {
            for (int i = 1; i <= 12; i++)
            {
                var angle = (i * 30 - 90) * Math.PI / 180;
                var x = centerX + (float)(ClockRadius * Math.Cos(angle));
                var y = centerY + (float)(ClockRadius * Math.Sin(angle));
                var isSelected = (_selectedHour % 12 == i % 12);
                if (isSelected)
                {
                    using var selBgPaint = new SKPaint { Color = SelectedColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawCircle(x, y, 18, selBgPaint);
                    textPaint.Color = SKColors.White;
                }
                else textPaint.Color = TextColor;
                var textBounds = new SKRect();
                textPaint.MeasureText(i.ToString(), ref textBounds);
                canvas.DrawText(i.ToString(), x - textBounds.MidX, y - textBounds.MidY, textPaint);
            }
            DrawClockHand(canvas, centerX, centerY, (_selectedHour % 12) * 30 - 90, ClockRadius - 18);
        }
        else
        {
            for (int i = 0; i < 12; i++)
            {
                var minute = i * 5;
                var angle = (minute * 6 - 90) * Math.PI / 180;
                var x = centerX + (float)(ClockRadius * Math.Cos(angle));
                var y = centerY + (float)(ClockRadius * Math.Sin(angle));
                var isSelected = (_selectedMinute / 5 == i);
                if (isSelected)
                {
                    using var selBgPaint = new SKPaint { Color = SelectedColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawCircle(x, y, 18, selBgPaint);
                    textPaint.Color = SKColors.White;
                }
                else textPaint.Color = TextColor;
                var textBounds = new SKRect();
                textPaint.MeasureText(minute.ToString("D2"), ref textBounds);
                canvas.DrawText(minute.ToString("D2"), x - textBounds.MidX, y - textBounds.MidY, textPaint);
            }
            DrawClockHand(canvas, centerX, centerY, _selectedMinute * 6 - 90, ClockRadius - 18);
        }
    }

    private void DrawClockHand(SKCanvas canvas, float centerX, float centerY, float angleDegrees, float length)
    {
        var angle = angleDegrees * Math.PI / 180;
        using var handPaint = new SKPaint { Color = SelectedColor, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawLine(centerX, centerY, centerX + (float)(length * Math.Cos(angle)), centerY + (float)(length * Math.Sin(angle)), handPaint);
        handPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(centerX, centerY, 6, handPaint);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (IsOpen)
        {
            // Use ScreenBounds for popup coordinate calculations (accounts for scroll offset)
            var screenBounds = ScreenBounds;
            var popupRect = GetPopupRect(screenBounds);

            // Check if click is in header area
            var headerRect = new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + HeaderHeight);
            if (headerRect.Contains(e.X, e.Y))
            {
                _isSelectingHours = e.X < popupRect.Left + ClockSize / 2;
                Invalidate();
                return;
            }

            // Check if click is in clock face area
            var clockCenterX = popupRect.Left + ClockSize / 2;
            var clockCenterY = popupRect.Top + HeaderHeight + ClockSize / 2;
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
                    if (Time.Hours >= 12 && _selectedHour != 12) _selectedHour += 12;
                    else if (Time.Hours < 12 && _selectedHour == 12) _selectedHour = 0;
                    _isSelectingHours = false;
                }
                else
                {
                    _selectedMinute = ((int)Math.Round(angle / 6) % 60);
                    Time = new TimeSpan(_selectedHour, _selectedMinute, 0);
                    IsOpen = false;
                }
                Invalidate();
                return;
            }

            // Click is outside clock - check if it's on the picker itself to toggle
            if (screenBounds.Contains(e.X, e.Y))
            {
                IsOpen = false;
            }
        }
        else
        {
            IsOpen = true;
            _isSelectingHours = true;
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

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Enter: case Key.Space:
                if (IsOpen) { if (_isSelectingHours) _isSelectingHours = false; else { Time = new TimeSpan(_selectedHour, _selectedMinute, 0); IsOpen = false; } }
                else { IsOpen = true; _isSelectingHours = true; }
                e.Handled = true; break;
            case Key.Escape: if (IsOpen) { IsOpen = false; e.Handled = true; } break;
            case Key.Up: if (_isSelectingHours) _selectedHour = (_selectedHour + 1) % 24; else _selectedMinute = (_selectedMinute + 1) % 60; e.Handled = true; break;
            case Key.Down: if (_isSelectingHours) _selectedHour = (_selectedHour - 1 + 24) % 24; else _selectedMinute = (_selectedMinute - 1 + 60) % 60; e.Handled = true; break;
            case Key.Left: case Key.Right: _isSelectingHours = !_isSelectingHours; e.Handled = true; break;
        }
        Invalidate();
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        return new SKSize(availableSize.Width < float.MaxValue ? Math.Min(availableSize.Width, 200) : 200, 40);
    }

    /// <summary>
    /// Override to include clock popup area in hit testing.
    /// </summary>
    protected override bool HitTestPopupArea(float x, float y)
    {
        // Use ScreenBounds for hit testing (accounts for scroll offset)
        var screenBounds = ScreenBounds;

        // Always include the picker button itself
        if (screenBounds.Contains(x, y))
            return true;

        // When open, also include the clock popup area (with edge detection)
        if (_isOpen)
        {
            var popupRect = GetPopupRect(screenBounds);
            return popupRect.Contains(x, y);
        }

        return false;
    }
}
