// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Graphics.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;
using SfPointerEventArgs = Syncfusion.Maui.Core.Internals.PointerEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

// The resource header layout (ResourceHeaderLayout) of the resource view: in the day, week and
// work-week views the resource names, the day header and the all-day panel, stacked in the order
// ResourceGroupType asks; in the month view the resource names above the months.
internal static partial class SfSchedulerResourceView
{
    /// <summary>The members the Windows and Mac ResourceHeaderLayout add for the day views.</summary>
    private sealed class HeaderState
    {
        public Line? TopBorder;
        public Layout? AllDay;                 // AllDayAppointmentsLayout
        public View? ViewHeader;               // DayHeaderView
        public View? ResourceTemplate;         // ResourceLayoutTemplateView (virtualized, with a header template)
        public View? DayHeaderTemplate;        // DayViewViewHeaderTemplateView (virtualized, with a day header template)
        public double AllDayHeight, ViewHeaderHeight, ResourceHeight, LayoutHeight, ViewPortWidth;
        public List<DateTime>? VisibleDates, DisabledDates;
    }

    private static readonly ConditionalWeakTable<object, HeaderState> s_headers = new();

    private static HeaderState HeaderStateOf(object header) => s_headers.GetValue(header, _ => new HeaderState());

    private static Layout? AllDayOf(object? header) => header != null && s_headers.TryGetValue(header, out var s) ? s.AllDay : null;

    private static View? ViewHeaderOf(object? header) => header != null && s_headers.TryGetValue(header, out var s) ? s.ViewHeader : null;

    private static void InstallResourceHeaderLayout(Harmony harmony)
    {
        var header = s_resourceHeaderLayout;
        Patch(harmony, header, ".ctor", postfix: nameof(HeaderCtor_Postfix));
        Patch(harmony, header, "Syncfusion.Maui.Core.Internals.ITouchListener.OnTouch", prefix: nameof(HeaderTouch_Prefix));
        Patch(harmony, header, "OnInteractionEvent", prefix: nameof(HeaderInteraction_Prefix));
        Patch(harmony, header, "LayoutMeasure", prefix: nameof(HeaderMeasure_Prefix));
        Patch(harmony, header, "LayoutArrangeChildren", prefix: nameof(HeaderArrange_Prefix));
        Patch(harmony, header, "UpdateScrollOffset", postfix: nameof(HeaderScrollOffset_Postfix));
        Patch(harmony, header, "InvalidateLayoutMeasure", postfix: nameof(HeaderInvalidateLayoutMeasure_Postfix));
        Patch(harmony, header, "InvalidateResourceHeaderLayout", postfix: nameof(HeaderInvalidateResourceHeaderLayout_Postfix));
    }

    // ResourceHeaderLayout(schedulerViewInfo, scrollTo): in the day views the desktop builds draw a
    // border under the headers (CreateHorizontalLine); the resource and day headers and the
    // all-day panel are added by the day view control.
    private static void HeaderCtor_Postfix(object __instance, object schedulerViewInfo) => Guard("resource header", () =>
    {
        if (!IsDesktop(schedulerViewInfo) || IsMonth(schedulerViewInfo))
            return;
        var state = HeaderStateOf(__instance);
        state.TopBorder = new Line { StrokeThickness = 1.0, Stroke = Get(schedulerViewInfo, "CellBorderBrush") as Brush ?? Brush.Default, HeightRequest = 1.0 };
        ((Layout)__instance).Add(state.TopBorder);
    });

    // ---- The members the day view control drives -----------------------------------------------------

    /// <summary>ResourceHeaderLayout.CreateResourceView: the resource names, across the day views.</summary>
    private static void CreateResourceView(Layout header, object info, object scrollTo, List<DateTime> visibleDates, bool isCurrentView)
    {
        if (Get(header, "resourceView") != null)
            return;
        var click = Bind<Action<SemanticsNode>>(header, "OnSemanticsNodeClick");
        var view = (View)New(s_resourceHeaderView!, info, scrollTo, click, isCurrentView, visibleDates)!;
        Set(header, "resourceView", view);
        header.Add(view);
    }

