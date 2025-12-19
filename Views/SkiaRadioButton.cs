// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered radio button control.
/// </summary>
public class SkiaRadioButton : SkiaView
{
    private bool _isChecked;
    private string _content = "";
    private object? _value;
    private string? _groupName;

    // Styling
    public SKColor RadioColor { get; set; } = new SKColor(0x21, 0x96, 0xF3);
    public SKColor UncheckedColor { get; set; } = new SKColor(0x75, 0x75, 0x75);
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor DisabledColor { get; set; } = new SKColor(0xBD, 0xBD, 0xBD);
    public float FontSize { get; set; } = 14;
    public float RadioSize { get; set; } = 20;
    public float Spacing { get; set; } = 8;

    // Static group management
    private static readonly Dictionary<string, List<WeakReference<SkiaRadioButton>>> _groups = new();

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;

                if (_isChecked && !string.IsNullOrEmpty(_groupName))
                {
                    UncheckOthersInGroup();
                }

                CheckedChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public string Content
    {
        get => _content;
        set { _content = value ?? ""; Invalidate(); }
    }

    public object? Value
    {
        get => _value;
        set { _value = value; }
    }

    public string? GroupName
    {
        get => _groupName;
        set
        {
            if (_groupName != value)
            {
                RemoveFromGroup();
                _groupName = value;
                AddToGroup();
            }
        }
    }

    public event EventHandler? CheckedChanged;

    public SkiaRadioButton()
    {
        IsFocusable = true;
    }

    private void AddToGroup()
    {
        if (string.IsNullOrEmpty(_groupName)) return;

        if (!_groups.TryGetValue(_groupName, out var group))
        {
            group = new List<WeakReference<SkiaRadioButton>>();
            _groups[_groupName] = group;
        }

        // Clean up dead references and add this one
        group.RemoveAll(wr => !wr.TryGetTarget(out _));
        group.Add(new WeakReference<SkiaRadioButton>(this));
    }

    private void RemoveFromGroup()
    {
        if (string.IsNullOrEmpty(_groupName)) return;

        if (_groups.TryGetValue(_groupName, out var group))
        {
            group.RemoveAll(wr => !wr.TryGetTarget(out var target) || target == this);
            if (group.Count == 0)
            {
                _groups.Remove(_groupName);
            }
        }
    }

    private void UncheckOthersInGroup()
    {
        if (string.IsNullOrEmpty(_groupName)) return;

        if (_groups.TryGetValue(_groupName, out var group))
        {
            foreach (var weakRef in group)
            {
                if (weakRef.TryGetTarget(out var radioButton) && radioButton != this)
                {
                    radioButton._isChecked = false;
                    radioButton.CheckedChanged?.Invoke(radioButton, EventArgs.Empty);
                    radioButton.Invalidate();
                }
            }
        }
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var radioRadius = RadioSize / 2;
        var radioCenterX = bounds.Left + radioRadius;
        var radioCenterY = bounds.MidY;

        // Draw outer circle
        using var outerPaint = new SKPaint
        {
            Color = IsEnabled ? (_isChecked ? RadioColor : UncheckedColor) : DisabledColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius - 1, outerPaint);

        // Draw inner circle if checked
        if (_isChecked)
        {
            using var innerPaint = new SKPaint
            {
                Color = IsEnabled ? RadioColor : DisabledColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(radioCenterX, radioCenterY, radioRadius - 5, innerPaint);
        }

        // Draw focus ring
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

        // Draw content text
        if (!string.IsNullOrEmpty(_content))
        {
            using var font = new SKFont(SKTypeface.Default, FontSize);
            using var textPaint = new SKPaint(font)
            {
                Color = IsEnabled ? TextColor : DisabledColor,
                IsAntialias = true
            };

            var textX = bounds.Left + RadioSize + Spacing;
            var textBounds = new SKRect();
            textPaint.MeasureText(_content, ref textBounds);
            canvas.DrawText(_content, textX, bounds.MidY - textBounds.MidY, textPaint);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        if (!_isChecked)
        {
            IsChecked = true;
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                if (!_isChecked)
                {
                    IsChecked = true;
                }
                e.Handled = true;
                break;
        }
    }

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var textWidth = 0f;
        if (!string.IsNullOrEmpty(_content))
        {
            using var font = new SKFont(SKTypeface.Default, FontSize);
            using var paint = new SKPaint(font);
            textWidth = paint.MeasureText(_content) + Spacing;
        }

        return new SKSize(RadioSize + textWidth, Math.Max(RadioSize, FontSize * 1.5f));
    }
}
