// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The kind of MAUI cell a <see cref="SkiaCellView"/> renders. Drives the
/// default layout: text and detail, an inline editor, a trailing switch, a
/// leading image, arbitrary hosted content, or a group header band.
/// </summary>
public enum SkiaCellKind
{
    Text,
    Entry,
    Switch,
    Image,
    View,
    GroupHeader
}

/// <summary>
/// One row of a <see cref="SkiaTableView"/> or a ListView: draws the cell's
/// text and detail (and image) itself and hosts the cell's interactive
/// parts as children — an inline editor (EntryCell), a trailing accessory
/// (SwitchCell) or the whole content (ViewCell). Row taps are raised by the
/// owning list; a cell only forwards them through <see cref="Tapped"/>.
/// </summary>
public class SkiaCellView : SkiaLayoutView
{
    public const float DefaultHeight = 44f;
    public const float DetailHeight = 60f;
    public const float GroupHeaderHeight = 32f;
    private const float HorizontalPadding = 16f;
    private const float ImageSize = 32f;

    private string _text = string.Empty;
    private string? _detail;
    private SkiaView? _content;
    private SkiaView? _accessory;
    private SkiaView? _editor;
    private SKBitmap? _image;
    private Color? _textColor;
    private Color? _detailColor;
    private SkiaCellKind _kind = SkiaCellKind.Text;
    private float _rowHeight;
    private FontAttributes _fontAttributes = FontAttributes.None;

    /// <summary>The MAUI cell this row renders, when created from one.</summary>
    public object? Cell { get; set; }

    /// <summary>Called when the row is detached from its cell (unsubscribes cell events).</summary>
    public Action? Detach { get; set; }

    public SkiaCellKind Kind
    {
        get => _kind;
        set { _kind = value; InvalidateMeasure(); }
    }

    public string Text
    {
        get => _text;
        set { _text = value ?? string.Empty; Invalidate(); }
    }

    public string? Detail
    {
        get => _detail;
        set { _detail = value; InvalidateMeasure(); }
    }

    public Color? TextColor
    {
        get => _textColor;
        set { _textColor = value; Invalidate(); }
    }

    public Color? DetailColor
    {
        get => _detailColor;
        set { _detailColor = value; Invalidate(); }
    }

    public FontAttributes FontAttributes
    {
        get => _fontAttributes;
        set { _fontAttributes = value; Invalidate(); }
    }

    /// <summary>Leading image (ImageCell); drawn at 32x32.</summary>
    public SKBitmap? Image
    {
        get => _image;
        set { _image = value; Invalidate(); }
    }

    /// <summary>
    /// Fixed row height. Zero (the default) sizes the row to its content:
    /// the hosted content's height, or 44 / 60 for single / two-line text.
    /// </summary>
    public float RowHeight
    {
        get => _rowHeight;
        set { _rowHeight = Math.Max(0f, value); InvalidateMeasure(); }
    }

    /// <summary>Content filling the row (ViewCell).</summary>
    public SkiaView? Content
    {
        get => _content;
        set => Swap(ref _content, value);
    }

    /// <summary>Trailing accessory, vertically centred (SwitchCell's switch).</summary>
    public SkiaView? Accessory
    {
        get => _accessory;
        set => Swap(ref _accessory, value);
    }

    /// <summary>Inline editor filling the space right of the label (EntryCell's entry).</summary>
    public SkiaView? Editor
    {
        get => _editor;
        set => Swap(ref _editor, value);
    }

    /// <summary>Raised by the owning list when the row is tapped.</summary>
    public event EventHandler? Tapped;

    /// <summary>Raises <see cref="Tapped"/>; called by the owning list.</summary>
    public void RaiseTapped() => Tapped?.Invoke(this, EventArgs.Empty);

    private void Swap(ref SkiaView? field, SkiaView? value)
    {
        if (ReferenceEquals(field, value)) return;
        if (field != null) RemoveChild(field);
        field = value;
        if (field != null) AddChild(field);
        InvalidateMeasure();
    }

    /// <summary>Natural height for the row's text configuration.</summary>
    private float NaturalHeight => Kind switch
    {
        SkiaCellKind.GroupHeader => GroupHeaderHeight,
        _ => string.IsNullOrEmpty(Detail) ? DefaultHeight : DetailHeight
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) || availableSize.Width >= double.MaxValue
            ? 300
            : availableSize.Width;