    /// <summary>ResourceHeaderLayout.CreateViewHeader: the day header, repeated per resource.</summary>
    private static void CreateViewHeader(Layout header, object info, List<DateTime> visibleDates, List<DateTime> disabledDates,
        Action<SemanticsNode> semanticsNodeClick, bool isCurrentView)
    {
        var state = HeaderStateOf(header);
        if (state.ViewHeader != null)
            return;
        state.ViewHeader = (View)New(s_dayHeaderView!, info, visibleDates, disabledDates, semanticsNodeClick, isCurrentView)!;
        header.Add(state.ViewHeader);
        state.VisibleDates = visibleDates;
        state.DisabledDates = disabledDates;
    }

    /// <summary>ResourceHeaderLayout.CreateAllDayAppointmentLayout: the all-day panel, across the resources.</summary>
    private static void CreateAllDayAppointmentLayout(Layout header, object? visibleAppointments, List<DateTime> visibleDates, double timeRulerWidth,
        object dayViewInteraction, List<DateTime> disabledDates, Action<SemanticsNode, bool> semanticsNodeClick)
    {
        var state = HeaderStateOf(header);
        state.AllDay = (Layout)New(s_allDayLayout!, visibleAppointments, visibleDates, InfoOf(header), timeRulerWidth,
            dayViewInteraction, disabledDates, semanticsNodeClick)!;
        header.Add(state.AllDay);
        state.VisibleDates = visibleDates;
        state.DisabledDates = disabledDates;
    }

    /// <summary>ResourceHeaderLayout.RemoveViewHeaderHandler: a day header height of zero removes it.</summary>
    private static void RemoveViewHeaderHandler(Layout? header)
    {
        if (header == null || !s_headers.TryGetValue(header, out var state))
            return;
        if (state.ViewHeader != null)
        {
            Call(state.ViewHeader, "RemoveViewHeaderTemplateHandler");
            Remove(header, state.ViewHeader);
            state.ViewHeader = null;
        }
        if (state.DayHeaderTemplate != null)
        {
            Call(state.DayHeaderTemplate, "RemoveViewHeaderTemplateHandler");
            Remove(header, state.DayHeaderTemplate);
            state.DayHeaderTemplate = null;
        }
    }

    private static void UpdateVisibleDatesChange(Layout header, List<DateTime> visibleDates)
    {
        var state = HeaderStateOf(header);
        Call(AllDayOf(header), "UpdateVisibleDatesChange", visibleDates);
        Call(Get(header, "resourceView"), "UpdateVisibleDatesChange", visibleDates);
        Call(state.ResourceTemplate, "UpdateVisibleDatesChange", visibleDates);
        state.VisibleDates = visibleDates;
    }

    /// <summary>ResourceHeaderLayout.UpdateViewHeaderVisiblesDates: the day header and its template view.</summary>
    private static void UpdateViewHeaderVisibleDates(Layout header, List<DateTime> visibleDates, bool isCurrentView)
    {
        Call(ViewHeaderOf(header), "UpdateVisibleDates", visibleDates, isCurrentView);
        if (s_headers.TryGetValue(header, out var state))
            Call(state.DayHeaderTemplate, "UpdateVisibleDates", visibleDates);
    }

    /// <summary>ResourceHeaderLayout.UpdateViewHeaderLayout: the day header templates are measured again.</summary>
    private static void UpdateViewHeaderLayout(Layout header)
    {
        Call(ViewHeaderOf(header), "InvalidateTemplateMeasure");
        if (s_headers.TryGetValue(header, out var state))
            Call(state.DayHeaderTemplate, "InvalidateTemplateMeasure");
    }

    private static void HeaderInvalidateLayoutMeasure_Postfix(object __instance, bool isCollectionChanged) => Guard("resource templates", () =>
    {
        if (s_headers.TryGetValue(__instance, out var state))
            Call(state.ResourceTemplate, "InvalidateViewMeasure", isCollectionChanged);
    });

    private static void HeaderInvalidateResourceHeaderLayout_Postfix(object __instance) => Guard("resource templates", () =>
    {
        if (s_headers.TryGetValue(__instance, out var state))
            Call(state.ResourceTemplate, "InvalidateViewMeasure", true);
    });

