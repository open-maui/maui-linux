// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using Syncfusion.Maui.Core.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;
using SfPointerEventArgs = Syncfusion.Maui.Core.Internals.PointerEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfScheduler's mouse interaction, as its Windows build has it; the platform-neutral build
/// Linux apps get carries the touch path only. Each part ports the Windows code:
/// <list type="bullet">
/// <item><b>Mouse hold.</b> WinUI raises no hold (long press) for a mouse, so the Windows build
/// times one itself: a press held for 300 ms on a day, week, month, timeline, all-day, agenda
/// or header view raises <c>LongPressed</c> (and opens the context menu) and, over an
/// appointment, starts dragging it; a tap or right-tap that ends such a hold is not raised.
/// The views get the same timer (<c>StartLongPressTimer</c>, <c>LongPressTimer_Tick</c>), and
/// the gesture detector's own long press, which OpenMaui's pointer bridge raises for the mouse,
/// is not passed to them, as WinUI does not raise it. The drag then follows the pointer through
/// the views' touch handling, which the Windows build feeds from WinUI drag-and-drop events.</item>
/// <item><b>Resizing with the mouse.</b> Over the top or bottom edge of an appointment in the
/// day, week and work-week views (the left or right edge in the all-day panel and the month
/// view), the resize cursor shows and a drag resizes it (<c>HandleAppointmentResizing</c> and
/// the appointment views' <c>OnAppointmentResizingTouch</c>, absent from the neutral build;
/// the timeline views have them). <c>AppointmentResizingHelper.SetCursorFor</c>, empty in the
/// neutral build, shows the cursor.</item>
/// <item><b>Appointment tool tips</b> on hover in the day, all-day, month and timeline views
/// (<c>HandleAppointmentToolTip</c>), hidden on press and release.</item>
/// <item><b>Header right-click.</b> The header's right-tap (the date range or week number,
/// raising <c>RightTapped</c> and the context menu) and touch handlers, and the vertical month
/// header's right-tap, which the neutral build leaves empty.</item>
/// </list>
/// The scheduler is optional, so its types are looked up by name.
/// </summary>
internal static class SfSchedulerPatches
{
    private const string Asm = "Syncfusion.Maui.Scheduler";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>The Windows build's mouse-hold time (StartLongPressTimer's 300 ms interval).</summary>
    internal static readonly TimeSpan HoldDelay = TimeSpan.FromMilliseconds(300);

    private static int s_installed;
    private static Type? s_viewsHelper, s_viewHelper, s_resizingHelper, s_selectionHelper, s_resourceHelper;

    /// <summary>Per view: the mouse-hold timer and the resize edge under the pointer.</summary>
    private sealed class State
    {
        public IDispatcherTimer? Timer;
        public Point Point;
        public bool Holding;
        public object? ResizeMode; // AppointmentResizeEdge?
        public bool Running;
    }

    private static readonly ConditionalWeakTable<object, State> s_states = new();

    // The views whose Windows build times a mouse hold, and what the hold does there.
    private static readonly Dictionary<string, Action<object, State>> s_holdActions = new()
    {
        ["DayViewLayout"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false, true);
            if (!IsResizing(ViewInfo(view)))
                Call(Get(view, "dayAppointmentsView"), "EnableAppointmentDragging");
        },
        ["TimelineViewLayout"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false, true);
            if (!IsResizing(ViewInfo(view)))
                Call(Get(view, "timelineAppointmentView"), "EnableAppointmentDragging");
        },
        ["MonthViewLayout"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false, false, true);
            Call(Get(view, "appointmentsView"), "EnableAppointmentDragging");
        },
        ["AllDayAppointmentsLayout"] = (view, state) =>
        {
            var info = ViewInfo(view);
            if (!IsResizing(info))
                Call(Get(view, "appointmentsView"), "EnableAppointmentDragging");
            var resource = s_resourceHelper == null ? null
                : CallStatic(s_resourceHelper, "GetSelectedHorizontalResourceView", info, state.Point.X, ((VisualElement)view).DesiredSize.Width);
            Call(view, "OnTapGestureAction", state.Point, false, 0, Is(info, "IsRTLLayout"), resource, false, true);
        },
        ["DayViewControl"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false, false);
        },
        ["ResourceHeaderLayout"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false, false);
        },
        ["TimelineHeaderLayout"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false);
        },
        ["AgendaWeekViewLayout"] = (view, state) =>
        {
            state.Holding = true;
            Call(view, "OnInteractionEvent", state.Point, false, 1, false, true);
        },
        ["HeaderLayout"] = (view, state) =>
        {
            state.Holding = true;
            var border = Get(view, "weekNumberBorder") as VisualElement;
            double weekWidth = border?.Width ?? 0, weekHeight = border?.Height ?? 0;
            var totalWidth = (double)Call(view, "CalculateTotalWidth")!;
            var headerInfo = Get(view, "headerInfo");
            if (Call(view, "CanShowWeekNumber") is true && Get<double>(Get(headerInfo, "HeaderView"), "Height") > 0
                && Call(view, "IsTapWithinWeekNumber", state.Point.X, state.Point.Y, totalWidth, weekWidth, weekHeight) is true)
            {
                var dateTimeHelper = Type("Syncfusion.Maui.Scheduler.SchedulerDateTimeHelper", Asm)!;
                var weekStart = CallStatic(dateTimeHelper, "GetWeekStartDate", Get(headerInfo, "VisibleDates"));
                var weekNumber = (int)CallStatic(s_viewHelper!, "GetWeekNumber", Get(headerInfo, "CalendarType"), weekStart, Get(headerInfo, "FirstDayOfWeek"))!;
                Call(view, "OnInteractionEvent", true, true, 0, weekNumber, false);
            }
            else
            {
                Call(view, "OnInteractionEvent", true, false, 0, -1, false);
            }
        },
    };

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var scheduler = Type("Syncfusion.Maui.Scheduler.SfScheduler", Asm);
        if (scheduler == null)
            return; // SfScheduler is not part of the app
        s_viewsHelper = Type("Syncfusion.Maui.Scheduler.AppointmentsViewHelper", Asm);
        s_viewHelper = Type("Syncfusion.Maui.Scheduler.SchedulerViewHelper", Asm);
        s_resizingHelper = Type("Syncfusion.Maui.Scheduler.AppointmentResizingHelper", Asm);
        s_selectionHelper = Type("Syncfusion.Maui.Scheduler.SelectionHelper", Asm);
        s_resourceHelper = Type("Syncfusion.Maui.Scheduler.ResourceViewHelper", Asm);
        var harmony = new Harmony("com.openmaui.syncfusion.scheduler");

        // The neutral build has no long-press timer at all: a Windows build keeps it.
        var dayLayout = Type("Syncfusion.Maui.Scheduler.DayViewLayout", Asm);
        if (dayLayout == null || HasMethod(dayLayout, "StartLongPressTimer"))
            return;

        Patch(harmony, s_resizingHelper, "SetCursorFor", nameof(SetCursorFor_Prefix), when: m => Il(m).Length <= 2);

        foreach (var name in s_holdActions.Keys)
        {
            var type = Type("Syncfusion.Maui.Scheduler." + name, Asm);
            if (type == null)
                continue;
            // Taps and right-taps that end a hold are not raised; a double tap ends the hold;
            // the detector's long press is not WinUI's (no hold for a mouse).
            Patch(harmony, type, "Syncfusion.Maui.Core.Internals.ITapGestureListener.OnTap", nameof(OnTap_Prefix));
            Patch(harmony, type, "Syncfusion.Maui.Core.Internals.IRightTapGestureListener.OnRightTap", nameof(OnRightTap_Prefix));
            Patch(harmony, type, "Syncfusion.Maui.Core.Internals.IDoubleTapGestureListener.OnDoubleTap", nameof(OnDoubleTap_Prefix));
            Patch(harmony, type, "Syncfusion.Maui.Core.Internals.ILongPressGestureListener.OnLongPress", nameof(OnLongPress_Prefix));
        }

        // Day, week and work week: resizing, tool tip and hold, around TouchInteraction.
        Patch(harmony, dayLayout, "TouchInteraction", nameof(DayTouch_Prefix));
        Patch(harmony, dayLayout, "TouchInteraction", nameof(DayTouch_Postfix), postfix: true);
        var allDay = Type("Syncfusion.Maui.Scheduler.AllDayAppointmentsLayout", Asm);
        Patch(harmony, allDay, "TouchInteraction", nameof(AllDayTouch_Prefix));
        Patch(harmony, allDay, "TouchInteraction", nameof(AllDayTouch_Postfix), postfix: true);
        var month = Type("Syncfusion.Maui.Scheduler.MonthViewLayout", Asm);
        Patch(harmony, month, "TouchInteraction", nameof(MonthTouch_Prefix));
        Patch(harmony, month, "TouchInteraction", nameof(MonthTouch_Postfix), postfix: true);
        var timeline = Type("Syncfusion.Maui.Scheduler.TimelineViewLayout", Asm);
        Patch(harmony, timeline, "TouchInteraction", nameof(TimelineTouch_Postfix), postfix: true);

        // An all-day resize maps the pointer to a date across the all-day panel's width (the
        // neutral build passes a zero width there, which indexes past the visible dates).
        Patch(harmony, Type("Syncfusion.Maui.Scheduler.DayViewControl", Asm), "Syncfusion.Maui.Scheduler.INotifySchedulerLayout.GetResizePositionToDateTime",
            nameof(GetResizePositionToDateTime_Prefix), when: m => !Calls(m, "IsHorizontalResourceViewDesktop"));

        // Views whose Windows build times the hold in OnTouch.
        foreach (var name in new[] { "DayViewControl", "ResourceHeaderLayout", "TimelineHeaderLayout", "AgendaWeekViewLayout" })
            Patch(harmony, Type("Syncfusion.Maui.Scheduler." + name, Asm), "Syncfusion.Maui.Core.Internals.ITouchListener.OnTouch", nameof(OnTouch_Postfix), postfix: true);

        // The time indicator that follows a timeline resize (the header shows the resized time).
        var snap = Type("Syncfusion.Maui.Scheduler.CustomSnapLayout", Asm);
        Patch(harmony, snap, "AddAppointmentResizingView", nameof(AddAppointmentResizingView_Postfix), postfix: true,
            when: m => !Calls(m, "AddAppointmentResizeIndicatorView"));
        Patch(harmony, snap, "RemoveAppointmentResizeIndicatorView", nameof(RemoveAppointmentResizeIndicatorView_Prefix),
            when: m => !Calls(m, "RemoveAppointmentResizeIndicatorView"));
        // A layout update re-measures the header too (HeaderLayout.InvalidateLayout).
        Patch(harmony, scheduler, "UpdateLayout", nameof(UpdateLayout_Postfix), postfix: true, when: m => !Calls(m, "InvalidateLayout"));

        // Header: right-tap and touch are empty in the neutral build.
        var header = Type("Syncfusion.Maui.Scheduler.HeaderLayout", Asm);
        Patch(harmony, header, "Syncfusion.Maui.Core.Internals.IRightTapGestureListener.OnRightTap", nameof(HeaderRightTap_Prefix),
            when: m => Il(m).Length <= 2, order: Priority.First);
        Patch(harmony, header, "Syncfusion.Maui.Core.Internals.ITouchListener.OnTouch", nameof(OnTouch_Postfix), postfix: true,
            when: m => Il(m).Length <= 2);
        Patch(harmony, Type("Syncfusion.Maui.Scheduler.VerticalMonthViewHeader", Asm),
            "Syncfusion.Maui.Core.Internals.IRightTapGestureListener.OnRightTap", nameof(VerticalMonthHeaderRightTap_Prefix), when: m => Il(m).Length <= 2);
    }

    private static void Patch(Harmony harmony, Type? type, string method, string patch, bool postfix = false,
        Func<MethodInfo, bool>? when = null, int order = Priority.Normal)
    {
        if (type == null)
            return;
        foreach (var target in type.GetMethods(Any).Where(m => m.Name == method && !m.IsAbstract))
        {
            if (when != null && !when(target))
                continue;
            try
            {
                var hm = new HarmonyMethod(typeof(SfSchedulerPatches).GetMethod(patch, BindingFlags.Static | BindingFlags.NonPublic)) { priority = order };
                if (postfix)
                    harmony.Patch(target, postfix: hm);
                else
                    harmony.Patch(target, prefix: hm);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Syncfusion", $"Patching SfScheduler {type.Name}.{method} failed", ex);
            }
        }
    }

    private static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"SfScheduler {what} failed", ex);
        }
    }

    private static State StateOf(object view) => s_states.GetValue(view, _ => new State());

    private static object? ViewInfo(object view) => Get(view, "schedulerViewInfo") ?? Get(view, "SchedulerViewInfo");

    private static bool IsResizing(object? info) =>
        s_viewsHelper != null && CallStatic(s_viewsHelper, "IsAppointmentResizing", Get(info, "AppointmentResizingController")) is true;

    private static bool IsDragging(object? info) =>
        s_viewsHelper != null && CallStatic(s_viewsHelper, "IsAppointmentDragging", Get(info, "DragAndDropController")) is true;

    // ---- Mouse hold ---------------------------------------------------------------------------

    /// <summary>StartLongPressTimer: a one-shot 300 ms dispatcher timer, as on Windows.</summary>
    private static void StartHold(object view, Point point, bool resetHolding = true)
    {
        var state = StateOf(view);
        state.Point = point;
        if (resetHolding)
            state.Holding = false;
        if (state.Timer == null && view is BindableObject bindable && bindable.Dispatcher is { } dispatcher)
        {
            var timer = dispatcher.CreateTimer();
            timer.Interval = HoldDelay;
            timer.IsRepeating = false;
            var weak = new WeakReference(view);
            timer.Tick += (_, _) =>
            {
                if (weak.Target is { } target)
                    FireHold(target);
            };
            state.Timer = timer;
        }
        state.Running = true;
        state.Timer?.Stop();
        state.Timer?.Start();
    }

    private static void StopHold(object view)
    {
        if (!s_states.TryGetValue(view, out var state))
            return;
        state.Running = false;
        state.Timer?.Stop();
    }

    /// <summary>LongPressTimer_Tick of the view's Windows build.</summary>
    internal static void FireHold(object view)
    {
        if (!s_states.TryGetValue(view, out var state) || !state.Running)
            return;
        state.Running = false;
        state.Timer?.Stop();
        if (s_holdActions.TryGetValue(view.GetType().Name, out var action))
            Guard("mouse hold", () => action(view, state));
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    /// <summary>Fires every running hold timer (tests: the compat host's timers never tick).</summary>
    internal static int FireHolds()
    {
        int fired = 0;
        foreach (var (view, state) in s_states)
        {
            if (state.Running)
            {
                FireHold(view);
                fired++;
            }
        }
        return fired;
    }

    /// <summary>The Windows hold bookkeeping of a pointer event: press starts, release stops.</summary>
    private static void TrackHold(object view, SfPointerEventArgs e, bool stopOnMove, bool resetHolding = true)
    {
        if (e.PointerDeviceType != PointerDeviceType.Mouse)
            return;
        switch (e.Action)
        {
            case PointerActions.Pressed:
                StartHold(view, e.TouchPoint, resetHolding);
                break;
            case PointerActions.Moved when stopOnMove && !IsDragging(ViewInfo(view)):
            case PointerActions.Released:
            case PointerActions.Cancelled:
                StopHold(view);
                break;
        }
    }

    private static bool OnTap_Prefix(object __instance) => !(s_states.TryGetValue(__instance, out var s) && s.Holding);

    private static bool OnRightTap_Prefix(object __instance) => !(s_states.TryGetValue(__instance, out var s) && s.Holding);

    private static void OnDoubleTap_Prefix(object __instance) => StopHold(__instance);

    private static bool OnLongPress_Prefix() => false;

    // ITouchListener.OnTouch of DayViewControl, the header layouts and the agenda week view.
    private static void OnTouch_Postfix(object __instance, SfPointerEventArgs e) => Guard("pointer", () =>
    {
        if (__instance.GetType().Name == "DayViewControl" && !InDayViewHeader(__instance, e))
            return;
        TrackHold(__instance, e, stopOnMove: false);
    });

    // DayViewControl times the hold over its view header only (where it shows the hover).
    private static bool InDayViewHeader(object control, SfPointerEventArgs e)
    {
        var info = ViewInfo(control);
        if (s_selectionHelper == null || s_viewHelper == null || Get(control, "hoverView") == null)
            return false;
        Func<bool> canHighlight = () => Call(info, "CanHighlightView") is true;
        if (CallStatic(s_selectionHelper, "IsViewHeaderMouseHover", canHighlight, Get(info, "AllowViewNavigation"), Get(info, "AllowedViews"), Get(info, "View")) is not true)
            return false;
        var daysView = Get(info, "DaysView");
        bool single = CallStatic(s_viewHelper, "IsSingleNumberOfDay", Get(info, "View"), Get(daysView, "NumberOfVisibleDays")) is true;
        double headerHeight = Convert.ToDouble(CallStatic(s_viewHelper, "GetViewHeaderHeight", Get(info, "View"), Get(Get(daysView, "ViewHeaderSettings"), "Height")));
        double allDayHeight = Get<double>(control, "allDayLayoutHeight");
        double limit = single && headerHeight < allDayHeight ? allDayHeight : headerHeight;
        return e.TouchPoint.Y <= limit;
    }

    // ---- Day, week and work week ---------------------------------------------------------------

    [ThreadStatic] private static object? t_dayAppointment;

    // DayViewLayout.TouchInteraction: before the drag handling, the mouse resize (Windows order).
    private static void DayTouch_Prefix(object __instance, SfPointerEventArgs e)
    {
        t_dayAppointment = null;
        Guard("resize", () =>
        {
            var info = ViewInfo(__instance)!;
            var view = Get(__instance, "dayAppointmentsView");
            if (view == null)
                return;
            double ruler = s_viewHelper != null && CallStatic(s_viewHelper, "IsHorizontalResourceViewDesktop", info) is true
                ? 0.0 : Get<double>(Get(info, "DaysView"), "TimeRulerWidth");
            object? resource = null;
            if (s_viewHelper != null && CallStatic(s_viewHelper, "IsHorizontalResourceViewMobile", info) is true
                && Get(info, "SchedulerResources") is System.Collections.IEnumerable resources)
            {
                var selectedId = Get(Get(info, "ResourceView"), "SelectedResourceId");
                foreach (var r in resources)
                {
                    if (Equals(Get(r, "Id"), selectedId))
                    {
                        resource = r;
                        break;
                    }
                }
            }
            var point = new Point(e.TouchPoint.X - (Is(info, "IsRTLLayout") ? 0.0 : ruler), e.TouchPoint.Y);
            var args = new object?[] { point, null, -1, resource };
            var rects = FindMethod(view.GetType(), "GetSelectedAppointmentViewRect", typeof(Point), Type("Syncfusion.Maui.Scheduler.SchedulerAppointment", Asm)!.MakeByRefType(), typeof(int), Type("Syncfusion.Maui.Scheduler.SchedulerResource", Asm)!)?.Invoke(view, args);
            t_dayAppointment = args[1];
            HandleResizing(__instance, e, rects, info, view, "AnyTopOrBottomHit", (s, ev) =>
                DayResizingTouch(view, info, ev.TouchPoint, s, ruler));
        });
    }

    // DayAppointmentsView.OnAppointmentResizingTouch(Point parentPos, GestureStatus status).
    private static void DayResizingTouch(object view, object info, Point parentPos, GestureStatus status, double ruler)
    {
        var controller = Get(info, "AppointmentResizingController");
        switch (status)
        {
            case GestureStatus.Started:
            {
                double x = parentPos.X - (Is(info, "IsRTLLayout") ? 0.0 : ruler);
                if (controller != null && SfSchedulerResourceView.IsDesktop(info) && s_resourceHelper != null)
                    Set(controller, "OriginalResizeStartResource", CallStatic(s_resourceHelper, "GetSelectedHorizontalResourceView", info, x, ((VisualElement)view).Width));
                var appointmentInfo = Call(view, "GetAppointmentViewInfo", new Point(x, parentPos.Y));
                Call(controller, "ProcessOnResizeEnter", appointmentInfo, parentPos, false);
                break;
            }
            case GestureStatus.Running:
                Call(controller, "ProcessOnResizing", parentPos, false, null, null);
                break;
            case GestureStatus.Completed:
                Call(controller, "ProcessOnResizeDone", false);
                Call(view, "InvalidateDrawable");
                break;
        }
    }

    // DayViewLayout.TouchInteraction, after: the appointment tool tip and the mouse hold.
    private static void DayTouch_Postfix(object __instance, SfPointerEventArgs e)
    {
        var appointment = t_dayAppointment;
        Guard("tool tip", () => ToolTip(__instance, e, () => Call(__instance, "HandleAppointmentToolTip", e.TouchPoint, appointment)));
        TrackHold(__instance, e, stopOnMove: true);
    }

    private static void ToolTip(object layout, SfPointerEventArgs e, Action show)
    {
        if (e.PointerDeviceType != PointerDeviceType.Mouse)
            return;
        show();
        if (e.Action is PointerActions.Released or PointerActions.Cancelled or PointerActions.Exited)
            Call(ViewInfo(layout), "HideAppointmentTooltip");
    }

    /// <summary>
    /// HandleAppointmentResizing of the Windows build (day, all-day and month layouts): the edge
    /// under the pointer, the press that starts a resize, the release that ends it, and the cursor.
    /// </summary>
    private static void HandleResizing(object layout, SfPointerEventArgs e, object? rects, object info, object? appointmentsView,
        string hitTest, Action<GestureStatus, SfPointerEventArgs> operation, Point? hitPoint = null, object? spanInfo = null)
    {
        var controller = Get(info, "AppointmentResizingController");
        if (controller == null || e.PointerDeviceType != PointerDeviceType.Mouse || s_resizingHelper == null)
            return;
        var state = StateOf(layout);
        var point = hitPoint ?? e.TouchPoint;
        void Operate()
        {
            switch (e.Action)
            {
                case PointerActions.Pressed: operation(GestureStatus.Started, e); break;
                case PointerActions.Moved: operation(GestureStatus.Running, e); break;
                case PointerActions.Released:
                case PointerActions.Cancelled:
                case PointerActions.Exited: operation(GestureStatus.Completed, e); break;
            }
        }
        if (e.Action is PointerActions.Entered or PointerActions.Moved)
        {
            state.ResizeMode = hitTest == "AnyTopOrBottomHit"
                ? CallStatic(s_resizingHelper, hitTest, rects, (float)point.Y, 3f)
                : CallStatic(s_resizingHelper, hitTest, rects, (float)point.X, (float)(hitPoint != null ? point.Y : 0.0), spanInfo ?? NoSpan());
            if (Is(controller, "IsAppointmentResizing"))
                Set(controller, "IsAppointmentResizingInProgress", true);
            Operate();
        }
        else if (e.Action == PointerActions.Pressed && state.ResizeMode != null)
        {
            Set(controller, "AppointmentResizeMode", state.ResizeMode);
            Call(appointmentsView, "UpdateAppointmentResizingState", true);
            Operate();
        }
        else if (e.Action is PointerActions.Released or PointerActions.Exited or PointerActions.Cancelled)
        {
            Operate();
            state.ResizeMode = null;
            Set(controller, "AppointmentResizeMode", null);
            Call(appointmentsView, "UpdateAppointmentResizingState", false);
            Set(controller, "IsAppointmentResizingInProgress", false);
        }
        if (!IsResizing(info))
            CallStatic(s_resizingHelper, "SetCursorFor", (layout as VisualElement)?.Handler?.PlatformView, state.ResizeMode);
    }

    private static object NoSpan()
    {
        var infoType = Type("Syncfusion.Maui.Scheduler.AppointmentViewInfo", Asm)!;
        return Activator.CreateInstance(typeof(ValueTuple<,>).MakeGenericType(infoType, infoType), null, null)!;
    }

    // ---- All-day panel -------------------------------------------------------------------------

    // AllDayAppointmentsLayout.TouchInteraction: the mouse resize (multi-day views only).
    private static void AllDayTouch_Prefix(object __instance, SfPointerEventArgs e) => Guard("all-day resize", () =>
    {
        var info = ViewInfo(__instance)!;
        var view = Get(__instance, "appointmentsView");
        var appointmentInfo = Call(view, "GetAppointmentViewInfo", e.TouchPoint);
        object? rects = null;
        if (appointmentInfo != null)
            rects = new List<Rect> { Get<Rect>(appointmentInfo, "AppointmentViewRect") };
        if (s_viewHelper == null || CallStatic(s_viewHelper, "IsSingleNumberOfDay", Get(info, "View"), Get(Get(info, "DaysView"), "NumberOfVisibleDays")) is true)
            return;
        // Side by side resources resize in the all-day panel only when grouped by resource.
        if (SfSchedulerResourceView.IsDesktop(info) && CallStatic(s_viewHelper, "IsResourceType", info) is not true)
            return;
        HandleResizing(__instance, e, rects, info, view, "AnyLeftOrRightHit", (status, ev) =>
            AllDayResizingTouch(view!, info, ev.TouchPoint, status));
    });

    // AllDayAppointmentsView.OnAppointmentResizingTouch(Point parentPos, GestureStatus status): with
    // resources side by side, the resource the resize starts in and the one under the pointer.
    private static void AllDayResizingTouch(object view, object info, Point parentPos, GestureStatus status)
    {
        var controller = Get(info, "AppointmentResizingController");
        bool desktop = SfSchedulerResourceView.IsDesktop(info) && s_resourceHelper != null;
        double width = ((VisualElement)view).Width;
        switch (status)
        {
            case GestureStatus.Started:
                Call(controller, "ProcessOnResizeEnter", Call(view, "GetAppointmentViewInfo", parentPos), parentPos, true);
                if (desktop && controller != null)
                {
                    Set(controller, "OriginalResizeStartResource", CallStatic(s_resourceHelper!, "GetSelectedHorizontalResourceView", info, parentPos.X, width));
                    Set(controller, "ViewWidth", width);
                }
                break;
            case GestureStatus.Running:
                var resource = desktop ? CallStatic(s_resourceHelper!, "GetSelectedHorizontalResourceView", info, parentPos.X, width) : null;
                Call(controller, "ProcessOnResizing", parentPos, true, resource, null);
                break;
            case GestureStatus.Completed:
                Call(controller, "ProcessOnResizeDone", true);
                Call(view, "InvalidateDrawable");
                break;
        }
    }

    // AllDayAppointmentsLayout.TouchInteraction, after: tool tip (HandleAppointmentToolTip) and hold.
    private static void AllDayTouch_Postfix(object __instance, SfPointerEventArgs e)
    {
        Guard("all-day tool tip", () => ToolTip(__instance, e, () =>
        {
            var info = ViewInfo(__instance);
            var appointmentInfo = Call(Get(__instance, "appointmentsView"), "GetAppointmentViewInfo", e.TouchPoint);
            if (appointmentInfo != null)
            {
                bool timeline = s_viewHelper != null && CallStatic(s_viewHelper, "IsTimelineView", Get(info, "View")) is true;
                var position = CallStatic(s_viewsHelper!, "GetContainerPoints", e.TouchPoint, (__instance as VisualElement)?.Handler?.PlatformView,
                    Get(info, "PlatformView"), timeline, Is(info, "IsVirtualizationNeeded"));
                Call(info, "ShowAppointmentTooltip", position, Get(appointmentInfo, "Appointment"));
            }
            else
            {
                Call(info, "HideAppointmentTooltip");
            }
        }));
        TrackHold(__instance, e, stopOnMove: true, resetHolding: false);
    }

    // DayViewControl.INotifySchedulerLayout.GetResizePositionToDateTime(Point resizingPoint), for a
    // left or right edge (the all-day panel), as the Windows build has it.
    private static bool GetResizePositionToDateTime_Prefix(object __instance, Point resizingPoint, ref DateTime? __result)
    {
        var info = ViewInfo(__instance);
        var mode = Get(Get(info, "AppointmentResizingController"), "AppointmentResizeMode")?.ToString();
        if (mode is not ("Left" or "Right") || s_selectionHelper == null)
            return true;
        try
        {
            double width = SfSchedulerResourceView.AllDayResizeWidth(__instance);
            __result = CallStatic(s_selectionHelper, "GetDaysViewHoverDate", resizingPoint, Get(__instance, "VisibleDates"), info, width) as DateTime?;
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfScheduler all-day resize position failed", ex);
            return true;
        }
    }

    // ---- Month ---------------------------------------------------------------------------------

    [ThreadStatic] private static object? t_monthAppointment;

    // MonthViewLayout.TouchInteraction: the mouse resize, unless the inline appointment view shows.
    private static void MonthTouch_Prefix(object __instance, SfPointerEventArgs e)
    {
        t_monthAppointment = null;
        Guard("month resize", () =>
        {
            var info = ViewInfo(__instance)!;
            var monthView = Get(info, "MonthView");
            bool vertical = IsEnum(Get(monthView, "NavigationDirection"), "Vertical");
            double header = vertical ? 0.0 : Convert.ToDouble(CallStatic(s_viewHelper!, "GetViewHeaderHeight", Get(info, "View"), Get(Get(monthView, "ViewHeaderSettings"), "Height")));
            float weekNumberWidth = (float)CallStatic(s_viewHelper!, "GetMonthWeekNumberWidth", Is(info, "ShowWeekNumber"))!;
            if (Get(__instance, "MonthInlineScrollView") is VisualElement { IsVisible: true } inline && inline.Bounds.Inflate(1, 1).Contains(e.TouchPoint))
                return;
            var view = Get(__instance, "appointmentsView");
            var point = new Point(e.TouchPoint.X - (Is(info, "IsRTLLayout") ? 0f : weekNumberWidth), e.TouchPoint.Y - header);
            var args = new object?[] { point, null };
            var rects = FindMethod(view!.GetType(), "GetSelectedAppointmentViewRect", typeof(Point), Type("Syncfusion.Maui.Scheduler.SchedulerAppointment", Asm)!.MakeByRefType())?.Invoke(view, args);
            t_monthAppointment = args[1];
            var inlineResult = Call(__instance, "CanShowAppointmentsInlineView");
            if (Get(inlineResult, "Item1") is true)
                return;
            var appointmentInfo = Call(view, "GetAppointmentViewInfo", point);
            object span = appointmentInfo != null ? Call(view, "GetMonthStartAndEndAppointmentViewInfo", appointmentInfo)! : NoSpan();
            HandleResizing(__instance, e, rects, info, view, "AnyLeftOrRightHit",
                (status, _) => Call(view, "OnAppointmentResizingTouch", point, status), hitPoint: point, spanInfo: span);
        });
    }

    // MonthViewLayout.TouchInteraction, after: tool tip and hold.
    private static void MonthTouch_Postfix(object __instance, SfPointerEventArgs e)
    {
        var appointment = t_monthAppointment;
        Guard("month tool tip", () => ToolTip(__instance, e, () => Call(__instance, "HandleAppointmentToolTip", e.TouchPoint, appointment)));
        TrackHold(__instance, e, stopOnMove: true);
    }

    // ---- Timeline ------------------------------------------------------------------------------

    // TimelineViewLayout.TouchInteraction, after: tool tip and hold (the resize is in the neutral build).
    private static void TimelineTouch_Postfix(object __instance, SfPointerEventArgs e)
    {
        Guard("timeline tool tip", () => ToolTip(__instance, e, () => Call(__instance, "HandleAppointmentToolTip", e.TouchPoint)));
        TrackHold(__instance, e, stopOnMove: true);
    }

    // ---- Cursor --------------------------------------------------------------------------------

    // AppointmentResizingHelper.SetCursorFor(object? platformView, AppointmentResizeEdge? appointmentResizeMode).
    private static bool SetCursorFor_Prefix(object? platformView, object? appointmentResizeMode)
    {
        if (platformView is SkiaView view)
        {
            view.CursorType = appointmentResizeMode?.ToString() switch
            {
                "Left" or "Right" => CursorType.SizeWestEast,
                "Top" or "Bottom" => CursorType.SizeNorthSouth,
                _ => CursorType.Arrow,
            };
        }
        return false;
    }

    // ---- Resize indicator and layout updates -------------------------------------------------------

    private static object? CurrentChild(object snapLayout) =>
        snapLayout is Layout { Children.Count: > 0 } layout && Get(snapLayout, "CurrentChildIndex") is int index && index >= 0 && index < layout.Children.Count
            ? layout.Children[index] : null;

    // CustomSnapLayout.AddAppointmentResizingView(..., AppointmentResizeIndicatorView? appointmentResizeIndicatatorView):
    // a timeline shows the resized time in its header (the day view's indicator goes in the time
    // ruler of the horizontal resource view, which the neutral build does not have).
    private static void AddAppointmentResizingView_Postfix(object __instance, object? __3) => Guard("resize indicator", () =>
    {
        if (__3 == null || CurrentChild(__instance) is not { } child)
            return;
        if (child.GetType().Name == "TimelineViewControl")
            Call(Get(child, "timelineHeaderLayout"), "AddAppointmentResizeIndicatorView", __3);
        else if (child.GetType().Name == "DayViewControl" && __3 is View indicator)
            SfSchedulerResourceView.AddAppointmentResizeIndicatorView(child, indicator);
    });

    // CustomSnapLayout.RemoveAppointmentResizeIndicatorView().
    private static bool RemoveAppointmentResizeIndicatorView_Prefix(object __instance)
    {
        Guard("resize indicator", () =>
        {
            if (CurrentChild(__instance) is not { } child)
                return;
            if (child.GetType().Name == "TimelineViewControl")
                Call(Get(child, "timelineHeaderLayout"), "RemoveAppointmentResizeIndicatorView");
            else if (child.GetType().Name == "DayViewControl")
                SfSchedulerResourceView.RemoveAppointmentResizeIndicatorView(child);
        });
        return false;
    }

    // SfScheduler.UpdateLayout: headerLayout?.InvalidateLayout() (measure and arrange the header again).
    private static void UpdateLayout_Postfix(object __instance)
    {
        if (Get(__instance, "headerLayout") is IView header)
        {
            header.InvalidateMeasure();
            header.InvalidateArrange();
        }
    }

    // ---- Headers -------------------------------------------------------------------------------

    // HeaderLayout.IRightTapGestureListener.OnRightTap(RightTapEventArgs e).
    private static bool HeaderRightTap_Prefix(object __instance, RightTapEventArgs e)
    {
        if (s_states.TryGetValue(__instance, out var state) && state.Holding)
            return false;
        Guard("header right-tap", () =>
        {
            var border = Get(__instance, "weekNumberBorder") as VisualElement;
            double weekWidth = border?.Bounds.Width ?? 0, weekHeight = border?.Bounds.Height ?? 0;
            var totalWidth = (double)Call(__instance, "CalculateTotalWidth")!;
            Call(__instance, "HandleRightTapEvent", e, totalWidth, weekWidth, weekHeight, true);
        });
        return false;
    }

    // VerticalMonthViewHeader.IRightTapGestureListener.OnRightTap(RightTapEventArgs e).
    private static bool VerticalMonthHeaderRightTap_Prefix(object __instance, RightTapEventArgs e)
    {
        Guard("month header right-tap", () => Call(__instance, "OnInteractionEvent", e.TapPoint, false, 0, true));
        return false;
    }
}
