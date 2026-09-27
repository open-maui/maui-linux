// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Skia platform view for CommunityToolkit.Maui's <c>DrawingView</c> (a
/// signature/sketch pad). The toolkit's generic net10.0 build has no platform
/// view (its handler's PlatformView is <c>object</c>), so OpenMaui supplies this
/// one: it draws the view's <c>Lines</c> plus the stroke in progress, and turns
/// pointer strokes into lines exactly like the toolkit's MauiDrawingView does
/// on its own platforms (start / point drawn / completed / cancelled callbacks,
/// multi-line and clear-on-finish modes).
/// </summary>
/// <remarks>
/// OpenMaui does not reference the toolkit: the virtual view is driven through
/// its public <c>CommunityToolkit.Maui.Core.IDrawingView</c> and
/// <c>IDrawingLine</c> contracts, bound by name once per process
/// (<see cref="DrawingViewContract"/>).
/// </remarks>
public class SkiaDrawingView : SkiaView
{
    private object? _drawingView;
    private DrawingViewContract? _contract;
    private INotifyCollectionChanged? _observedLines;
    private readonly List<INotifyCollectionChanged> _observedPoints = new();
    private List<PointF>? _currentStroke;

    /// <summary>The toolkit DrawingView this view renders (null when disconnected).</summary>
    internal object? DrawingView
    {
        get => _drawingView;
        set
        {
            if (ReferenceEquals(_drawingView, value)) return;
            Unobserve();
            _drawingView = value;
            _contract = value == null ? null : DrawingViewContract.For(value.GetType());
            Observe();
            Invalidate();
        }
    }