    // ---- Templates in a virtualized header ------------------------------------------------------------

    // The resource and day headers whose own template views were removed for a template view across
    // the full width (the desktop builds' needToCreatedResourceViewTemplated and
    // needToCreatedDayHeaderViewTemplated, false there).
    private static readonly ConditionalWeakTable<object, object> s_templatesRemoved = new();

    /// <summary>
    /// The template part of ResourceHeaderLayout.LayoutMeasure of the Windows build: virtualized, the
    /// resource and day header templates are laid out by template views across the full width (the
    /// headers draw only a window); otherwise the headers hold their template views again.
    /// </summary>
    private static void UpdateHeaderTemplates(Layout header, object info, HeaderState state, object? resourceView)
    {
        bool resourceTemplate = Get(Get(info, "ResourceView"), "HeaderTemplate") != null;
        bool dayHeaderTemplate = Get(Get(info, "DaysView"), "ViewHeaderTemplate") != null;
        if (IsVirtualized(info))
        {
            if (resourceTemplate && state.ResourceTemplate == null && resourceView != null)
            {
                Call(resourceView, "RemoveHandlers");
                s_templatesRemoved.AddOrUpdate(resourceView, true);
                state.ResourceTemplate = (View)New(T("ResourceLayoutTemplateView")!, info, state.VisibleDates)!;
                header.Insert(header.IndexOf((IView)resourceView), state.ResourceTemplate);
            }
            if (dayHeaderTemplate && state.DayHeaderTemplate == null && state.ViewHeader != null)
            {
                Call(state.ViewHeader, "RemoveViewHeaderTemplateHandler");
                s_templatesRemoved.AddOrUpdate(state.ViewHeader, true);
                state.DayHeaderTemplate = (View)New(T("DayViewViewHeaderTemplateView")!, info, state.VisibleDates)!;
                header.Insert(header.IndexOf(state.ViewHeader), state.DayHeaderTemplate);
            }
            return;
        }
        if (resourceTemplate)
        {
            if (state.ResourceTemplate != null)
            {
                Call(state.ResourceTemplate, "RemoveHandlers");
                Remove(header, state.ResourceTemplate);
                state.ResourceTemplate = null;
            }
            RestoreTemplates(resourceView, "CreateResourceTemplateViews");
        }
        if (dayHeaderTemplate)
        {
            if (state.DayHeaderTemplate != null)
            {
                Call(state.DayHeaderTemplate, "RemoveViewHeaderTemplateHandler");
                Remove(header, state.DayHeaderTemplate);
                state.DayHeaderTemplate = null;
            }
            RestoreTemplates(state.ViewHeader, "CreateViewHeaderTemplateViews");
        }
    }

    // ResourceHeaderView.UpdateIntialResourceTemplateView, DayHeaderView.UpdateIntialDayHeaderTemplateView.
    private static void RestoreTemplates(object? view, string create)
    {
        if (view == null || !s_templatesRemoved.TryGetValue(view, out _))
            return;
        s_templatesRemoved.Remove(view);
        Call(view, create);
    }

    private static void UpdateDisabledDatesChange(Layout? header, List<DateTime> disabledDates)
    {
        if (header == null)
            return;
        Call(ViewHeaderOf(header), "UpdateDisabledDatesChange", disabledDates);
        Call(AllDayOf(header), "UpdateDisabledDatesChange", disabledDates);
        HeaderStateOf(header).DisabledDates = disabledDates;
    }

    private static void UpdateSemanticsNodes(Layout? header, bool isCurrentView)
    {
        if (header == null)
            return;
        Call(Get(header, "resourceView"), "InvalidateSemanticsNode", isCurrentView);
        Call(ViewHeaderOf(header), "InvalidateSemanticsNode", isCurrentView);
    }

    private static void UpdateResourceHeaderLayout(Layout? header)
    {
        if (header == null)
            return;
        ((IView)header).InvalidateMeasure();
        InvalidateDrawable(Get(header, "resourceView"));
    }

