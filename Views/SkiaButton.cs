// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered button control matching the .NET MAUI Button API.
/// </summary>
public class SkiaButton : SkiaView, IButtonController
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaButton),
        string.Empty,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnTextChanged());

    /// <summary>
    /// Bindable property for TextColor.
    /// Default is null to match MAUI Button.TextColor (falls back to platform default).
    /// </summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(SkiaButton),
        null,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(
        nameof(CharacterSpacing),
        typeof(double),
        typeof(SkiaButton),
        0.0,
        propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(SkiaButton),
        string.Empty,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(SkiaButton),
        14.0,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontAttributes.
    /// </summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes),
        typeof(FontAttributes),
        typeof(SkiaButton),
        FontAttributes.None,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontAutoScalingEnabled.
    /// </summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(
        nameof(FontAutoScalingEnabled),
        typeof(bool),
        typeof(SkiaButton),
        true,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for TextTransform.
    /// </summary>
    public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(
        nameof(TextTransform),
        typeof(TextTransform),
        typeof(SkiaButton),
        TextTransform.Default,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor),
        typeof(Color),
        typeof(SkiaButton),
        null,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderWidth.
    /// Default is -1 to match MAUI Button.BorderWidth (unset/platform default).
    /// </summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(
        nameof(BorderWidth),
        typeof(double),
        typeof(SkiaButton),
        -1.0,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(int),
        typeof(SkiaButton),
        -1,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static new readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding),
        typeof(Thickness),
        typeof(SkiaButton),
        new Thickness(14, 10),
        propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for Command.
    /// </summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(SkiaButton),
        null,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnCommandChanged((ICommand?)o, (ICommand?)n));

    /// <summary>
    /// Bindable property for CommandParameter.
    /// </summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter),
        typeof(object),
        typeof(SkiaButton),
        null);

    /// <summary>
    /// Bindable property for ImageSource.
    /// </summary>
    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
        nameof(ImageSource),
        typeof(ImageSource),
        typeof(SkiaButton),
        null,
        propertyChanged: (b, o, n) => ((SkiaButton)b).OnImageSourceChanged());

    /// <summary>
    /// Bindable property for ContentLayout.
    /// </summary>
    public static readonly BindableProperty ContentLayoutProperty = BindableProperty.Create(
        nameof(ContentLayout),
        typeof(ButtonContentLayout),
        typeof(SkiaButton),
        new ButtonContentLayout(ButtonContentLayout.ImagePosition.Left, 10),
        propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for LineBreakMode.
    /// </summary>
    public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(
        nameof(LineBreakMode),
        typeof(LineBreakMode),
        typeof(SkiaButton),
        LineBreakMode.NoWrap,
        propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    #endregion

    #region Fields

    private bool _focusFromKeyboard;
    private SKBitmap? _loadedImage;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the text displayed on the button.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the color of the text.
    /// Null means use platform default (white on buttons for Linux).
    /// </summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the spacing between characters in the text.
    /// </summary>
    public double CharacterSpacing
    {
        get => (double)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the font attributes (bold, italic).
    /// </summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether font auto-scaling is enabled.
    /// </summary>
    public bool FontAutoScalingEnabled
    {
        get => (bool)GetValue(FontAutoScalingEnabledProperty);
        set => SetValue(FontAutoScalingEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the text transform.
    /// </summary>
    public TextTransform TextTransform
    {
        get => (TextTransform)GetValue(TextTransformProperty);
        set => SetValue(TextTransformProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border width.
    /// </summary>
    public double BorderWidth
    {
        get => (double)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public int CornerRadius
    {
        get => (int)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public new Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when clicked.
    /// </summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the parameter passed to the command.
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// Gets or sets the image source.
    /// </summary>
    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the content layout (image position and spacing).
    /// </summary>
    public ButtonContentLayout ContentLayout
    {
        get => (ButtonContentLayout)GetValue(ContentLayoutProperty);
        set => SetValue(ContentLayoutProperty, value);
    }

    /// <summary>
    /// Gets or sets the line break mode.
    /// </summary>
    public LineBreakMode LineBreakMode
    {
        get => (LineBreakMode)GetValue(LineBreakModeProperty);
        set => SetValue(LineBreakModeProperty, value);
    }

    /// <summary>
    /// Gets whether the button is currently pressed.
    /// </summary>
    public bool IsPressed { get; private set; }

    /// <summary>
    /// Gets whether the pointer is over the button.
    /// </summary>
    public bool IsPointerOver { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the button is clicked.
    /// </summary>
    public event EventHandler? Clicked;

    /// <summary>
    /// Occurs when the button is pressed.
    /// </summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// Occurs when the button is released.
    /// </summary>
    public event EventHandler? Released;

    #endregion

    #region Constructor

    public SkiaButton()
    {
        IsFocusable = true;
    }

    #endregion

    #region IButtonController

    void IButtonController.SendClicked() => OnClicked();
    void IButtonController.SendPressed() => OnPressed();
    void IButtonController.SendReleased() => OnReleased();

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

    private void OnImageSourceChanged()
    {
        // Load the image asynchronously
        LoadImageAsync();
        InvalidateMeasure();
        Invalidate();
    }

    private async void LoadImageAsync()
    {
        _loadedImage = null;
        if (ImageSource == null) return;

        try
        {
            // Handle FileImageSource
            if (ImageSource is FileImageSource fileSource)
            {
                var path = fileSource.File;
                if (System.IO.File.Exists(path))
                {
                    _loadedImage = SKBitmap.Decode(path);
                }
            }
            // Handle StreamImageSource
            else if (ImageSource is StreamImageSource streamSource)
            {
                var stream = await streamSource.Stream(System.Threading.CancellationToken.None);
                if (stream != null)
                {
                    _loadedImage = SKBitmap.Decode(stream);
                }
            }
            // Handle UriImageSource
            else if (ImageSource is UriImageSource uriSource)
            {
                using var client = new System.Net.Http.HttpClient();
                var data = await client.GetByteArrayAsync(uriSource.Uri);
                _loadedImage = SKBitmap.Decode(data);
            }

            Invalidate();
        }
        catch
        {
            // Image loading failed - leave as null
        }
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

    private void OnClicked()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    private void OnPressed()
    {
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    private void OnReleased()
    {
        Released?.Invoke(this, EventArgs.Empty);
    }

    private string ApplyTextTransform(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        return TextTransform switch
        {
            TextTransform.Uppercase => text.ToUpperInvariant(),
            TextTransform.Lowercase => text.ToLowerInvariant(),
            _ => text
        };
    }

    private SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }

    private float GetEffectiveCornerRadius()
    {
        // MAUI uses -1 to mean "use default" which is typically 5
        return CornerRadius < 0 ? 5f : CornerRadius;
    }

    #endregion

    #region Drawing

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // BackgroundColor is inherited from SkiaView as SKColor
        var bgColor = BackgroundColor;
        bool hasBackground = bgColor.Alpha > 0;

        // Determine current state color
        SKColor currentBgColor;
        if (!IsEnabled)
        {
            currentBgColor = hasBackground ? bgColor.WithAlpha(128) : SKColors.Transparent;
        }
        else if (IsPressed)
        {
            currentBgColor = hasBackground ? DarkenColor(bgColor, 0.2f) : new SKColor(0, 0, 0, 30);
        }
        else if (IsPointerOver)
        {
            currentBgColor = hasBackground ? LightenColor(bgColor, 0.1f) : new SKColor(0, 0, 0, 15);
        }
        else
        {
            currentBgColor = bgColor;
        }

        float cornerRadius = GetEffectiveCornerRadius();

        // Draw shadow for raised buttons
        if (IsEnabled && !IsPressed && hasBackground)
        {
            DrawButtonShadow(canvas, bounds, cornerRadius);
        }

        var roundRect = new SKRoundRect(bounds, cornerRadius);

        // Draw background
        if (currentBgColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint
            {
                Color = currentBgColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(roundRect, bgPaint);
        }

        // Draw border
        if (BorderWidth > 0 && BorderColor != null)
        {
            using var borderPaint = new SKPaint
            {
                Color = ToSKColor(BorderColor),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)BorderWidth
            };
            canvas.DrawRoundRect(roundRect, borderPaint);
        }

        // Draw focus ring
        if (IsFocused && _focusFromKeyboard)
        {
            using var focusPaint = new SKPaint
            {
                Color = new SKColor(33, 150, 243, 128),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f
            };
            var focusRect = new SKRoundRect(bounds, cornerRadius + 2f);
            focusRect.Inflate(2f, 2f);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }

        // Draw content (text and/or image)
        DrawContent(canvas, bounds);
    }

    private void DrawContent(SKCanvas canvas, SKRect bounds)
    {
        var padding = Padding;
        var contentBounds = new SKRect(
            bounds.Left + (float)padding.Left,
            bounds.Top + (float)padding.Top,
            bounds.Right - (float)padding.Right,
            bounds.Bottom - (float)padding.Bottom);

        // Prepare font
        bool isBold = FontAttributes.HasFlag(FontAttributes.Bold);
        bool isItalic = FontAttributes.HasFlag(FontAttributes.Italic);

        var fontStyle = new SKFontStyle(
            isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;
        float fontSize = FontSize > 0 ? (float)FontSize : 14f;

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, fontStyle) ?? SKTypeface.Default,
            fontSize);

        // Prepare text color (null means use platform default: white for buttons)
        var textColor = TextColor != null ? ToSKColor(TextColor) : SKColors.White;
        if (!IsEnabled)
        {
            textColor = textColor.WithAlpha(128);
        }

        using var textPaint = new SKPaint(font)
        {
            Color = textColor,
            IsAntialias = true
        };

        string displayText = ApplyTextTransform(Text);
        bool hasText = !string.IsNullOrEmpty(displayText);
        bool hasImage = _loadedImage != null;

        // Measure text
        var textBounds = new SKRect();
        float textWidth = 0;
        float textHeight = 0;
        if (hasText)
        {
            textPaint.MeasureText(displayText, ref textBounds);
            textWidth = textBounds.Width;
            if (CharacterSpacing != 0 && displayText.Length > 1)
            {
                textWidth += (float)(CharacterSpacing * (displayText.Length - 1));
            }
            textHeight = textBounds.Height;
        }

        // Measure image
        float imageWidth = 0;
        float imageHeight = 0;
        if (hasImage)
        {
            float maxImageSize = Math.Min(contentBounds.Height, 24f);
            float scale = Math.Min(maxImageSize / _loadedImage!.Width, maxImageSize / _loadedImage.Height);
            imageWidth = _loadedImage.Width * scale;
            imageHeight = _loadedImage.Height * scale;
        }

        // Get layout settings
        var layout = ContentLayout;
        float spacing = (float)layout.Spacing;
        bool isHorizontal = layout.Position == ButtonContentLayout.ImagePosition.Left ||
                           layout.Position == ButtonContentLayout.ImagePosition.Right;

        // Calculate total content size
        float totalWidth, totalHeight;
        if (hasImage && hasText)
        {
            if (isHorizontal)
            {
                totalWidth = imageWidth + spacing + textWidth;
                totalHeight = Math.Max(imageHeight, textHeight);
            }
            else
            {
                totalWidth = Math.Max(imageWidth, textWidth);
                totalHeight = imageHeight + spacing + textHeight;
            }
        }
        else if (hasImage)
        {
            totalWidth = imageWidth;
            totalHeight = imageHeight;
        }
        else
        {
            totalWidth = textWidth;
            totalHeight = textHeight;
        }

        // Calculate starting position (centered)
        float startX = contentBounds.MidX - totalWidth / 2;
        float startY = contentBounds.MidY - totalHeight / 2;

        // Draw based on layout position
        if (hasImage && hasText)
        {
            float imageX, imageY, textX, textY;

            switch (layout.Position)
            {
                case ButtonContentLayout.ImagePosition.Top:
                    imageX = contentBounds.MidX - imageWidth / 2;
                    imageY = startY;
                    textX = contentBounds.MidX - textWidth / 2;
                    textY = startY + imageHeight + spacing - textBounds.Top;
                    break;

                case ButtonContentLayout.ImagePosition.Bottom:
                    textX = contentBounds.MidX - textWidth / 2;
                    textY = startY - textBounds.Top;
                    imageX = contentBounds.MidX - imageWidth / 2;
                    imageY = startY + textHeight + spacing;
                    break;

                case ButtonContentLayout.ImagePosition.Right:
                    textX = startX;
                    textY = contentBounds.MidY - textBounds.MidY;
                    imageX = startX + textWidth + spacing;
                    imageY = contentBounds.MidY - imageHeight / 2;
                    break;

                default: // Left
                    imageX = startX;
                    imageY = contentBounds.MidY - imageHeight / 2;
                    textX = startX + imageWidth + spacing;
                    textY = contentBounds.MidY - textBounds.MidY;
                    break;
            }

            // Draw image
            var imageRect = new SKRect(imageX, imageY, imageX + imageWidth, imageY + imageHeight);
            using var imagePaint = new SKPaint { IsAntialias = true };
            if (!IsEnabled)
            {
                imagePaint.ColorFilter = SKColorFilter.CreateBlendMode(
                    new SKColor(128, 128, 128, 128), SKBlendMode.Modulate);
            }
            canvas.DrawBitmap(_loadedImage!, imageRect, imagePaint);

            // Draw text
            DrawTextWithSpacing(canvas, displayText, textX, textY, textPaint);
        }
        else if (hasImage)
        {
            float imageX = contentBounds.MidX - imageWidth / 2;
            float imageY = contentBounds.MidY - imageHeight / 2;
            var imageRect = new SKRect(imageX, imageY, imageX + imageWidth, imageY + imageHeight);
            using var imagePaint = new SKPaint { IsAntialias = true };
            if (!IsEnabled)
            {
                imagePaint.ColorFilter = SKColorFilter.CreateBlendMode(
                    new SKColor(128, 128, 128, 128), SKBlendMode.Modulate);
            }
            canvas.DrawBitmap(_loadedImage!, imageRect, imagePaint);
        }
        else if (hasText)
        {
            float textX = contentBounds.MidX - textWidth / 2;
            float textY = contentBounds.MidY - textBounds.MidY;
            DrawTextWithSpacing(canvas, displayText, textX, textY, textPaint);
        }
    }

    private void DrawTextWithSpacing(SKCanvas canvas, string text, float x, float y, SKPaint paint)
    {
        if (CharacterSpacing == 0 || string.IsNullOrEmpty(text) || text.Length <= 1)
        {
            canvas.DrawText(text, x, y, paint);
            return;
        }

        // Draw each character with spacing
        float currentX = x;
        foreach (char c in text)
        {
            string charStr = c.ToString();
            canvas.DrawText(charStr, currentX, y, paint);
            currentX += paint.MeasureText(charStr) + (float)CharacterSpacing;
        }
    }

    private void DrawButtonShadow(SKCanvas canvas, SKRect bounds, float cornerRadius)
    {
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 50),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
        };

        var shadowRect = new SKRoundRect(
            new SKRect(bounds.Left + 2f, bounds.Top + 4f, bounds.Right + 2f, bounds.Bottom + 4f),
            cornerRadius);
        canvas.DrawRoundRect(shadowRect, shadowPaint);
    }

    private SKColor DarkenColor(SKColor color, float amount)
    {
        return new SKColor(
            (byte)Math.Max(0, color.Red * (1 - amount)),
            (byte)Math.Max(0, color.Green * (1 - amount)),
            (byte)Math.Max(0, color.Blue * (1 - amount)),
            color.Alpha);
    }

    private SKColor LightenColor(SKColor color, float amount)
    {
        return new SKColor(
            (byte)Math.Min(255, color.Red + (255 - color.Red) * amount),
            (byte)Math.Min(255, color.Green + (255 - color.Green) * amount),
            (byte)Math.Min(255, color.Blue + (255 - color.Blue) * amount),
            color.Alpha);
    }

    #endregion

    #region Pointer Events

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsPointerOver = true;
            SkiaVisualStateManager.GoToState(this, "PointerOver");
            Invalidate();
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsPointerOver = false;
        if (IsPressed)
        {
            IsPressed = false;
        }
        SkiaVisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled");
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            IsPressed = true;
            _focusFromKeyboard = false;
            SkiaVisualStateManager.GoToState(this, "Pressed");
            Invalidate();
            OnPressed();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (IsEnabled)
        {
            bool wasPressed = IsPressed;
            IsPressed = false;
            SkiaVisualStateManager.GoToState(this, IsPointerOver ? "PointerOver" : "Normal");
            Invalidate();
            OnReleased();
            if (wasPressed)
            {
                OnClicked();
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
            OnPressed();
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
                OnReleased();
                OnClicked();
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
        var padding = Padding;
        float paddingH = (float)(padding.Left + padding.Right);
        float paddingV = (float)(padding.Top + padding.Bottom);
        float fontSize = FontSize > 0 ? (float)FontSize : 14f;

        // Prepare font for measurement
        bool isBold = FontAttributes.HasFlag(FontAttributes.Bold);
        bool isItalic = FontAttributes.HasFlag(FontAttributes.Italic);

        var fontStyle = new SKFontStyle(
            isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var fontFamily = string.IsNullOrEmpty(FontFamily) ? "Sans" : FontFamily;

        using var font = new SKFont(
            SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(fontFamily, fontStyle) ?? SKTypeface.Default,
            fontSize);

        using var paint = new SKPaint(font);

        string displayText = ApplyTextTransform(Text);
        bool hasText = !string.IsNullOrEmpty(displayText);
        bool hasImage = _loadedImage != null;

        float textWidth = 0, textHeight = 0;
        if (hasText)
        {
            var textBounds = new SKRect();
            paint.MeasureText(displayText, ref textBounds);
            textWidth = textBounds.Width;
            if (CharacterSpacing != 0 && displayText.Length > 1)
            {
                textWidth += (float)(CharacterSpacing * (displayText.Length - 1));
            }
            textHeight = textBounds.Height;
        }

        float imageWidth = 0, imageHeight = 0;
        if (hasImage)
        {
            float maxImageSize = 24f;
            float scale = Math.Min(maxImageSize / _loadedImage!.Width, maxImageSize / _loadedImage.Height);
            imageWidth = _loadedImage.Width * scale;
            imageHeight = _loadedImage.Height * scale;
        }

        float width, height;
        var layout = ContentLayout;
        bool isHorizontal = layout.Position == ButtonContentLayout.ImagePosition.Left ||
                           layout.Position == ButtonContentLayout.ImagePosition.Right;

        if (hasImage && hasText)
        {
            if (isHorizontal)
            {
                width = imageWidth + (float)layout.Spacing + textWidth;
                height = Math.Max(imageHeight, textHeight);
            }
            else
            {
                width = Math.Max(imageWidth, textWidth);
                height = imageHeight + (float)layout.Spacing + textHeight;
            }
        }
        else if (hasImage)
        {
            width = imageWidth;
            height = imageHeight;
        }
        else if (hasText)
        {
            width = textWidth;
            height = textHeight;
        }
        else
        {
            width = 40f;
            height = fontSize;
        }

        width += paddingH;
        height += paddingV;

        // Respect explicit size requests
        if (WidthRequest >= 0)
        {
            width = (float)WidthRequest;
        }
        if (HeightRequest >= 0)
        {
            height = (float)HeightRequest;
        }

        return new SKSize(Math.Max(width, 44f), Math.Max(height, 30f));
    }

    #endregion
}

/// <summary>
/// Specifies the position of the image and the spacing between image and text on a Button.
/// </summary>
public class ButtonContentLayout
{
    /// <summary>
    /// Specifies the position of the image relative to the text.
    /// </summary>
    public enum ImagePosition
    {
        Left,
        Top,
        Right,
        Bottom
    }

    /// <summary>
    /// Gets the position of the image.
    /// </summary>
    public ImagePosition Position { get; }

    /// <summary>
    /// Gets the spacing between the image and text.
    /// </summary>
    public double Spacing { get; }

    /// <summary>
    /// Creates a new ButtonContentLayout.
    /// </summary>
    public ButtonContentLayout(ImagePosition position, double spacing)
    {
        Position = position;
        Spacing = spacing;
    }
}

/// <summary>
/// Interface for button controller (matches MAUI).
/// </summary>
public interface IButtonController
{
    void SendClicked();
    void SendPressed();
    void SendReleased();
}
