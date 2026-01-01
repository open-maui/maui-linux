// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered button control with full XAML styling support.
/// </summary>
public class SkiaButton : SkiaView
{
    #region BindableProperties

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaButton),
        "",
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnTextChanged());

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(SKColor),
        typeof(SkiaButton),
        SKColors.White,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty ButtonBackgroundColorProperty = BindableProperty.Create(
        nameof(ButtonBackgroundColor),
        typeof(SKColor),
        typeof(SkiaButton),
        new SKColor(33, 150, 243), // Material Blue
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty PressedBackgroundColorProperty = BindableProperty.Create(
        nameof(PressedBackgroundColor),
        typeof(SKColor),
        typeof(SkiaButton),
        new SKColor(25, 118, 210),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty DisabledBackgroundColorProperty = BindableProperty.Create(
        nameof(DisabledBackgroundColor),
        typeof(SKColor),
        typeof(SkiaButton),
        new SKColor(189, 189, 189),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty HoveredBackgroundColorProperty = BindableProperty.Create(
        nameof(HoveredBackgroundColor),
        typeof(SKColor),
        typeof(SkiaButton),
        new SKColor(66, 165, 245),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor),
        typeof(SKColor),
        typeof(SkiaButton),
        SKColors.Transparent,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(SkiaButton),
        "Sans",
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(float),
        typeof(SkiaButton),
        14f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    public static readonly BindableProperty IsBoldProperty = BindableProperty.Create(
        nameof(IsBold),
        typeof(bool),
        typeof(SkiaButton),
        false,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    public static readonly BindableProperty IsItalicProperty = BindableProperty.Create(
        nameof(IsItalic),
        typeof(bool),
        typeof(SkiaButton),
        false,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(
        nameof(CharacterSpacing),
        typeof(float),
        typeof(SkiaButton),
        0f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(float),
        typeof(SkiaButton),
        4f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(
        nameof(BorderWidth),
        typeof(float),
        typeof(SkiaButton),
        0f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding),
        typeof(SKRect),
        typeof(SkiaButton),
        new SKRect(16f, 8f, 16f, 8f),
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(SkiaButton),
        null,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnCommandChanged((ICommand?)o, (ICommand?)n));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter),
        typeof(object),
        typeof(SkiaButton),
        null,
        BindingMode.TwoWay);

    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
        nameof(ImageSource),
        typeof(SKBitmap),
        typeof(SkiaButton),
        null,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    public static readonly BindableProperty ImageSpacingProperty = BindableProperty.Create(
        nameof(ImageSpacing),
        typeof(float),
        typeof(SkiaButton),
        8f,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    public static readonly BindableProperty ContentLayoutPositionProperty = BindableProperty.Create(
        nameof(ContentLayoutPosition),
        typeof(int),
        typeof(SkiaButton),
        0,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    #endregion

    #region Fields

    private bool _focusFromKeyboard;

    #endregion

    #region Properties

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public SKColor ButtonBackgroundColor
    {
        get => (SKColor)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    public SKColor PressedBackgroundColor
    {
        get => (SKColor)GetValue(PressedBackgroundColorProperty);
        set => SetValue(PressedBackgroundColorProperty, value);
    }

    public SKColor DisabledBackgroundColor
    {
        get => (SKColor)GetValue(DisabledBackgroundColorProperty);
        set => SetValue(DisabledBackgroundColorProperty, value);
    }

    public SKColor HoveredBackgroundColor
    {
        get => (SKColor)GetValue(HoveredBackgroundColorProperty);
        set => SetValue(HoveredBackgroundColorProperty, value);
    }

    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public bool IsBold
    {
        get => (bool)GetValue(IsBoldProperty);
        set => SetValue(IsBoldProperty, value);
    }

    public bool IsItalic
    {
        get => (bool)GetValue(IsItalicProperty);
        set => SetValue(IsItalicProperty, value);
    }

    public float CharacterSpacing
    {
        get => (float)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public float BorderWidth
    {
        get => (float)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    public SKRect Padding
    {
        get => (SKRect)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public SKBitmap? ImageSource
    {
        get => (SKBitmap?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public float ImageSpacing
    {
        get => (float)GetValue(ImageSpacingProperty);
        set => SetValue(ImageSpacingProperty, value);
    }

    public int ContentLayoutPosition
    {
        get => (int)GetValue(ContentLayoutPositionProperty);
        set => SetValue(ContentLayoutPositionProperty, value);
    }

    public bool IsPressed { get; private set; }

    public bool IsHovered { get; private set; }

    #endregion

    #region Events

    public event EventHandler? Clicked;

    public event EventHandler? Pressed;

    public event EventHandler? Released;

    #endregion

    #region Constructor

    public SkiaButton()
    {
        IsFocusable = true;
    }

    #endregion

    #region Private Methods

    private void OnTextChanged()
    {
        InvalidateMeasure();
        Invalidate();
    }

    private void OnFontChanged()
    {
        InvalidateMeasure();
        Invalidate();
    }

    private void OnCommandChanged(ICommand? oldCommand, ICommand? newCommand)
    {
        if (oldCommand != null)
        {
            oldCommand.CanExecuteChanged -= OnCanExecuteChanged;
        }
        if (newCommand != null)
        {
            newCommand.CanExecuteChanged += OnCanExecuteChanged;
            UpdateIsEnabled();
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateIsEnabled();
    }

    private void UpdateIsEnabled()
    {
        if (Command != null)
        {
            IsEnabled = Command.CanExecute(CommandParameter);
        }
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        SKColor buttonBackgroundColor = ButtonBackgroundColor;
        bool isTextOnly = buttonBackgroundColor.Alpha == 0;

        SKColor color;
        if (!IsEnabled)
        {
            color = isTextOnly ? SKColors.Transparent : DisabledBackgroundColor;
        }
        else if (IsPressed)
        {
            color = isTextOnly ? new SKColor(0, 0, 0, 20) : PressedBackgroundColor;
        }
        else if (IsHovered)
        {
            color = isTextOnly ? new SKColor(0, 0, 0, 10) : HoveredBackgroundColor;
        }
        else
        {
            color = ButtonBackgroundColor;
        }

        if (IsEnabled && !IsPressed && !isTextOnly)
        {
            DrawShadow(canvas, bounds);
        }

        var roundRect = new SKRoundRect(bounds, CornerRadius);

        if (color.Alpha > 0)
        {
            using var bgPaint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(roundRect, bgPaint);
        }

        if (BorderWidth > 0f && BorderColor != SKColors.Transparent)
        {
            using var borderPaint = new SKPaint
            {
                Color = BorderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderWidth
            };
            canvas.DrawRoundRect(roundRect, borderPaint);
        }

        if (IsFocused && _focusFromKeyboard)
        {
            using var focusPaint = new SKPaint
            {
                Color = new SKColor(33, 150, 243, 128),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f
            };
            var focusRect = new SKRoundRect(bounds, CornerRadius + 2f);
            focusRect.Inflate(2f, 2f);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }

        DrawContent(canvas, bounds, isTextOnly);
    }

    private void DrawContent(SKCanvas canvas, SKRect bounds, bool isTextOnly)
    {
        var style = new SKFontStyle(
            IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, style) ?? SKTypeface.Default,
            FontSize, 1f, 0f);

        SKColor textColorToUse;
        if (!IsEnabled)
        {
            textColorToUse = TextColor.WithAlpha(128);
        }
        else if (isTextOnly && (IsHovered || IsPressed))
        {
            textColorToUse = new SKColor(
                (byte)Math.Max(0, TextColor.Red - 40),
                (byte)Math.Max(0, TextColor.Green - 40),
                (byte)Math.Max(0, TextColor.Blue - 40),
                TextColor.Alpha);
        }
        else
        {
            textColorToUse = TextColor;
        }

        using var textPaint = new SKPaint(font)
        {
            Color = textColorToUse,
            IsAntialias = true
        };

        var textBounds = new SKRect();
        bool hasText = !string.IsNullOrEmpty(Text);
        if (hasText)
        {
            textPaint.MeasureText(Text, ref textBounds);
        }

        bool hasImage = ImageSource != null;
        float imageWidth = 0f;
        float imageHeight = 0f;
        if (hasImage)
        {
            float maxSize = Math.Min(bounds.Height - 8f, 24f);
            float scale = Math.Min(maxSize / ImageSource!.Width, maxSize / ImageSource.Height);
            imageWidth = ImageSource.Width * scale;
            imageHeight = ImageSource.Height * scale;
        }

        bool isHorizontal = ContentLayoutPosition == 0 || ContentLayoutPosition == 2;
        float totalWidth;
        float totalHeight;

        if (hasImage && hasText)
        {
            if (isHorizontal)
            {
                totalWidth = imageWidth + ImageSpacing + textBounds.Width;
                totalHeight = Math.Max(imageHeight, textBounds.Height);
            }
            else
            {
                totalWidth = Math.Max(imageWidth, textBounds.Width);
                totalHeight = imageHeight + ImageSpacing + textBounds.Height;
            }
        }
        else if (hasImage)
        {
            totalWidth = imageWidth;
            totalHeight = imageHeight;
        }
        else
        {
            totalWidth = textBounds.Width;
            totalHeight = textBounds.Height;
        }

        float startX = bounds.MidX - totalWidth / 2f;
        float startY = bounds.MidY - totalHeight / 2f;

        if (hasImage)
        {
            float imageX;
            float imageY;
            float textX = 0f;
            float textY = 0f;

            switch (ContentLayoutPosition)
            {
                case 1: // Top
                    imageX = bounds.MidX - imageWidth / 2f;
                    imageY = startY;
                    textX = bounds.MidX - textBounds.Width / 2f;
                    textY = startY + imageHeight + ImageSpacing - textBounds.Top;
                    break;
                case 2: // Right
                    textX = startX;
                    textY = bounds.MidY - textBounds.MidY;
                    imageX = startX + textBounds.Width + ImageSpacing;
                    imageY = bounds.MidY - imageHeight / 2f;
                    break;
                case 3: // Bottom
                    textX = bounds.MidX - textBounds.Width / 2f;
                    textY = startY - textBounds.Top;
                    imageX = bounds.MidX - imageWidth / 2f;
                    imageY = startY + textBounds.Height + ImageSpacing;
                    break;
                default: // 0 = Left
                    imageX = startX;
                    imageY = bounds.MidY - imageHeight / 2f;
                    textX = startX + imageWidth + ImageSpacing;
                    textY = bounds.MidY - textBounds.MidY;
                    break;
            }

            var imageRect = new SKRect(imageX, imageY, imageX + imageWidth, imageY + imageHeight);
            using var imagePaint = new SKPaint { IsAntialias = true };

            if (!IsEnabled)
            {
                imagePaint.ColorFilter = SKColorFilter.CreateBlendMode(
                    new SKColor(128, 128, 128, 128), SKBlendMode.Modulate);
            }

            canvas.DrawBitmap(ImageSource!, imageRect, imagePaint);

            if (hasText)
            {
                canvas.DrawText(Text!, textX, textY, textPaint);
            }
            return;
        }

        if (hasText)
        {
            float x = bounds.MidX - textBounds.MidX;
            float y = bounds.MidY - textBounds.MidY;
            canvas.DrawText(Text!, x, y, textPaint);
        }
    }

    private void DrawShadow(SKCanvas canvas, SKRect bounds)
    {
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 50),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
        };

        var shadowRect = new SKRoundRect(
            new SKRect(bounds.Left + 2f, bounds.Top + 4f, bounds.Right + 2f, bounds.Bottom + 4f),
            CornerRadius);
        canvas.DrawRoundRect(shadowRect, shadowPaint);
    }

    #endregion

    #region Pointer Events

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsHovered = true;
            SkiaVisualStateManager.GoToState(this, "PointerOver");
            Invalidate();
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        if (IsPressed)
        {
            IsPressed = false;
        }
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Console.WriteLine($"[SkiaButton] OnPointerPressed - Text='{Text}', IsEnabled={IsEnabled}");
        if (IsEnabled)
        {
            IsPressed = true;
            _focusFromKeyboard = false;
            SkiaVisualStateManager.GoToState(this, "Pressed");
            Invalidate();
            Pressed?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            bool wasPressed = IsPressed;
            IsPressed = false;
            SkiaVisualStateManager.GoToState(this, IsHovered ? "PointerOver" : "Normal");
            Invalidate();
            Released?.Invoke(this, EventArgs.Empty);
            if (wasPressed)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
                Command?.Execute(CommandParameter);
            }
        }
    }

    #endregion

    #region Keyboard Events

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (IsEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            IsPressed = true;
            _focusFromKeyboard = true;
            SkiaVisualStateManager.GoToState(this, "Pressed");
            Invalidate();
            Pressed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        if (IsEnabled && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            if (IsPressed)
            {
                IsPressed = false;
                SkiaVisualStateManager.GoToState(this, "Normal");
                Invalidate();
                Released?.Invoke(this, EventArgs.Empty);
                Clicked?.Invoke(this, EventArgs.Empty);
                Command?.Execute(CommandParameter);
            }
            e.Handled = true;
        }
    }

    #endregion

    #region State Changes

    protected override void OnEnabledChanged()
    {
        base.OnEnabledChanged();
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
    }

    #endregion

    #region Measurement

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        SKRect padding = Padding;
        float paddingLeft = float.IsNaN(padding.Left) ? 16f : padding.Left;
        float paddingRight = float.IsNaN(padding.Right) ? 16f : padding.Right;
        float paddingTop = float.IsNaN(padding.Top) ? 8f : padding.Top;
        float paddingBottom = float.IsNaN(padding.Bottom) ? 8f : padding.Bottom;
        float fontSize = (float.IsNaN(FontSize) || FontSize <= 0f) ? 14f : FontSize;

        if (string.IsNullOrEmpty(Text))
        {
            return new SKSize(paddingLeft + paddingRight + 40f, paddingTop + paddingBottom + fontSize);
        }

        var style = new SKFontStyle(
            IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, style) ?? SKTypeface.Default,
            fontSize, 1f, 0f);

        using var paint = new SKPaint(font);
        var textBounds = new SKRect();
        paint.MeasureText(Text, ref textBounds);

        float width = textBounds.Width + paddingLeft + paddingRight;
        float height = textBounds.Height + paddingTop + paddingBottom;

        if (float.IsNaN(width) || width < 0f)
        {
            width = 72f;
        }
        if (float.IsNaN(height) || height < 0f)
        {
            height = 30f;
        }

        if (WidthRequest >= 0.0)
        {
            width = (float)WidthRequest;
        }
        if (HeightRequest >= 0.0)
        {
            height = (float)HeightRequest;
        }

        return new SKSize(width, height);
    }

    #endregion
}
