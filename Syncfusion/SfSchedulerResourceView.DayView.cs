// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Graphics.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

// The day view control (DayViewControl: day, week and work week) in the resource view.
internal static partial class SfSchedulerResourceView
{
    private const string NotifyLayout = "Syncfusion.Maui.Scheduler.INotifySchedulerLayout.";
    private const string NotifyTimeSlot = "Syncfusion.Maui.Scheduler.INotifyTimeSlotLayout.";
    private const string DaysInteraction = "Syncfusion.Maui.Scheduler.IDaysViewInteraction.";

    /// <summary>The members the Windows and Mac DayViewControl add for the resource view.</summary>
    private sealed class DayState
    {
        public Layout? ResourceHeader;          // ResourceHeaderLayout
        public ScrollView? ResourceScroll;       // ScrollViewExt, horizontal
        public Layout? ResourceHeaderGrid;       // SchedulerGrid
        public SfSchedulerTimeRulerView? TimeRuler;
        public ScrollView? TimeRulerScroll;      // ScrollViewExt, vertical
        public Layout? TimeRulerGrid;            // SchedulerGrid
        public Line? LeftBorder;
        public SfSchedulerAllDayExpanderView? Expander;
        public double ScrollOffset, ScrollXOffset;
        public Size Measured = new(-1, -1);
        public bool TracksInvalidation;
        public EventHandler<Microsoft.Maui.Controls.ScrolledEventArgs>? RulerScrolled, ResourceScrolled;
    }

    private static readonly ConditionalWeakTable<object, DayState> s_days = new();

    private static DayState DayStateOf(object control) => s_days.GetValue(control, _ => new DayState());

    private static Layout? HeaderOf(object control) => s_days.TryGetValue(control, out var state) ? state.ResourceHeader : null;

    private static object? InfoOf(object view) => Get(view, "SchedulerViewInfo") ?? Get(view, "schedulerViewInfo");

    private static ScrollView ScrollViewOf(object control) => (ScrollView)Get(control, "scrollView")!;

    private static void InstallDayViewControl(Harmony harmony)
    {
        var day = s_dayViewControl;
        Patch(harmony, day, ".ctor", postfix: nameof(DayCtor_Postfix));
        Patch(harmony, day, "UpdateDisabledDatesChange", postfix: nameof(DayDisabledDates_Postfix));
        Patch(harmony, day, NotifyLayout + "UpdateSemanticsNodes", postfix: nameof(DaySemantics_Postfix));
        Patch(harmony, day, NotifyLayout + "UpdateTodayTimer", postfix: nameof(DayTodayTimer_Postfix));
        Patch(harmony, day, NotifyLayout + "RemoveAppointmentResizingView", postfix: nameof(DayRemoveResizingView_Postfix));
        Patch(harmony, day, "UpdateVisibleDatesChange", prefix: nameof(DayVisibleDates_Prefix), postfix: nameof(DayVisibleDates_Postfix));
        Patch(harmony, day, NotifyTimeSlot + "UpdateTimeSlotCount", postfix: nameof(DayTimeSlotCount_Postfix));
        Patch(harmony, day, "UpdateVisibleAppointments", prefix: nameof(DayVisibleAppointments_Prefix), postfix: nameof(DayVisibleAppointments_Postfix));
        Patch(harmony, day, NotifyLayout + "UpdateSelection", postfix: nameof(DayUpdateSelection_Postfix), when: m => m.GetParameters().Length == 3);
        Patch(harmony, day, DaysInteraction + "ExpandAndCollapseAllDay", prefix: nameof(DayExpandAndCollapse_Prefix));
        Patch(harmony, day, DaysInteraction + "AllDayAnimationIsRunning", postfix: nameof(DayAnimationRunning_Postfix));
        Patch(harmony, day, DaysInteraction + "IsExpanded", postfix: nameof(DayIsExpanded_Postfix));
        Patch(harmony, day, DaysInteraction + "IsExpandable", postfix: nameof(DayIsExpandable_Postfix));
        Patch(harmony, day, DaysInteraction + "ClearAllDayAppointmentSelection", postfix: nameof(DayClearAllDaySelection_Postfix));
        Patch(harmony, day, NotifyLayout + "UpdateViewHeader", postfix: nameof(DayUpdateViewHeader_Postfix));
        Patch(harmony, day, NotifyLayout + "InvalidateAppointmentsDraw", postfix: nameof(DayInvalidateAppointmentsDraw_Postfix));
        Patch(harmony, day, NotifyLayout + "InvalidateDrawView", postfix: nameof(DayInvalidateDrawView_Postfix));
        Patch(harmony, day, NotifyLayout + "InvalidateLayout", postfix: nameof(DayInvalidateLayout_Postfix));
        Patch(harmony, day, NotifyLayout + "UpdateTodayStyleChange", postfix: nameof(DayTodayStyle_Postfix));
        Patch(harmony, day, NotifyTimeSlot + "UpdateScrollPosition", prefix: nameof(DayUpdateScrollPosition_Prefix));
        Patch(harmony, day, NotifyLayout + "GetAutoScrollPosition", postfix: nameof(DayAutoScrollPosition_Postfix));
        Patch(harmony, day, NotifyLayout + "UpdateScrollOnDrag", postfix: nameof(DayScrollOnDrag_Postfix));
        Patch(harmony, day, "AddAppointmentResizingView", postfix: nameof(DayAddResizingView_Postfix));
        Patch(harmony, day, "UpdateResourcegroupType", postfix: nameof(DayResourceGroupType_Postfix));
        Patch(harmony, day, "OnScrollViewScrolled", prefix: nameof(DayScrolled_Prefix), postfix: nameof(DayScrolled_Postfix));
        Patch(harmony, day, "ResourceViewScrollView_Scrolled", postfix: nameof(DayResourceScrolled_Postfix));
        Patch(harmony, day, "UpdateResourceHeaderHeightChanged", postfix: nameof(DayResourceHeaderHeight_Postfix));
        Patch(harmony, day, "LayoutArrangeChildren", prefix: nameof(DayArrange_Prefix));
        Patch(harmony, day, "LayoutMeasure", prefix: nameof(DayMeasure_Prefix));
        Patch(harmony, day, "GetSingleDayViewHeaderHeight", postfix: nameof(DaySingleDayHeaderHeight_Postfix));
        Patch(harmony, day, "CreateViewHeader", prefix: nameof(DayCreateViewHeader_Prefix));
        Patch(harmony, day, "OnSemanticsNodeClick", postfix: nameof(DaySemanticsNodeClick_Postfix));
        Patch(harmony, day, "ExpandAllDayLayout", prefix: nameof(DayExpand_Prefix));
        Patch(harmony, day, "CollapseAllDayLayout", prefix: nameof(DayCollapse_Prefix));
        Patch(harmony, day, "SetScrollOffset", postfix: nameof(DaySetScrollOffset_Postfix));
        Patch(harmony, day, "OnInteractionEvent", prefix: nameof(DayInteraction_Prefix));
        Patch(harmony, day, "RemoveViewHeaderHandler", prefix: nameof(DayRemoveViewHeader_Prefix));
    }

