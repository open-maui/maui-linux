// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;
using SfPointerEventArgs = Syncfusion.Maui.Core.Internals.PointerEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfScheduler's desktop horizontal resource view, as its Windows and Mac builds have it: with
/// resources, the day, week and work-week views show the resources side by side (each resource
/// a column group of the visible days, or each day a group of the resources, by
/// <c>ResourceGroupType</c>), under a resource header, beside a time ruler that stays in place
/// while the time slots scroll in both directions; the month view shows a month per resource.
/// The platform-neutral build Linux apps get answers <c>IsHorizontalResourceViewDesktop</c> with
/// false and lays resources out as the mobile builds do (timeline views only); it keeps the
/// drawing code of the desktop layout, but not the view tree that holds it. This bridge answers
/// the question as the desktop builds do and supplies that tree:
/// <list type="bullet">
/// <item>The day view control's resource header (a <c>ResourceHeaderLayout</c> holding the
/// resource header, the day header and the all-day panel, in a scroll view that follows the
/// time slots horizontally), its time ruler (<see cref="SfSchedulerTimeRulerView"/>, in a scroll
/// view that follows them vertically), the all-day expander
/// (<see cref="SfSchedulerAllDayExpanderView"/>) and the border between ruler and slots, with
/// the measure, arrange, scrolling and notifications that keep them in step
/// (<c>AddHorizontalResourceViewHeaderLayout</c> and the members around it).</item>
/// <item>The resource header layout's desktop arrangement, hover, taps and all-day panel.</item>
/// <item>The snap layout's and scheduler's notifications that add, remove and refresh the
/// resource layout when resources or their settings change, the month resource layout's
/// heights and visible dates, and the drag and resize details that depend on the resource
/// under the pointer.</item>
/// <item>Render virtualization, as the Windows build has it: when the resource columns are more
/// than three viewports wide (<c>IsVirtualizationNeeded</c>), the time slots, the resource
/// header, the day header, the all-day panel and the hover views are drawn in a window three
/// viewports wide that follows the horizontal scroll offset, instead of at the full width; the
/// resource, day header, all-day, time region, appointment and cell selection templates are then
/// laid out across the full width by the template views the Windows build adds
/// (SfSchedulerResourceView.Templates.cs and the header's).</item>
/// </list>
/// The scheduler is optional, so its types are looked up by name.
/// </summary>
internal static partial class SfSchedulerResourceView
{
    private const string Asm = "Syncfusion.Maui.Scheduler";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static int s_installed;

    /// <summary>SchedulerViewHelper, ResourceViewHelper, SelectionHelper, AppointmentsViewHelper, SfSchedulerResources.</summary>
    internal static Type? ViewHelper, ResourceHelper, SelectionHelper, AppointmentsHelper, Resources;

    private static Type? s_resourceHeaderLayout, s_dayHeaderView, s_resourceHeaderView, s_allDayLayout, s_scrollViewExt, s_schedulerGrid, s_dayViewControl;

    // IResourceViewInfo's SchedulerResources, ResourceView.VisibleResourceCount and View, compiled:
    // IsHorizontalResourceViewDesktop is asked on every draw.
    private static Func<object, ICollection?>? s_schedulerResources;
    private static Func<object, int>? s_visibleResourceCount, s_view;

    // SfScheduler.IsVirtualizationNeeded (the internal property behind ISchedulerViewInfo's), compiled:
    // the virtualized views read it on every draw.
    private static Func<object, bool>? s_getVirtualized;
    private static Action<object, bool>? s_setVirtualized;

    /// <summary>True once the desktop resource view is bridged (the neutral build answered false).</summary>
    internal static bool IsInstalled { get; private set; }

    /// <summary>
    /// Day view control arranges in the resource view (diagnostics: a settled view makes none; the
    /// resource header is not counted, as a scroll view measures its content on every draw).
    /// </summary>
    internal static int LayoutPasses { get; private set; }

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        ViewHelper = T("SchedulerViewHelper");
        var isDesktop = ViewHelper?.GetMethod("IsHorizontalResourceViewDesktop", BindingFlags.Static | BindingFlags.NonPublic);
        // A desktop build (Windows, Mac) has the resource view; only the neutral stub is bridged.
        if (isDesktop == null || Il(isDesktop).Length > 2)
            return;
        ResourceHelper = T("ResourceViewHelper");
        SelectionHelper = T("SelectionHelper");
        AppointmentsHelper = T("AppointmentsViewHelper");
        Resources = T("SfSchedulerResources");
        s_resourceHeaderLayout = T("ResourceHeaderLayout");
        s_dayHeaderView = T("DayHeaderView");
        s_resourceHeaderView = T("ResourceHeaderView");
        s_allDayLayout = T("AllDayAppointmentsLayout");
        s_scrollViewExt = T("ScrollViewExt");
        s_schedulerGrid = T("SchedulerGrid");
        s_dayViewControl = T("DayViewControl");
        if (!CompileAccessors() || s_resourceHeaderLayout == null || s_dayViewControl == null || s_allDayLayout == null
            || s_dayHeaderView == null || s_resourceHeaderView == null || s_scrollViewExt == null || s_schedulerGrid == null)
        {
            DiagnosticLog.Warn("Syncfusion", "This Syncfusion.Maui.Scheduler release lacks members the desktop resource view needs; resources stay in the timeline views.");
            return;
        }

