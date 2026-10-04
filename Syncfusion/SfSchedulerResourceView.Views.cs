// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

// The views around the resource view: the time slots, the month resource layout, the vertical
// month's resource header, the snap layout that holds the views and the scheduler itself.
internal static partial class SfSchedulerResourceView
{
    private static void InstallViews(Harmony harmony)
    {
        // The time slots: the appointments view knows the viewport width (a drag's container width).
        Patch(harmony, T("DayViewLayout"), "UpdateViewPortWidth", postfix: nameof(DayLayoutViewPortWidth_Postfix));
        // Virtualized, the time slots' hover draws from the scroll offset and the full width too.
        Patch(harmony, T("DayViewLayout"), "UpdateScrollOffset", postfix: nameof(DayLayoutScrollOffset_Postfix));
        Patch(harmony, T("DayViewLayout"), "UpdateTotalDayViewWidth", postfix: nameof(DayLayoutTotalWidth_Postfix));
        Patch(harmony, T("DayAppointmentsView"), "OnTouch", prefix: nameof(DayAppointmentsTouch_Prefix));

        // Month: the resource layout's row heights and dates, re-measuring, horizontal drag scrolling.
        var month = T("MonthViewLayout");
        Patch(harmony, month, "AddMonthResourceLayout", postfix: nameof(MonthAddResourceLayout_Postfix));
        Patch(harmony, month, "UpdateResourceHeaderHeightChanged", postfix: nameof(MonthResourceHeaderHeight_Postfix));
        Patch(harmony, month, NotifyLayout + "GetAutoScrollPosition", postfix: nameof(MonthAutoScrollPosition_Postfix));
        Patch(harmony, month, NotifyLayout + "UpdateScrollOnDrag", postfix: nameof(MonthScrollOnDrag_Postfix));
        Patch(harmony, T("MonthResourceViewControl"), "LayoutArrangeChildren", prefix: nameof(MonthResourceArrange_Prefix));

        // The vertical month's resource header (ResourceHeaderControl): its viewport and dates.
        Patch(harmony, T("SchedulerVerticalStackLayout"), "MeasureContent", prefix: nameof(StackMeasure_Prefix));
        Patch(harmony, T("ResourceHeaderControl"), "InvalidateLayoutMeasure", postfix: nameof(ResourceHeaderControlInvalidate_Postfix));
    }

    private static void InstallSnapLayoutAndScheduler(Harmony harmony)
    {
        var snap = T("CustomSnapLayout");
        Patch(harmony, snap, "UpdateViewChange", postfix: nameof(SnapViewChange_Postfix));
        Patch(harmony, snap, "UpdateResourceGroupType", postfix: nameof(SnapResourceGroupType_Postfix));
        Patch(harmony, snap, "ResourceViewInvalidateDrawView", postfix: nameof(SnapResourceInvalidateDraw_Postfix));
        Patch(harmony, snap, "RemoveHorizontalResourceViewLayout", prefix: nameof(SnapRemoveResourceLayout_Prefix));
        Patch(harmony, snap, "AddHorizontalResourceViewHeaderLayout", prefix: nameof(SnapAddResourceLayout_Prefix));
        Patch(harmony, snap, "InvalidateLayoutMeasure", postfix: nameof(SnapInvalidateLayoutMeasure_Postfix));

        var scheduler = T("SfScheduler");
        Patch(harmony, scheduler, "Syncfusion.Maui.Scheduler.IResourceViewInfo.UpdateResourceViewSettingsChanged", prefix: nameof(SchedulerResourceSettings_Prefix));
        Patch(harmony, scheduler, "Syncfusion.Maui.Scheduler.IScheduler.UpdateVisibleDates", postfix: nameof(SchedulerVisibleDates_Postfix));
    }

    // ---- Time slots --------------------------------------------------------------------------------

    private static void DayLayoutViewPortWidth_Postfix(object __instance, double width) =>
        Guard("viewport", () => Call(Get(__instance, "dayAppointmentsView"), "UpdateViewPortWidth", width));

