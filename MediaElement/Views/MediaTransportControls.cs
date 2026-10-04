// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Views;

/// <summary>A request made through the playback controls overlay.</summary>
internal enum MediaTransportCommand
{
    /// <summary>Play when paused or stopped, pause when playing.</summary>
    PlayPause,
    /// <summary>Seek to <c>Value</c> seconds.</summary>
    Seek,
    ToggleMute,
    ToggleRepeat,
    /// <summary>Set the playback rate to <c>Value</c>.</summary>
    SetRate,
}

/// <summary>
/// The playback controls drawn over the video when
/// <c>MediaElement.ShouldShowPlaybackControls</c> is true, the counterpart of
/// the WinUI <c>MediaTransportControls</c> the toolkit enables on Windows
/// (<c>MediaPlayerElement.AreTransportControlsEnabled</c>): a seek bar with the
/// elapsed and remaining time, a mute button, play/pause, the playback rate
/// menu (0.25, 0.5, Normal, 1.5, 2, as WinUI offers), repeat and zoom
/// (aspect fit / fill). Like WinUI's they show while the media is not playing
/// and, while it plays, for three seconds after the pointer last moved over
/// the video.
/// </summary>
internal sealed class MediaTransportControls
{
    internal static readonly double[] Rates = { 0.25, 0.5, 1.0, 1.5, 2.0 };
    internal static readonly TimeSpan HideDelay = TimeSpan.FromSeconds(3);

    private const float BarHeight = 84f;
    private const float ButtonSize = 36f;
    private const float SeekRowY = 18f;      // centre of the seek row from the bar top
    private const float ButtonRowY = 56f;    // centre of the button row from the bar top
    private const float TimeWidth = 64f;
    private const float SidePadding = 12f;

    private static readonly SKColor Background = new(0, 0, 0, 0xB0);
    private static readonly SKColor Foreground = SKColors.White;
    private static readonly SKColor Track = new(255, 255, 255, 0x60);
    private static readonly SKColor Accent = new(0x00, 0x78, 0xD4);
    private static readonly SKColor Hover = new(255, 255, 255, 0x30);

    private readonly Func<DateTime> _clock;
    private DateTime _lastActivity = DateTime.MinValue;
    private Part _pressed = Part.None;
    private Part _hovered = Part.None;
    private double? _dragSeconds;
    private bool _rateMenuOpen;

    public MediaTransportControls(Func<DateTime>? clock = null) => _clock = clock ?? (() => DateTime.UtcNow);

    /// <summary>Raised for a control the user clicked; the value is the seek target (seconds) or the rate.</summary>
    public event Action<MediaTransportCommand, double>? Command;

    // ---- state pushed by the element ------------------------------------
    public bool Enabled { get; set; }
    public bool HasMedia { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsMuted { get; set; }
    public bool IsLooping { get; set; }
    public double Rate { get; set; } = 1.0;
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }

    internal enum Part
    {
        None,
        Bar,
        SeekBar,
        Mute,
        PlayPause,
        RateButton,
        Repeat,
        Zoom,
        RateItem0,
    }

    /// <summary>Zoom was clicked (handled by the element: aspect fit / fill).</summary>
    public event Action? ZoomRequested;

    /// <summary>True when the controls are drawn and take input.</summary>
    public bool IsVisible
    {
        get
        {
            if (!Enabled || !HasMedia) return false;
            if (!IsPlaying || _pressed != Part.None || _rateMenuOpen) return true;
            return _clock() - _lastActivity < HideDelay;
        }
    }

    /// <summary>When the controls of a playing element hide by themselves, or null.</summary>
    public TimeSpan? TimeUntilHide
    {
        get
        {
            if (!Enabled || !HasMedia || !IsPlaying || _pressed != Part.None || _rateMenuOpen) return null;
            var left = HideDelay - (_clock() - _lastActivity);
            return left > TimeSpan.Zero ? left : null;
        }
    }

    /// <summary>The pointer moved over the video: the controls show.</summary>
    public void NoteActivity() => _lastActivity = _clock();

    // ---- layout -----------------------------------------------------------

    internal static SKRect BarRect(SKRect bounds)
    {
        float height = Math.Min(BarHeight, bounds.Height);
        return new SKRect(bounds.Left, bounds.Bottom - height, bounds.Right, bounds.Bottom);
    }