    /// <summary>ResourceHeaderLayout.UpdateTotalDayViewWidth: the full width the virtualized views draw from.</summary>
    private static void UpdateTotalDayViewWidth(object header, double width)
    {
        Call(Get(header, "resourceView"), "UpdateTotalDayViewWidth", width);
        Call(ViewHeaderOf(header), "UpdateTotalDayViewWidth", width);
        Call(AllDayOf(header), "UpdateTotalDayViewWidth", width);
        Call(Get(header, "hoverView"), "UpdateTotalDayViewWidth", width);
    }

    // ResourceHeaderLayout.UpdateScrollOffset(hOffset): the day header, the all-day panel and the
    // hover follow the horizontal offset too (the neutral build passes it to the resource names only).
    private static void HeaderScrollOffset_Postfix(object __instance, double hOffset) => Guard("scroll offset", () =>
    {
        Call(ViewHeaderOf(__instance), "UpdateScrollOffset", hOffset);
        Call(AllDayOf(__instance), "UpdateScrollOffset", hOffset);
        Call(Get(__instance, "hoverView"), "UpdateScrollOffset", hOffset);
    });

    private static void UpdateViewPortWidth(Layout header, double width)
    {
        var state = HeaderStateOf(header);
        bool changed = state.ViewPortWidth != width;
        state.ViewPortWidth = width;
        Call(AllDayOf(header), "UpdateViewPortWidth", width);
        // Invalidated only on a change: see the day view control's measure.
        if (changed)
            ((IView)header).InvalidateMeasure();
    }

    private static void UpdateResourceViewGridHeight(Layout header, double allDayHeight, double viewHeaderHeight, double resourceHeight, double layoutHeight)
    {
        var state = HeaderStateOf(header);
        bool changed = state.AllDayHeight != allDayHeight || state.ViewHeaderHeight != viewHeaderHeight
            || state.ResourceHeight != resourceHeight || state.LayoutHeight != layoutHeight;
        state.AllDayHeight = allDayHeight;
        state.ViewHeaderHeight = viewHeaderHeight;
        state.ResourceHeight = resourceHeight;
        state.LayoutHeight = layoutHeight;
        if (changed)
            ((IView)header).InvalidateMeasure();
    }

    private static double GetAllDayLayoutHeight(Layout header, double viewHeaderHeight) =>
        AllDayOf(header) is { } allDay ? (double)Call(allDay, "GetAllDayLayoutHeight", viewHeaderHeight)! : viewHeaderHeight;

    private static double GetSingleDayViewHeaderHeight(Layout header, double dayViewHeaderHeight)
    {
        var info = InfoOf(header)!;
        if (!IsSingleDay(info) || AllDayOf(header) is not { } allDay || Call(allDay, "IsExpandable") is not true)
            return dayViewHeaderHeight;
        return dayViewHeaderHeight - (Get<double>(Get(info, "DaysView"), "AllDayAppointmentHeight") + 1.0);
    }

    // ---- Measure and arrange ------------------------------------------------------------------------

    /// <summary>ResourceHeaderLayout.CalculateArrangeRectPosition (without virtualization).</summary>
    private static (double ResourceY, double ViewHeaderY, double AllDayY, double BorderY) HeaderPositions(object info, HeaderState state)
    {
        double resourceY, viewHeaderY, borderY;
        if (IsResourceType(info))
        {
            // Resources first, each over its own days.
            resourceY = 0.0;
            viewHeaderY = state.ResourceHeight;
            borderY = state.ResourceHeight - 1.0;
        }
        else
        {
            // Days first, each over the resources.
            viewHeaderY = 0.0;
            resourceY = state.ViewHeaderHeight;
            borderY = state.ViewHeaderHeight - 1.0;
        }
        return (resourceY, viewHeaderY, state.ResourceHeight + state.ViewHeaderHeight, borderY);
    }