    private static void DayLayoutScrollOffset_Postfix(object __instance, double hOffset) =>
        Guard("scroll offset", () => Call(Get(__instance, "hoverView"), "UpdateScrollOffset", hOffset));

    private static void DayLayoutTotalWidth_Postfix(object __instance, double width) =>
        Guard("virtualization", () => Call(Get(__instance, "hoverView"), "UpdateTotalDayViewWidth", width));

    // DayAppointmentsView.OnTouch(parentPos, childPos, status): a drag starts with the viewport as
    // its container width when the resources are side by side.
    private static bool DayAppointmentsTouch_Prefix(object __instance, Point parentPos, GestureStatus status)
    {
        if (status != GestureStatus.Started)
            return true;
        var info = InfoOf(__instance);
        if (!IsDesktop(info))
            return true;
        return Guard("drag start", () =>
        {
            var appointmentInfo = Call(__instance, "GetAppointmentViewInfo", parentPos);
            if (Get(info, "DragAndDropController") is { } controller)
            {
                Call(controller, "ProcessOnDragEnter", appointmentInfo, parentPos, (__instance as VisualElement)?.Handler?.PlatformView,
                    Get<double>(__instance, "viewPortWidth"), (double?)0.0, false);
            }
            return false;
        }, true);
    }

    // ---- Month ---------------------------------------------------------------------------------------

    private static void MonthAddResourceLayout_Postfix(object __instance) => Guard("month resource header", () =>
    {
        if (Get(__instance, "monthResourceLayout") is { } header)
        {
            var state = HeaderStateOf(header);
            state.VisibleDates ??= Get<List<DateTime>>(__instance, "VisibleDates");
            state.DisabledDates ??= Get<List<DateTime>>(__instance, "disabledDates");
        }
    });

    private static void MonthResourceHeaderHeight_Postfix(object __instance) =>
        Guard("month resource header", () => UpdateResourceHeaderLayout(Get(__instance, "monthResourceLayout") as Layout));

    /// <summary>MonthViewLayout.InvalidateLayoutMeasure of the desktop builds.</summary>
    private static void InvalidateMonthLayoutMeasure(object month, bool isCollectionChanged)
    {
        Call(Get(month, "monthResourceLayout"), "InvalidateLayoutMeasure", isCollectionChanged);
        (Get(month, "monthResourceViewControl") as VisualElement)?.InvalidateMeasure();
    }

    // MonthResourceViewControl.LayoutArrangeChildren(bounds): the resource header's row heights.
    private static void MonthResourceArrange_Prefix(object __instance) => Guard("month resource header", () =>
    {
        var info = InfoOf(__instance);
        if (Get(__instance, "resourceHeaderLayout") is not { } header || info == null)
            return;
        bool vertical = IsEnum(Get(Get(info, "MonthView"), "NavigationDirection"), "Vertical");
        double viewHeaderHeight = vertical ? 0.0 : ViewHeaderHeight(info, "MonthView");
        double resourceHeight = vertical ? 0.0 : ResourceTotalHeight(info);
        var state = HeaderStateOf(header);
        state.ViewHeaderHeight = viewHeaderHeight;
        state.ResourceHeight = resourceHeight;
        state.LayoutHeight = viewHeaderHeight + resourceHeight;
    });

    // INotifySchedulerLayout.GetAutoScrollPosition: past the left or right edge of the visible months
    // the month resource view scrolls horizontally.
    private static void MonthAutoScrollPosition_Postfix(object __instance, object? appViewInfo, Point dragStartPoint, Point draggingPoint, ref object __result)
    {
        if (Convert.ToInt32(__result) != 0 || appViewInfo == null || draggingPoint == Point.Zero || InfoOf(__instance) == null)
            return;
        const double margin = 10.0;
        var rect = Get<Rect>(appViewInfo, "AppointmentViewRect");
        if (draggingPoint.X - (dragStartPoint.X - rect.Left) <= -margin)
            __result = Enum.ToObject(__result.GetType(), 1); // Left
        else if (Get(__instance, "monthResourceScrollView") is ScrollView scroll && draggingPoint.X + (rect.Right - dragStartPoint.X) >= scroll.Frame.Width + margin)
            __result = Enum.ToObject(__result.GetType(), 3); // Right
    }