    internal static SKRect SeekTrackRect(SKRect bounds)
    {
        var bar = BarRect(bounds);
        float y = bar.Top + SeekRowY;
        float left = bar.Left + SidePadding + TimeWidth;
        float right = Math.Max(left, bar.Right - SidePadding - TimeWidth);
        return new SKRect(left, y - 2, right, y + 2);
    }

    internal static SKRect ButtonRect(SKRect bounds, Part part)
    {
        var bar = BarRect(bounds);
        float y = bar.Top + ButtonRowY;
        float cx = part switch
        {
            Part.Mute => bar.Left + SidePadding + ButtonSize / 2,
            Part.PlayPause => bar.MidX,
            Part.Zoom => bar.Right - SidePadding - ButtonSize / 2,
            Part.Repeat => bar.Right - SidePadding - ButtonSize * 1.5f,
            Part.RateButton => bar.Right - SidePadding - ButtonSize * 2.5f,
            _ => float.NaN,
        };
        return new SKRect(cx - ButtonSize / 2, y - ButtonSize / 2, cx + ButtonSize / 2, y + ButtonSize / 2);
    }

    internal static SKRect RateItemRect(SKRect bounds, int index)
    {
        var button = ButtonRect(bounds, Part.RateButton);
        const float itemHeight = 28f, width = 84f;
        float bottom = button.Top - 4 - (Rates.Length - 1 - index) * itemHeight;
        float left = Math.Max(bounds.Left, button.MidX - width / 2);
        return new SKRect(left, bottom - itemHeight, left + width, bottom);
    }

    internal Part PartAt(SKRect bounds, float x, float y)
    {
        if (_rateMenuOpen)
        {
            for (int i = 0; i < Rates.Length; i++)
                if (RateItemRect(bounds, i).Contains(x, y))
                    return Part.RateItem0 + i;
        }
        var bar = BarRect(bounds);
        if (!bar.Contains(x, y)) return Part.None;
        foreach (var part in new[] { Part.Mute, Part.PlayPause, Part.RateButton, Part.Repeat, Part.Zoom })
            if (ButtonRect(bounds, part).Contains(x, y))
                return part;
        var track = SeekTrackRect(bounds);
        var seekHit = new SKRect(track.Left - 6, track.MidY - 10, track.Right + 6, track.MidY + 10);
        if (seekHit.Contains(x, y)) return Part.SeekBar;
        return Part.Bar;
    }

    /// <summary>True when the point is on the visible controls (they take the press).</summary>
    public bool HitTest(SKRect bounds, float x, float y) => IsVisible && PartAt(bounds, x, y) != Part.None;

    // ---- input ------------------------------------------------------------

    public void PointerMoved(SKRect bounds, float x, float y)
    {
        NoteActivity();
        _hovered = IsVisible ? PartAt(bounds, x, y) : Part.None;
        if (_pressed == Part.SeekBar)
            _dragSeconds = SecondsAt(bounds, x);
    }

    public void PointerExited()
    {
        _hovered = Part.None;
    }

    /// <summary>A press on the controls; false when it is not on them.</summary>
    public bool PointerPressed(SKRect bounds, float x, float y)
    {
        NoteActivity();
        if (!IsVisible) return false;
        var part = PartAt(bounds, x, y);
        if (part == Part.None)
        {
            _rateMenuOpen = false;
            return false;
        }
        _pressed = part;
        if (part == Part.SeekBar)
            _dragSeconds = SecondsAt(bounds, x);
        return true;
    }

