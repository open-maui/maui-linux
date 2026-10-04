// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The stamp annotation view of Syncfusion's annotation layer
/// (<c>Syncfusion.Maui.Core.Annotations.StampView</c>, the base of
/// SfPdfViewer's stamp annotations). Its Windows build overrides
/// <c>OnSizeAllocated</c> to size the stamp's text to the new bounds
/// (<c>UpdateStampSize</c>) as soon as the view is laid out; the neutral build
/// does it only when the stamp next draws. The view runs it on each size change
/// here, right after <c>OnSizeAllocated</c>, as the Windows override does.
/// </summary>
internal static class SfStampViewPatches
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var stamp = typeof(global::Syncfusion.Maui.Core.SfView).Assembly.GetType("Syncfusion.Maui.Core.Annotations.StampView");
        if (stamp == null || stamp.GetMethod("OnSizeAllocated", Any) != null || stamp.GetMethod("UpdateStampSize", Any) == null)
            return; // a build that sizes the stamp itself, or one without the method
        try
        {
            var harmony = new Harmony("com.openmaui.syncfusion.stampview");
            var postfix = new HarmonyMethod(typeof(SfStampViewPatches).GetMethod(nameof(Constructor_Postfix), BindingFlags.Static | BindingFlags.NonPublic));
            foreach (var ctor in stamp.GetConstructors(Any))
                harmony.Patch(ctor, postfix: postfix);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching StampView failed", ex);
        }
    }

    private static void Constructor_Postfix(VisualElement __instance) => __instance.SizeChanged += OnSizeChanged;

    private static void OnSizeChanged(object? sender, EventArgs e)
    {
        try
        {
            SfDyn.Call(sender, "UpdateStampSize");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Sizing a stamp annotation failed", ex);
        }
    }
}