    private static bool HeaderMeasure_Prefix(object __instance, double widthConstraint, double heightConstraint, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        try
        {
            var state = HeaderStateOf(__instance);
            double width = double.IsFinite(widthConstraint) ? widthConstraint : 0.0;
            double height = ResourceTotalHeight(info!);
            double monthHeaderHeight = ViewHeaderHeight(info!, "MonthView");
            bool verticalMonth = CallStatic(ViewHelper!, "IsSchedulerResourceVerticalMonthView", info) is true;
            if (!IsMonth(info) || verticalMonth)
            {
                width = (double)CallStatic(ViewHelper!, "HorizontalResourceViewDesktopViewPortWidth", info, state.ViewPortWidth)!;
                height = verticalMonth ? height : state.LayoutHeight;
            }
            // Virtualized, the headers are a window three viewports wide (the all-day panel
            // virtualizes its own children).
            double window = width;
            if (IsVirtualized(info))
            {
                window = VirtualWidth(state.ViewPortWidth);
                UpdateTotalDayViewWidth(__instance, width);
            }
            var resourceView = Get(__instance, "resourceView");
            if (!IsMonth(info))
                UpdateHeaderTemplates((Layout)__instance, info!, state, resourceView);
            var monthHeader = Get(__instance, "verticalMonthViewHeader");
            var hover = Get(__instance, "hoverView") as VisualElement;
            foreach (var child in ((Layout)__instance).Children.ToList())
            {
                if (ReferenceEquals(child, state.ResourceTemplate))
                    child.Measure(width, state.ResourceHeight);
                else if (ReferenceEquals(child, state.DayHeaderTemplate))
                    child.Measure(width, state.ViewHeaderHeight);
                else if (ReferenceEquals(child, resourceView))
                    child.Measure(window, IsMonth(info) ? height : state.ResourceHeight);
                else if (ReferenceEquals(child, monthHeader))
                    child.Measure(window, monthHeaderHeight);
                else if (ReferenceEquals(child, state.ViewHeader))
                    child.Measure(window, state.ViewHeaderHeight);
                else if (ReferenceEquals(child, state.AllDay))
                    child.Measure(width, state.AllDayHeight);
                else if (ReferenceEquals(child, state.TopBorder))
                    child.Measure(window, 1.0);
                else if (state.ResourceHeight == 0.0 && state.ViewHeaderHeight == 0.0)
                {
                    if (verticalMonth)
                        hover?.Measure(window, ResourceTotalHeight(info!));
                }
                else
                {
                    hover?.Measure(window, state.ResourceHeight + state.ViewHeaderHeight);
                }
            }
            var size = new Size(width, height);
            Set(__instance, "DesiredSize", size);
            __result = size;
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: measuring the resource header failed", ex);
            return true;
        }
    }

    private static bool HeaderArrange_Prefix(object __instance, Rect bounds, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        try
        {
            var state = HeaderStateOf(__instance);
            double resourceTotal = ResourceTotalHeight(info!);
            double monthHeaderHeight = ViewHeaderHeight(info!, "MonthView");
            var (resourceY, viewHeaderY, allDayY, borderY) = HeaderPositions(info!, state);
            double width = bounds.Width;
            // Virtualized, the headers are arranged as a window around the scroll offset.
            bool virtualized = IsVirtualized(info);
            double window = virtualized ? VirtualWidth(state.ViewPortWidth) : width;
            double x = virtualized ? VirtualX(Get<double>(__instance, "scrollXOffset"), window) : 0.0;
            var resourceView = Get(__instance, "resourceView");
            var hover = Get(__instance, "hoverView");
            var monthHeaderType = T("VerticalMonthViewHeader");
            foreach (var child in ((Layout)__instance).Children)
            {
                if (ReferenceEquals(child, state.ResourceTemplate))
                {
                    child.Arrange(new Rect(0.0, resourceY, width, state.ResourceHeight));
                }
                else if (ReferenceEquals(child, state.DayHeaderTemplate))
                {
                    child.Arrange(new Rect(0.0, viewHeaderY, width, state.ViewHeaderHeight));
                }
                else if (ReferenceEquals(child, state.ViewHeader))
                {
                    child.Arrange(new Rect(x, viewHeaderY, window, state.ViewHeaderHeight));
                }
                else if (ReferenceEquals(child, state.AllDay))
                {
                    child.Arrange(new Rect(0.0, allDayY, width, state.AllDayHeight));
                }
                else if (ReferenceEquals(child, state.TopBorder))
                {
                    state.TopBorder!.X1 = 0.0;
                    state.TopBorder.X2 = window;
                    state.TopBorder.Y1 = 0.0;
                    state.TopBorder.Y2 = 0.0;
                    child.Arrange(new Rect(x, borderY, window, 1.0));
                }
                else if (ReferenceEquals(child, resourceView))
                {
                    child.Arrange(IsMonth(info) ? new Rect(x, 0.0, window, resourceTotal) : new Rect(x, resourceY, window, state.ResourceHeight));
                }
                else if (monthHeaderType != null && monthHeaderType.IsInstanceOfType(child))
                {
                    child.Arrange(new Rect(x, resourceTotal, window, monthHeaderHeight));
                }
                else if (ReferenceEquals(child, hover))
                {
                    if (allDayY == 0.0)
                    {
                        if (CallStatic(ViewHelper!, "IsSchedulerResourceVerticalMonthView", info) is true)
                            child.Arrange(new Rect(x, 0.0, window, resourceTotal));
                    }
                    else
                    {
                        child.Arrange(new Rect(x, 0.0, window, allDayY));
                    }
                }
                else
                {
                    child.Arrange(new Rect(x, resourceY, window, state.ResourceHeight));
                }
            }
            __result = bounds.Size;
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: arranging the resource header failed", ex);
            return true;
        }
    }

