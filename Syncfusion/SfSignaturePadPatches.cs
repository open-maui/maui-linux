// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// <c>SfSignaturePad.ToImageSource</c> and <c>GetSignaturePoints</c> ask the
/// control's handler only when it is Syncfusion's own <c>SignaturePadHandler</c>
/// (whose platform-neutral export throws); with
/// <see cref="SfSignaturePadBridgeHandler"/> they returned null. Patched to
/// answer from the bridge handler when it is the control's handler. No
/// compile-time reference: SfSignaturePad is optional.
/// </summary>
internal static class SfSignaturePadPatches
{
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var pad = Type.GetType("Syncfusion.Maui.SignaturePad.SfSignaturePad, Syncfusion.Maui.SignaturePad");
            if (pad == null)
                return; // SfSignaturePad is not part of the app

            var harmony = new Harmony("com.openmaui.syncfusion.signaturepad");
            const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
            const BindingFlags Own = BindingFlags.Static | BindingFlags.NonPublic;
            if (pad.GetMethod("ToImageSource", Public, Type.EmptyTypes) is { } toImage)
                harmony.Patch(toImage, new HarmonyMethod(typeof(SfSignaturePadPatches).GetMethod(nameof(ToImageSource_Prefix), Own)));
            if (pad.GetMethod("GetSignaturePoints", Public, Type.EmptyTypes) is { } points)
                harmony.Patch(points, new HarmonyMethod(typeof(SfSignaturePadPatches).GetMethod(nameof(GetSignaturePoints_Prefix), Own)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfSignaturePad's export failed", ex);
        }
    }

    private static bool ToImageSource_Prefix(VisualElement __instance, ref ImageSource? __result)
    {
        if (__instance.Handler is not SfSignaturePadBridgeHandler handler)
            return true;
        try
        {
            __result = handler.ToImageSource();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Exporting the signature failed", ex);
            __result = null;
        }
        return false;
    }

    private static bool GetSignaturePoints_Prefix(VisualElement __instance, ref List<List<float>>? __result)
    {
        if (__instance.Handler is not SfSignaturePadBridgeHandler handler)
            return true;
        __result = handler.GetSignaturePoints();
        return false;
    }
}