    private static void MonthScrollOnDrag_Postfix(object __instance, int autoScrollPosition) => Guard("drag scroll", () =>
    {
        if (autoScrollPosition is not (1 or 3) || Get(Get(InfoOf(__instance), "DragDropSettings"), "AllowScroll") is not true
            || Get(__instance, "monthResourceScrollView") is not ScrollView scroll)
            return;
        scroll.ScrollToAsync(scroll.ScrollX + (autoScrollPosition == 1 ? -10.0 : 10.0), scroll.ScrollY, false);
    });

    // SchedulerVerticalStackLayout.MeasureContent: the vertical month's resource header spans the
    // months (the width less the week numbers).
    private static void StackMeasure_Prefix(object __instance, double widthConstraint) => Guard("vertical month resources", () =>
    {
        var info = InfoOf(__instance);
        if (info == null || CallStatic(ViewHelper!, "IsSchedulerResourceVerticalMonthView", info) is not true)
            return;
        double width = double.IsFinite(widthConstraint) ? widthConstraint : 0.0;
        width -= (float)CallStatic(ViewHelper!, "GetMonthWeekNumberWidth", Is(info, "ShowWeekNumber"))!;
        var controlType = T("ResourceHeaderControl");
        foreach (var child in ((IEnumerable)Get(__instance, "Children")!))
        {
            if (controlType != null && controlType.IsInstanceOfType(child) && Get(child, "resourceHeaderLayout") is Layout header)
                UpdateViewPortWidth(header, width);
        }
    });

    // ResourceHeaderControl.InvalidateLayoutMeasure: the scroll view is measured again even before
    // the control has a size.
    private static void ResourceHeaderControlInvalidate_Postfix(object __instance) => Guard("resource header", () =>
    {
        var view = (VisualElement)__instance;
        if (view.Width > 0.0 && view.Height > 0.0)
            return;
        if (Get(__instance, "resourceScrollView") is ScrollView scroll)
        {
            ((IView)scroll).InvalidateMeasure();
            ((IView?)scroll.Content)?.InvalidateMeasure();
        }
    });

    // ---- Snap layout -------------------------------------------------------------------------------------

    private static IEnumerable<(int Index, object Child)> SnapChildren(object snap) =>
        ((Layout)snap).Children.Select((child, index) => (index, (object)child));

    private static bool IsDayViewControl(object child) => s_dayViewControl!.IsInstanceOfType(child);

    private static void SnapViewChange_Postfix(object __instance) => Guard("view change", () =>
    {
        var children = ((Layout)__instance).Children;
        var info = Get(__instance, "schedulerInfo");
        if (children.Count > 0 && IsDayViewControl(children[0]) && IsDesktop(info)
            && CallStatic(ViewHelper!, "IsVerticalTimeSlotView", Get(info, "View")) is true)
            InvalidateResourceHeaderLayout(__instance);
    });

    /// <summary>CustomSnapLayout.InvalidateResourceHeaderLayout of the desktop builds.</summary>
    private static void InvalidateResourceHeaderLayout(object snap)
    {
        var children = ((Layout)snap).Children;
        for (int i = 0; i < 3 && i < children.Count; i++)
        {
            if (IsDayViewControl(children[i]))
                Call(HeaderOf(children[i]), "InvalidateResourceHeaderLayout");
        }
    }

    private static void SnapResourceGroupType_Postfix(object __instance) => Guard("resource group type", () =>
    {
        foreach (var (_, child) in SnapChildren(__instance))
        {
            if (IsDayViewControl(child) && HeaderOf(child) != null)
                Call(child, "UpdateResourcegroupType");
        }
    });

