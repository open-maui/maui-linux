// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Handler for SfImageEditor's image view (Syncfusion's internal <c>ImageViewExt</c>), the
/// Linux counterpart of the Windows <c>ImageEditorHandler</c>, which the platform-neutral build
/// ships as a bare image handler. The picture shows in OpenMaui's image view; the handler keeps
/// the edited bitmap the way the Windows handler keeps its processed stream: flips and quarter
/// turns are applied to it, a crop replaces it (with the annotations merged in), effects are
/// previewed on a filtered copy and either kept or dropped, and save, <c>GetImageStream</c> and
/// undo read and restore it. <see cref="SfImageEditorPatches"/> routes the editor's calls here.
/// </summary>
public class SfImageEditorBridgeHandler : ImageHandler
{
    public static new IPropertyMapper<IImage, SfImageEditorBridgeHandler> Mapper =
        new PropertyMapper<IImage, SfImageEditorBridgeHandler>(ImageHandler.Mapper)
        {
            [nameof(IImageSourcePart.Source)] = MapEditorSource,
        };

    private SKBitmap? _processed;
    private SKBitmap? _original;
    private SKBitmap? _filtered;
    private List<KeyValuePair<string, float>>? _effects;
    private bool _suppressSource;

    public SfImageEditorBridgeHandler() : base(Mapper, CommandMapper)
    {
    }

    /// <summary>The format of the last effect request (opacity and JPEG flattening depend on it).</summary>
    internal string? ImageFormat { get; set; }

    /// <summary>True while effects are being previewed (between StartEffect and EndEffect).</summary>
    internal bool InEffect => _effects != null;

    private static void MapEditorSource(SfImageEditorBridgeHandler handler, IImage image)
    {
        // The filtered preview replaces the picture without a source (SetPlatformImageSource).
        if (handler._suppressSource)
            return;
        ImageHandler.MapSource(handler, image);
    }

    protected override void DisconnectHandler(SkiaImage platformView)
    {
        DisposeObjects();
        base.DisconnectHandler(platformView);
    }

    /// <summary>The edited bitmap, read from the picture on first use (SetImageStream).</summary>
    internal SKBitmap? Processed
    {
        get
        {
            if (_processed == null && PlatformView?.Bitmap is { } shown)
            {
                _processed = SfImageProcessing.Copy(shown);
                _original ??= SfImageProcessing.Copy(shown);
            }
            return _processed;
        }
    }

    /// <summary>Replaces the edited bitmap (undo and redo: SetImageStream).</summary>
    internal void SetProcessed(SKBitmap? bitmap)
    {
        if (!ReferenceEquals(_processed, bitmap))
            _processed?.Dispose();
        _processed = bitmap;
    }

    /// <summary>The edited bitmap, encoded (GetImageStream); null before a picture shows.</summary>
    internal byte[]? EncodeProcessed(string? format = "Png") => Processed is { } bitmap ? SfImageProcessing.Encode(bitmap, format) : null;

    internal void Flip(bool horizontal)
    {
        if (Processed is { } bitmap)
            SetProcessed(SfImageProcessing.Flip(bitmap, horizontal));
    }

    internal void Rotate(double angle)
    {
        if (Processed is { } bitmap)
            SetProcessed(SfImageProcessing.Rotate(bitmap, angle));
    }

    /// <summary>ResetImage: a new source drops everything; a reset goes back to the original picture.</summary>
    internal void Reset(bool isSourceChange)
    {
        ClearEffects();
        if (isSourceChange)
        {
            SetProcessed(null);
            _original?.Dispose();
            _original = null;
        }
        else if (_original != null)
        {
            SetProcessed(SfImageProcessing.Copy(_original));
        }
        else
        {
            SetProcessed(null);
        }
    }

