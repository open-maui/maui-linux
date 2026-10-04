// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform.Linux.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Essentials <see cref="Permissions"/> on Linux. The portable build's
/// <c>BasePlatformPermission</c> throws NotImplementedInReferenceAssemblyException from all four
/// members, so every <c>Permissions.CheckStatusAsync&lt;T&gt;()</c> / <c>RequestAsync&lt;T&gt;()</c>
/// call (which libraries and apps make before using the camera, location, contacts, ...)
/// threw on Linux.
///
/// A desktop Linux app has no install-time permission manifest and no runtime permission
/// grants of its own: access is the user's (and, inside a sandbox, decided by the portal when
/// the feature is used, e.g. the Location or Camera portal asks then). That is the Windows
/// model for an unpackaged app, and this mirrors Windows' BasePlatformPermission:
/// <list type="bullet">
/// <item>CheckStatusAsync: Granted.</item>
/// <item>RequestAsync: the status; for the location permissions it must be called on the main
/// thread (PermissionException otherwise), as on Windows, Android and iOS, since that is where
/// a platform may prompt.</item>
/// <item>EnsureDeclared: nothing to declare (no manifest).</item>
/// <item>ShouldShowRationale: false.</item>
/// </list>
/// A permission class an app or library writes itself (deriving from BasePlatformPermission and
/// overriding these) keeps its own behaviour: only the base members are replaced.
/// </summary>
internal static class PermissionsPatches
{
    internal static void Install(Harmony harmony)
    {
        var type = typeof(Permissions.BasePlatformPermission);
        Patch(harmony, type, nameof(Permissions.BasePermission.CheckStatusAsync), nameof(CheckStatusAsync_Prefix));
        Patch(harmony, type, nameof(Permissions.BasePermission.RequestAsync), nameof(RequestAsync_Prefix));
        Patch(harmony, type, nameof(Permissions.BasePermission.EnsureDeclared), nameof(EnsureDeclared_Prefix));
        Patch(harmony, type, nameof(Permissions.BasePermission.ShouldShowRationale), nameof(ShouldShowRationale_Prefix));
    }

    private static void Patch(Harmony harmony, Type type, string method, string prefixName)
    {
        var original = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
        if (original == null)
        {
            DiagnosticLog.Error("PermissionsPatches", $"BasePlatformPermission.{method} not found");
            return;
        }
        harmony.Patch(original, new HarmonyMethod(typeof(PermissionsPatches).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic)!));
    }

    /// <summary>The status a permission has on Linux (no per-app grants: Granted).</summary>
    internal static PermissionStatus StatusOf(Permissions.BasePermission permission) => PermissionStatus.Granted;

    /// <summary>Whether requesting <paramref name="permission"/> must happen on the main thread.</summary>
    internal static bool RequestNeedsMainThread(Permissions.BasePermission permission) =>
        permission is Permissions.LocationWhenInUse or Permissions.LocationAlways;

    private static bool CheckStatusAsync_Prefix(Permissions.BasePermission __instance, ref Task<PermissionStatus> __result)
    {
        __result = Task.FromResult(StatusOf(__instance));
        return false;
    }

    private static bool RequestAsync_Prefix(Permissions.BasePermission __instance, ref Task<PermissionStatus> __result)
    {
        // A faulted task, as the other platforms' async RequestAsync produce.
        __result = RequestNeedsMainThread(__instance) && !LinuxDispatcher.IsMainThread
            ? Task.FromException<PermissionStatus>(new PermissionException("Permission request must be invoked on main thread."))
            : Task.FromResult(StatusOf(__instance));
        return false;
    }

    private static bool EnsureDeclared_Prefix() => false;

    private static bool ShouldShowRationale_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
