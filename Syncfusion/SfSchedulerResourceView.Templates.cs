// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

// Templates in a virtualized resource view. The views that draw only a window around the scroll
// offset cannot hold template views across the full width, so the Windows build lays templates out
// in views of their own when it virtualizes: the all-day appointment and "more" templates
// (AllDayAppointmentsTemplateView), the time region template (SpecialTimeRegionTemplateView), and
// the appointment and cell selection templates at the full width (the resource and day header
// templates are in SfSchedulerResourceView.Header.cs). The neutral build has those views, but not
// the members that add, measure, arrange and update them.
internal static partial class SfSchedulerResourceView
{
    /// <summary>The template views the Windows AllDayAppointmentsLayout and DayViewLayout add.</summary>
    private sealed class TemplateState
    {
        public View? AllDayTemplate;    // AllDayAppointmentsTemplateView
        public View? TimeRegionTemplate; // SpecialTimeRegionTemplateView
    }

    private static readonly ConditionalWeakTable<object, TemplateState> s_templates = new();

    private static View? AllDayTemplateOf(object layout) => s_templates.TryGetValue(layout, out var s) ? s.AllDayTemplate : null;

    private static View? TimeRegionTemplateOf(object layout) => s_templates.TryGetValue(layout, out var s) ? s.TimeRegionTemplate : null;

    private static void InstallTemplates(Harmony harmony)
    {
        var allDay = s_allDayLayout;
        Patch(harmony, allDay, "LayoutMeasure", prefix: nameof(AllDayMeasure_Prefix));
        Patch(harmony, allDay, "LayoutArrangeChildren", prefix: nameof(AllDayArrange_Prefix));
        Patch(harmony, allDay, "UpdateVisibleDatesChange", postfix: nameof(AllDayVisibleDates_Postfix));
        Patch(harmony, allDay, "UpdateVisibleAppointments", postfix: nameof(AllDayVisibleAppointments_Postfix));
        Patch(harmony, allDay, "OnTapGestureAction", prefix: nameof(AllDayTap_Prefix));

        var day = T("DayViewLayout");
        Patch(harmony, day, "LayoutMeasure", prefix: nameof(DayLayoutMeasure_Prefix));
        Patch(harmony, day, "LayoutArrangeChildren", prefix: nameof(DayLayoutArrange_Prefix));
        Patch(harmony, day, "UpdateDisabledDatesChange", prefix: nameof(DayLayoutDisabledDates_Prefix), postfix: nameof(DayLayoutDisabledDates_Postfix));
        Patch(harmony, day, "UpdateVisibleDatesChange", prefix: nameof(DayLayoutVisibleDates_Prefix), postfix: nameof(DayLayoutVisibleDates_Postfix));
        Patch(harmony, day, "InvalidateTimeRegionViewMeasure", postfix: nameof(DayLayoutTimeRegionMeasure_Postfix));
        Patch(harmony, day, "UpdateTimeRegions", postfix: nameof(DayLayoutTimeRegionMeasure_Postfix));
        Patch(harmony, day, "UpdateTimeSlotCount", prefix: nameof(DayLayoutTimeSlots_Prefix), postfix: nameof(DayLayoutTimeSlots_Postfix));
        Patch(harmony, day, "UpdateVisibleTimeRegions", postfix: nameof(DayLayoutTimeRegions_Postfix));
    }

    private static bool HasTemplate(object? settings, string property) => Get(settings, property) != null;

    private static void RemoveTemplateView(Layout layout, View view, params string[] removeHandlers)
    {
        foreach (var method in removeHandlers)
            Call(view, method);
        Remove(layout, view);
    }

    // ---- All-day panel --------------------------------------------------------------------------------

    // AllDayAppointmentsLayout.LayoutMeasure with resources side by side, as the Windows build has it:
    // virtualized, the all-day and "more" templates move to a template view measured across the
    // full width (the other children are a window); otherwise the appointments view holds them again.
    private static bool AllDayMeasure_Prefix(object __instance, double widthConstraint, double heightConstraint, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        try
        {
            __result = MeasureAllDay(__instance, info!, widthConstraint, heightConstraint);
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: measuring the all-day panel failed", ex);
            return true;
        }
    }