    /// <summary>Completes a press the controls took (a click, or the end of a seek drag).</summary>
    public void PointerReleased(SKRect bounds, float x, float y)
    {
        var pressed = _pressed;
        _pressed = Part.None;
        NoteActivity();
        if (pressed == Part.SeekBar)
        {
            var seconds = SecondsAt(bounds, x);
            _dragSeconds = null;
            if (seconds is double s)
            {
                Position = TimeSpan.FromSeconds(s);
                Command?.Invoke(MediaTransportCommand.Seek, s);
            }
            return;
        }

        if (PartAt(bounds, x, y) != pressed) return; // released elsewhere: no click
        switch (pressed)
        {
            case Part.PlayPause:
                Command?.Invoke(MediaTransportCommand.PlayPause, 0);
                break;
            case Part.Mute:
                Command?.Invoke(MediaTransportCommand.ToggleMute, 0);
                break;
            case Part.Repeat:
                Command?.Invoke(MediaTransportCommand.ToggleRepeat, 0);
                break;
            case Part.Zoom:
                ZoomRequested?.Invoke();
                break;
            case Part.RateButton:
                _rateMenuOpen = !_rateMenuOpen;
                break;
            case >= Part.RateItem0:
                _rateMenuOpen = false;
                Command?.Invoke(MediaTransportCommand.SetRate, Rates[pressed - Part.RateItem0]);
                break;
        }
    }

    private double? SecondsAt(SKRect bounds, float x)
    {
        if (Duration <= TimeSpan.Zero) return null;
        var track = SeekTrackRect(bounds);
        if (track.Width <= 0) return null;
        double fraction = Math.Clamp((x - track.Left) / track.Width, 0, 1);
        return fraction * Duration.TotalSeconds;
    }

    // ---- drawing ----------------------------------------------------------

