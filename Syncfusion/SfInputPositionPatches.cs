// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The point conversions Syncfusion's item controls make through the platform
/// view, which the platform-neutral build returns as <c>Point.Zero</c>:
/// <list type="bullet">
/// <item><c>ListViewItemExtensions.GetRawPoints</c> and
/// <c>TreeViewItemExtensions.GetRawPoints</c> turn an item-local touch point
/// into a window point (Windows: <c>TransformToVisual(null)</c>), mirrored
/// for a right-to-left horizontal list. Drag-and-drop reorder, its
/// auto-scroll and swipe offsets are computed from it: without it every
/// dragged row followed a pointer stuck at the window's corner.</item>
/// <item>Scheduler's <c>AppointmentsViewHelper.GetContainerPoints</c> turns
/// a point in a child view into its container's coordinates, less the
/// child's own horizontal offset in timeline and virtualised day views
/// (Windows: <c>TransformToVisual(parent)</c> minus <c>ActualOffset.X</c>);
/// appointment drag and resize positions depend on it.</item>
/// </list>
/// Positions come from the Skia views' window bounds, adjusted for scrolling.
/// The controls are optional, so their types are looked up by name.
/// </summary>
internal static class SfInputPositionPatches
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var harmony = new Harmony("com.openmaui.syncfusion.input-positions");
        Patch(harmony, "Syncfusion.Maui.ListView.ListViewItemExtensions, Syncfusion.Maui.ListView", "GetRawPoints", nameof(ListViewRawPoints_Prefix));
        Patch(harmony, "Syncfusion.Maui.TreeView.TreeViewItemExtensions, Syncfusion.Maui.TreeView", "GetRawPoints", nameof(TreeViewRawPoints_Prefix));
        Patch(harmony, "Syncfusion.Maui.Scheduler.AppointmentsViewHelper, Syncfusion.Maui.Scheduler", "GetContainerPoints", nameof(ContainerPoints_Prefix));
    }

    private static void Patch(Harmony harmony, string typeName, string method, string prefix)
    {
        try
        {
            if (Type.GetType(typeName) is not { } type || type.GetMethod(method, Static) is not { } target)
                return; // the control is not part of the app
            // Only the stub (Point.Zero); a build that implements it needs nothing.
            if (target.GetMethodBody()?.GetILAsByteArray()?.Length > 16)
                return;
            harmony.Patch(target, new HarmonyMethod(typeof(SfInputPositionPatches).GetMethod(prefix, Static)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Patching {typeName.Split(',')[0]}.{method} failed", ex);
        }
    }

    // Parameter names match ListViewItemExtensions.GetRawPoints.
    private static bool ListViewRawPoints_Prefix(object? listViewItem, Point touchPoints, object? platformView, ref Point __result)
    {
        if (platformView is not SkiaView skia)
            return true;
        var point = WindowPoint(skia, touchPoints);
        // listViewItem.ListViewItemInfo.ListView: isRTL, Orientation (Horizontal = 1).
        if (Member(Member(listViewItem, "ListViewItemInfo"), "ListView") is VisualElement listView
            && Member(listView, "isRTL") is true && Member(listView, "Orientation") is Enum orientation && Convert.ToInt32(orientation) == 1)
            point.X = listView.Width - point.X;
        __result = point;
        return false;
    }

    // Parameter names match TreeViewItemExtensions.GetRawPoints.
    private static bool TreeViewRawPoints_Prefix(object? treeViewItem, Point touchPoints, object? platformView, ref Point __result)
    {
        if (platformView is not SkiaView skia)
            return true;
        var point = WindowPoint(skia, touchPoints);
        // treeViewItem.TreeViewItemInfo.TreeView: IsRTL, ExtendedScrollView.Orientation (Horizontal = 1).
        if (Member(Member(treeViewItem, "TreeViewItemInfo"), "TreeView") is VisualElement treeView
            && Member(treeView, "IsRTL") is true
            && Member(treeView, "ExtendedScrollView") is ScrollView { Orientation: Microsoft.Maui.ScrollOrientation.Horizontal })
            point.X = treeView.Width - point.X;
        __result = point;
        return false;
    }

    // Parameter names match AppointmentsViewHelper.GetContainerPoints.
    private static bool ContainerPoints_Prefix(Point touchPoints, object? childElement, object? parentElement,
        bool isTimelineView, bool isDayViewVirtualizationNeeded, ref Point __result)
    {
        if (childElement is not SkiaView child || parentElement is not SkiaView parent)
            return true;
        var childOrigin = child.ScreenBounds;
        var parentOrigin = parent.ScreenBounds;
        var point = new Point(touchPoints.X + childOrigin.X - parentOrigin.X, touchPoints.Y + childOrigin.Y - parentOrigin.Y);
        if (isTimelineView || isDayViewVirtualizationNeeded)
            point.X -= child.Bounds.X - (child.Parent?.Bounds.X ?? 0);
        __result = point;
        return false;
    }

    private static Point WindowPoint(SkiaView view, Point local)
    {
        var bounds = view.ScreenBounds;
        return new Point(bounds.X + local.X, bounds.Y + local.Y);
    }

    private static object? Member(object? target, string name)
    {
        if (target == null)
            return null;
        var type = target.GetType();
        try
        {
            if (type.GetProperty(name, Instance) is { } property && property.GetIndexParameters().Length == 0)
                return property.GetValue(target);
            return type.GetField(name, Instance)?.GetValue(target);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