    /// <summary>
    /// GetCroppedImageStream: the edited bitmap with the annotations merged in, cropped; it
    /// becomes the edited bitmap, and its encoding is returned for the new picture.
    /// </summary>
    internal byte[]? Crop(View? annotationLayout, Rect cropRect, Size renderedSize, string cropType, string format)
    {
        using var merged = SfImageProcessing.Merge(Processed, annotationLayout?.Handler?.PlatformView as SkiaView);
        if (merged == null)
            return null;
        var cropped = SfImageProcessing.Crop(merged, cropRect, renderedSize, cropType);
        SetProcessed(cropped);
        // A circle or ellipse needs transparency: always PNG (as the Windows helper switches to it).
        return SfImageProcessing.Encode(cropped, cropType is "Circle" or "Ellipse" ? "Png" : format);
    }

    /// <summary>GetMergedStream: the edited bitmap with the annotation layer, optionally resized.</summary>
    internal byte[]? Merged(View? annotationLayout, string format, Size? size) =>
        MergedOf(Processed, annotationLayout, format, size);

    internal static byte[]? MergedOf(SKBitmap? image, View? annotationLayout, string format, Size? size)
    {
        var merged = SfImageProcessing.Merge(image, annotationLayout?.Handler?.PlatformView as SkiaView);
        if (merged == null)
            return null;
        try
        {
            if (size is { Width: > 0, Height: > 0 } s)
            {
                var resized = SfImageProcessing.Resize(merged, (int)s.Width, (int)s.Height);
                merged.Dispose();
                merged = resized;
            }
            return SfImageProcessing.Encode(merged, format);
        }
        finally
        {
            merged.Dispose();
        }
    }

    internal void StartEffect()
    {
        _ = Processed;
        _effects ??= new List<KeyValuePair<string, float>>();
    }

    /// <summary>
    /// ApplyEffect: updates the effect's value (Win2D keeps one effect of each kind in its
    /// composite, opacity first) and shows the filtered picture.
    /// </summary>
    internal void ApplyEffect(string effect, double value, string? format)
    {
        if (format != null)
            ImageFormat = format;
        StartEffect();
        if (Processed is not { } source)
            return;
        float v = (float)Math.Round(value, 2);
        int index = _effects!.FindIndex(e => e.Key == effect);
        if (index >= 0)
        {
            if (_effects[index].Value == v)
                return;
            _effects[index] = new(effect, v);
        }
        else if (effect == "Opacity")
        {
            _effects.Insert(0, new(effect, v));
        }
        else
        {
            _effects.Add(new(effect, v));
        }
        var filtered = SfImageProcessing.ApplyEffects(source, _effects);
        _filtered?.Dispose();
        _filtered = filtered;
        ShowBitmap(filtered);
    }

    /// <summary>
    /// EndEffect: kept effects make the filtered bitmap the edited one and return the previous
    /// one (for undo); dropped effects return the edited bitmap to show again.
    /// </summary>
    internal byte[]? EndEffect(bool isSave)
    {
        byte[]? result;
        if (isSave && _filtered != null)
        {
            result = EncodeProcessed(ImageFormat ?? "Png");
            SetProcessed(_filtered);
            _filtered = null;
        }
        else
        {
            result = EncodeProcessed(ImageFormat ?? "Png");
        }
        ClearEffects();
        return result;
    }

    /// <summary>
    /// SetPlatformImageSource: shows a bitmap in place of the source, with the view's rotation
    /// and flips reset (the bitmap already carries them).
    /// </summary>
    internal void ShowBitmap(SKBitmap bitmap)
    {
        if (VirtualView is not Image image || PlatformView is not { } view)
            return;
        image.Rotation = 0;
        image.RotationX = 0;
        image.RotationY = 0;
        if (image.Source != null)
        {
            _suppressSource = true;
            try
            {
                image.Source = null;
            }
            finally
            {
                _suppressSource = false;
            }
        }
        view.LoadFromBitmap(SfImageProcessing.Copy(bitmap));
    }

    private void ClearEffects()
    {
        _effects = null;
        _filtered?.Dispose();
        _filtered = null;
    }

    private void DisposeObjects()
    {
        ClearEffects();
        SetProcessed(null);
        _original?.Dispose();
        _original = null;
    }
}