    // ---- Hover and taps -------------------------------------------------------------------------------

    // ITouchListener.OnTouch(e): the mouse hover over the resource names and the day header.
    private static bool HeaderTouch_Prefix(object __instance, SfPointerEventArgs e)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        Guard("resource header hover", () =>
        {
            if (Call(info, "CanHighlightView") is not true || Get(__instance, "hoverView") is not VisualElement hover)
                return;
            if (e.Action == global::Syncfusion.Maui.Core.Internals.PointerActions.Exited)
            {
                Call(hover, "UpdateMouseHover", null, null, false, null, null, false, 0.0);
                return;
            }
            if (e.Action != global::Syncfusion.Maui.Core.Internals.PointerActions.Moved)
                return;
            var layout = (Layout)__instance;
            if (hover.ZIndex != layout.Children.Count)
                hover.ZIndex = layout.Children.Count;
            var state = HeaderStateOf(__instance);
            var resource = HorizontalResource(info!, e.TouchPoint.X, ((VisualElement)__instance).DesiredSize.Width);
            if (state.VisibleDates == null || state.DisabledDates == null)
                return;
            var date = CallStatic(SelectionHelper!, "GetDaysViewHoverDate", e.TouchPoint, state.VisibleDates, info, ((VisualElement)__instance).Width) as DateTime?;
            if (!date.HasValue)
                return;
            double y = e.TouchPoint.Y;
            bool noTemplate = Get(Get(info, "ResourceView"), "HeaderTemplate") == null;
            void HoverResource() => Call(hover, "UpdateMouseHover", date, null, false, null, Get(resource, "Id"), true, state.ViewHeaderHeight);
            if (IsResourceType(info!))
            {
                bool overResources = y < state.ResourceHeight;
                if (state.ResourceHeight == 0.0 && !overResources && CallStatic(ViewHelper!, "IsSchedulerResourceVerticalMonthView", info) is true)
                    overResources = y < ResourceTotalHeight(info!);
                if (y > 0.0 && overResources && noTemplate)
                    HoverResource();
                else if (state.ViewHeaderHeight > 0.0 && y > state.ResourceHeight && y < state.ResourceHeight + state.ViewHeaderHeight)
                    UpdateDayViewHeaderMouseHover(info!, hover, state, e, resource, date);
                else
                    Call(hover, "UpdateMouseHover", null, null, false, null, null, false, 0.0);
                return;
            }
            if (state.ViewHeaderHeight > 0.0 && y > 0.0 && y < state.ViewHeaderHeight)
                UpdateDayViewHeaderMouseHover(info!, hover, state, e, resource, date);
            else if (y > state.ViewHeaderHeight && y < state.ResourceHeight + state.ViewHeaderHeight && noTemplate)
                HoverResource();
            else
                Call(hover, "UpdateMouseHover", null, null, false, null, null, false, 0.0);
        });
        return false;
    }

    // ResourceHeaderLayout.UpdateDayViewHeaderMouseHover(e, selectedResource, dateTime).
    private static void UpdateDayViewHeaderMouseHover(object info, object hover, HeaderState state, SfPointerEventArgs e, object? resource, DateTime? date)
    {
        Func<bool> canHighlight = () => Call(info, "CanHighlightView") is true;
        if (CallStatic(SelectionHelper!, "IsViewHeaderMouseHover", canHighlight, Get(info, "AllowViewNavigation"), Get(info, "AllowedViews"), Get(info, "View")) is not true)
        {
            Call(hover, "UpdateMouseHover", null, null, false, null, null, false, 0.0);
            return;
        }
        if (state.ViewHeader == null || state.DisabledDates == null || !date.HasValue)
            return;
        if (CallStatic(SelectionHelper!, "CanSelectTimeSlot", date.Value, state.DisabledDates, Get(info, "MinimumDateTime"), Get(info, "MaximumDateTime"),
            Get(info, "View"), Get(info, "DaysView")) is not true)
        {
            Call(hover, "UpdateViewHeaderHover", -1, null, null, false, 0.0);
            return;
        }
        Call(hover, "UpdateViewHeaderHover", (int)e.TouchPoint.X, date, Get(resource, "Id"), false, state.ResourceHeight);
    }

    // OnInteractionEvent(interactionPoint, isTapped, tapCount, isRightTapped, showContextMenu): taps on
    // the resource names, the day header and the all-day panel, by the order of the rows.
    private static bool HeaderInteraction_Prefix(object __instance, Point interactionPoint, bool isTapped, int tapCount, bool isRightTapped, bool showContextMenu)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        Guard("resource header tap", () =>
        {
            var state = HeaderStateOf(__instance);
            if (state.VisibleDates == null || state.DisabledDates == null)
                return;
            var resource = HorizontalResource(info!, interactionPoint.X, ((VisualElement)__instance).DesiredSize.Width);
            var date = CallStatic(SelectionHelper!, "GetDaysViewHoverDate", interactionPoint, state.VisibleDates, info, ((VisualElement)__instance).Width) as DateTime?;
            var elements = T("SchedulerElement")!;
            double y = interactionPoint.Y;
            double rows = state.ResourceHeight + state.ViewHeaderHeight;
            bool CanSelectHeader() => date.HasValue && CallStatic(SelectionHelper!, "CanSelectViewHeader", date.Value, state.DisabledDates,
                Get(info, "MinimumDateTime"), Get(info, "MaximumDateTime"), Get(info, "View")) is true;
            void Raise(DateTime? when, string element, object? target) => Call(info, "TriggerSchedulerInteractionEvent", isTapped, tapCount, when,
                EnumValue(elements, element), null, false, target, false, -1, isRightTapped);
            void AllDayTap() => Call(state.AllDay, "OnTapGestureAction", new Point(interactionPoint.X, y - rows), isTapped, tapCount,
                Is(info, "IsRTLLayout"), resource, isRightTapped, showContextMenu);
            if (IsResourceType(info!))
            {
                if (y > 0.0 && y < state.ResourceHeight)
                {
                    Raise(null, "ResourceHeader", resource);
                }
                else if (state.ViewHeaderHeight > 0.0 && y > state.ResourceHeight && y < rows)
                {
                    if (!CanSelectHeader())
                        return;
                    Raise(date, "ViewHeader", resource);
                }
                else if (state.AllDayHeight > 0.0 && y > rows && y < rows + state.AllDayHeight)
                {
                    AllDayTap();
                }
            }
            else
            {
                if (state.ViewHeaderHeight > 0.0 && y > 0.0 && y < state.ViewHeaderHeight)
                {
                    if (!CanSelectHeader())
                        return;
                    Raise(date, "ViewHeader", null);
                }
                else if (y > state.ViewHeaderHeight && y < rows)
                {
                    if (!date.HasValue)
                        return;
                    Raise(date, "ResourceHeader", resource);
                }
                else if (state.AllDayHeight > 0.0 && y > rows && y < rows + state.AllDayHeight)
                {
                    AllDayTap();
                }
            }
            if (!isTapped && tapCount == 1)
                Call(state.AllDay, "EnableAppointmentDragging");
        });
        return false;
    }
}
