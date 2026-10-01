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
/// <para>
/// Its layout after a scroll. Each scroll moves <c>VisualContainer.ScrollOffset</c>, which
/// calls <c>InvalidateForceLayout</c> so the rows are laid out again at the new offset; the
/// sticky group header is placed there (at the offset, in content coordinates). The Windows
/// build invalidates the container's measure; the platform-neutral build hands a linear list
/// to <c>InvalidateNativeInstance</c>, an iOS-only call that is empty in it, so nothing laid
/// the rows out again and the header stayed where an earlier pass put it (CiteLynq's Economy
/// list: a gap above the header once scrolled back to the top). The stub is patched to
/// invalidate the measure, as the Windows build does.
/// </para>
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
            InstallRowClip();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfListView's row clip failed", ex);
        }
        try
        {
            InstallForceLayout();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfListView's layout after a scroll failed", ex);
        }
    }

    private static void InstallRowClip()
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

    private static FieldInfo? s_isEnsured;

    private static void InstallForceLayout()
    {
        var container = Type.GetType("Syncfusion.Maui.ListView.VisualContainer, Syncfusion.Maui.ListView");
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var forceLayout = container?.GetMethod("InvalidateForceLayout", Instance, null, new[] { typeof(bool) }, null);
        s_isEnsured = container?.GetField("IsEnsured", Instance);
        if (forceLayout == null || s_isEnsured == null || !CallsMethodNamed(forceLayout, "InvalidateNativeInstance"))
            return; // a build that lays itself out (the Windows one invalidates the measure here)
        new Harmony("com.openmaui.syncfusion.listview.layout").Patch(forceLayout,
            new HarmonyMethod(typeof(SfListViewPatches).GetMethod(nameof(InvalidateForceLayout_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
    }

    /// <summary>True when <paramref name="method"/>'s IL calls a method named <paramref name="name"/>.</summary>
    private static bool CallsMethodNamed(MethodInfo method, string name)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null)
            return false;
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28 && il[i] != 0x6F) // call, callvirt
                continue;
            try
            {
                if (method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1))?.Name == name)
                    return true;
            }
            catch (ArgumentException)
            {
                // not a method token: an operand byte that looked like an opcode
            }
        }
        return false;
    }

    // Parameter names match VisualContainer.InvalidateForceLayout.
    private static bool InvalidateForceLayout_Prefix(object __instance, bool isEnsured)
    {
        try
        {
            s_isEnsured?.SetValue(__instance, isEnsured);
            (__instance as Microsoft.Maui.Controls.VisualElement)?.InvalidateMeasure();
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfListView layout after a scroll failed", ex);
            return true;
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
