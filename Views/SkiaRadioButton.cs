// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered radio button control with full XAML styling support.
/// </summary>
public class SkiaRadioButton : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(SkiaRadioButton), false, BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).OnIsCheckedChanged());

    public static readonly BindableProperty ContentProperty =
        BindableProperty.Create(nameof(Content), typeof(string), typeof(SkiaRadioButton), "",
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(object), typeof(SkiaRadioButton), null);

    public static readonly BindableProperty GroupNameProperty =
        BindableProperty.Create(nameof(GroupName), typeof(string), typeof(SkiaRadioButton), null,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).OnGroupNameChanged((string?)o, (string?)n));

    public static readonly BindableProperty RadioColorProperty =
        BindableProperty.Create(nameof(RadioColor), typeof(SKColor), typeof(SkiaRadioButton), new SKColor(0x21, 0x96, 0xF3),
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty UncheckedColorProperty =
        BindableProperty.Create(nameof(UncheckedColor), typeof(SKColor), typeof(SkiaRadioButton), new SKColor(0x75, 0x75, 0x75),
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(SKColor), typeof(SkiaRadioButton), SKColors.Black,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty DisabledColorProperty =
        BindableProperty.Create(nameof(DisabledColor), typeof(SKColor), typeof(SkiaRadioButton), new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).Invalidate());

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(float), typeof(SkiaRadioButton), 14f,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty RadioSizeProperty =
        BindableProperty.Create(nameof(RadioSize), typeof(float), typeof(SkiaRadioButton), 20f,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(float), typeof(SkiaRadioButton), 8f,
            propertyChanged: (b, o, n) => ((SkiaRadioButton)b).InvalidateMeasure());

    #endregion

    #region Properties

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string Content
    {
        get => (string)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? GroupName
    {
        get => (string?)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public SKColor RadioColor
    {
        get => (SKColor)GetValue(RadioColorProperty);
        set => SetValue(RadioColorProperty, value);
    }

    public SKColor UncheckedColor
    {
        get => (SKColor)GetValue(UncheckedColorProperty);
        set => SetValue(UncheckedColorProperty, value);
    }

    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public SKColor DisabledColor
    {
        get => (SKColor)GetValue(DisabledColorProperty);
        set => SetValue(DisabledColorProperty, value);
    }

    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public float RadioSize
    {
        get => (float)GetValue(RadioSizeProperty);
        set => SetValue(RadioSizeProperty, value);
    }

    public float Spacing
    {
        get => (float)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    #endregion

    private static readonly Dictionary<string, List<WeakReference<SkiaRadioButton>>> _groups = new();

    public event EventHandler? CheckedChanged;

    public SkiaRadioButton()
    {
        IsFocusable = true;
    }

    private void OnIsCheckedChanged()
    {
        if (IsChecked && !string.IsNullOrEmpty(GroupName))
        {
            UncheckOthersInGroup();
        }
        CheckedChanged?.Invoke(this, EventArgs.Empty);
        SkiaVisualStateManager.GoToState(this, IsChecked ? SkiaVisualStateManager.CommonStates.Checked : SkiaVisualStateManager.CommonStates.Unchecked);
        Invalidate();
    }

    private void OnGroupNameChanged(string? oldValue, string? newValue)
    {
        RemoveFromGroup(oldValue);
        AddToGroup(newValue);
    }

    private void AddToGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;

        if (!_groups.TryGetValue(groupName, out var group))
        {
            group = new List<WeakReference<SkiaRadioButton>>();
            _groups[groupName] = group;
        }

        group.RemoveAll(wr => !wr.TryGetTarget(out _));
        group.Add(new WeakReference<SkiaRadioButton>(this));
    }

    private void RemoveFromGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;

        if (_groups.TryGetValue(groupName, out var group))
        {
            group.RemoveAll(wr => !wr.TryGetTarget(out var target) || target == this);
            if (group.Count == 0) _groups.Remove(groupName);
        }
    }

    private void UncheckOthersInGroup()
    {
        if (string.IsNullOrEmpty(GroupName)) return;

        if (_groups.TryGetValue(GroupName, out var group))
        {
            foreach (var weakRef in group)
            {
                if (weakRef.TryGetTarget(out var radioButton) && radioButton != this && radioButton.IsChecked)
                {
                    radioButton.SetValue(IsCheckedProperty, false);
                }
            }
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var radioRadius = RadioSize / 2;
        var radioCenterX = bounds.Left + radioRadius;
        var radioCenterY = bounds.MidY;

        using var outerPaint = new SKPaint
        {
            Color = IsEnabled ? (IsChecked ? RadioColor : UncheckedColor) : DisabledColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius - 1, outerPaint);

        if (IsChecked)
        {
            using var innerPaint = new SKPaint
            {
                Color = IsEnabled ? RadioColor : DisabledColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius - 5, innerPaint);
        }

        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = RadioColor.WithAlpha(80),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius + 4, focusPaint);
        }

        if (!string.IsNullOrEmpty(Content))
        {
            using var font = new SKFont(SKTypeface.Default, FontSize);
            using var textPaint = new SKPaint(font)
            {
                Color = IsEnabled ? TextColor : DisabledColor,
                IsAntialias = true
            };

            var textX = bounds.Left + RadioSize + Spacing;
            var textBounds = new SKRect();
            textPaint.MeasureText(Content, ref textBounds);
            canvas.DrawText(Content, textX, bounds.MidY - textBounds.MidY, textPaint);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        if (!IsChecked) IsChecked = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Space || e.Key == Key.Enter)
        {
            if (!IsChecked) IsChecked = true;
            e.Handled = true;
        }
    }

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var textWidth = 0f;
        if (!string.IsNullOrEmpty(Content))
        {
            using var font = new SKFont(SKTypeface.Default, FontSize);
            using var paint = new SKPaint(font);
            textWidth = paint.MeasureText(Content) + Spacing;
        }
        return new SKSize(RadioSize + textWidth, Math.Max(RadioSize, FontSize * 1.5f));
    }
}