    // ---- Building and removing the resource view -------------------------------------------------------

    // DayViewControl(schedulerViewInfo, visibleDates, disabledDates, visibleAppointments, selectedDate, isCurrentView):
    // the desktop builds add the resource view in place of the all-day panel and day header.
    private static void DayCtor_Postfix(object __instance, bool isCurrentView) => Guard("building the day view", () =>
    {
        if (IsDesktop(InfoOf(__instance)))
            AddHorizontalResourceViewHeaderLayout(__instance, isCurrentView);
    });

    /// <summary>DayViewControl.AddHorizontalResourceViewHeaderLayout of the desktop builds.</summary>
    internal static void AddHorizontalResourceViewHeaderLayout(object control, bool isCurrentView)
    {
        var info = InfoOf(control)!;
        var layout = (Layout)control;
        var state = DayStateOf(control);
        Call(control, "RemoveAllDayAppointmentsLayoutAndViewHeader");
        // The desktop builds add these before the top border and the time slots.
        int index = 0;
        if (state.Expander == null)
        {
            state.Expander = new SfSchedulerAllDayExpanderView(Get(control, "visibleAppointments") as IList, info, control,
                Bind<Action<SemanticsNode, bool>>(control, "OnSemanticsNodeClick") ?? ((_, _) => { }));
            layout.Insert(index++, state.Expander);
        }
        if (state.TimeRulerGrid == null)
        {
            state.TimeRulerGrid = (Layout)New(s_schedulerGrid!)!;
            state.TimeRuler = new SfSchedulerTimeRulerView(info);
            state.TimeRulerScroll = (ScrollView)New(s_scrollViewExt!)!;
            state.RulerScrolled = Bind<EventHandler<Microsoft.Maui.Controls.ScrolledEventArgs>>(Get(control, "proxy")!, "OnTimeRulerScrollViewScrolled");
            if (state.RulerScrolled != null)
                state.TimeRulerScroll.Scrolled += state.RulerScrolled;
            state.TimeRulerScroll.Orientation = Microsoft.Maui.ScrollOrientation.Vertical;
            state.TimeRulerScroll.VerticalScrollBarVisibility = Microsoft.Maui.ScrollBarVisibility.Never;
            state.TimeRulerGrid.Add(state.TimeRulerScroll);
            state.TimeRulerScroll.Content = state.TimeRuler;
            layout.Insert(index++, state.TimeRulerGrid);
        }
        if (state.ResourceHeaderGrid == null)
        {
            state.ResourceHeaderGrid = (Layout)New(s_schedulerGrid!)!;
            var header = (Layout)New(s_resourceHeaderLayout!, info, control)!;
            state.ResourceHeader = header;
            var visibleDates = Get<List<DateTime>>(control, "VisibleDates")!;
            var disabledDates = Get<List<DateTime>>(control, "DisabledDates")!;
            double rulerWidth = Get<double>(Get(info, "DaysView"), "TimeRulerWidth");
            CreateResourceView(header, info, control, visibleDates, isCurrentView);
            CreateViewHeader(header, info, visibleDates, disabledDates, Bind<Action<SemanticsNode>>(control, "OnViewHeaderSemanticsNodeClick")!, isCurrentView);
            double allDayWidth = (double)CallStatic(ViewHelper!, "GetTimeRulerBasedViewHeaderWidth", rulerWidth,
                Get<int>(Get(info, "DaysView"), "NumberOfVisibleDays"), Get(info, "View"), rulerWidth)!;
            CreateAllDayAppointmentLayout(header, Get(control, "visibleAppointments"), visibleDates, allDayWidth, control, disabledDates,
                Bind<Action<SemanticsNode, bool>>(control, "OnSemanticsNodeClick")!);
            Call(Get(header, "hoverView"), "UpdateVisibleDates", visibleDates);
            state.ResourceScroll = (ScrollView)New(s_scrollViewExt!)!;
            state.ResourceScrolled = Bind<EventHandler<Microsoft.Maui.Controls.ScrolledEventArgs>>(Get(control, "proxy")!, "OnResourceViewScrollViewScrolled");
            if (state.ResourceScrolled != null)
                state.ResourceScroll.Scrolled += state.ResourceScrolled;
            state.ResourceScroll.HorizontalScrollBarVisibility = Microsoft.Maui.ScrollBarVisibility.Never;
            state.ResourceScroll.Orientation = Microsoft.Maui.ScrollOrientation.Horizontal;
            state.ResourceHeaderGrid.Add(state.ResourceScroll);
            state.ResourceScroll.Content = header;
            layout.Insert(index++, state.ResourceHeaderGrid);
        }
        if (state.LeftBorder == null)
        {
            state.LeftBorder = new Line { StrokeThickness = 1.0, Stroke = Get(info, "CellBorderBrush") as Brush ?? Brush.Default, WidthRequest = 1.0 };
            layout.Insert(index, state.LeftBorder);
        }
        if (!state.TracksInvalidation)
        {
            // A measure invalidated (here or in a child) is redone at the next arrange, which the
            // snap layout may run without measuring (see DayArrange_Prefix).
            state.TracksInvalidation = true;
            ((VisualElement)control).MeasureInvalidated += (_, _) => state.Measured = new Size(-1, -1);
        }
        Call(control, "SetTimeSlotScrollOrientation");
    }

