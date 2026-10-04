// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfCalendar's FlowDirection. The Windows build gives SfCalendar its own
/// <c>FlowDirection</c> property (so WinUI does not mirror the drawn
/// calendar) whose change handler, <c>OnFlowDirectionChanged</c>, lays the
/// month, header and footer out again for the new direction. The
/// platform-neutral build uses VisualElement.FlowDirection, and its change
/// handling relays the calendar out only for LeftToRight: switching a shown
/// calendar to RightToLeft flipped its RTL flag but kept the old layout until
/// the next view change. After every FlowDirection change the calendar is
/// laid out as the Windows handler does it.
/// </summary>
internal static class SfCalendarPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        if (Type.GetType("Syncfusion.Maui.Calendar.SfCalendar, Syncfusion.Maui.Calendar") is not { } calendar)
            return; // SfCalendar is not part of the app
        try
        {
            var original = calendar.GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, [typeof(string)], null);
            if (original == null)
                return;
            new Harmony("com.openmaui.syncfusion.calendar").Patch(original,
                postfix: new HarmonyMethod(typeof(SfCalendarPatches).GetMethod(nameof(OnPropertyChanged_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfCalendar failed", ex);
        }
    }

    /// <summary>The Windows build's <c>OnFlowDirectionChanged</c>.</summary>
    private static void OnPropertyChanged_Postfix(object __instance, string? propertyName)
    {
        if (propertyName != "FlowDirection")
            return;
        try
        {
            if (SfMembers.Get(__instance, "customScrollLayout") is not { } scrollLayout)
            {
                SfMembers.Call(__instance, "UpdateLayoutFlowDirection");
                return;
            }
            SfMembers.Call(__instance, "UpdateFlowDirection");
            SfMembers.Call(scrollLayout, "UpdateVisibleDateOnView");
            SfInvalidation.InvalidateAll(drawingOnly: false);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Laying SfCalendar out for its flow direction failed", ex);
        }
    }
}
