// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// A view's Loaded comes once it has its platform view, as on the other platforms. MAUI's
/// platform-neutral build counts a view as loaded as soon as it is in a window (IsLoaded is
/// Window != null), and OpenMaui puts the page in its window before its views have handlers:
/// Loaded came first, and MAUI's PlatformBehavior, which reads Handler.PlatformView in it, threw
/// (every CommunityToolkit platform behavior: IconTintColorBehavior on an image crashed the page).
/// Held back here; <see cref="SkiaView"/> sends it at the view's first arrange.
/// </summary>
internal static class LoadedEventPatches
{
    private static int s_installed;
    private static MethodInfo? s_sendLoaded;
    private static FieldInfo? s_isLoadedFired;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var element = typeof(VisualElement);
            s_sendLoaded = element.GetMethod("SendLoaded", Instance, Type.EmptyTypes);
            s_isLoadedFired = element.GetField("_isLoadedFired", Instance);
            var target = element.GetMethod("HandlePlatformUnloadedLoaded", Instance, Type.EmptyTypes);
            if (target == null || s_sendLoaded == null || s_isLoadedFired == null)
            {
                DiagnosticLog.Warn("LoadedEventPatches", "VisualElement's Loaded wiring not found; Loaded may come before a view's handler");
                return;
            }
            new Harmony("com.openmaui.loaded").Patch(target,
                new HarmonyMethod(typeof(LoadedEventPatches).GetMethod(nameof(HandlePlatformUnloadedLoaded_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("LoadedEventPatches", "Patching VisualElement's Loaded failed", ex);
        }
    }

    /// <summary>Holds Loaded back while the view, in a window, has no platform view yet.</summary>
    private static bool HandlePlatformUnloadedLoaded_Prefix(VisualElement __instance) =>
        !(__instance.Window != null && (__instance.Handler?.PlatformView == null || IsClosed(__instance.Window)));

    // MAUI windows whose native window has closed: their views stay unloaded (MAUI's
    // platform-neutral wiring would load them again on the next window handler change,
    // since they still have a Window).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.Maui.Controls.Window, object> s_closedWindows = new();

    private static bool IsClosed(Microsoft.Maui.Controls.Window window) => s_closedWindows.TryGetValue(window, out _);

    /// <summary>Marks a MAUI window's native window closed (or shown again).</summary>
    internal static void SetWindowClosed(Microsoft.Maui.Controls.Window window, bool closed)
    {
        if (closed)
            s_closedWindows.AddOrUpdate(window, new object());
        else
            s_closedWindows.Remove(window);
    }

    /// <summary>
    /// Sends Loaded (once) for a view in a window that has its platform view: at its first arrange.
    /// Through MAUI's own SendLoaded, so a later window change neither repeats nor loses it.
    /// </summary>
    internal static void SendLoadedIfDue(VisualElement element)
    {
        if (s_sendLoaded == null || s_isLoadedFired == null)
            return;
        if (element.Window == null || element.Handler?.PlatformView == null)
            return;
        if (s_isLoadedFired.GetValue(element) is true)
            return;
        s_sendLoaded.Invoke(element, null);
    }

    /// <summary>True when the patch is in place (its sender replaces the direct invoke).</summary>
    internal static bool IsInstalled => s_sendLoaded != null && s_isLoadedFired != null && Volatile.Read(ref s_installed) == 1;

    private static MethodInfo? s_sendUnloaded;

    /// <summary>
    /// Sends Unloaded (once) to an element that had Loaded: what a platform does for a
    /// view tree whose native window went away. Through MAUI's own SendUnloaded, so a
    /// later Loaded (the view shown in another window) is sent again as usual.
    /// </summary>
    internal static void SendUnloadedIfLoaded(VisualElement element)
    {
        s_sendUnloaded ??= typeof(VisualElement).GetMethod("SendUnloaded", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);
        if (s_sendUnloaded == null || s_isLoadedFired == null)
            return;
        if (s_isLoadedFired.GetValue(element) is not true)
            return;
        s_sendUnloaded.Invoke(element, null);
    }
}
