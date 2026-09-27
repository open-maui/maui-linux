// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Graphics.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Syncfusion's "redraw me": <c>SfView.InvalidateDrawable</c> and
/// <c>SfDrawableView.InvalidateDrawable</c> forward only to Syncfusion's own
/// handler types, so with the bridge's handlers every request was dropped.
/// They now invalidate the view's platform view as well, so a control repaints
/// when it changes and only then; before, every visible Syncfusion view was
/// repainted on every animation frame to catch changes it could not report,
/// which kept the UI thread busy while anything animated.
/// </summary>
internal static class SfInvalidationPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var harmony = new Harmony("com.openmaui.syncfusion.invalidate");
            var postfix = new HarmonyMethod(typeof(SfInvalidationPatches).GetMethod(nameof(Invalidate_Postfix), BindingFlags.Static | BindingFlags.NonPublic));
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var type in new[] { typeof(SfView), typeof(SfDrawableView) })
            {
                var method = type.GetMethod("InvalidateDrawable", Instance, null, Type.EmptyTypes, null);
                if (method != null && method.DeclaringType == type)
                    harmony.Patch(method, postfix: postfix);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching Syncfusion invalidation failed", ex);
        }
    }

    private static void Invalidate_Postfix(object __instance)
    {
        if (__instance is VisualElement { Handler.PlatformView: SkiaView view })
            view.Invalidate();
    }
}