    private static Size MeasureAllDay(object layoutObject, object info, double widthConstraint, double heightConstraint)
    {
        var layout = (Layout)layoutObject;
        double width = widthConstraint;
        double height = double.IsFinite(heightConstraint) ? heightConstraint : 0.0;
        double window = width;
        bool virtualized = IsVirtualized(info);
        var daysView = Get(info, "DaysView");
        bool templated = HasTemplate(daysView, "AllDayAppointmentTemplate") || HasTemplate(daysView, "MoreAppointmentsTemplate");
        var appointments = Get(layoutObject, "appointmentsView");
        var state = s_templates.GetValue(layoutObject, _ => new TemplateState());
        if (virtualized)
        {
            window = VirtualWidth(Get<double>(layoutObject, "viewPortWidth"));
            Call(layoutObject, "UpdateTotalDayViewWidth", width);
            if (templated && state.AllDayTemplate == null && appointments != null)
            {
                Call(appointments, "RemoveAppointmentTemplateHandlers");
                Call(appointments, "RemoveMoreTemplateHandlers");
                var view = (View)New(T("AllDayAppointmentsTemplateView")!, Get(layoutObject, "visibleAppointments"), Get(layoutObject, "visibleDates"), info,
                    Get(layoutObject, "dayViewInteraction"))!;
                state.AllDayTemplate = view;
                layout.Insert(layout.IndexOf((IView)appointments), view);
                ShareAppointmentViewsInfo(appointments, view);
                Call(view, "UpdateAllDayAppointmentsTemplateView");
            }
        }
        else if (templated && state.AllDayTemplate != null)
        {
            RemoveTemplateView(layout, state.AllDayTemplate, "RemoveAppointmentTemplateHandlers", "RemoveMoreTemplateHandlers");
            state.AllDayTemplate = null;
            Call(appointments, "UpdateAppointmentViewsInfo");
        }
        var selection = Get(layoutObject, "selectionView");
        bool selectionTemplate = virtualized && HasTemplate(Get(info, "CellSelectionView"), "Template");
        foreach (var child in layout.Children.ToList())
        {
            if (ReferenceEquals(child, selection))
            {
                Call(selection, "UpdateDrawingOrder");
                child.Measure(selectionTemplate ? width : window, height);
            }
            else if (ReferenceEquals(child, state.AllDayTemplate) && virtualized)
            {
                child.Measure(width, height);
            }
            else
            {
                child.Measure(window, height);
            }
        }
        return new Size(width, height);
    }

    // The template view lays out the appointments view's appointment and "more" rectangles.
    private static void ShareAppointmentViewsInfo(object appointments, object template)
    {
        if (Call(appointments, "GetAppointmentViewsInfo") is ITuple { Length: 2 } info)
            Call(template, "GetAppointmentViewsInfo", info[0], info[1]);
    }

    // AllDayAppointmentsLayout.LayoutArrangeChildren with resources side by side (its start position
    // is 0 then): the children in the window around the scroll offset; virtualized, the template view,
    // and with an all-day template the resize view, across the full width.
    private static bool AllDayArrange_Prefix(object __instance, Rect bounds, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        try
        {
            bool virtualized = IsVirtualized(info);
            double window = virtualized ? VirtualWidth(Get<double>(__instance, "viewPortWidth")) : bounds.Width;
            double x = virtualized ? VirtualX(Get<double>(__instance, "scrollXOffset"), window) : 0.0;
            var full = new Rect(0.0, 0.0, bounds.Width, bounds.Height);
            var drawn = new Rect(x, 0.0, window, bounds.Height);
            var selection = Get(__instance, "selectionView");
            var resize = Get(__instance, "appointmentResizeView");
            bool allDayTemplate = HasTemplate(Get(info, "DaysView"), "AllDayAppointmentTemplate");
            bool selectionTemplate = virtualized && HasTemplate(Get(info, "CellSelectionView"), "Template");
            foreach (var child in ((Layout)__instance).Children)
            {
                if (ReferenceEquals(child, selection))
                {
                    Call(selection, "UpdateDrawingOrder");
                    child.Arrange(selectionTemplate ? full : drawn);
                }
                else if (ReferenceEquals(child, AllDayTemplateOf(__instance)) && virtualized)
                {
                    child.Arrange(full);
                }
                else
                {
                    child.Arrange(ReferenceEquals(child, resize) && allDayTemplate ? full : drawn);
                }
            }
            __result = bounds.Size;
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: arranging the all-day panel failed", ex);
            return true;
        }
    }

    private static void AllDayVisibleDates_Postfix(object __instance, List<DateTime> visibleDates) =>
        Guard("all-day templates", () => Call(AllDayTemplateOf(__instance), "UpdateVisibleDates", visibleDates));

    private static void AllDayVisibleAppointments_Postfix(object __instance, object visibleAppointments) => Guard("all-day templates", () =>
    {
        if (AllDayTemplateOf(__instance) is not { } view)
            return;
        Call(view, "UpdateVisibleAppointments", visibleAppointments);
        if (Get(__instance, "appointmentsView") is { } appointments)
            ShareAppointmentViewsInfo(appointments, view);
        Call(view, "UpdateAllDayAppointmentsTemplateView");
    });

