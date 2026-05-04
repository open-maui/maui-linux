// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered button control with full XAML styling support.
/// </summary>
public class SkiaButton : SkiaView
{
    #region BindableProperties

    /// <summary>
    /// Bindable property for Text.
    /// </summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaButton),
            "",
            propertyChanged: (b, o, n) => ((SkiaButton)b).OnTextChanged());

    /// <summary>
    /// Bindable property for TextColor.
    /// </summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(SKColor),
            typeof(SkiaButton),
            SKColors.White,
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for ButtonBackgroundColor (distinct from base BackgroundColor).
    /// </summary>
    public static readonly BindableProperty ButtonBackgroundColorProperty =
        BindableProperty.Create(
            nameof(ButtonBackgroundColor),
            typeof(SKColor),
            typeof(SkiaButton),
            new SKColor(0x21, 0x96, 0xF3), // Material Blue
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for PressedBackgroundColor.
    /// </summary>
    public static readonly BindableProperty PressedBackgroundColorProperty =
        BindableProperty.Create(
            nameof(PressedBackgroundColor),
            typeof(SKColor),
            typeof(SkiaButton),
            new SKColor(0x19, 0x76, 0xD2),
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for DisabledBackgroundColor.
    /// </summary>
    public static readonly BindableProperty DisabledBackgroundColorProperty =
        BindableProperty.Create(
            nameof(DisabledBackgroundColor),
            typeof(SKColor),
            typeof(SkiaButton),
            new SKColor(0xBD, 0xBD, 0xBD),
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for HoveredBackgroundColor.
    /// </summary>
    public static readonly BindableProperty HoveredBackgroundColorProperty =
        BindableProperty.Create(
            nameof(HoveredBackgroundColor),
            typeof(SKColor),
            typeof(SkiaButton),
            new SKColor(0x42, 0xA5, 0xF5),
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderColor.
    /// </summary>
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(
            nameof(BorderColor),
            typeof(SKColor),
            typeof(SkiaButton),
            SKColors.Transparent,
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for FontFamily.
    /// </summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaButton),
            "Sans",
            propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for FontSize.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(float),
            typeof(SkiaButton),
            14f,
            propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for IsBold.
    /// </summary>
    public static readonly BindableProperty IsBoldProperty =
        BindableProperty.Create(
            nameof(IsBold),
            typeof(bool),
            typeof(SkiaButton),
            false,
            propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for IsItalic.
    /// </summary>
    public static readonly BindableProperty IsItalicProperty =
        BindableProperty.Create(
            nameof(IsItalic),
            typeof(bool),
            typeof(SkiaButton),
            false,
            propertyChanged: (b, o, n) => ((SkiaButton)b).OnFontChanged());

    /// <summary>
    /// Bindable property for CharacterSpacing.
    /// </summary>
    public static readonly BindableProperty CharacterSpacingProperty =
        BindableProperty.Create(
            nameof(CharacterSpacing),
            typeof(float),
            typeof(SkiaButton),
            0f,
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for CornerRadius.
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(
            nameof(CornerRadius),
            typeof(float),
            typeof(SkiaButton),
            4f,
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for BorderWidth.
    /// </summary>
    public static readonly BindableProperty BorderWidthProperty =
        BindableProperty.Create(
            nameof(BorderWidth),
            typeof(float),
            typeof(SkiaButton),
            0f,
            propertyChanged: (b, o, n) => ((SkiaButton)b).Invalidate());

    /// <summary>
    /// Bindable property for Padding.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(
            nameof(Padding),
            typeof(SKRect),
            typeof(SkiaButton),
            new SKRect(16, 8, 16, 8),
            propertyChanged: (b, o, n) => ((SkiaButton)b).InvalidateMeasure());

    /// <summary>
    /// Bindable property for Command.
    /// </summary>
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(
            nameof(Command),
            typeof(System.Windows.Input.ICommand),
            typeof(SkiaButton),
            null,
            propertyChanged: (b, o, n) => ((SkiaButton)b).OnCommandChanged((System.Windows.Input.ICommand?)o, (System.Windows.Input.ICommand?)n));

    /// <summary>
    /// Bindable property for CommandParameter.
    /// </summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(
            nameof(CommandParameter),
            typeof(object),
            typeof(SkiaButton),
            null);

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the button text.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the button background color.
    /// </summary>
    public SKColor ButtonBackgroundColor
    {
        get => (SKColor)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the pressed background color.
    /// </summary>
    public SKColor PressedBackgroundColor
    {
        get => (SKColor)GetValue(PressedBackgroundColorProperty);
        set => SetValue(PressedBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the disabled background color.
    /// </summary>
    public SKColor DisabledBackgroundColor
    {
        get => (SKColor)GetValue(DisabledBackgroundColorProperty);
        set => SetValue(DisabledBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the hovered background color.
    /// </summary>
    public SKColor HoveredBackgroundColor
    {
        get => (SKColor)GetValue(HoveredBackgroundColorProperty);
        set => SetValue(HoveredBackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color.
    /// </summary>
    public SKColor BorderColor
    {
        get => (SKColor)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
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
    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text is bold.
    /// </summary>
    public bool IsBold
    {
        get => (bool)GetValue(IsBoldProperty);
        set => SetValue(IsBoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the text is italic.
    /// </summary>
    public bool IsItalic
    {
        get => (bool)GetValue(IsItalicProperty);
        set => SetValue(IsItalicProperty, value);
    }

    /// <summary>
    /// Gets or sets the character spacing.
    /// </summary>
    public float CharacterSpacing
    {
        get => (float)GetValue(CharacterSpacingProperty);
        set => SetValue(CharacterSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the border width.
    /// </summary>
    public float BorderWidth
    {
        get => (float)GetValue(BorderWidthProperty);
        set => SetValue(BorderWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding.
    /// </summary>
    public SKRect Padding
    {
        get => (SKRect)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when clicked.
    /// </summary>
    public System.Windows.Input.ICommand? Command
    {
        get => (System.Windows.Input.ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command parameter.
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// Gets whether the button is currently pressed.
    /// </summary>
    public bool IsPressed { get; private set; }

    /// <summary>
    /// Gets whether the pointer is currently over the button.
    /// </summary>
    public bool IsHovered { get; private set; }

    #endregion

    private bool _focusFromKeyboard;

    /// <summary>
    /// Event raised when the button is clicked.
    /// </summary>
    public event EventHandler? Clicked;

    /// <summary>
    /// Event raised when the button is pressed.
    /// </summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// Event raised when the button is released.
    /// </summary>
    public event EventHandler? Released;

    public SkiaButton()
    {
        IsFocusable = true;
    }

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

    private void OnCommandChanged(System.Windows.Input.ICommand? oldCommand, System.Windows.Input.ICommand? newCommand)
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

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Check if this is a "text only" button (transparent background)
        var isTextOnly = ButtonBackgroundColor.Alpha == 0;

        // Determine background color based on state
        SKColor bgColor;
        if (!IsEnabled)
        {
            bgColor = isTextOnly ? SKColors.Transparent : DisabledBackgroundColor;
        }
        else if (IsPressed)
        {
            // For text-only buttons, use a subtle press effect
            bgColor = isTextOnly ? new SKColor(0, 0, 0, 20) : PressedBackgroundColor;
        }
        else if (IsHovered)
        {
            // For text-only buttons, use a subtle hover effect instead of full background
            bgColor = isTextOnly ? new SKColor(0, 0, 0, 10) : HoveredBackgroundColor;
        }
        else
        {
            bgColor = ButtonBackgroundColor;
        }

        // Draw shadow (for elevation effect) - skip for text-only buttons
        if (IsEnabled && !IsPressed && !isTextOnly)
        {
            DrawShadow(canvas, bounds);
        }

        // Create rounded rect for background and border
        var rect = new SKRoundRect(bounds, CornerRadius);

        // Draw background with rounded corners (skip if fully transparent)
        if (bgColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(rect, bgPaint);
        }

        // Draw border
        if (BorderWidth > 0 && BorderColor != SKColors.Transparent)
        {
            using var borderPaint = new SKPaint
            {
                Color = BorderColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BorderWidth
            };
            canvas.DrawRoundRect(rect, borderPaint);
        }

        // Draw focus ring only for keyboard focus
        if (IsFocused && _focusFromKeyboard)
        {
            using var focusPaint = new SKPaint
            {
                Color = new SKColor(0x21, 0x96, 0xF3, 0x80),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            var focusRect = new SKRoundRect(bounds, CornerRadius + 2);
            focusRect.Inflate(2, 2);
            canvas.DrawRoundRect(focusRect, focusPaint);
        }

        // Draw text
        if (!string.IsNullOrEmpty(Text))
        {
            var fontStyle = new SKFontStyle(
                IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                          ?? SKTypeface.Default;

            using var font = new SKFont(typeface, FontSize);

            // For text-only buttons, darken text on hover/press for feedback
            SKColor textColorToUse;
            if (!IsEnabled)
            {
                textColorToUse = TextColor.WithAlpha(128);
            }
            else if (isTextOnly && (IsHovered || IsPressed))
            {
                // Darken the text color slightly for hover/press feedback
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

            using var paint = new SKPaint(font)
            {
                Color = textColorToUse,
                IsAntialias = true
            };

            // Measure text
            var textBounds = new SKRect();
            paint.MeasureText(Text, ref textBounds);

            // Center text
            var x = bounds.MidX - textBounds.MidX;
            var y = bounds.MidY - textBounds.MidY;

            canvas.DrawText(Text, x, y, paint);
        }
    }

    private void DrawShadow(SKCanvas canvas, SKRect bounds)
    {
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 50),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
        };

        var shadowRect = new SKRect(
            bounds.Left + 2,
            bounds.Top + 4,
            bounds.Right + 2,
            bounds.Bottom + 4);

        var roundRect = new SKRoundRect(shadowRect, CornerRadius);
        canvas.DrawRoundRect(roundRect, shadowPaint);
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        if (!IsEnabled) return;
        IsHovered = true;
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.PointerOver);
        Invalidate();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        if (IsPressed)
        {
            IsPressed = false;
        }
        SkiaVisualStateManager.GoToState(this, IsEnabled ? SkiaVisualStateManager.CommonStates.Normal : SkiaVisualStateManager.CommonStates.Disabled);
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Console.WriteLine($"[SkiaButton] OnPointerPressed - Text='{Text}', IsEnabled={IsEnabled}");
        if (!IsEnabled) return;

        IsPressed = true;
        _focusFromKeyboard = false;
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
        Invalidate();
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var wasPressed = IsPressed;
        IsPressed = false;
        SkiaVisualStateManager.GoToState(this, IsHovered ? SkiaVisualStateManager.CommonStates.PointerOver : SkiaVisualStateManager.CommonStates.Normal);
        Invalidate();

        Released?.Invoke(this, EventArgs.Empty);

        // Fire click if button was pressed
        // Note: Hit testing already verified the pointer is over this button,
        // so we don't need to re-check bounds (which would fail due to coordinate system differences)
        if (wasPressed)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            Command?.Execute(CommandParameter);
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        // Activate on Enter or Space
        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            IsPressed = true;
            _focusFromKeyboard = true;
            SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
            Invalidate();
            Pressed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            if (IsPressed)
            {
                IsPressed = false;
                SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Normal);
                Invalidate();
                Released?.Invoke(this, EventArgs.Empty);
                Clicked?.Invoke(this, EventArgs.Empty);
                Command?.Execute(CommandParameter);
            }
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
        // Ensure we never return NaN - use safe defaults
        var paddingLeft = float.IsNaN(Padding.Left) ? 16f : Padding.Left;
        var paddingRight = float.IsNaN(Padding.Right) ? 16f : Padding.Right;
        var paddingTop = float.IsNaN(Padding.Top) ? 8f : Padding.Top;
        var paddingBottom = float.IsNaN(Padding.Bottom) ? 8f : Padding.Bottom;
        var fontSize = float.IsNaN(FontSize) || FontSize <= 0 ? 14f : FontSize;

        if (string.IsNullOrEmpty(Text))
        {
            return new SKSize(
                paddingLeft + paddingRight + 40, // Minimum width
                paddingTop + paddingBottom + fontSize);
        }

        var fontStyle = new SKFontStyle(
                IsBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        var typeface = SkiaRenderingEngine.Current?.ResourceCache.GetTypeface(FontFamily, fontStyle)
                      ?? SKTypeface.Default;

        using var font = new SKFont(typeface, fontSize);
        using var paint = new SKPaint(font);

        var textBounds = new SKRect();
        paint.MeasureText(Text, ref textBounds);

        var width = textBounds.Width + paddingLeft + paddingRight;
        var height = textBounds.Height + paddingTop + paddingBottom;

        // Ensure valid, non-NaN return values
        if (float.IsNaN(width) || width < 0) width = 72f;
        if (float.IsNaN(height) || height < 0) height = 30f;

        // Respect WidthRequest and HeightRequest when set
        if (WidthRequest >= 0)
            width = (float)WidthRequest;
        if (HeightRequest >= 0)
            height = (float)HeightRequest;

        return new SKSize(width, height);
    }
}
