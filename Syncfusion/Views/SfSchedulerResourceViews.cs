// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.Graphics.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The time ruler of SfScheduler's desktop resource view (the Windows and Mac builds'
/// <c>DayViewTimeRulerView</c>, absent from the platform-neutral build). With resources side by
/// side the day view's own time ruler would scroll away horizontally with them, so the desktop
/// builds draw the time labels in a separate column, in a scroll view of its own that follows
/// the time slots vertically. The labels are the day view's: one per time slot line from the
/// second, in the time ruler's text style and format.
/// </summary>
internal sealed class SfSchedulerTimeRulerView : SfView
{
    private readonly object _info; // ISchedulerViewInfo
    private View? _resizeIndicator;
    private double _viewPortHeight;

    internal SfSchedulerTimeRulerView(object schedulerViewInfo)
    {
        _info = schedulerViewInfo;
        Set(this, "DrawingOrder", DrawingOrder.AboveContent);
    }

    /// <summary>Shows the time a resized appointment's edge has reached, beside the ruler.</summary>
    internal void AddAppointmentResizeIndicatorView(View indicator)
    {
        if (_resizeIndicator != null)
            return;
        _resizeIndicator = indicator;
        Children.Add(indicator);
    }

    internal void RemoveAppointmentResizeIndicatorView()
    {
        if (_resizeIndicator == null)
            return;
        Children.Remove(_resizeIndicator);
        _resizeIndicator.Handler?.DisconnectHandler();
        _resizeIndicator = null;
    }

    internal void InvalidateLayout()
    {
        ((IView)this).InvalidateMeasure();
        ((IDrawableLayout)this).InvalidateDrawable();
    }

    internal void UpdateViewPortHeight(double height) => _viewPortHeight = height;

    protected override void OnDraw(ICanvas canvas, RectF dirtyRect)
    {
        try
        {
            var helper = SfSchedulerResourceView.ViewHelper!;
            var daysView = Get(_info, "DaysView");
            var view = Get(_info, "View");
            canvas.SaveState();
            canvas.StrokeSize = 1f;
            canvas.StrokeColor = (Color)CallStatic(helper, "BrushToColorConverter", Get(_info, "CellBorderBrush"))!;
            int lines = (int)CallStatic(helper, "GetHorizontalLinesCount", daysView, view)!;
            float rulerWidth = (float)Get<double>(daysView, "TimeRulerWidth");
            float interval = (float)(double)CallStatic(helper, "GetTimeInterval", daysView)!;
            float slot = (float)CallStatic(helper, "GetTimeIntervalSize", Get<double>(daysView, "TimeIntervalHeight"), view, (double)dirtyRect.Height, lines)!;
            double startHour = Get<double>(daysView, "StartHour");
            int wholeHour = (int)startHour;
            double minutes = (startHour - wholeHour) * 60.0;
            var now = DateTime.Now;
            var time = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).AddMinutes(wholeHour * 60f + interval + minutes);
            float y = slot / 2f;
            float x = Is(_info, "IsRTLLayout") ? dirtyRect.Width - rulerWidth : 0f;
            var culture = CallStatic(helper, "GetCurrentUICultureInfo", Get(_info, "CalendarType")) as IFormatProvider;
            var format = Get<string>(daysView, "TimeFormat");
            var style = Get(daysView, "TimeRulerTextStyle");
            for (int i = 1; i < lines; i++)
            {
                CallStatic(helper, "DrawText", canvas, time.ToString(format, culture), style, new Rect(x, y, rulerWidth, slot),
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                y += slot;
                time = time.AddMinutes(interval);
            }
            canvas.RestoreState();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfScheduler time ruler drawing failed", ex);
        }
    }

    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var helper = SfSchedulerResourceView.ViewHelper!;
        var daysView = Get(_info, "DaysView");
        var view = Get(_info, "View");
        int lines = (int)CallStatic(helper, "GetHorizontalLinesCount", daysView, view)!;
        double width = double.IsFinite(widthConstraint) ? widthConstraint : 0.0;
        double height = double.IsFinite(heightConstraint) ? heightConstraint : 0.0;
        double intervalHeight = Get<double>(daysView, "TimeIntervalHeight");
        if (intervalHeight != -1.0)
            height = lines * (float)CallStatic(helper, "GetTimeIntervalSize", intervalHeight, view, 0.0, 0)!;
        else if (!double.IsFinite(heightConstraint))
            height = _viewPortHeight;
        return new Size(width, height);
    }
}