    // OnTapGestureAction: the "more" indicators the template view laid out are the ones tapped.
    private static void AllDayTap_Prefix(object __instance) => Guard("all-day templates", () =>
    {
        if (AllDayTemplateOf(__instance) is { } view)
            Call(Get(__instance, "appointmentsView"), "GetMoreAppointmentItemInfo", Call(view, "GetMoreAppointmentViewsInfo"));
    });

    // ---- Time slots ---------------------------------------------------------------------------------------

    // DayViewLayout.LayoutMeasure with resources side by side, as the Windows build has it:
    // virtualized, the time slots are a window three viewports wide, the time region template moves to
    // a template view across the full width, and the appointments and the cell selection with a
    // template are measured across the full width too.
    private static bool DayLayoutMeasure_Prefix(object __instance, double widthConstraint, double heightConstraint, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        try
        {
            __result = MeasureDayLayout(__instance, info!, widthConstraint, heightConstraint);
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: measuring the time slots failed", ex);
            return true;
        }
    }

    private static Size MeasureDayLayout(object layoutObject, object info, double widthConstraint, double heightConstraint)
    {
        var layout = (Layout)layoutObject;
        var daysView = Get(info, "DaysView");
        double height = double.IsFinite(heightConstraint) ? heightConstraint : 0.0;
        double intervalHeight = Get<double>(daysView, "TimeIntervalHeight");
        if (intervalHeight != -1.0)
            height = Get<int>(layoutObject, "timeSlotCount") * (float)CallStatic(ViewHelper!, "GetTimeIntervalSize", intervalHeight, Get(info, "View"), 0.0, 0)!;
        else if (!double.IsFinite(heightConstraint))
            height = Get<double>(layoutObject, "viewPortHeight");
        double viewPortWidth = Get<double>(layoutObject, "viewPortWidth");
        double width = (double)CallStatic(ViewHelper!, "HorizontalResourceViewDesktopViewPortWidth", info, viewPortWidth)!;
        bool virtualized = width > VirtualWidth(viewPortWidth);
        SetVirtualized(info, virtualized);
        double window = width;
        if (virtualized)
        {
            window = VirtualWidth(viewPortWidth);
            Call(layoutObject, "UpdateTotalDayViewWidth", width);
        }
        var dayView = Get(layoutObject, "dayView") as View;
        var state = s_templates.GetValue(layoutObject, _ => new TemplateState());
        if (HasTemplate(daysView, "TimeRegionTemplate"))
        {
            if (virtualized)
            {
                if (state.TimeRegionTemplate == null && dayView != null)
                {
                    Call(dayView, "RemoveTimeRegionTemplateViews");
                    var view = (View)New(T("SpecialTimeRegionTemplateView")!, Get(layoutObject, "visibleDates"), Get(layoutObject, "disabledDates"), info,
                        Get<int>(layoutObject, "timeSlotCount"))!;
                    state.TimeRegionTemplate = view;
                    layout.Insert(layout.IndexOf(dayView) + 1, view);
                    Call(view, "UpdateVisibleTimeRegions", Get(layoutObject, "visibleTimeRegions"));
                }
            }
            else if (state.TimeRegionTemplate != null)
            {
                // (The Windows build keeps its reference to the removed view and never adds another.)
                RemoveTemplateView(layout, state.TimeRegionTemplate, "RemoveTimeRegionTemplateViews");
                state.TimeRegionTemplate = null;
                Call(dayView, "UpdateVisibleTimeRegions", Get(layoutObject, "visibleTimeRegions"));
            }
        }
        var appointments = Get(layoutObject, "dayAppointmentsView");
        var selection = Get(layoutObject, "selectionView");
        bool appointmentTemplate = virtualized && HasTemplate(daysView, "AppointmentTemplate");
        bool selectionTemplate = virtualized && HasTemplate(Get(info, "CellSelectionView"), "Template");
        foreach (var child in layout.Children.ToList())
        {
            if (ReferenceEquals(child, dayView) || ReferenceEquals(child, Get(layoutObject, "currentTimeIndicatorView"))
                || ReferenceEquals(child, Get(layoutObject, "appointmentResizeView")))
            {
                child.Measure(window, height);
            }
            else if (ReferenceEquals(child, state.TimeRegionTemplate))
            {
                child.Measure(width, height);
            }
            else if (ReferenceEquals(child, appointments))
            {
                child.Measure(appointmentTemplate ? width : window, height);
            }
            else if (ReferenceEquals(child, selection))
            {
                Call(selection, "UpdateDrawingOrder");
                child.Measure(selectionTemplate ? width : window, height);
            }
            else
            {
                child.Measure(window, height);
            }
        }
        return new Size(width, height);
    }

