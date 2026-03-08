// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Converters;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Event args for time picker time changes.
/// </summary>
public class TimeChangedEventArgs : EventArgs
{
    public TimeSpan OldTime { get; }
    public TimeSpan NewTime { get; }

    public TimeChangedEventArgs(TimeSpan oldTime, TimeSpan newTime)
    {
        OldTime = oldTime;
        NewTime = newTime;
    }
}

/// <summary>
/// Skia-rendered time picker control with clock popup.
/// Implements MAUI ITimePicker interface patterns.
/// </summary>
public class SkiaTimePicker : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty TimeProperty =
        BindableProperty.Create(nameof(Time), typeof(TimeSpan), typeof(SkiaTimePicker), DateTime.Now.TimeOfDay, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).OnTimePropertyChanged((TimeSpan)o, (TimeSpan)n));

    public static readonly BindableProperty FormatProperty =
        BindableProperty.Create(nameof(Format), typeof(string), typeof(SkiaTimePicker), "t", BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkiaTimePicker), Colors.Black, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkiaTimePicker), Color.FromRgb(189, 189, 189), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty ClockBackgroundColorProperty =
        BindableProperty.Create(nameof(ClockBackgroundColor), typeof(Color), typeof(SkiaTimePicker), Colors.White, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty ClockFaceColorProperty =
        BindableProperty.Create(nameof(ClockFaceColor), typeof(Color), typeof(SkiaTimePicker), Color.FromRgb(245, 245, 245), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty SelectedColorProperty =
        BindableProperty.Create(nameof(SelectedColor), typeof(Color), typeof(SkiaTimePicker), Color.FromRgb(33, 150, 243), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty HeaderColorProperty =
        BindableProperty.Create(nameof(HeaderColor), typeof(Color), typeof(SkiaTimePicker), Color.FromRgb(33, 150, 243), BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkiaTimePicker), string.Empty, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkiaTimePicker), 14.0, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).InvalidateMeasure());

    public static readonly BindableProperty FontAttributesProperty =
        BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkiaTimePicker), FontAttributes.None, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkiaTimePicker), 0.0, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaTimePicker)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkiaTimePicker), 4.0, BindingMode.TwoWay,
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

    public Color ClockBackgroundColor
    {
        get => (Color)GetValue(ClockBackgroundColorProperty);
        set => SetValue(ClockBackgroundColorProperty, value);
    }

    public Color ClockFaceColor
    {
        get => (Color)GetValue(ClockFaceColorProperty);
        set => SetValue(ClockFaceColorProperty, value);
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public Color HeaderColor
    {
        get => (Color)GetValue(HeaderColorProperty);
        set => SetValue(HeaderColorProperty, value);
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
                    RegisterPopupOverlay(this, DrawClockOverlay);
                else
                    UnregisterPopupOverlay(this);
                Invalidate();
            }
        }
    }

    #endregion

    #region Fields

    private bool _isOpen;
    private int _selectedHour;
    private int _selectedMinute;
    private bool _isSelectingHours = true;

    private const float ClockSize = 280;
    private const float ClockRadius = 100;
    private const float HeaderHeight = 80;
    private const float PopupHeight = ClockSize + HeaderHeight;

    #endregion

    #region Events

    public event EventHandler<TimeChangedEventArgs>? TimeSelected;

    #endregion

    #region Constructor

    public SkiaTimePicker()
    {
        IsFocusable = true;
        _selectedHour = DateTime.Now.Hour;
        _selectedMinute = DateTime.Now.Minute;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts a MAUI Color to SkiaSharp SKColor.
    /// </summary>
    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor();
    }

    /// <summary>
    /// Converts a MAUI Color to SKColor with modified alpha.
    /// </summary>
    private static SKColor ToSKColorWithAlpha(Color? color, byte alpha)
    {
        if (color == null) return SKColors.Transparent;
        return color.ToSKColor().WithAlpha(alpha);
    }

    /// <summary>
    /// Gets the clock popup rectangle with edge detection applied.
    /// </summary>
    private SKRect GetPopupRect(SKRect pickerBounds)
    {
        float cornerRadius = (float)CornerRadius;
        var windowWidth = LinuxApplication.Current?.MainWindow?.Width ?? 800;
        var windowHeight = LinuxApplication.Current?.MainWindow?.Height ?? 600;

        var popupLeft = pickerBounds.Left;
        var popupTop = pickerBounds.Bottom + 4;

        if (popupLeft + ClockSize > windowWidth)
        {
            popupLeft = windowWidth - ClockSize - 4;
        }
        if (popupLeft < 0) popupLeft = 4;

        if (popupTop + PopupHeight > windowHeight)
        {
            popupTop = pickerBounds.Top - PopupHeight - 4;
        }
        if (popupTop < 0) popupTop = 4;

        return new SKRect(popupLeft, popupTop, popupLeft + ClockSize, popupTop + PopupHeight);
    }

    #endregion

    #region Private Methods

    private void OnTimePropertyChanged(TimeSpan oldValue, TimeSpan newValue)
    {
        _selectedHour = newValue.Hours;
        _selectedMinute = newValue.Minutes;
        TimeSelected?.Invoke(this, new TimeChangedEventArgs(oldValue, newValue));
        Invalidate();
    }

    private void DrawClockOverlay(SKCanvas canvas)
    {
        if (!_isOpen) return;
        var sb = ScreenBounds;
        DrawClockPopup(canvas, new SKRect((float)sb.Left, (float)sb.Top, (float)sb.Right, (float)sb.Bottom));
    }

    private void DrawPickerButton(SKCanvas canvas, SKRect bounds)
    {
        float cornerRadius = (float)CornerRadius;
        float fontSize = (float)FontSize;
        bool isDark = SkiaTheme.IsDarkMode;
        SKColor textColor = isDark ? SkiaTheme.DarkTextSK : ToSKColor(TextColor);
        SKColor borderColor = isDark ? SkiaTheme.Gray600SK : ToSKColor(BorderColor);
        SKColor selectedColor = ToSKColor(SelectedColor);

        var bgColor = GetEffectiveBackgroundColor();
        if (bgColor.Alpha == 0) bgColor = isDark ? SkiaTheme.DarkSurfaceSK : SKColors.White;
        using var bgPaint = new SKPaint
        {
            Color = IsEnabled ? bgColor : (isDark ? SkiaTheme.DarkBackgroundSK : SkiaTheme.Gray100SK),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), bgPaint);

        using var borderPaint = new SKPaint
        {
            Color = IsFocused ? selectedColor : borderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = IsFocused ? 2 : 1,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(bounds, cornerRadius), borderPaint);

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

        using var font = new SKFont(typeface, fontSize);
        using var textPaint = new SKPaint(font)
        {
            Color = IsEnabled ? textColor : textColor.WithAlpha(128),
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
        SKColor textColor = SkiaTheme.IsDarkMode ? SkiaTheme.DarkTextSK : ToSKColor(TextColor);
        using var paint = new SKPaint
        {
            Color = IsEnabled ? textColor : textColor.WithAlpha(128),
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
        float cornerRadius = (float)CornerRadius;
        var popupRect = GetPopupRect(bounds);
        bool isDark = SkiaTheme.IsDarkMode;

        using var shadowPaint = new SKPaint { Color = SkiaTheme.Shadow25SK, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4), Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(popupRect.Left + 2, popupRect.Top + 2, popupRect.Right + 2, popupRect.Bottom + 2), cornerRadius), shadowPaint);

        using var bgPaint = new SKPaint { Color = isDark ? SkiaTheme.DarkBackgroundSK : ToSKColor(ClockBackgroundColor), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, cornerRadius), bgPaint);

        using var borderPaint = new SKPaint { Color = isDark ? SkiaTheme.Gray600SK : ToSKColor(BorderColor), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(popupRect, cornerRadius), borderPaint);

        DrawTimeHeader(canvas, new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + HeaderHeight));
        DrawClockFace(canvas, new SKRect(popupRect.Left, popupRect.Top + HeaderHeight, popupRect.Right, popupRect.Bottom));
    }

    private void DrawTimeHeader(SKCanvas canvas, SKRect bounds)
    {
        float cornerRadius = (float)CornerRadius;
        using var headerPaint = new SKPaint { Color = ToSKColor(HeaderColor), Style = SKPaintStyle.Fill };
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + cornerRadius * 2), cornerRadius));
        canvas.DrawRect(bounds, headerPaint);
        canvas.Restore();
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Top + cornerRadius, bounds.Right, bounds.Bottom), headerPaint);

        using var font = new SKFont(SKTypeface.Default, 32);
        using var selectedPaint = new SKPaint(font) { Color = SkiaTheme.BackgroundWhiteSK, IsAntialias = true };
        using var unselectedPaint = new SKPaint(font) { Color = SkiaTheme.WhiteSemiTransparentSK, IsAntialias = true };

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
        bool isDark = SkiaTheme.IsDarkMode;

        SKColor textColor = isDark ? SkiaTheme.DarkTextSK : ToSKColor(TextColor);
        SKColor clockFaceColor = isDark ? SkiaTheme.DarkSurfaceSK : ToSKColor(ClockFaceColor);
        SKColor selectedColor = ToSKColor(SelectedColor);

        using var facePaint = new SKPaint { Color = clockFaceColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(centerX, centerY, ClockRadius + 20, facePaint);

        using var font = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint(font) { Color = textColor, IsAntialias = true };

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
                    using var selBgPaint = new SKPaint { Color = selectedColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawCircle(x, y, 18, selBgPaint);
                    textPaint.Color = SkiaTheme.BackgroundWhiteSK;
                }
                else textPaint.Color = textColor;
                var tBounds = new SKRect();
                textPaint.MeasureText(i.ToString(), ref tBounds);
                canvas.DrawText(i.ToString(), x - tBounds.MidX, y - tBounds.MidY, textPaint);
            }
            DrawClockHand(canvas, centerX, centerY, (_selectedHour % 12) * 30 - 90, ClockRadius - 18, selectedColor);
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
                    using var selBgPaint = new SKPaint { Color = selectedColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawCircle(x, y, 18, selBgPaint);
                    textPaint.Color = SkiaTheme.BackgroundWhiteSK;
                }
                else textPaint.Color = textColor;
                var tBounds = new SKRect();
                textPaint.MeasureText(minute.ToString("D2"), ref tBounds);
                canvas.DrawText(minute.ToString("D2"), x - tBounds.MidX, y - tBounds.MidY, textPaint);
            }
            DrawClockHand(canvas, centerX, centerY, _selectedMinute * 6 - 90, ClockRadius - 18, selectedColor);
        }
    }

    private void DrawClockHand(SKCanvas canvas, float centerX, float centerY, float angleDegrees, float length, SKColor color)
    {
        var angle = angleDegrees * Math.PI / 180;
        using var handPaint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawLine(centerX, centerY, centerX + (float)(length * Math.Cos(angle)), centerY + (float)(length * Math.Sin(angle)), handPaint);
        handPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(centerX, centerY, 6, handPaint);
    }

    #endregion

    #region Overrides

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawPickerButton(canvas, bounds);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (IsOpen)
        {
            var screenBounds = ScreenBounds;
            var popupRect = GetPopupRect(new SKRect((float)screenBounds.Left, (float)screenBounds.Top, (float)screenBounds.Right, (float)screenBounds.Bottom));

            var headerRect = new SKRect(popupRect.Left, popupRect.Top, popupRect.Right, popupRect.Top + HeaderHeight);
            if (headerRect.Contains(e.X, e.Y))
            {
                _isSelectingHours = e.X < popupRect.Left + ClockSize / 2;
                Invalidate();
                return;
            }

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
            case Key.Enter:
            case Key.Space:
                if (IsOpen)
                {
                    if (_isSelectingHours) _isSelectingHours = false;
                    else { Time = new TimeSpan(_selectedHour, _selectedMinute, 0); IsOpen = false; }
                }
                else { IsOpen = true; _isSelectingHours = true; }
                e.Handled = true;
                break;
            case Key.Escape:
                if (IsOpen) { IsOpen = false; e.Handled = true; }
                break;
            case Key.Up:
                if (_isSelectingHours) _selectedHour = (_selectedHour + 1) % 24;
                else _selectedMinute = (_selectedMinute + 1) % 60;
                e.Handled = true;
                break;
            case Key.Down:
                if (_isSelectingHours) _selectedHour = (_selectedHour - 1 + 24) % 24;
                else _selectedMinute = (_selectedMinute - 1 + 60) % 60;
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

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(availableSize.Width < double.MaxValue ? Math.Min(availableSize.Width, 200) : 200, 40);
    }

    protected override bool HitTestPopupArea(float x, float y)
    {
        var screenBounds = ScreenBounds;
        if (screenBounds.Contains(x, y))
            return true;

        if (_isOpen)
        {
            var popupRect = GetPopupRect(new SKRect((float)screenBounds.Left, (float)screenBounds.Top, (float)screenBounds.Right, (float)screenBounds.Bottom));
            return popupRect.Contains(x, y);
        }

        return false;
    }

    #endregion
}