        var harmony = new Harmony("com.openmaui.syncfusion.scheduler.resource-view");
        Patch(harmony, ViewHelper, "IsHorizontalResourceViewDesktop", nameof(IsHorizontalResourceViewDesktop_Prefix));
        InstallVirtualization(harmony);
        InstallDayViewControl(harmony);
        InstallResourceHeaderLayout(harmony);
        InstallViews(harmony);
        InstallSnapLayoutAndScheduler(harmony);
        InstallTemplates(harmony);
        IsInstalled = true;
    }

    private static Type? T(string name) => Type("Syncfusion.Maui.Scheduler." + name, Asm);

    private static bool CompileAccessors()
    {
        var info = T("IResourceViewInfo");
        // An interface's own properties exclude those of the interfaces it extends.
        PropertyInfo? Find(string name) => info?.GetProperty(name)
            ?? info?.GetInterfaces().Select(i => i.GetProperty(name)).FirstOrDefault(p => p != null);
        var resources = Find("SchedulerResources");
        var resourceView = Find("ResourceView");
        var view = Find("View");
        var count = resourceView?.PropertyType.GetProperty("VisibleResourceCount");
        if (info == null || resources == null || resourceView == null || view == null || count == null)
            return false;
        var target = Expression.Parameter(typeof(object), "info");
        var typed = Expression.Convert(target, view.DeclaringType!);
        var typedInfo = Expression.Convert(target, info);
        s_schedulerResources = Expression.Lambda<Func<object, ICollection?>>(
            Expression.TypeAs(Expression.Property(typedInfo, resources), typeof(ICollection)), target).Compile();
        var settings = Expression.Property(typedInfo, resourceView);
        s_visibleResourceCount = Expression.Lambda<Func<object, int>>(
            Expression.Condition(Expression.Equal(settings, Expression.Constant(null, resourceView.PropertyType)),
                Expression.Constant(-1), Expression.Property(settings, count)), target).Compile();
        s_view = Expression.Lambda<Func<object, int>>(Expression.Convert(Expression.Property(typed, view), typeof(int)), target).Compile();
        return true;
    }

    /// <summary>
    /// SchedulerViewHelper.IsHorizontalResourceViewDesktop of the desktop builds: resources shown
    /// side by side when there are resources, the visible resource count is not zero and the
    /// view is the day, week, work-week or month view.
    /// </summary>
    internal static bool IsDesktop(object? info)
    {
        if (info == null || s_schedulerResources == null)
            return false;
        try
        {
            if (s_schedulerResources(info) is not { Count: > 0 } || s_visibleResourceCount!(info) == 0)
                return false;
            return s_view!(info) is >= 0 and <= 3; // Day, Week, WorkWeek, Month
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private static bool IsMonth(object? info) => info != null && s_view!(info) == 3;

    // ---- Render virtualization ----------------------------------------------------------------------

    // The neutral build answers ISchedulerViewInfo.IsVirtualizationNeeded with false and drops what
    // is set, so its virtualized drawing code never runs; the Windows build reads and writes
    // SfScheduler.IsVirtualizationNeeded there.
    private static void InstallVirtualization(Harmony harmony)
    {
        const string Name = "Syncfusion.Maui.Scheduler.ISchedulerViewInfo.";
        var scheduler = T("SfScheduler");
        var property = scheduler?.GetProperty("IsVirtualizationNeeded", BindingFlags.Instance | BindingFlags.NonPublic);
        var getter = scheduler?.GetMethod(Name + "get_IsVirtualizationNeeded", BindingFlags.Instance | BindingFlags.NonPublic);
        if (property?.GetMethod == null || property.SetMethod == null || getter == null || Il(getter).Length > 2)
            return;
        var target = Expression.Parameter(typeof(object), "scheduler");
        var value = Expression.Parameter(typeof(bool), "value");
        var typed = Expression.Property(Expression.Convert(target, scheduler!), property);
        s_getVirtualized = Expression.Lambda<Func<object, bool>>(typed, target).Compile();
        s_setVirtualized = Expression.Lambda<Action<object, bool>>(Expression.Assign(typed, value), target, value).Compile();
        Patch(harmony, scheduler, Name + "get_IsVirtualizationNeeded", nameof(VirtualizationGet_Prefix));
        Patch(harmony, scheduler, Name + "set_IsVirtualizationNeeded", nameof(VirtualizationSet_Prefix));
    }

    private static bool VirtualizationGet_Prefix(object __instance, ref bool __result)
    {
        __result = s_getVirtualized!(__instance);
        return false;
    }

    private static bool VirtualizationSet_Prefix(object __instance, bool value)
    {
        s_setVirtualized!(__instance, value);
        return false;
    }

    /// <summary>ISchedulerViewInfo.IsVirtualizationNeeded: the resource columns are drawn in a window.</summary>
    private static bool IsVirtualized(object? info) => info != null && s_getVirtualized != null && s_getVirtualized(info);

    private static void SetVirtualized(object? info, bool value)
    {
        if (info != null && s_setVirtualized != null)
            s_setVirtualized(info, value);
    }

    /// <summary>
    /// The day views' desktop rule (DayViewControl.LayoutMeasure of the Windows build): columns more
    /// than three viewports wide are virtualized.
    /// </summary>
    private static bool NeedsVirtualization(object info, double viewPortWidth) =>
        s_getVirtualized != null && viewPortWidth > 0.0 && (double)CallStatic(ViewHelper!, "HorizontalResourceViewDesktopViewPortWidth", info, viewPortWidth)! > VirtualWidth(viewPortWidth);

    /// <summary>The width of the drawn window (SchedulerViewHelper.GetViewPortWidth: three viewports).</summary>
    private static double VirtualWidth(double viewPortWidth) => (double)CallStatic(ViewHelper!, "GetViewPortWidth", viewPortWidth)!;

    /// <summary>The window's left edge: a viewport before the scroll offset, from the start at the least.</summary>
    private static double VirtualX(double scrollX, double windowWidth) => Math.Max(0.0, scrollX - windowWidth / 3.0);

    // Parameter names match SchedulerViewHelper.IsHorizontalResourceViewDesktop(IResourceViewInfo schedulerViewInfo).
    private static bool IsHorizontalResourceViewDesktop_Prefix(object schedulerViewInfo, ref bool __result)
    {
        __result = IsDesktop(schedulerViewInfo);
        return false;
    }

    // ---- Patching ---------------------------------------------------------------------------------

    private static void Patch(Harmony harmony, Type? type, string method, string? prefix = null, string? postfix = null,
        Func<MethodBase, bool>? when = null)
    {
        if (type == null)
            return;
        IEnumerable<MethodBase> targets = method == ".ctor"
            ? type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            : type.GetMethods(Any).Where(m => m.Name == method && !m.IsAbstract);
        bool found = false;
        foreach (var target in targets)
        {
            if (when != null && !when(target))
                continue;
            found = true;
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(PatchMethod(prefix)),
                    postfix: postfix == null ? null : new HarmonyMethod(PatchMethod(postfix)));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("Syncfusion", $"Patching SfScheduler {type.Name}.{method} for the resource view failed", ex);
            }
        }
        if (!found)
            DiagnosticLog.Warn("Syncfusion", $"SfScheduler {type.Name}.{method} was not found; the desktop resource view skips it.");
    }

    private static MethodInfo PatchMethod(string name) =>
        typeof(SfSchedulerResourceView).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(SfSchedulerResourceView), name);

    private static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"SfScheduler resource view: {what} failed", ex);
        }
    }

    private static T Guard<T>(string what, Func<T> func, T fallback)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"SfScheduler resource view: {what} failed", ex);
            return fallback;
        }
    }

    /// <summary>A delegate to a (private) instance method of a Syncfusion view.</summary>
    private static TDelegate? Bind<TDelegate>(object target, string method) where TDelegate : Delegate
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (m != null)
                return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), target, m);
        }
        return null;
    }

    private static double ViewHeaderHeight(object info, string settingsOwner) =>
        (float)CallStatic(ViewHelper!, "GetViewHeaderHeight", Get(info, "View"), Get<double>(Get(Get(info, settingsOwner), "ViewHeaderSettings"), "Height"))!;

    private static double ResourceTotalHeight(object info) => (double)CallStatic(ResourceHelper!, "GetTimelineResourceTotalHeight", info)!;

    private static bool IsSingleDay(object info) =>
        CallStatic(ViewHelper!, "IsSingleNumberOfDay", Get(info, "View"), Get<int>(Get(info, "DaysView"), "NumberOfVisibleDays")) is true;

    private static bool IsResourceType(object info) => CallStatic(ViewHelper!, "IsResourceType", info) is true;

    private static bool IsDragging(object? info) =>
        AppointmentsHelper != null && CallStatic(AppointmentsHelper, "IsAppointmentDragging", Get(info, "DragAndDropController")) is true;

    private static object? HorizontalResource(object info, double x, double width) =>
        CallStatic(ResourceHelper!, "GetSelectedHorizontalResourceView", info, x, width);

    private static void Remove(Layout layout, View? view)
    {
        if (view == null)
            return;
        layout.Remove(view);
        if (view.Handler?.PlatformView != null)
            view.Handler.DisconnectHandler();
    }
}
