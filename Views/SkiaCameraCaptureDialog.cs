// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services.Camera;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The camera capture UI behind <c>MediaPicker.CapturePhotoAsync</c> and <c>CaptureVideoAsync</c>
/// on Linux, the counterpart of the Windows camera capture UI MAUI opens: a live preview in a
/// modal card over the app window; for a photo "Take photo", then "Retake" or "Use photo"; for a
/// video "Record", then "Stop". Cancel (or Escape, from any state) closes it with no file. Enter
/// or Space presses the main button.
/// </summary>
internal sealed class SkiaCameraCaptureDialog : SkiaModalDialog
{
    private const float WindowMargin = 24;
    private const float MaxCardWidth = 960;
    private const float TitleHeight = 32;

    private readonly CameraCaptureController _controller;
    private readonly string _title;
    private SKRect _primaryBounds;
    private SKRect _secondaryBounds;
    private int _hovered = -1;

    public SkiaCameraCaptureDialog(CameraCaptureController controller, string? title)
    {
        _controller = controller;
        _title = string.IsNullOrWhiteSpace(title) ? (controller.IsPhoto ? "Take a photo" : "Record a video") : title!;
        _controller.Changed += Invalidate;
    }

    /// <summary>Completes with the captured file, or null when cancelled.</summary>
    public Task<string?> Result => _controller.Result;

    /// <summary>The main and secondary buttons' labels in the current state (null: no such button).</summary>
    internal (string? Primary, string? Secondary) Buttons => _controller.State switch
    {
        CameraCaptureState.Starting => (null, "Cancel"),
        CameraCaptureState.Live => (_controller.IsPhoto ? "Take photo" : "Record", "Cancel"),
        CameraCaptureState.Review => ("Use photo", "Retake"),
        CameraCaptureState.Recording => ("Stop", null),
        CameraCaptureState.Finishing => (null, null),
        CameraCaptureState.Failed => (null, "Close"),
        _ => (null, null),
    };

    /// <summary>Presses the main button (Take photo, Use photo, Record, Stop).</summary>
    internal void PressPrimary()
    {
        switch (_controller.State)
        {
            case CameraCaptureState.Live when _controller.IsPhoto:
                _controller.TakePhoto();
                break;
            case CameraCaptureState.Live:
                _ = _controller.StartRecordingAsync();
                break;
            case CameraCaptureState.Review:
                Close();
                _ = _controller.AcceptAsync();
                break;
            case CameraCaptureState.Recording:
                _ = FinishRecordingAsync();
                break;
        }
    }

    /// <summary>Presses the secondary button (Cancel, Retake, Close).</summary>
    internal void PressSecondary()
    {
        switch (_controller.State)
        {
            case CameraCaptureState.Review:
                _controller.Retake();
                break;
            case CameraCaptureState.Starting or CameraCaptureState.Live or CameraCaptureState.Failed:
                Close();
                _ = _controller.CancelAsync();
                break;
        }
    }

    private async Task FinishRecordingAsync()
    {
        await _controller.StopRecordingAsync().ConfigureAwait(false);
        Close();
    }