    internal static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    public void Draw(SKCanvas canvas, SKRect bounds)
    {
        if (!IsVisible || bounds.Width < 2 * ButtonSize || bounds.Height < ButtonSize) return;

        var bar = BarRect(bounds);
        using var fill = new SKPaint { Color = Background, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRect(bar, fill);

        // Seek row: elapsed | track | remaining.
        var position = _dragSeconds is double drag ? TimeSpan.FromSeconds(drag) : Position;
        var track = SeekTrackRect(bounds);
        using (var font = SkiaFontFactory.Create(12f))
        using (var text = new SKPaint { Color = Foreground, IsAntialias = true })
        {
            float baseline = track.MidY + 4;
            canvas.DrawText(FormatTime(position), bar.Left + SidePadding, baseline, SKTextAlign.Left, font, text);
            var remaining = Duration > TimeSpan.Zero ? Duration - position : TimeSpan.Zero;
            canvas.DrawText("-" + FormatTime(remaining), bar.Right - SidePadding, baseline, SKTextAlign.Right, font, text);
        }
        if (track.Width > 0)
        {
            fill.Color = Track;
            canvas.DrawRoundRect(track, 2, 2, fill);
            if (Duration > TimeSpan.Zero)
            {
                float fraction = (float)Math.Clamp(position.TotalSeconds / Duration.TotalSeconds, 0, 1);
                float x = track.Left + track.Width * fraction;
                fill.Color = Accent;
                canvas.DrawRoundRect(new SKRect(track.Left, track.Top, x, track.Bottom), 2, 2, fill);
                fill.Color = Foreground;
                canvas.DrawCircle(x, track.MidY, _pressed == Part.SeekBar || _hovered == Part.SeekBar ? 8 : 6, fill);
            }
        }

        // Button row.
        foreach (var part in new[] { Part.Mute, Part.PlayPause, Part.RateButton, Part.Repeat, Part.Zoom })
        {
            var rect = ButtonRect(bounds, part);
            if (_hovered == part || _pressed == part)
            {
                fill.Color = Hover;
                canvas.DrawRoundRect(rect, 4, 4, fill);
            }
            DrawIcon(canvas, part, rect);
        }

        if (_rateMenuOpen)
            DrawRateMenu(canvas, bounds);
    }

    private void DrawIcon(SKCanvas canvas, Part part, SKRect r)
    {
        using var paint = new SKPaint { Color = Foreground, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var stroke = new SKPaint { Color = Foreground, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
        float cx = r.MidX, cy = r.MidY, s = r.Width * 0.28f;
        using var path = new SKPathBuilder();
        switch (part)
        {
            case Part.PlayPause when IsPlaying:
                canvas.DrawRect(new SKRect(cx - s * 0.8f, cy - s, cx - s * 0.25f, cy + s), paint);
                canvas.DrawRect(new SKRect(cx + s * 0.25f, cy - s, cx + s * 0.8f, cy + s), paint);
                break;
            case Part.PlayPause:
                path.MoveTo(cx - s * 0.7f, cy - s);
                path.LineTo(cx + s, cy);
                path.LineTo(cx - s * 0.7f, cy + s);
                path.Close();
                using (var shape = path.Detach()) canvas.DrawPath(shape, paint);
                break;
            case Part.Mute:
                path.MoveTo(cx - s, cy - s * 0.4f);
                path.LineTo(cx - s * 0.4f, cy - s * 0.4f);
                path.LineTo(cx + s * 0.2f, cy - s);
                path.LineTo(cx + s * 0.2f, cy + s);
                path.LineTo(cx - s * 0.4f, cy + s * 0.4f);
                path.LineTo(cx - s, cy + s * 0.4f);
                path.Close();
                using (var shape = path.Detach()) canvas.DrawPath(shape, paint);
                if (IsMuted)
                {
                    canvas.DrawLine(cx + s * 0.5f, cy - s * 0.4f, cx + s * 1.1f, cy + s * 0.4f, stroke);
                    canvas.DrawLine(cx + s * 1.1f, cy - s * 0.4f, cx + s * 0.5f, cy + s * 0.4f, stroke);
                }
                else
                {
                    canvas.DrawArc(new SKRect(cx - s * 0.2f, cy - s * 0.7f, cx + s * 1.0f, cy + s * 0.7f), -50, 100, false, stroke);
                }
                break;
            case Part.Repeat:
                stroke.Color = IsLooping ? Accent : Foreground;
                canvas.DrawRoundRect(new SKRect(cx - s, cy - s * 0.6f, cx + s, cy + s * 0.6f), s * 0.4f, s * 0.4f, stroke);
                paint.Color = stroke.Color;
                path.MoveTo(cx + s * 0.1f, cy - s * 1.0f);
                path.LineTo(cx + s * 0.6f, cy - s * 0.6f);
                path.LineTo(cx + s * 0.1f, cy - s * 0.2f);
                path.Close();
                using (var shape = path.Detach()) canvas.DrawPath(shape, paint);
                break;
            case Part.Zoom:
                canvas.DrawRect(new SKRect(cx - s, cy - s * 0.7f, cx + s, cy + s * 0.7f), stroke);
                canvas.DrawLine(cx - s * 0.5f, cy, cx + s * 0.5f, cy, stroke);
                canvas.DrawLine(cx - s * 0.5f, cy, cx - s * 0.2f, cy - s * 0.3f, stroke);
                canvas.DrawLine(cx - s * 0.5f, cy, cx - s * 0.2f, cy + s * 0.3f, stroke);
                canvas.DrawLine(cx + s * 0.5f, cy, cx + s * 0.2f, cy - s * 0.3f, stroke);
                canvas.DrawLine(cx + s * 0.5f, cy, cx + s * 0.2f, cy + s * 0.3f, stroke);
                break;
            case Part.RateButton:
                using (var font = SkiaFontFactory.Create(12f))
                    canvas.DrawText(RateLabel(Rate, compact: true), cx, cy + 4, SKTextAlign.Center, font, paint);
                break;
        }
    }

    internal static string RateLabel(double rate, bool compact)
    {
        if (Math.Abs(rate - 1.0) < 0.001) return compact ? "1x" : "Normal";
        return rate.ToString("0.##", CultureInfo.InvariantCulture) + (compact ? "x" : "");
    }

    private void DrawRateMenu(SKCanvas canvas, SKRect bounds)
    {
        using var fill = new SKPaint { Color = new SKColor(0x20, 0x20, 0x20, 0xF0), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var text = new SKPaint { Color = Foreground, IsAntialias = true };
        using var font = SkiaFontFactory.Create(13f);
        for (int i = 0; i < Rates.Length; i++)
        {
            var rect = RateItemRect(bounds, i);
            bool selected = Math.Abs(Rates[i] - Rate) < 0.001;
            fill.Color = _hovered == Part.RateItem0 + i ? new SKColor(0x40, 0x40, 0x40, 0xF0) : new SKColor(0x20, 0x20, 0x20, 0xF0);
            canvas.DrawRect(rect, fill);
            text.Color = selected ? Accent : Foreground;
            canvas.DrawText(RateLabel(Rates[i], compact: false), rect.MidX, rect.MidY + 5, SKTextAlign.Center, font, text);
        }
    }
}