    /// <summary>DayViewControl.RemoveHorizontalResourceViewHeaderLayout of the desktop builds.</summary>
    internal static void RemoveHorizontalResourceViewHeaderLayout(object control, bool isCurrentView)
    {
        var layout = (Layout)control;
        var state = DayStateOf(control);
        SetVirtualized(InfoOf(control), false);
        Call(Get(control, "dayViewLayout"), "UpdateScrollOffset", 0.0);
        Call(state.ResourceHeader, "UpdateScrollOffset", 0.0);
        if (state.Expander != null)
        {
            layout.Children.Remove(state.Expander);
            state.Expander = null;
        }
        if (state.TimeRulerGrid != null)
        {
            if (state.TimeRulerScroll != null)
            {
                if (state.RulerScrolled != null)
                    state.TimeRulerScroll.Scrolled -= state.RulerScrolled;
                state.TimeRulerScroll.Content = null;
            }
            state.TimeRulerGrid.Clear();
            layout.Remove(state.TimeRulerGrid);
            state.TimeRuler = null;
            state.TimeRulerScroll = null;
            state.TimeRulerGrid = null;
        }
        if (state.LeftBorder != null)
        {
            layout.Children.Remove(state.LeftBorder);
            state.LeftBorder = null;
        }
        if (state.ResourceScroll != null)
        {
            if (state.ResourceScrolled != null)
                state.ResourceScroll.Scrolled -= state.ResourceScrolled;
            state.ResourceScroll.Content = null;
        }
        state.ResourceHeader = null;
        if (state.ResourceHeaderGrid != null)
        {
            state.ResourceHeaderGrid.Clear();
            layout.Remove(state.ResourceHeaderGrid);
            state.ResourceHeaderGrid = null;
        }
        state.ResourceScroll = null;
        Call(control, "SetTimeSlotScrollOrientation");
        Call(control, "AddAllDayAppointmentsLayoutAndViewHeader", isCurrentView);
    }

    /// <summary>DayViewControl.InvalidateLayoutMeasure of the desktop builds.</summary>
    internal static void InvalidateDayLayoutMeasure(object control, bool isCollectionChanged)
    {
        Call(HeaderOf(control), "InvalidateLayoutMeasure", isCollectionChanged);
        ((IView)ScrollViewOf(control)).InvalidateMeasure();
        CallStatic(ViewHelper!, "InvalidateMeasure", control);
        ((VisualElement)control).InvalidateMeasure();
        Call(Get(control, "dayViewLayout"), "InvalidateLayout");
    }

    /// <summary>The width a left or right all-day resize maps across (the panel, or the resource header).</summary>
    internal static double AllDayResizeWidth(object control) =>
        IsDesktop(InfoOf(control)) ? HeaderOf(control)?.Width ?? 0.0 : (Get(control, "allDayAppointmentsLayout") as VisualElement)?.Width ?? 0.0;

    internal static void AddAppointmentResizeIndicatorView(object control, View indicator)
    {
        if (s_days.TryGetValue(control, out var state))
            state.TimeRuler?.AddAppointmentResizeIndicatorView(indicator);
    }

    internal static void RemoveAppointmentResizeIndicatorView(object control)
    {
        if (s_days.TryGetValue(control, out var state))
            state.TimeRuler?.RemoveAppointmentResizeIndicatorView();
    }

    // ---- Notifications the resource view follows -------------------------------------------------------

    private static void DayDisabledDates_Postfix(object __instance, List<DateTime> disabledDates) =>
        Guard("disabled dates", () => UpdateDisabledDatesChange(HeaderOf(__instance), disabledDates));

    private static void DaySemantics_Postfix(object __instance, bool isCurrentView) =>
        Guard("semantics", () => UpdateSemanticsNodes(HeaderOf(__instance), isCurrentView));

    private static void DayTodayTimer_Postfix(object __instance) =>
        Guard("today timer", () => Call(ViewHeaderOf(HeaderOf(__instance)), "UpdateTodayTimer"));