    private void Close()
    {
        _controller.Changed -= Invalidate;
        Hide();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        DrawOverlay(canvas, bounds);

        var cardWidth = Math.Min(MaxCardWidth, bounds.Width - WindowMargin * 2);
        var maxCardHeight = bounds.Height - WindowMargin * 2;
        var chrome = DialogPadding * 2 + TitleHeight + ButtonSpacing + ButtonHeight + ButtonSpacing;
        var previewWidth = cardWidth - DialogPadding * 2;
        var previewHeight = Math.Max(60, Math.Min(previewWidth * 9 / 16, maxCardHeight - chrome));
        var cardHeight = chrome + previewHeight;
        var card = new SKRect(bounds.MidX - cardWidth / 2, bounds.MidY - cardHeight / 2, bounds.MidX + cardWidth / 2, bounds.MidY + cardHeight / 2);
        DrawCard(canvas, card);

        using (var titleFont = SkiaFontFactory.Create(18))
        using (var titlePaint = new SKPaint { Color = TitleColor, IsAntialias = true })
        {
            titleFont.Embolden = true;
            var text = TextRenderingHelper.Ellipsize(titleFont, _title, previewWidth);
            canvas.DrawText(text, card.Left + DialogPadding, TextRenderingHelper.BaselineForVerticalCenter(titleFont, card.Top + DialogPadding + TitleHeight / 2), titleFont, titlePaint);
        }

        var preview = new SKRect(card.Left + DialogPadding, card.Top + DialogPadding + TitleHeight, card.Right - DialogPadding, card.Top + DialogPadding + TitleHeight + previewHeight);
        DrawPreview(canvas, preview);

        var (primary, secondary) = Buttons;
        var buttonTop = preview.Bottom + ButtonSpacing * 2;
        var buttonWidth = Math.Min(180, (previewWidth - ButtonSpacing) / 2);
        _primaryBounds = primary == null ? SKRect.Empty : new SKRect(card.Right - DialogPadding - buttonWidth, buttonTop, card.Right - DialogPadding, buttonTop + ButtonHeight);
        var secondaryRight = primary == null ? card.Right - DialogPadding : _primaryBounds.Left - ButtonSpacing;
        _secondaryBounds = secondary == null ? SKRect.Empty : new SKRect(secondaryRight - buttonWidth, buttonTop, secondaryRight, buttonTop + ButtonHeight);

        if (secondary != null)
            DrawButton(canvas, _secondaryBounds, secondary, _hovered == 1 ? CancelButtonHoverColor : CancelButtonColor);
        if (primary != null)
        {
            var color = _controller.State == CameraCaptureState.Recording
                ? (_hovered == 0 ? DestructiveButtonHoverColor : DestructiveButtonColor)
                : (_hovered == 0 ? ButtonHoverColor : ButtonColor);
            DrawButton(canvas, _primaryBounds, primary, color);
        }

        if (_controller.State == CameraCaptureState.Recording)
        {
            var elapsed = _controller.RecordingElapsed;
            using var font = SkiaFontFactory.Create(15);
            using var dot = new SKPaint { Color = DestructiveButtonColor, IsAntialias = true };
            using var textPaint = new SKPaint { Color = TitleColor, IsAntialias = true };
            var y = _primaryBounds.MidY;
            canvas.DrawCircle(card.Left + DialogPadding + 6, y, 6, dot);
            canvas.DrawText($"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}", card.Left + DialogPadding + 20, TextRenderingHelper.BaselineForVerticalCenter(font, y), font, textPaint);
        }
    }

    private void DrawPreview(SKCanvas canvas, SKRect area)
    {
        using (var background = new SKPaint { Color = SKColors.Black, IsAntialias = true })
            canvas.DrawRoundRect(area, 8, 8, background);

        var image = _controller.State is CameraCaptureState.Failed ? null : _controller.CurrentImage();
        if (image != null && image.Width > 0 && image.Height > 0)
        {
            var scale = Math.Min(area.Width / image.Width, area.Height / image.Height);
            var w = image.Width * scale;
            var h = image.Height * scale;
            var dest = new SKRect(area.MidX - w / 2, area.MidY - h / 2, area.MidX + w / 2, area.MidY + h / 2);
            using var sk = SKImage.FromBitmap(image);
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(area, 8, 8), antialias: true);
            canvas.DrawImage(sk, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            canvas.Restore();
            return;
        }

        var message = _controller.State switch
        {
            CameraCaptureState.Failed => "The camera could not be opened" + (string.IsNullOrEmpty(_controller.ErrorMessage) ? "." : ": " + _controller.ErrorMessage),
            CameraCaptureState.Finishing => "Saving the video...",
            _ => "Starting the camera...",
        };
        using var font = SkiaFontFactory.Create(15);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var lines = WrapText(message, area.Width - 32, 15);
        var y = area.MidY - (lines.Count - 1) * 11;
        foreach (var line in lines)
        {
            var width = font.MeasureText(line);
            canvas.DrawText(line, area.MidX - width / 2, TextRenderingHelper.BaselineForVerticalCenter(font, y), font, paint);
            y += 22;
        }
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        var hovered = !_primaryBounds.IsEmpty && _primaryBounds.Contains(e.X, e.Y) ? 0
            : !_secondaryBounds.IsEmpty && _secondaryBounds.Contains(e.X, e.Y) ? 1 : -1;
        if (hovered != _hovered)
        {
            _hovered = hovered;
            Invalidate();
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!_primaryBounds.IsEmpty && _primaryBounds.Contains(e.X, e.Y))
            PressPrimary();
        else if (!_secondaryBounds.IsEmpty && _secondaryBounds.Contains(e.X, e.Y))
            PressSecondary();
        Invalidate();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Escape cancels the capture from any state (a recording is discarded).
            if (_controller.State is not (CameraCaptureState.Finishing or CameraCaptureState.Done))
            {
                Close();
                _ = _controller.CancelAsync();
            }
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            PressPrimary();
            e.Handled = true;
        }
        Invalidate();
    }
}
