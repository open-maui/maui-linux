// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Platform view for <c>SfSignaturePad</c>. Syncfusion's platform-neutral
/// <c>PlatformSignaturePad</c> carries the stroke model but its drawing,
/// input and export are empty stubs (<c>ToImageSource</c> throws). This view
/// runs the same model as the native builds: each pointer drag is smoothed
/// into Bézier segments through the recorded points, and each segment is
/// inked as a run of discs whose radius falls from
/// <c>MaximumStrokeThickness</c> towards <c>MinimumStrokeThickness</c> as the
/// pointer speeds up, blended with the previous speed so the width changes
/// smoothly.
/// </summary>
/// <remarks>
/// The ink is kept as a path in the view's own coordinates: it draws at any
/// scale and exports to a PNG (<see cref="ToPng"/>) without a second model.
/// Changing the thickness or colour re-inks the recorded strokes, as the native
/// builds' Redraw does.
/// </remarks>
public class SkiaSfSignaturePad : SkiaView
{
    private const float VelocityWeight = 0.9f;

    private sealed class TimedPoint
    {
        public float X, Y;
        public long Time;

        public TimedPoint(float x, float y, long time)
        {
            X = x; Y = y; Time = time;
        }

        public float DistanceTo(TimedPoint p) => MathF.Sqrt((p.X - X) * (p.X - X) + (p.Y - Y) * (p.Y - Y));

        /// <summary>Pixels per millisecond from <paramref name="start"/>.</summary>
        public float VelocityFrom(TimedPoint start)
        {
            long dt = Math.Max(1, Time - start.Time);
            float v = DistanceTo(start) / dt;
            return float.IsFinite(v) ? v : 0f;
        }
    }

    private readonly List<List<TimedPoint>> _strokes = new();
    private readonly List<List<float>> _pointsCollection = new();
    private readonly List<TimedPoint> _window = new();
    private List<float>? _currentPoints;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private SKPathBuilder _ink = new();
    private SKPath? _inkSnapshot;
    private bool _hasInk;

    private float _previousVelocity;
    private float _previousWidth = 4f; // (minimum + maximum) / 2, as the thickness setters leave it
    private float _minimumWidth = 3f;
    private float _maximumWidth = 5f;
    private SKColor _strokeColor = SKColors.Black;
    private Color _strokeColorMaui = Colors.Black;
    private bool _drawing;

    /// <summary>Asked before a stroke starts; false cancels it (the DrawStarted event).</summary>
    public Func<bool>? StrokeStarting { get; set; }

    /// <summary>Raised when a stroke ends (the DrawCompleted event).</summary>
    public event EventHandler? StrokeCompleted;

    public float MinimumStrokeThickness
    {
        get => _minimumWidth;
        set { if (_minimumWidth != value) { _minimumWidth = value; Reink(); } }
    }

    public float MaximumStrokeThickness
    {
        get => _maximumWidth;
        set { if (_maximumWidth != value) { _maximumWidth = value; Reink(); } }
    }

    public Color StrokeColor
    {
        get => _strokeColorMaui;
        set
        {
            _strokeColorMaui = value ?? Colors.Black;
            _strokeColor = _strokeColorMaui.ToSKColor();
            Invalidate();
        }
    }

    /// <summary>True when nothing has been drawn.</summary>
    public bool IsEmpty => _strokes.Count == 0;

    /// <summary>The completed strokes' points, x and y alternating (GetSignaturePoints).</summary>
    public List<List<float>> PointsCollection => _pointsCollection;

    /// <summary>Clears the signature.</summary>
    public void Clear()
    {
        _strokes.Clear();
        _pointsCollection.Clear();
        _window.Clear();
        _currentPoints = null;
        _drawing = false;
        ResetInk();
        ResetVelocity();
        Invalidate();
    }

    #region Stroke model

    private void ResetInk()
    {
        _ink.Dispose();
        _ink = new SKPathBuilder();
        _inkSnapshot?.Dispose();
        _inkSnapshot = null;
        _hasInk = false;
    }

    private void AddDisc(float x, float y, float radius)
    {
        _ink.AddCircle(x, y, radius, SKPathDirection.Clockwise);
        _inkSnapshot?.Dispose();
        _inkSnapshot = null;
        _hasInk = true;
    }

    /// <summary>The ink so far, as a path (cached until the next disc).</summary>
    private SKPath Ink => _inkSnapshot ??= _ink.Snapshot();

    private void ResetVelocity()
    {
        _previousVelocity = 0f;
        _previousWidth = (_minimumWidth + _maximumWidth) / 2f;
    }

    private void BeginStroke(float x, float y)
    {
        _currentPoints = new List<float> { x, y };
        _window.Clear();
        var stroke = new List<TimedPoint>();
        _strokes.Add(stroke);
        var point = new TimedPoint(x, y, _clock.ElapsedMilliseconds);
        stroke.Add(point);
        AddPoint(point);
    }

    private void ContinueStroke(float x, float y)
    {
        if (_currentPoints == null || _strokes.Count == 0)
            return;
        _currentPoints.Add(x);
        _currentPoints.Add(y);
        var point = new TimedPoint(x, y, _clock.ElapsedMilliseconds);
        _strokes[^1].Add(point);
        AddPoint(point);
    }

    private void EndStroke(float x, float y)
    {
        ContinueStroke(x, y);
        if (_currentPoints != null)
            _pointsCollection.Add(_currentPoints);
        _currentPoints = null;
    }