    private static void DayRemoveResizingView_Postfix(object __instance) => Guard("resize view", () =>
    {
        RemoveAppointmentResizeIndicatorView(__instance);
        Call(AllDayOf(HeaderOf(__instance)), "RemoveAppointmentResizeView");
    });

    // 0: the same dates (nothing to do), 1: new dates, 2: new dates and a new number of days.
    private static void DayVisibleDates_Prefix(object __instance, List<DateTime> visibleDates, out int __state) =>
        __state = ReferenceEquals(Get(__instance, "VisibleDates"), visibleDates) ? 0
            : Get<IList>(__instance, "VisibleDates")?.Count != visibleDates.Count ? 2 : 1;

    // UpdateVisibleDatesChange(visibleDates, isCurrentView): the header's day header, hover, all-day
    // panel and resource header follow the new dates; a change of day count measures the header again.
    private static void DayVisibleDates_Postfix(object __instance, List<DateTime> visibleDates, bool isCurrentView, int __state) =>
        Guard("visible dates", () =>
        {
            if (__state == 0 || HeaderOf(__instance) is not { } header)
                return;
            UpdateViewHeaderVisibleDates(header, visibleDates, isCurrentView);
            Call(Get(header, "hoverView"), "UpdateVisibleDates", visibleDates);
            UpdateVisibleDatesChange(header, visibleDates);
            if (__state == 2)
                ((IView)header).InvalidateMeasure();
        });