    private static void SnapResourceInvalidateDraw_Postfix(object __instance) => Guard("resource header drawing", () =>
    {
        foreach (var (_, child) in SnapChildren(__instance))
        {
            if (IsDayViewControl(child))
                Call(HeaderOf(child), "InvalidateDrawView");
        }
    });

    // RemoveHorizontalResourceViewLayout: back to the layout without resources side by side.
    private static void SnapRemoveResourceLayout_Prefix(object __instance) => Guard("removing the resource view", () =>
    {
        int current = Get<int>(__instance, "CurrentChildIndex");
        foreach (var (index, child) in SnapChildren(__instance).ToList())
        {
            if (child.GetType().Name == "MonthViewLayout")
                Call(child, "UpdateHorizontalResourceViewLayout");
            else if (IsDayViewControl(child) && HeaderOf(child) != null)
                RemoveHorizontalResourceViewHeaderLayout(child, index == current);
        }
    });

    // AddHorizontalResourceViewHeaderLayout: resources were added, or the visible resource count changed.
    private static void SnapAddResourceLayout_Prefix(object __instance) => Guard("adding the resource view", () =>
    {
        int current = Get<int>(__instance, "CurrentChildIndex");
        foreach (var (index, child) in SnapChildren(__instance).ToList())
        {
            if (IsDayViewControl(child))
                AddHorizontalResourceViewHeaderLayout(child, index == current);
            else if (child.GetType().Name == "MonthViewLayout")
                Call(child, "UpdateHorizontalResourceViewLayout");
            Call(child, NotifyLayout + "InvalidateDrawView", (string?)null);
        }
    });

    private static void SnapInvalidateLayoutMeasure_Postfix(object __instance, bool isCollectionChanged) => Guard("resource layout", () =>
    {
        foreach (var (_, child) in SnapChildren(__instance))
        {
            if (IsDayViewControl(child))
            {
                if (HeaderOf(child) != null)
                    InvalidateDayLayoutMeasure(child, isCollectionChanged);
            }
            else if (child.GetType().Name == "MonthViewLayout")
            {
                InvalidateMonthLayoutMeasure(child, isCollectionChanged);
            }
        }
    });

    // ---- Scheduler -------------------------------------------------------------------------------------

    // IResourceViewInfo.UpdateResourceViewSettingsChanged: resources set after the views were built
    // add the resource layout (or, in a timeline view, the resource header).
    private static void SchedulerResourceSettings_Prefix(object __instance) => Guard("resource settings", () =>
    {
        if (Get(__instance, "resourceHeaderControl") != null)
            return;
        var view = Get(__instance, "View");
        if (CallStatic(ViewHelper!, "IsTimelineView", view) is true)
        {
            if (s_schedulerResources!(__instance) is { Count: > 0 } && s_visibleResourceCount!(__instance) != 0)
                Call(__instance, "AddTimelineResourceHeaderLayout");
        }
        else if (IsDesktop(__instance) && !IsMonth(__instance))
        {
            Call(Get(__instance, "customScrollLayout"), "AddHorizontalResourceViewHeaderLayout");
        }
    });

    // IScheduler.UpdateVisibleDates: the vertical month's resource header follows the visible dates.
    private static void SchedulerVisibleDates_Postfix(object __instance) => Guard("visible dates", () =>
    {
        if (Get(__instance, "resourceHeaderControl") is not { } control || Get(__instance, "customScrollLayout") is not { } snap)
            return;
        var visibleDates = Get<List<DateTime>>(__instance, "visibleDates");
        if (visibleDates == null || Get(control, "resourceHeaderLayout") is not { } header)
            return;
        var state = HeaderStateOf(header);
        state.VisibleDates = visibleDates;
        state.DisabledDates = Call(snap, "GetMonthDisabledDates", visibleDates) as List<DateTime>;
    });
}