    /// <summary>
    /// Adds a point to the four-point window: with four points, the curve
    /// between the middle two is inked; the first point of a stroke inks a dot.
    /// </summary>
    private void AddPoint(TimedPoint point)
    {
        _window.Add(point);
        if (_window.Count > 3)
        {
            var (_, c2) = ControlPoints(_window[0], _window[1], _window[2]);
            var (c3, _) = ControlPoints(_window[1], _window[2], _window[3]);
            var start = _window[1];
            var end = _window[2];
            float velocity = end.VelocityFrom(start);
            velocity = VelocityWeight * velocity + (1f - VelocityWeight) * _previousVelocity;
            float width = Math.Max(_maximumWidth / (velocity + 1f), _minimumWidth);
            InkCurve(start, c2, c3, end, _previousWidth, width);
            _previousVelocity = velocity;
            _previousWidth = width;
            _window.RemoveAt(0);
        }
        else if (_window.Count == 1)
        {
            // Duplicate the first point so the first curve starts at it.
            _window.Add(new TimedPoint(point.X, point.Y, point.Time));
            AddDisc(point.X, point.Y, (_maximumWidth + _minimumWidth) / 2f);
        }
    }

    private static (SKPoint First, SKPoint Second) ControlPoints(TimedPoint s1, TimedPoint s2, TimedPoint s3)
    {
        float dx1 = s1.X - s2.X, dy1 = s1.Y - s2.Y;
        float dx2 = s2.X - s3.X, dy2 = s2.Y - s3.Y;
        float m1x = (s1.X + s2.X) / 2f, m1y = (s1.Y + s2.Y) / 2f;
        float m2x = (s2.X + s3.X) / 2f, m2y = (s2.Y + s3.Y) / 2f;
        float l1 = MathF.Sqrt(dx1 * dx1 + dy1 * dy1);
        float l2 = MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
        float k = l2 / (l1 + l2);
        if (float.IsNaN(k))
            k = 0f;
        float cmx = m2x + (m1x - m2x) * k;
        float cmy = m2y + (m1y - m2y) * k;
        float tx = s2.X - cmx, ty = s2.Y - cmy;
        return (new SKPoint(m1x + tx, m1y + ty), new SKPoint(m2x + tx, m2y + ty));
    }

    /// <summary>Inks a cubic Bézier as discs, one per pixel of its length, widening as the native builds do.</summary>
    private void InkCurve(TimedPoint start, SKPoint c1, SKPoint c2, TimedPoint end, float startWidth, float endWidth)
    {
        var p0 = new SKPoint(start.X, start.Y);
        var p3 = new SKPoint(end.X, end.Y);
        float steps = MathF.Ceiling(CurveLength(p0, c1, c2, p3));
        float delta = endWidth - startWidth;
        for (int i = 0; i < steps; i++)
        {
            float t = i / steps;
            var p = Bezier(p0, c1, c2, p3, t);
            AddDisc(p.X, p.Y, startWidth + t * t * t * delta);
        }
        Invalidate();
    }

    private static SKPoint Bezier(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float t)
    {
        float u = 1f - t;
        float a = u * u * u, b = 3f * u * u * t, c = 3f * u * t * t, d = t * t * t;
        return new SKPoint(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
    }

    private static float CurveLength(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3)
    {
        const int Samples = 10;
        float length = 0f;
        var previous = p0;
        for (int i = 1; i <= Samples; i++)
        {
            var p = Bezier(p0, p1, p2, p3, i / (float)Samples);
            length += SKPoint.Distance(previous, p);
            previous = p;
        }
        return length;
    }

    /// <summary>Re-inks every recorded stroke (thickness changed), as the native Redraw does.</summary>
    private void Reink()
    {
        ResetInk();
        foreach (var stroke in _strokes)
        {
            ResetVelocity();
            _window.Clear();
            foreach (var point in stroke)
                AddPoint(point);
        }
        ResetVelocity();
        Invalidate();
    }

    #endregion

    #region Drawing and export

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = WidthRequest >= 0 ? WidthRequest : double.IsInfinity(availableSize.Width) ? 350 : availableSize.Width;
        double height = HeightRequest >= 0 ? HeightRequest : double.IsInfinity(availableSize.Height) ? 350 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (!_hasInk)
            return;
        canvas.Save();
        canvas.ClipRect(bounds);
        canvas.Translate(bounds.Left, bounds.Top);
        using var paint = new SKPaint { Color = _strokeColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawPath(Ink, paint);
        canvas.Restore();
    }

    /// <summary>
    /// The signature as a PNG the size of the pad (times <paramref name="scale"/>),
    /// strokes only on a transparent background, as the native builds export it.
    /// Null when the pad has no size yet.
    /// </summary>
    public byte[]? ToPng(float scale = 1f)
    {
        int width = (int)Math.Ceiling(Bounds.Width * scale);
        int height = (int)Math.Ceiling(Bounds.Height * scale);
        if (width <= 0 || height <= 0)
            return null;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface == null)
            return null;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        using (var paint = new SKPaint { Color = _strokeColor, Style = SKPaintStyle.Fill, IsAntialias = true })
            canvas.DrawPath(Ink, paint);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray();
    }

    #endregion

    #region Input

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || _drawing || e.Button == PointerButton.Right)
            return;
        if (StrokeStarting != null && !StrokeStarting())
            return;
        _drawing = true;
        BeginStroke(e.X - (float)Bounds.Left, e.Y - (float)Bounds.Top);
        e.Handled = true;
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_drawing)
            return;
        ContinueStroke(e.X - (float)Bounds.Left, e.Y - (float)Bounds.Top);
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_drawing)
            return;
        _drawing = false;
        EndStroke(e.X - (float)Bounds.Left, e.Y - (float)Bounds.Top);
        e.Handled = true;
        StrokeCompleted?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
