// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using Svg.Skia;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia-rendered image button control.
/// Combines button behavior with image display.
/// Implements MAUI IImageButton interface requirements.
/// </summary>
public class SkiaImageButton : SkiaView
{
    #region Private Fields
    private SKBitmap? _bitmap;
    private SKImage? _image;
    private bool _isLoading;
    #endregion

    #region SKColor Helper
    private static SKColor ToSKColor(Color? color)
    {
        if (color == null) return SKColors.Transparent;
        return new SKColor(
            (byte)(color.Red * 255),
            (byte)(color.Green * 255),
            (byte)(color.Blue * 255),
            (byte)(color.Alpha * 255));
    }
    #endregion

    #region BindableProperties

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect), typeof(Aspect), typeof(SkiaImageButton), Aspect.AspectFit,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty IsOpaqueProperty = BindableProperty.Create(
        nameof(IsOpaque), typeof(bool), typeof(SkiaImageButton), false,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
        nameof(StrokeColor), typeof(Color), typeof(SkiaImageButton), Colors.Transparent,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(
        nameof(StrokeThickness), typeof(double), typeof(SkiaImageButton), 0.0,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(int), typeof(SkiaImageButton), 0,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding), typeof(Thickness), typeof(SkiaImageButton), new Thickness(0),
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty PressedBackgroundColorProperty = BindableProperty.Create(
        nameof(PressedBackgroundColor), typeof(Color), typeof(SkiaImageButton),
        Color.FromRgba(0, 0, 0, 30),
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty HoveredBackgroundColorProperty = BindableProperty.Create(
        nameof(HoveredBackgroundColor), typeof(Color), typeof(SkiaImageButton),
        Color.FromRgba(0, 0, 0, 15),
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty ImageBackgroundColorProperty = BindableProperty.Create(
        nameof(ImageBackgroundColor), typeof(Color), typeof(SkiaImageButton), null,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).Invalidate());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SkiaImageButton), null,
        propertyChanged: OnCommandChanged);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(SkiaImageButton), null,
        propertyChanged: (b, o, n) => ((SkiaImageButton)b).UpdateCommandCanExecute());

    #endregion

    #region Properties

    public SKBitmap? Bitmap
    {
        get => _bitmap;
        set
        {
            _bitmap?.Dispose();
            _bitmap = value;
            _image?.Dispose();
            _image = value != null ? SKImage.FromBitmap(value) : null;
            Invalidate();
        }
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    public bool IsOpaque
    {
        get => (bool)GetValue(IsOpaqueProperty);
        set => SetValue(IsOpaqueProperty, value);
    }

    public bool IsLoading => _isLoading;

    public Color StrokeColor
    {
        get => (Color)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public int CornerRadius
    {
        get => (int)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Color PressedBackgroundColor
    {
        get => (Color)GetValue(PressedBackgroundColorProperty);
        set => SetValue(PressedBackgroundColorProperty, value);
    }

    public Color HoveredBackgroundColor
    {
        get => (Color)GetValue(HoveredBackgroundColorProperty);
        set => SetValue(HoveredBackgroundColorProperty, value);
    }

    public Color? ImageBackgroundColor
    {
        get => (Color?)GetValue(ImageBackgroundColorProperty);
        set => SetValue(ImageBackgroundColorProperty, value);
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

    // Button state
    public bool IsPressed { get; private set; }
    public bool IsHovered { get; private set; }

    #endregion

    #region Events
    public event EventHandler? Clicked;
    public event EventHandler? Pressed;
    public event EventHandler? Released;
    public event EventHandler? ImageLoaded;
    public event EventHandler<ImageLoadingErrorEventArgs>? ImageLoadingError;
    #endregion

    #region Constructor

    public SkiaImageButton()
    {
        IsFocusable = true;
    }

    #endregion

    #region Command Support

    private static void OnCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var button = (SkiaImageButton)bindable;

        if (oldValue is ICommand oldCommand)
        {
            oldCommand.CanExecuteChanged -= button.OnCommandCanExecuteChanged;
        }

        if (newValue is ICommand newCommand)
        {
            newCommand.CanExecuteChanged += button.OnCommandCanExecuteChanged;
        }

        button.UpdateCommandCanExecute();
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateCommandCanExecute();
    }

    private void UpdateCommandCanExecute()
    {
        if (Command != null)
        {
            IsEnabled = Command.CanExecute(CommandParameter);
        }
    }

    private void ExecuteCommand()
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    #endregion

    #region Rendering

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        var padding = Padding;
        var contentBounds = new SKRect(
            bounds.Left + (float)padding.Left,
            bounds.Top + (float)padding.Top,
            bounds.Right - (float)padding.Right,
            bounds.Bottom - (float)padding.Bottom);

        // Determine background color
        SKColor bgColor;
        if (IsPressed)
        {
            bgColor = ToSKColor(PressedBackgroundColor);
        }
        else if (IsHovered)
        {
            bgColor = ToSKColor(HoveredBackgroundColor);
        }
        else if (ImageBackgroundColor != null)
        {
            bgColor = ToSKColor(ImageBackgroundColor);
        }
        else
        {
            bgColor = BackgroundColor;
        }

        // Draw background
        if (bgColor != SKColors.Transparent || !IsOpaque)
        {
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            if (CornerRadius > 0)
            {
                var roundRect = new SKRoundRect(bounds, CornerRadius);
                canvas.DrawRoundRect(roundRect, bgPaint);
            }
            else
            {
                canvas.DrawRect(bounds, bgPaint);
            }
        }

        // Draw image
        if (_image != null)
        {
            var imageWidth = _image.Width;
            var imageHeight = _image.Height;

            if (imageWidth > 0 && imageHeight > 0)
            {
                var destRect = CalculateDestRect(contentBounds, imageWidth, imageHeight);

                using var paint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };

                // Apply opacity when disabled
                if (!IsEnabled)
                {
                    paint.Color = paint.Color.WithAlpha(128);
                }

                canvas.DrawImage(_image, destRect, paint);
            }
        }

        // Draw stroke/border
        var strokeThickness = (float)StrokeThickness;
        var strokeColor = ToSKColor(StrokeColor);
        if (strokeThickness > 0 && strokeColor != SKColors.Transparent)
        {
            using var strokePaint = new SKPaint
            {
                Color = strokeColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeThickness,
                IsAntialias = true
            };

            if (CornerRadius > 0)
            {
                var roundRect = new SKRoundRect(bounds, CornerRadius);
                canvas.DrawRoundRect(roundRect, strokePaint);
            }
            else
            {
                canvas.DrawRect(bounds, strokePaint);
            }
        }

        // Draw focus ring
        if (IsFocused)
        {
            using var focusPaint = new SKPaint
            {
                Color = new SKColor(0x00, 0x00, 0x00, 0x40),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            var focusBounds = new SKRect(bounds.Left - 2, bounds.Top - 2, bounds.Right + 2, bounds.Bottom + 2);
            if (CornerRadius > 0)
            {
                var focusRect = new SKRoundRect(focusBounds, CornerRadius + 2);
                canvas.DrawRoundRect(focusRect, focusPaint);
            }
            else
            {
                canvas.DrawRect(focusBounds, focusPaint);
            }
        }
    }

    private SKRect CalculateDestRect(SKRect bounds, float imageWidth, float imageHeight)
    {
        float destX, destY, destWidth, destHeight;

        switch (Aspect)
        {
            case Aspect.Fill:
                return bounds;

            case Aspect.AspectFit:
                var fitScale = Math.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
                destWidth = imageWidth * fitScale;
                destHeight = imageHeight * fitScale;
                destX = bounds.Left + (bounds.Width - destWidth) / 2;
                destY = bounds.Top + (bounds.Height - destHeight) / 2;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);

            case Aspect.AspectFill:
                var fillScale = Math.Max(bounds.Width / imageWidth, bounds.Height / imageHeight);
                destWidth = imageWidth * fillScale;
                destHeight = imageHeight * fillScale;
                destX = bounds.Left + (bounds.Width - destWidth) / 2;
                destY = bounds.Top + (bounds.Height - destHeight) / 2;
                return new SKRect(destX, destY, destX + destWidth, destY + destHeight);

            case Aspect.Center:
                destX = bounds.Left + (bounds.Width - imageWidth) / 2;
                destY = bounds.Top + (bounds.Height - imageHeight) / 2;
                return new SKRect(destX, destY, destX + imageWidth, destY + imageHeight);

            default:
                return bounds;
        }
    }

    #endregion

    #region Image Loading

    public async Task LoadFromFileAsync(string filePath)
    {
        _isLoading = true;
        Invalidate();
        Console.WriteLine("[SkiaImageButton] LoadFromFileAsync: " + filePath);

        try
        {
            var searchPaths = new List<string>
            {
                filePath,
                Path.Combine(AppContext.BaseDirectory, filePath),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Images", filePath),
                Path.Combine(AppContext.BaseDirectory, "Resources", filePath)
            };

            // Also check for SVG version if PNG was requested
            if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var svgPath = Path.ChangeExtension(filePath, ".svg");
                searchPaths.Add(svgPath);
                searchPaths.Add(Path.Combine(AppContext.BaseDirectory, svgPath));
                searchPaths.Add(Path.Combine(AppContext.BaseDirectory, "Resources", "Images", svgPath));
                searchPaths.Add(Path.Combine(AppContext.BaseDirectory, "Resources", svgPath));
            }

            string? foundPath = null;
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    Console.WriteLine("[SkiaImageButton] Found file at: " + path);
                    break;
                }
            }

            if (foundPath == null)
            {
                Console.WriteLine("[SkiaImageButton] File not found: " + filePath);
                Console.WriteLine("[SkiaImageButton] Searched paths: " + string.Join(", ", searchPaths));
                _isLoading = false;
                ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(new FileNotFoundException(filePath)));
                return;
            }

            var padding = Padding;
            await Task.Run(() =>
            {
                if (foundPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    using var svg = new SKSvg();
                    svg.Load(foundPath);
                    if (svg.Picture != null)
                    {
                        var cullRect = svg.Picture.CullRect;
                        bool hasWidth = WidthRequest > 0;
                        bool hasHeight = HeightRequest > 0;

                        // Default to 24x24 for icons when no size specified
                        const float DefaultIconSize = 24f;
                        float targetWidth = hasWidth
                            ? (float)(WidthRequest - padding.Left - padding.Right)
                            : DefaultIconSize;
                        float targetHeight = hasHeight
                            ? (float)(HeightRequest - padding.Top - padding.Bottom)
                            : DefaultIconSize;

                        float scale = Math.Min(targetWidth / cullRect.Width, targetHeight / cullRect.Height);
                        int width = Math.Max(1, (int)(cullRect.Width * scale));
                        int height = Math.Max(1, (int)(cullRect.Height * scale));

                        var bitmap = new SKBitmap(width, height, false);
                        using var canvas = new SKCanvas(bitmap);
                        canvas.Clear(SKColors.Transparent);
                        canvas.Scale(scale);
                        canvas.DrawPicture(svg.Picture);
                        Bitmap = bitmap;
                        Console.WriteLine($"[SkiaImageButton] Loaded SVG: {foundPath} ({width}x{height})");
                    }
                }
                else
                {
                    using var stream = File.OpenRead(foundPath);
                    var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        Bitmap = bitmap;
                        Console.WriteLine("[SkiaImageButton] Loaded image: " + foundPath);
                    }
                }
            });

            _isLoading = false;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }

        Invalidate();
    }

    public async Task LoadFromStreamAsync(Stream stream)
    {
        _isLoading = true;
        Invalidate();

        try
        {
            await Task.Run(() =>
            {
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    Bitmap = bitmap;
                }
            });

            _isLoading = false;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }

        Invalidate();
    }

    public async Task LoadFromUriAsync(Uri uri)
    {
        _isLoading = true;
        Invalidate();

        try
        {
            using var httpClient = new HttpClient();
            var data = await httpClient.GetByteArrayAsync(uri);

            using var stream = new MemoryStream(data);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                Bitmap = bitmap;
            }

            _isLoading = false;
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isLoading = false;
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }

        Invalidate();
    }

    public void LoadFromData(byte[] data)
    {
        try
        {
            if (data == null || data.Length == 0)
            {
                Bitmap = null;
                return;
            }

            using var stream = new MemoryStream(data);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                Bitmap = bitmap;
            }
            ImageLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ImageLoadingError?.Invoke(this, new ImageLoadingErrorEventArgs(ex));
        }
    }

    public void LoadFromBitmap(SKBitmap bitmap)
    {
        Bitmap = bitmap;
        ImageLoaded?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Pointer Event Handlers

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
        SkiaVisualStateManager.GoToState(this, IsEnabled
            ? SkiaVisualStateManager.CommonStates.Normal
            : SkiaVisualStateManager.CommonStates.Disabled);
        Invalidate();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        IsPressed = true;
        SkiaVisualStateManager.GoToState(this, SkiaVisualStateManager.CommonStates.Pressed);
        Invalidate();
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        var wasPressed = IsPressed;
        IsPressed = false;
        SkiaVisualStateManager.GoToState(this, IsHovered
            ? SkiaVisualStateManager.CommonStates.PointerOver
            : SkiaVisualStateManager.CommonStates.Normal);
        Invalidate();

        Released?.Invoke(this, EventArgs.Empty);

        if (wasPressed && Bounds.Contains(new SKPoint(e.X, e.Y)))
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            ExecuteCommand();
        }
    }

    #endregion

    #region Keyboard Event Handlers

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            IsPressed = true;
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
                Invalidate();
                Released?.Invoke(this, EventArgs.Empty);
                Clicked?.Invoke(this, EventArgs.Empty);
                ExecuteCommand();
            }
            e.Handled = true;
        }
    }

    #endregion

    #region Layout

    protected override SKSize MeasureOverride(SKSize availableSize)
    {
        var padding = Padding;
        var paddingWidth = (float)(padding.Left + padding.Right);
        var paddingHeight = (float)(padding.Top + padding.Bottom);

        // Respect explicit WidthRequest/HeightRequest first (MAUI standard behavior)
        if (WidthRequest > 0 && HeightRequest > 0)
        {
            return new SKSize((float)WidthRequest, (float)HeightRequest);
        }
        if (WidthRequest > 0)
        {
            // Fixed width, calculate height from aspect ratio or use width
            float height = HeightRequest > 0 ? (float)HeightRequest
                         : _image != null ? (float)WidthRequest * _image.Height / _image.Width
                         : (float)WidthRequest;
            return new SKSize((float)WidthRequest, height);
        }
        if (HeightRequest > 0)
        {
            // Fixed height, calculate width from aspect ratio or use height
            float width = WidthRequest > 0 ? (float)WidthRequest
                        : _image != null ? (float)HeightRequest * _image.Width / _image.Height
                        : (float)HeightRequest;
            return new SKSize(width, (float)HeightRequest);
        }

        // No explicit size - calculate from content
        if (_image == null)
            return new SKSize(44 + paddingWidth, 44 + paddingHeight); // Default touch target size

        var imageWidth = _image.Width;
        var imageHeight = _image.Height;

        if (availableSize.Width < float.MaxValue && availableSize.Height < float.MaxValue)
        {
            var availableContent = new SKSize(
                availableSize.Width - paddingWidth,
                availableSize.Height - paddingHeight);
            var scale = Math.Min(availableContent.Width / imageWidth, availableContent.Height / imageHeight);
            return new SKSize(imageWidth * scale + paddingWidth, imageHeight * scale + paddingHeight);
        }
        else if (availableSize.Width < float.MaxValue)
        {
            var availableWidth = availableSize.Width - paddingWidth;
            var scale = availableWidth / imageWidth;
            return new SKSize(availableSize.Width, imageHeight * scale + paddingHeight);
        }
        else if (availableSize.Height < float.MaxValue)
        {
            var availableHeight = availableSize.Height - paddingHeight;
            var scale = availableHeight / imageHeight;
            return new SKSize(imageWidth * scale + paddingWidth, availableSize.Height);
        }

        return new SKSize(imageWidth + paddingWidth, imageHeight + paddingHeight);
    }

    protected override SKRect ArrangeOverride(SKRect bounds)
    {
        // If we have explicit size requests, constrain to desired size
        // This follows MAUI standard behavior - controls respect WidthRequest/HeightRequest
        var desiredWidth = DesiredSize.Width;
        var desiredHeight = DesiredSize.Height;

        // If desired size is smaller than available bounds, align within bounds
        if (desiredWidth > 0 && desiredHeight > 0 &&
            (desiredWidth < bounds.Width || desiredHeight < bounds.Height))
        {
            float finalWidth = Math.Min(desiredWidth, bounds.Width);
            float finalHeight = Math.Min(desiredHeight, bounds.Height);

            // Calculate position based on HorizontalOptions
            // LayoutAlignment: Start=0, Center=1, End=2, Fill=3
            float x = bounds.Left;
            var hAlignValue = (int)HorizontalOptions.Alignment;
            if (hAlignValue == 1) // Center
            {
                x = bounds.Left + (bounds.Width - finalWidth) / 2;
            }
            else if (hAlignValue == 2) // End
            {
                x = bounds.Right - finalWidth;
            }
            // Fill (3) and Start (0) both use x = bounds.Left

            // Calculate position based on VerticalOptions
            float y = bounds.Top;
            var vAlignValue = (int)VerticalOptions.Alignment;
            if (vAlignValue == 1) // Center
            {
                y = bounds.Top + (bounds.Height - finalHeight) / 2;
            }
            else if (vAlignValue == 2) // End
            {
                y = bounds.Bottom - finalHeight;
            }
            // Fill (3) and Start (0) both use y = bounds.Top

            return new SKRect(x, y, x + finalWidth, y + finalHeight);
        }

        return bounds;
    }

    #endregion

    #region Dispose

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from command
            if (Command != null)
            {
                Command.CanExecuteChanged -= OnCommandCanExecuteChanged;
            }

            _bitmap?.Dispose();
            _image?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}