        double height;
        if (_content != null)
        {
            var desired = _content.Measure(new Size(width, double.PositiveInfinity));
            height = double.IsInfinity(desired.Height) || desired.Height <= 0 ? DefaultHeight : desired.Height;
        }
        else
        {
            height = NaturalHeight;
            if (_editor != null)
            {
                var desired = _editor.Measure(new Size(width, double.PositiveInfinity));
                if (!double.IsInfinity(desired.Height))
                    height = Math.Max(height, desired.Height + 8);
            }
            if (_accessory != null)
            {
                var desired = _accessory.Measure(new Size(width, double.PositiveInfinity));
                if (!double.IsInfinity(desired.Height))
                    height = Math.Max(height, desired.Height + 8);
            }
        }

        if (_rowHeight > 0) height = _rowHeight;
        return new Size(width, height);
    }

    protected override Rect ArrangeOverride(Rect bounds)
    {
        if (_content != null)
        {
            _content.Arrange(bounds);
        }

        if (_accessory != null)
        {
            var desired = _accessory.DesiredSize;
            if (desired.Width <= 0 || desired.Height <= 0)
                desired = _accessory.Measure(new Size(bounds.Width, bounds.Height));
            double w = Math.Min(desired.Width, bounds.Width);
            double h = Math.Min(desired.Height, bounds.Height);
            _accessory.Arrange(new Rect(bounds.Right - HorizontalPadding - w, bounds.Top + (bounds.Height - h) / 2, w, h));
        }

        if (_editor != null)
        {
            float labelWidth = MeasureLabelWidth();
            double left = bounds.Left + HorizontalPadding + (labelWidth > 0 ? labelWidth + 12 : 0);
            double width = Math.Max(0, bounds.Right - HorizontalPadding - left);
            var desired = _editor.DesiredSize;
            if (desired.Height <= 0)
                desired = _editor.Measure(new Size(width, bounds.Height));
            double h = Math.Min(Math.Max(desired.Height, 32), bounds.Height);
            _editor.Arrange(new Rect(left, bounds.Top + (bounds.Height - h) / 2, width, h));
        }

        return bounds;
    }

    private float MeasureLabelWidth()
    {
        if (string.IsNullOrEmpty(Text)) return 0f;
        using var font = SkiaFontFactory.Create(15f);
        return font.MeasureText(Text);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (BackgroundColor != null && BackgroundColor != Colors.Transparent)
        {
            using var bgPaint = new SKPaint { Color = GetEffectiveBackgroundColor(), Style = SKPaintStyle.Fill };
            canvas.DrawRect(bounds, bgPaint);
        }

        if (_content == null)
        {
            DrawText(canvas, bounds);
        }

        foreach (var child in Children)
        {
            if (child.IsVisible)
                child.Draw(canvas);
        }
    }

    private void DrawText(SKCanvas canvas, SKRect bounds)
    {
        float textLeft = bounds.Left + HorizontalPadding;

        if (_image != null)
        {
            float imageTop = bounds.MidY - ImageSize / 2;
            using var imagePaint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(_image, new SKRect(textLeft, imageTop, textLeft + ImageSize, imageTop + ImageSize), imagePaint);
            textLeft += ImageSize + 12;
        }

        bool header = Kind == SkiaCellKind.GroupHeader;
        float textSize = header ? 13f : 15f;
        using var font = SkiaFontFactory.Create(textSize);
        font.Embolden = header || (_fontAttributes & FontAttributes.Bold) != 0;
        var effectiveColor = IsEnabled
            ? (_textColor?.ToSKColor() ?? SkiaTheme.CurrentTextSK)
            : SkiaTheme.TextTertiarySK;
        using var textPaint = new SKPaint { Color = effectiveColor, IsAntialias = true };

        string text = header ? Text.ToUpperInvariant() : Text;
        bool twoLine = !header && !string.IsNullOrEmpty(Detail);

        if (twoLine)
        {
            float textBaseline = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.Top + bounds.Height * 0.36f);
            canvas.DrawText(text, textLeft, textBaseline, font, textPaint);

            using var detailFont = SkiaFontFactory.Create(12f);
            var detailColor = IsEnabled
                ? (_detailColor?.ToSKColor() ?? SkiaTheme.TextSecondarySK)
                : SkiaTheme.TextTertiarySK;
            using var detailPaint = new SKPaint { Color = detailColor, IsAntialias = true };
            float detailBaseline = TextRenderingHelper.BaselineForVerticalCenter(detailFont, bounds.Top + bounds.Height * 0.70f);
            canvas.DrawText(Detail!, textLeft, detailBaseline, detailFont, detailPaint);
        }
        else
        {
            float baseline = TextRenderingHelper.BaselineForVerticalCenter(font, bounds.MidY);
            canvas.DrawText(text, textLeft, baseline, font, textPaint);
        }
    }
}