    /// <summary>Re-reads the lines collection (after the virtual view replaced it) and redraws.</summary>
    internal void Refresh()
    {
        Unobserve();
        Observe();
        Invalidate();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Like GraphicsView: take the space offered, with a usable default when unconstrained.
        double w = double.IsInfinity(availableSize.Width) || availableSize.Width >= double.MaxValue ? 200 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) || availableSize.Height >= double.MaxValue ? 200 : availableSize.Height;
        return new Size(w, h);
    }

    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        if (_drawingView is not { } view || _contract is not { } c) return;

        canvas.Save();
        canvas.ClipRect(bounds);
        canvas.Translate(bounds.Left, bounds.Top);
        var local = new RectF(0, 0, bounds.Width, bounds.Height);

        using (var mauiCanvas = new SkiaCanvas { Canvas = canvas })
        {
            if (view is IView { Background: SolidPaint { Color: { } background } })
            {
                mauiCanvas.FillColor = background;
                mauiCanvas.FillRectangle(local);
            }

            c.GetDrawAction(view)?.Invoke(mauiCanvas, local);

            foreach (var line in c.GetLines(view))
            {
                if (line == null) continue;
                DrawStroke(mauiCanvas, c.GetLinePoints(line), c.GetLineColor(line), c.GetLineWidth(line));
            }

            if (_currentStroke is { Count: > 0 })
                DrawStroke(mauiCanvas, _currentStroke, c.GetLineColor(view), c.GetLineWidth(view));
        }

        canvas.Restore();
    }

    private static void DrawStroke(ICanvas canvas, IReadOnlyList<PointF> points, Color? color, float width)
    {
        if (points.Count == 0) return;
        canvas.StrokeColor = color ?? Colors.Black;
        canvas.StrokeSize = width;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeDashPattern = null;

        if (points.Count == 1)
        {
            canvas.FillColor = canvas.StrokeColor = color ?? Colors.Black;
            canvas.FillCircle(points[0].X, points[0].Y, width / 2f);
            return;
        }

        var path = new PathF(points[0]);
        for (int i = 1; i < points.Count; i++)
            path.LineTo(points[i]);
        canvas.DrawPath(path);
    }

    // ---- Input: pointer strokes become lines --------------------------------

    private PointF ToLocal(PointerEventArgs e) => new((float)(e.X - Bounds.Left), (float)(e.Y - Bounds.Top));

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_drawingView is not { } view || _contract is not { } c || !IsEnabled) return;

        var point = ToLocal(e);
        if (!c.GetIsMultiLineModeEnabled(view))
            c.ClearLines(view);
        _currentStroke = new List<PointF> { point };
        c.OnDrawingLineStarted(view, point);
        e.Handled = true;
        Invalidate();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_currentStroke == null || _drawingView is not { } view || _contract is not { } c) return;

        var point = ToLocal(e);
        _currentStroke.Add(point);
        c.OnPointDrawn(view, point);
        e.Handled = true;
        Invalidate();
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_currentStroke is not { } stroke || _drawingView is not { } view || _contract is not { } c) return;
        _currentStroke = null;

        var line = c.CreateLine(view, stroke);
        c.AddLine(view, line);
        c.OnDrawingLineCompleted(view, line);
        if (c.GetShouldClearOnFinish(view))
            c.ClearLines(view);
        e.Handled = true;
        Invalidate();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        // A stroke that leaves the pad while the button is down keeps drawing
        // (pointer capture delivers the moves); only an explicit cancel drops it.
    }

    /// <summary>Abandons the stroke in progress (e.g. the window lost the pointer grab).</summary>
    internal void CancelStroke()
    {
        if (_currentStroke == null || _drawingView is not { } view || _contract is not { } c) return;
        _currentStroke = null;
        c.OnDrawingLineCancelled(view);
        Invalidate();
    }

    // ---- Change tracking ----------------------------------------------------------

    private void Observe()
    {
        if (_drawingView is not { } view || _contract is not { } c) return;
        if (view is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnViewPropertyChanged;
        _observedLines = c.GetLinesCollection(view) as INotifyCollectionChanged;
        if (_observedLines != null)
            _observedLines.CollectionChanged += OnLinesChanged;
        ObservePoints();
    }

    private void Unobserve()
    {
        if (_drawingView is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnViewPropertyChanged;
        if (_observedLines != null)
            _observedLines.CollectionChanged -= OnLinesChanged;
        _observedLines = null;
        foreach (var points in _observedPoints)
            points.CollectionChanged -= OnPointsChanged;
        _observedPoints.Clear();
    }

    private void ObservePoints()
    {
        foreach (var points in _observedPoints)
            points.CollectionChanged -= OnPointsChanged;
        _observedPoints.Clear();
        if (_drawingView is not { } view || _contract is not { } c) return;
        foreach (var line in c.GetLines(view))
        {
            if (line != null && c.GetLinePointsCollection(line) is INotifyCollectionChanged points)
            {
                points.CollectionChanged += OnPointsChanged;
                _observedPoints.Add(points);
            }
        }
    }

    private void OnViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Lines")
            Refresh();
        else
            Invalidate();
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObservePoints();
        Invalidate();
    }

    private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Unobserve();
        base.Dispose(disposing);
    }
}

/// <summary>
/// The members of CommunityToolkit.Maui.Core's <c>IDrawingView</c> and
/// <c>IDrawingLine</c> that <see cref="SkiaDrawingView"/> uses, bound by name
/// from the loaded toolkit assembly (interface members, so explicit
/// implementations on DrawingView are reached too).
/// </summary>
internal sealed class DrawingViewContract
{
    public const string DrawingViewTypeName = "CommunityToolkit.Maui.Views.DrawingView, CommunityToolkit.Maui";
    private const string ViewInterface = "CommunityToolkit.Maui.Core.IDrawingView";
    private const string LineInterface = "CommunityToolkit.Maui.Core.IDrawingLine";
    private const string LineType = "CommunityToolkit.Maui.Core.Views.DrawingLine";

    private static readonly Dictionary<Type, DrawingViewContract?> s_cache = new();

    private readonly PropertyInfo _lines, _lineColor, _lineWidth, _multiLine, _clearOnFinish, _drawAction;
    private readonly MethodInfo _started, _cancelled, _pointDrawn, _completed;
    private readonly PropertyInfo _linePoints, _lineLineColor, _lineLineWidth;
    private readonly Type _lineType;