    // DayViewLayout.LayoutArrangeChildren with resources side by side (no time ruler inside): the
    // children in the window around the scroll offset; the time region template view, the resize
    // view with an appointment template, and virtualized the appointments and the cell selection with
    // a template, across the full width.
    private static bool DayLayoutArrange_Prefix(object __instance, Rect bounds, ref Size __result)
    {
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        try
        {
            var daysView = Get(info, "DaysView");
            double height = bounds.Height;
            double intervalHeight = Get<double>(daysView, "TimeIntervalHeight");
            if (intervalHeight != -1.0)
                height = Get<int>(__instance, "timeSlotCount") * (float)CallStatic(ViewHelper!, "GetTimeIntervalSize", intervalHeight, Get(info, "View"), 0.0, 0)!;
            bool virtualized = IsVirtualized(info);
            double window = virtualized ? VirtualWidth(Get<double>(__instance, "viewPortWidth")) : bounds.Width;
            double x = virtualized ? VirtualX(Get<double>(__instance, "scrollXOffset"), window) : 0.0;
            var full = new Rect(0.0, 0.0, bounds.Width, height);
            var drawn = new Rect(x, 0.0, window, height);
            var appointments = Get(__instance, "dayAppointmentsView");
            var selection = Get(__instance, "selectionView");
            var resize = Get(__instance, "appointmentResizeView");
            bool appointmentTemplate = HasTemplate(daysView, "AppointmentTemplate");
            bool selectionTemplate = HasTemplate(Get(info, "CellSelectionView"), "Template");
            foreach (var child in ((Layout)__instance).Children)
            {
                if (ReferenceEquals(child, TimeRegionTemplateOf(__instance)))
                    child.Arrange(full);
                else if (ReferenceEquals(child, resize))
                    child.Arrange(appointmentTemplate ? full : drawn);
                else if (ReferenceEquals(child, appointments))
                    child.Arrange(virtualized && appointmentTemplate ? full : drawn);
                else if (ReferenceEquals(child, selection))
                    child.Arrange(virtualized && selectionTemplate ? full : drawn);
                else
                    child.Arrange(drawn);
            }
            __result = bounds.Size;
            return false;
        }
        catch (Exception ex)
        {
            Services.DiagnosticLog.Error("Syncfusion", "SfScheduler resource view: arranging the time slots failed", ex);
            return true;
        }
    }

    private static void DayLayoutDisabledDates_Prefix(object __instance, List<DateTime> disabledDates, out bool __state) =>
        __state = !ReferenceEquals(Get(__instance, "disabledDates"), disabledDates);

    private static void DayLayoutDisabledDates_Postfix(object __instance, List<DateTime> disabledDates, bool __state) => Guard("time region templates", () =>
    {
        if (__state && ReferenceEquals(Get(__instance, "disabledDates"), disabledDates))
            Call(TimeRegionTemplateOf(__instance), "UpdateDisabledDatesChange", disabledDates, false);
    });

    private static void DayLayoutVisibleDates_Prefix(object __instance, List<DateTime> visibleDates, out bool __state) =>
        __state = !ReferenceEquals(Get(__instance, "visibleDates"), visibleDates);

    private static void DayLayoutVisibleDates_Postfix(object __instance, List<DateTime> visibleDates, bool isCurrentView, bool __state) =>
        Guard("time region templates", () =>
        {
            if (__state)
                Call(TimeRegionTemplateOf(__instance), "UpdateVisibleDatesChange", visibleDates, isCurrentView);
        });

    private static void DayLayoutTimeRegionMeasure_Postfix(object __instance) =>
        Guard("time region templates", () => Call(TimeRegionTemplateOf(__instance), "InvalidateTemplateMeasure"));

    private static void DayLayoutTimeSlots_Prefix(object __instance, out int __state) => __state = Get<int>(__instance, "timeSlotCount");

    private static void DayLayoutTimeSlots_Postfix(object __instance, int __state) => Guard("time region templates", () =>
    {
        int count = Get<int>(__instance, "timeSlotCount");
        if (count != __state)
            Call(TimeRegionTemplateOf(__instance), "UpdateTimeSlotCount", count);
    });

    private static void DayLayoutTimeRegions_Postfix(object __instance) =>
        Guard("time region templates", () => Call(TimeRegionTemplateOf(__instance), "UpdateVisibleTimeRegions", Get(__instance, "visibleTimeRegions") as IList));
}