    private static void DayTimeSlotCount_Postfix(object __instance) => Guard("time slot count", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state))
            return;
        state.TimeRuler?.InvalidateLayout();
        ((IView?)state.TimeRulerScroll)?.InvalidateMeasure();
    });

    private static void DayVisibleAppointments_Prefix(object __instance, out int __state) =>
        __state = Get<IList>(__instance, "allDayAppointments")?.Count ?? 0;

    // UpdateVisibleAppointments: the resource header's all-day panel and the expander get the all-day ones.
    private static void DayVisibleAppointments_Postfix(object __instance, int __state) => Guard("visible appointments", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state) || state.ResourceHeader == null)
            return;
        var allDay = Get<IList>(__instance, "allDayAppointments");
        Call(AllDayOf(state.ResourceHeader), "UpdateVisibleAppointments", allDay);
        state.Expander?.UpdateVisibleAppointments(allDay);
        if ((allDay?.Count ?? 0) > 0 || __state > 0)
        {
            ((IView)state.ResourceHeader).InvalidateMeasure();
            ((VisualElement)__instance).InvalidateMeasure();
        }
    });

    // INotifySchedulerLayout.UpdateSelection(selectedDate, appointment, selectedResource): an all-day
    // appointment is selected in the resource header's all-day panel.
    private static void DayUpdateSelection_Postfix(object __instance, object? appointment, object? selectedResource, ref bool __result)
    {
        if (appointment == null || HeaderOf(__instance) is not { } header)
            return;
        var info = InfoOf(__instance);
        bool allDay = Is(appointment, "IsAllDay") || (Is(appointment, "IsSpanned") && !Is(Get(info, "DaysView"), "AllowSpannedAppointmentsInTimeSlots"));
        if (allDay && IsDesktop(info))
        {
            var target = AllDayOf(header);
            __result = target != null && Call(target, "UpdateAppointmentSelection", appointment, selectedResource) is true;
        }
    }

    private static void DayExpandAndCollapse_Prefix(object __instance) => Guard("all-day expander", () =>
    {
        if (HeaderOf(__instance) is not { } header)
            return;
        Call(__instance, Call(AllDayOf(header), "IsExpaned") is true ? "CollapseAllDayLayout" : "ExpandAllDayLayout");
    });

    private static void DayAnimationRunning_Postfix(object __instance, ref bool __result)
    {
        if (IsDesktop(InfoOf(__instance)) && HeaderOf(__instance) == null)
            __result = false;
    }

    private static void DayIsExpanded_Postfix(object __instance, ref bool __result)
    {
        if (IsDesktop(InfoOf(__instance)) && HeaderOf(__instance) is { } header)
            __result = Call(AllDayOf(header), "IsExpaned") is true;
    }

    private static void DayIsExpandable_Postfix(object __instance, ref bool __result)
    {
        if (IsDesktop(InfoOf(__instance)) && HeaderOf(__instance) is { } header)
            __result = Call(AllDayOf(header), "IsExpandable") is true;
    }

    private static void DayClearAllDaySelection_Postfix(object __instance) =>
        Guard("all-day selection", () => Call(AllDayOf(HeaderOf(__instance)), "ClearAppointmentSelection"));

    private static void DayUpdateViewHeader_Postfix(object __instance, bool isHeightChange) => Guard("day header", () =>
    {
        if (HeaderOf(__instance) is not { } header)
            return;
        if (isHeightChange)
        {
            Call(AllDayOf(header), "UpdateViewHeaderHeight");
            ((IView)header).InvalidateMeasure();
        }
        else if (ViewHeaderHeight(InfoOf(__instance)!, "DaysView") != 0)
        {
            InvalidateDrawable(ViewHeaderOf(header));
        }
    });

    private static void DayInvalidateAppointmentsDraw_Postfix(object __instance) =>
        Guard("all-day drawing", () => Call(AllDayOf(HeaderOf(__instance)), "InvalidateAllDayAppointmentsDraw"));

    private static void DayInvalidateDrawView_Postfix(object __instance, string? propertyName) => Guard("drawing", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state))
            return;
        var header = state.ResourceHeader;
        if (header != null)
        {
            switch (propertyName)
            {
                case "TimeRulerWidth":
                    UpdateViewHeaderLayout(header);
                    Call(AllDayOf(header), "InvalidateAllDayAppointmentsDraw");
                    break;
                case "MinimumDateTime":
                    UpdateViewHeaderLayout(header);
                    break;
            }
            InvalidateDrawable(ViewHeaderOf(header));
        }
        InvalidateDrawable(state.TimeRuler);
    });

    private static void DayInvalidateLayout_Postfix(object __instance) => Guard("layout", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state))
            return;
        Call(state.ResourceHeader, "InvalidateLayoutMeasure", false);
        state.TimeRuler?.InvalidateLayout();
    });

    private static void DayTodayStyle_Postfix(object __instance) => Guard("today style", () =>
    {
        if (Get<List<DateTime>>(__instance, "VisibleDates")?.Contains(DateTime.Now.Date) == true)
            InvalidateDrawable(ViewHeaderOf(HeaderOf(__instance)));
    });

    private static void DayAddResizingView_Postfix(object __instance, object appointmentResizeView, bool isAllDay, object? appointmentResizeRectangle) =>
        Guard("resize view", () =>
        {
            if (isAllDay && HeaderOf(__instance) is { } header)
                Call(AllDayOf(header), "AddAppointmentResizeView", appointmentResizeView, appointmentResizeRectangle);
        });

    private static void DayResourceGroupType_Postfix(object __instance) => Guard("resource group type", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state))
            return;
        Call(state.ResourceHeader, "InvalidateLayoutMeasure", true);
        state.TimeRuler?.InvalidateLayout();
    });

    private static void DayResourceHeaderHeight_Postfix(object __instance) =>
        Guard("resource header height", () => UpdateResourceHeaderLayout(HeaderOf(__instance)));

    private static void DaySingleDayHeaderHeight_Postfix(object __instance, double dayViewHeaderHeight, ref double __result)
    {
        if (Get(__instance, "allDayAppointmentsLayout") == null && HeaderOf(__instance) is { } header)
            __result = GetSingleDayViewHeaderHeight(header, dayViewHeaderHeight);
    }

    // CreateViewHeader(isCurrentView): with resources the day header lives in the resource header.
    private static bool DayCreateViewHeader_Prefix(object __instance, bool isCurrentView)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        Guard("day header", () =>
        {
            if (HeaderOf(__instance) is { } header)
                CreateViewHeader(header, info!, Get<List<DateTime>>(__instance, "VisibleDates")!, Get<List<DateTime>>(__instance, "DisabledDates")!,
                    Bind<Action<SemanticsNode>>(__instance, "OnViewHeaderSemanticsNodeClick")!, isCurrentView);
        });
        return false;
    }

    // OnSemanticsNodeClick(node, isDayView): a time slot node of the all-day panel.
    private static void DaySemanticsNodeClick_Postfix(object __instance, SemanticsNode node, bool isDayView)
    {
        if (isDayView && HeaderOf(__instance) is { } header)
            Guard("semantics", () => Call(AllDayOf(header), "OnSlotSemanticsNodeClick",
                new Point(node.Bounds.Left + 1.0, node.Bounds.Top + 1.0), Is(InfoOf(__instance), "IsRTLLayout")));
    }

    // ExpandAllDayLayout / CollapseAllDayLayout: the resource header's all-day panel animates.
    private static bool DayExpand_Prefix(object __instance) => !AnimateAllDay(__instance, expand: true);

    private static bool DayCollapse_Prefix(object __instance) => !AnimateAllDay(__instance, expand: false);

    private static bool AnimateAllDay(object control, bool expand)
    {
        if (!IsDesktop(InfoOf(control)) || HeaderOf(control) == null)
            return false;
        var view = (VisualElement)control;
        new Animation(value => Guard("all-day animation", () =>
        {
            Call(AllDayOf(HeaderOf(control)), "UpdateAllDayLayoutHeight", value, expand);
            Call(control, "LayoutMeasure", view.Width, view.Height);
            Call(control, "LayoutArrangeChildren", new Rect(0.0, 0.0, view.Width, view.Height));
        }), 0.0, 1.0).Commit(view, expand ? "AllDayExpand" : "AllDayCollapsed", 16u, 250u);
        return true;
    }

    private static void DaySetScrollOffset_Postfix(object __instance, double newOffset, double newXOffset) =>
        Guard("scroll offset", () => Call(AllDayOf(HeaderOf(__instance)), "SetScrollOffset", newOffset, newXOffset));

    // OnInteractionEvent: with resources the day view control itself takes only the all-day expander
    // (the headers and the all-day panel are the resource header's).
    private static bool DayInteraction_Prefix(object __instance, Point interactionPoint)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        Guard("expander tap", () =>
        {
            if (!s_days.TryGetValue(__instance, out var state) || state.Expander?.GetExpandableRect() is not { } rect)
                return;
            double header = ViewHeaderHeight(info!, "DaysView");
            double resources = ResourceTotalHeight(info!);
            double x = Is(info, "IsRTLLayout") ? ((VisualElement)__instance).Width - interactionPoint.X : interactionPoint.X;
            if (rect.Contains(new Point(x, interactionPoint.Y - header - resources)))
            {
                Call(__instance, "Syncfusion.Maui.Scheduler.IDaysViewInteraction.ExpandAndCollapseAllDay");
                Call(AllDayOf(state.ResourceHeader), "UpdateIsExpanded");
            }
        });
        return false;
    }

    private static void DayRemoveViewHeader_Prefix(object __instance) =>
        Guard("day header", () => RemoveViewHeaderHandler(HeaderOf(__instance)));

    // ---- Scrolling --------------------------------------------------------------------------------------

    // OnScrollViewScrolled: the time slots scroll in both directions; the day views and the resource
    // header follow horizontally, the time ruler vertically.
    private static void DayScrolled_Prefix(object __instance, Microsoft.Maui.Controls.ScrolledEventArgs e) => Guard("scrolling", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state) || state.ResourceHeader == null)
            return;
        if (state.ScrollXOffset != e.ScrollX)
        {
            var slots = Get(__instance, "dayViewLayout");
            Call(slots, "UpdateScrollOffset", e.ScrollX);
            if (IsVirtualized(InfoOf(__instance)))
                Rearrange(__instance, slots);
        }
        state.ScrollOffset = e.ScrollY;
        state.ScrollXOffset = e.ScrollX;
    });

    private static void DayScrolled_Postfix(object __instance, Microsoft.Maui.Controls.ScrolledEventArgs e) => Guard("scrolling", () =>
    {
        if (!s_days.TryGetValue(__instance, out var state))
            return;
        var slots = ScrollViewOf(__instance);
        state.TimeRulerScroll?.ScrollToAsync(e.ScrollX, slots.ScrollY, false);
        state.ResourceScroll?.ScrollToAsync(e.ScrollX, slots.ScrollY, false);
    });

    // ResourceViewScrollView_Scrolled: the resource header follows; a virtualized one moves its window.
    private static void DayResourceScrolled_Postfix(object __instance, Microsoft.Maui.Controls.ScrolledEventArgs e) => Guard("scrolling", () =>
    {
        if (HeaderOf(__instance) is not { } header)
            return;
        Call(header, "UpdateScrollOffset", e.ScrollX);
        if (IsVirtualized(InfoOf(__instance)))
        {
            Rearrange(__instance, header);
            Rearrange(__instance, AllDayOf(header));
        }
    });

    /// <summary>
    /// DayViewControl.UpdateDayViewLayout and UpdateDayViewResourceLayout of the Windows build: a
    /// virtualized layout arranges its drawn window at the new scroll offset and draws it again.
    /// </summary>
    private static void Rearrange(object control, object? layout)
    {
        var view = (VisualElement)control;
        if (layout == null || view.Width <= 0.0 || view.Height <= 0.0)
            return;
        CallStatic(ViewHelper!, "InvalidateArrange", layout);
        foreach (var child in ((Layout)layout).Children)
            InvalidateDrawable(child);
    }

    // INotifyTimeSlotLayout.UpdateScrollPosition(displayDate, scrollOffset, timelineOffset), as the
    // Windows build has it with resources: the position is kept and applied again after layout
    // (before the time slots are measured there is nowhere to scroll to).
    private static bool DayUpdateScrollPosition_Prefix(object __instance, DateTime? displayDate, double? scrollOffset, double? timelineOffset)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info) || HeaderOf(__instance) == null)
            return true;
        Guard("scroll position", () =>
        {
            var daysView = Get(info, "DaysView");
            double intervalHeight = Get<double>(daysView, "TimeIntervalHeight");
            if (intervalHeight == -1.0)
                return;
            var slots = ScrollViewOf(__instance);
            if (!scrollOffset.HasValue)
            {
                if (displayDate.HasValue)
                    displayDate = (DateTime)CallStatic(ViewHelper!, "GetMoveToDateTime", displayDate.Value, info)!;
                float slotSize = (float)CallStatic(ViewHelper!, "GetTimeIntervalSize", intervalHeight, Get(info, "View"), 0.0, 0)!;
                scrollOffset = Convert.ToDouble(CallStatic(ViewHelper!, "GetDateTimeVerticalPosition", slotSize,
                    displayDate ?? Get<DateTime>(info, "DisplayDate"), CallStatic(ViewHelper!, "GetTimeInterval", daysView), Get<double>(daysView, "StartHour")));
            }
            if (!timelineOffset.HasValue)
            {
                var visibleDates = Get<List<DateTime>>(__instance, "VisibleDates")!;
                int dateIndex = visibleDates.IndexOf((displayDate ?? Get<DateTime>(info, "DisplayDate")).Date);
                var resources = Get<IList>(info, "SchedulerResources");
                var selectedId = Get(Get(info, "ResourceView"), "SelectedResourceId");
                int resourceIndex = 0;
                if (resources != null)
                {
                    for (int i = 0; i < resources.Count; i++)
                    {
                        if (Equals(Get(resources[i], "Id"), selectedId))
                        {
                            resourceIndex = i;
                            break;
                        }
                    }
                }
                if (dateIndex >= 0)
                {
                    int count = visibleDates.Count;
                    int horizontalIndex = (int)CallStatic(ViewHelper!, "GetHorizontalResourceDateIndex", info, dateIndex, count, resources?.Count ?? 1, resourceIndex)!;
                    double rulerWidth = Math.Max(0.0, Get<double>(daysView, "TimeRulerWidth"));
                    var view = (VisualElement)__instance;
                    double width = view.DesiredSize.Width > 0.0 ? view.DesiredSize.Width : view.Width;
                    timelineOffset = (double)CallStatic(ViewHelper!, "GetDateTimeHorizontalPosition", info, Get<DateTime>(info, "DisplayDate"),
                        horizontalIndex, count, width - rulerWidth)!;
                }
                else
                {
                    timelineOffset = Is(info, "IsRTLLayout") ? slots.ContentSize.Width : 0.0;
                }
            }
            if (slots.ScrollY == scrollOffset && slots.ScrollX == timelineOffset)
                return;
            var state = DayStateOf(__instance);
            state.ScrollOffset = scrollOffset.Value;
            state.ScrollXOffset = timelineOffset.Value;
            Call(Get(__instance, "dayViewLayout"), "UpdateScrollOffset", timelineOffset.Value);
            if (slots.ContentSize.Width > timelineOffset.Value)
                slots.ScrollToAsync(timelineOffset.Value, scrollOffset.Value, false);
        });
        return false;
    }

    // INotifySchedulerLayout.GetAutoScrollPosition(appViewInfo, dragStartPoint, draggingPoint): an
    // appointment dragged past the left or right edge of the visible time slots scrolls them
    // horizontally (the Windows build tests this before the top and bottom edges).
    private static void DayAutoScrollPosition_Postfix(object __instance, object? appViewInfo, Point dragStartPoint, Point draggingPoint, ref object __result)
    {
        // AutoScrollPosition: None 0, Left 1, Top 2, Right 3, Bottom 4, LeftSwipe 5, RightSwipe 6.
        if (appViewInfo == null || draggingPoint == Point.Zero || Convert.ToInt32(__result) is 5 or 6)
            return;
        var info = InfoOf(__instance);
        if (info == null)
            return;
        bool desktop = IsDesktop(info);
        bool rtl = Is(info, "IsRTLLayout");
        double rulerWidth = Get<double>(Get(info, "DaysView"), "TimeRulerWidth");
        bool singleDay = !desktop && IsSingleDay(info);
        double leftMargin = !rtl ? rulerWidth : (!desktop ? 10 : 0);
        double rightMargin = singleDay || rtl ? rulerWidth : (!desktop ? 10 : 0);
        var rect = Get<Rect>(appViewInfo, "AppointmentViewRect");
        var frame = ScrollViewOf(__instance).Frame;
        if (draggingPoint.X - (dragStartPoint.X - rect.Left) <= -leftMargin)
            __result = Enum.ToObject(__result.GetType(), 1);
        else if (draggingPoint.X + (rect.Right - dragStartPoint.X) >= frame.Width + rightMargin)
            __result = Enum.ToObject(__result.GetType(), 3);
    }

    // INotifySchedulerLayout.UpdateScrollOnDrag(autoScrollPosition): Left and Right scroll by 10.
    private static void DayScrollOnDrag_Postfix(object __instance, int autoScrollPosition) => Guard("drag scroll", () =>
    {
        if (autoScrollPosition is not (1 or 3) || Get(Get(InfoOf(__instance), "DragDropSettings"), "AllowScroll") is not true)
            return;
        var slots = ScrollViewOf(__instance);
        slots.ScrollToAsync(slots.ScrollX + (autoScrollPosition == 1 ? -10.0 : 10.0), slots.ScrollY, false);
    });

    // ---- Measure and arrange ------------------------------------------------------------------------

    // LayoutMeasure(widthConstraint, heightConstraint) with resources: the resource header (resource
    // names, day header and all-day panel) above, the time ruler beside the time slots.
    private static bool DayMeasure_Prefix(object __instance, double widthConstraint, double heightConstraint, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info) || !s_days.TryGetValue(__instance, out var state) || state.ResourceHeader == null)
            return true;
        try
        {
            __result = MeasureDay(__instance, info!, state, widthConstraint, heightConstraint);
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: measuring the day view failed", ex);
            return true;
        }
    }

    private static Size MeasureDay(object __instance, object info, DayState state, double widthConstraint, double heightConstraint)
    {
        double width = double.IsFinite(widthConstraint) ? widthConstraint : 0.0;
        double height = double.IsFinite(heightConstraint) ? heightConstraint : 0.0;
        var daysView = Get(info, "DaysView");
        double rulerWidth = Get<double>(daysView, "TimeRulerWidth");
        double viewHeaderHeight = ViewHeaderHeight(info!, "DaysView");
        var header = state.ResourceHeader!;
        double allDayHeight = GetAllDayLayoutHeight(header, viewHeaderHeight);
        double resourceHeight = ResourceTotalHeight(info!);
        if (Get<IList>(__instance, "allDayAppointments")?.Count is null or 0)
            allDayHeight = 0.0;
        Set(__instance, "allDayLayoutHeight", allDayHeight);
        double slotsHeight = height - allDayHeight - viewHeaderHeight - resourceHeight;
        double headerHeight = height - slotsHeight;
        double viewPort = width - rulerWidth;
        if (Call(__instance, DaysInteraction + "AllDayAnimationIsRunning") is true && ((VisualElement)__instance).DesiredSize.Width > 0.0)
            viewPort = ((VisualElement)__instance).DesiredSize.Width - rulerWidth;
        var dayViewLayout = Get(__instance, "dayViewLayout");
        // The desktop builds invalidate the header and the time slots here on every pass; the
        // layout manager of WinUI and UIKit measures them again in the same pass, MAUI's on
        // Linux schedules another pass, so they are invalidated only when the viewport changed.
        bool viewPortChanged = Get<double>(dayViewLayout, "viewPortWidth") != viewPort;
        // Render virtualization: set before the header and the time slots measure, which read it.
        bool virtualized = NeedsVirtualization(info, viewPort);
        if (IsVirtualized(info) != virtualized)
        {
            SetVirtualized(info, virtualized);
            viewPortChanged = true;
            ((IView)header!).InvalidateMeasure();
        }
        UpdateViewPortWidth(header, viewPort);
        Call(dayViewLayout, "UpdateViewPortWidth", viewPort);
        UpdateResourceViewGridHeight(header, allDayHeight, viewHeaderHeight, resourceHeight, headerHeight);
        if (viewPortChanged)
            ((IView?)dayViewLayout)?.InvalidateMeasure();
        state.LeftBorder?.Measure(1.0, height);
        state.TimeRulerGrid?.Measure(rulerWidth, slotsHeight);
        state.Expander?.Measure(rulerWidth, allDayHeight);
        state.ResourceHeaderGrid?.Measure(width - rulerWidth, headerHeight);
        Call(dayViewLayout, "UpdateViewPortHeight", slotsHeight);
        state.TimeRuler?.UpdateViewPortHeight(slotsHeight);
        (Get(__instance, "grid") as VisualElement)?.Measure(width - rulerWidth, slotsHeight);
        (Get(__instance, "dayViewTopBorder") as VisualElement)?.Measure(width, 1.0);
        state.Measured = new Size(width, height);
        return state.Measured;
    }

    private static bool DayArrange_Prefix(object __instance, Rect bounds, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info) || !s_days.TryGetValue(__instance, out var state) || state.ResourceHeader == null)
            return true;
        LayoutPasses++;
        try
        {
            // A snap layout that replaces its views (a view switch) arranges the new ones without
            // measuring them; the resource view's sizes come from its measure.
            if (state.Measured != bounds.Size)
                MeasureDay(__instance, info!, state, bounds.Width, bounds.Height);
            double rulerWidth = Get<double>(Get(info, "DaysView"), "TimeRulerWidth");
            double allDayTop = (double)CallStatic(ViewHelper!, "GetAllDayTopPosition", info)!;
            double allDayHeight = Get<double>(__instance, "allDayLayoutHeight");
            bool rtl = Is(info, "IsRTLLayout");
            double rulerX = rtl ? bounds.Width - rulerWidth : 0.0;
            double slotsX = rtl ? 0.0 : rulerWidth;
            double borderX = (rtl ? bounds.Width - rulerWidth : rulerWidth) - 1.0;
            double slotsTop = allDayTop + allDayHeight + ResourceTotalHeight(info!);
            var grid = Get(__instance, "grid");
            var topBorder = Get(__instance, "dayViewTopBorder") as Line;
            foreach (var child in ((Layout)__instance).Children)
            {
                if (ReferenceEquals(child, topBorder))
                {
                    topBorder!.X1 = 0.0;
                    topBorder.X2 = bounds.Width;
                    topBorder.Y1 = 0.0;
                    topBorder.Y2 = 0.0;
                    child.Arrange(new Rect(0.0, slotsTop - 1.0, bounds.Width, 1.0));
                }
                else if (ReferenceEquals(child, grid))
                {
                    child.Arrange(new Rect(slotsX, slotsTop, bounds.Width - rulerWidth, bounds.Height - slotsTop));
                }
                else if (ReferenceEquals(child, state.ResourceHeaderGrid))
                {
                    child.Arrange(new Rect(slotsX, 0.0, bounds.Width - rulerWidth, slotsTop));
                }
                else if (ReferenceEquals(child, state.LeftBorder))
                {
                    state.LeftBorder!.X1 = 0.0;
                    state.LeftBorder.X2 = 0.0;
                    state.LeftBorder.Y1 = 0.0;
                    state.LeftBorder.Y2 = bounds.Height;
                    child.Arrange(new Rect(borderX, 0.0, 1.0, bounds.Height));
                }
                else if (ReferenceEquals(child, state.TimeRulerGrid))
                {
                    child.Arrange(new Rect(rulerX, slotsTop, rulerWidth, bounds.Height - slotsTop));
                }
                else if (ReferenceEquals(child, state.Expander))
                {
                    child.Arrange(new Rect(rulerX, slotsTop - allDayHeight, rulerWidth, allDayHeight));
                }
            }
            // The scroll position kept by UpdateScrollPosition and the scroll handler, once the time
            // slots are large enough to reach it (a scroll view clamps, and another arrange would
            // follow every attempt).
            var slots = ScrollViewOf(__instance);
            if ((state.ScrollOffset != slots.ScrollY || state.ScrollXOffset != slots.ScrollX) && !IsDragging(info)
                && state.ScrollOffset <= Math.Max(0.0, slots.ContentSize.Height - slots.Height)
                && state.ScrollXOffset <= Math.Max(0.0, slots.ContentSize.Width - slots.Width))
                slots.ScrollToAsync(state.ScrollXOffset, state.ScrollOffset, false);
            // The time ruler and the resource header follow the time slots (a new ruler starts at the top).
            if (state.TimeRulerScroll is { } ruler && ruler.ScrollY != slots.ScrollY)
                ruler.ScrollToAsync(0.0, slots.ScrollY, false);
            if (state.ResourceScroll is { } resources && resources.ScrollX != slots.ScrollX)
                resources.ScrollToAsync(slots.ScrollX, 0.0, false);
            __result = bounds.Size;
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: arranging the day view failed", ex);
            return true;
        }
    }

    private static void InvalidateDrawable(object? view)
    {
        if (view is IDrawableLayout layout)
            layout.InvalidateDrawable();
        else if (view != null)
            TryCall(view, "InvalidateDrawable", out _);
    }
}