    private DrawingViewContract(Type view, Type line, Type lineImpl)
    {
        _lines = view.GetProperty("Lines")!;
        _lineColor = view.GetProperty("LineColor")!;
        _lineWidth = view.GetProperty("LineWidth")!;
        _multiLine = view.GetProperty("IsMultiLineModeEnabled")!;
        _clearOnFinish = view.GetProperty("ShouldClearOnFinish")!;
        _drawAction = view.GetProperty("DrawAction")!;
        _started = view.GetMethod("OnDrawingLineStarted")!;
        _cancelled = view.GetMethod("OnDrawingLineCancelled")!;
        _pointDrawn = view.GetMethod("OnPointDrawn")!;
        _completed = view.GetMethod("OnDrawingLineCompleted")!;
        _linePoints = line.GetProperty("Points")!;
        _lineLineColor = line.GetProperty("LineColor")!;
        _lineLineWidth = line.GetProperty("LineWidth")!;
        _lineType = lineImpl;
    }

    /// <summary>The contract for a DrawingView type, or null if the toolkit's shape is not recognised.</summary>
    public static DrawingViewContract? For(Type drawingViewType)
    {
        lock (s_cache)
        {
            if (s_cache.TryGetValue(drawingViewType, out var cached))
                return cached;
            DrawingViewContract? contract = null;
            try
            {
                var view = drawingViewType.GetInterface(ViewInterface);
                var line = view?.Assembly.GetType(LineInterface);
                var impl = view?.Assembly.GetType(LineType);
                if (view != null && line != null && impl != null)
                    contract = new DrawingViewContract(view, line, impl);
            }
            catch (Exception ex) when (ex is AmbiguousMatchException or NullReferenceException or TypeLoadException)
            {
                contract = null;
            }
            if (contract == null)
                DiagnosticLog.Error("SkiaDrawingView", $"{drawingViewType.FullName} does not expose the expected IDrawingView contract");
            s_cache[drawingViewType] = contract;
            return contract;
        }
    }

    public IList? GetLinesCollection(object view) => _lines.GetValue(view) as IList;

    public IEnumerable<object?> GetLines(object view)
        => GetLinesCollection(view)?.Cast<object?>().ToArray() ?? Array.Empty<object?>();

    public Color? GetLineColor(object viewOrLine)
        => (viewOrLine.GetType().GetInterface(LineInterface) != null ? _lineLineColor : _lineColor).GetValue(viewOrLine) as Color;

    public float GetLineWidth(object viewOrLine)
        => (float)((viewOrLine.GetType().GetInterface(LineInterface) != null ? _lineLineWidth : _lineWidth).GetValue(viewOrLine) ?? 5f);

    public bool GetIsMultiLineModeEnabled(object view) => (bool)(_multiLine.GetValue(view) ?? false);

    public bool GetShouldClearOnFinish(object view) => (bool)(_clearOnFinish.GetValue(view) ?? false);

    public Action<ICanvas, RectF>? GetDrawAction(object view) => _drawAction.GetValue(view) as Action<ICanvas, RectF>;

    public IList? GetLinePointsCollection(object line) => _linePoints.GetValue(line) as IList;

    public IReadOnlyList<PointF> GetLinePoints(object line)
        => GetLinePointsCollection(line)?.Cast<PointF>().ToArray() ?? Array.Empty<PointF>();

    public void ClearLines(object view) => GetLinesCollection(view)?.Clear();

    public void AddLine(object view, object line) => GetLinesCollection(view)?.Add(line);

    /// <summary>A toolkit DrawingLine with the view's current colour and width.</summary>
    public object CreateLine(object view, IEnumerable<PointF> points)
    {
        var line = Activator.CreateInstance(_lineType)!;
        _lineLineColor.SetValue(line, GetLineColor(view));
        _lineLineWidth.SetValue(line, GetLineWidth(view));
        _linePoints.SetValue(line, new System.Collections.ObjectModel.ObservableCollection<PointF>(points));
        return line;
    }

    public void OnDrawingLineStarted(object view, PointF point) => _started.Invoke(view, new object[] { point });

    public void OnDrawingLineCancelled(object view) => _cancelled.Invoke(view, null);

    public void OnPointDrawn(object view, PointF point) => _pointDrawn.Invoke(view, new object[] { point });

    public void OnDrawingLineCompleted(object view, object line) => _completed.Invoke(view, new[] { line });
}
