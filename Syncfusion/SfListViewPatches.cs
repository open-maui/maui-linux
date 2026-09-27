// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfListView's row clip. Its layout calls <c>ListViewItemExtensions.ApplyClip</c>
/// on every row; the native builds compute the clip there
/// (<c>ApplyListViewItemClip</c>, which sets the row's <c>ClipRect</c>), but the
/// platform-neutral build Linux apps get ships <c>ApplyClip</c> empty, although
/// <c>ApplyListViewItemClip</c> is in it. With sticky group headers each row is
/// clipped where the header covers it; without the clip the rows showed through
/// a translucent header. The stub is patched to call the real computation, and
/// <see cref="SkiaSfLayout"/> applies the resulting <c>ClipRect</c> when drawing.
/// No compile-time reference: SfListView is optional.
/// </summary>
internal static class SfListViewPatches
{
    private static MethodInfo? s_applyListViewItemClip;
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var extensions = Type.GetType("Syncfusion.Maui.ListView.ListViewItemExtensions, Syncfusion.Maui.ListView");
            if (extensions == null)
                return; // SfListView is not part of the app

            const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var applyClip = extensions.GetMethod("ApplyClip", Static);
            s_applyListViewItemClip = extensions.GetMethod("ApplyListViewItemClip", Static);
            if (applyClip == null || s_applyListViewItemClip == null || applyClip.GetMethodBody() is not { } body || body.GetILAsByteArray()?.Length > 2)
                return; // not the empty stub (a build that implements it needs nothing)

            new Harmony("com.openmaui.syncfusion.listview").Patch(applyClip,
                new HarmonyMethod(typeof(SfListViewPatches).GetMethod(nameof(ApplyClip_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfListView's row clip failed", ex);
        }
    }

    // Parameter names match ListViewItemExtensions.ApplyClip.
    private static bool ApplyClip_Prefix(object listViewItem, object? line, double width, bool isVertical,
        bool hasStickyGroupHeader, bool shouldClipBasedOnClipRect, bool isInDragging, double clipValueOnDragAnimation)
    {
        // shouldClipBasedOnClipRect: the native builds turn an already-set ClipRect into a
        // platform clip there; the drawing reads ClipRect directly, so there is nothing to do.
        if (shouldClipBasedOnClipRect || (line == null && !isInDragging))
            return false;
        try
        {
            s_applyListViewItemClip?.Invoke(null,
                new[] { listViewItem, line, width, isVertical, hasStickyGroupHeader, isInDragging, clipValueOnDragAnimation });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfListView row clip failed", ex);
        }
        return false;
    }
}