/// <summary>
/// The expander of the all-day panel in SfScheduler's desktop resource view (the Windows and Mac
/// builds' <c>ExpandedIcon</c>): with resources side by side the all-day panel scrolls
/// horizontally, so the chevron that expands or collapses it is drawn in the time ruler column
/// below the resource header, where it stays in view. The day view control toggles the panel
/// when the chevron's rectangle is tapped.
/// </summary>
internal sealed class SfSchedulerAllDayExpanderView : SfView
{
    private readonly object _info;          // ISchedulerViewInfo
    private readonly object _dayView;       // IDaysViewInteraction (the DayViewControl)
    private readonly Action<SemanticsNode, bool> _semanticsNodeClick;
    private System.Collections.IList? _visibleAppointments;
    private Rect? _expanderRectangle;

    internal SfSchedulerAllDayExpanderView(System.Collections.IList? visibleAppointments, object schedulerViewInfo, object dayViewInteraction,
        Action<SemanticsNode, bool> semanticsNodeClick)
    {
        _info = schedulerViewInfo;
        _visibleAppointments = visibleAppointments;
        _dayView = dayViewInteraction;
        _semanticsNodeClick = semanticsNodeClick;
        Set(this, "DrawingOrder", DrawingOrder.BelowContent);
    }

    internal void UpdateVisibleAppointments(System.Collections.IList? visibleAppointments)
    {
        if (!ReferenceEquals(_visibleAppointments, visibleAppointments))
            _visibleAppointments = visibleAppointments;
    }

    /// <summary>The chevron's rectangle (in this view), once drawn.</summary>
    internal Rect? GetExpandableRect() => _expanderRectangle;

    private bool IsExpandable() => Call(_dayView, "IsExpandable") is true;

    private bool IsExpanded() => Call(_dayView, "IsExpanded") is true;

    private double AllDayAppointmentHeight() => (double)Call(_dayView, "AllDayAppointmentHeight")!;

    protected override void OnDraw(ICanvas canvas, RectF dirtyRect)
    {
        try
        {
            if (_visibleAppointments is not { Count: > 0 } || !IsExpandable())
                return;
            canvas.StrokeSize = 1f;
            double rowHeight = AllDayAppointmentHeight();
            canvas.StrokeColor = Get(_info, "MoreAppointmentIndicatorColor") as Color ?? Colors.Black;
            double centerX = dirtyRect.Width / 2f;
            double centerY = dirtyRect.Height - rowHeight / 2.0;
            const double arm = 7.0;
            // An upward chevron collapses the expanded panel, a downward one expands it.
            double tipY = IsExpanded() ? centerY - arm / 2.0 : centerY + arm / 2.0;
            double endY = IsExpanded() ? tipY + arm : tipY - arm;
            canvas.DrawLine((float)centerX, (float)tipY, (float)(centerX - arm), (float)endY);
            canvas.DrawLine((float)centerX, (float)tipY, (float)(centerX + arm), (float)endY);
            _expanderRectangle = new Rect(0.0, dirtyRect.Height - rowHeight, dirtyRect.Width, rowHeight);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfScheduler all-day expander drawing failed", ex);
        }
    }

    protected override List<SemanticsNode>? GetSemanticsNodesCore(double width, double height)
    {
        if (width == 0.0 || height == 0.0)
            return null;
        var nodes = new List<SemanticsNode>();
        if (!IsExpandable())
            return nodes;
        var helper = SfSchedulerResourceView.ViewHelper!;
        var daysView = Get(_info, "DaysView");
        double rulerWidth = Get<double>(daysView, "TimeRulerWidth");
        float headerWidth = (float)(double)CallStatic(helper, "GetTimeRulerBasedViewHeaderWidth", rulerWidth,
            Get<int>(daysView, "NumberOfVisibleDays"), Get(_info, "View"), rulerWidth)!;
        double x = Is(_info, "IsRTLLayout") ? width - headerWidth : 0.0;
        double y = height - AllDayAppointmentHeight();
        var resources = SfSchedulerResourceView.Resources!;
        nodes.Add(new SemanticsNode
        {
            Id = 0,
            Bounds = new Rect(x, y, headerWidth, height),
            Text = (string?)CallStatic(resources, "GetLocalizedString", IsExpanded() ? "CollapseMoreAlldayAppointments" : "ShowMoreAlldayAppointments") ?? string.Empty,
            IsTouchEnabled = true,
            OnClick = node => _semanticsNodeClick(node, true),
        });
        return nodes;
    }
}
