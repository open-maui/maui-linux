// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core.Internals;
using SfPointerEventArgs = Syncfusion.Maui.Core.Internals.PointerEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// The internal Syncfusion.Maui.Core members the bridge drives. On the other
/// platforms Syncfusion's own platform code calls them from native touch
/// events; the platform-neutral build leaves those subscriptions empty, so the
/// bridge calls them instead. Resolved once; a member that is missing (a
/// Syncfusion release that renamed it) disables only the input it feeds and is
/// reported once, never thrown.
/// </summary>
internal static class SfInternals
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static readonly BindableProperty? s_touchDetectorProperty =
        typeof(TouchListenerExtension).GetField("TouchDetectorProperty", Any)?.GetValue(null) as BindableProperty;

    private static readonly BindableProperty? s_gestureDetectorProperty =
        typeof(GestureListenerExtension).GetField("GestureDetectorProperty", Any)?.GetValue(null) as BindableProperty;

    private static readonly Action<TouchDetector, SfPointerEventArgs>? s_onTouch =
        Bind<Action<TouchDetector, SfPointerEventArgs>>(typeof(TouchDetector), "OnTouchAction", typeof(SfPointerEventArgs));

    private static readonly Action<GestureDetector, Point, int>? s_onTapped =
        Bind<Action<GestureDetector, Point, int>>(typeof(GestureDetector), "OnTapped", typeof(Point), typeof(int));

    private static readonly Action<GestureDetector, Point, PointerDeviceType>? s_onRightTapped =
        Bind<Action<GestureDetector, Point, PointerDeviceType>>(typeof(GestureDetector), "OnRightTapped",
            typeof(Point), typeof(PointerDeviceType));

    private static readonly Action<GestureDetector, Func<IElement?, Point?>?, Point>? s_onLongPress =
        Bind<Action<GestureDetector, Func<IElement?, Point?>?, Point>>(typeof(GestureDetector), "OnLongPress",
            typeof(Func<IElement?, Point?>), typeof(Point));

    private static readonly ConstructorInfo? s_pointerArgsCtor = typeof(SfPointerEventArgs).GetConstructor(Any, null,
        [typeof(Func<IElement?, Point?>), typeof(long), typeof(PointerActions), typeof(PointerDeviceType), typeof(Point)], null);

    private static readonly PropertyInfo? s_isLeftButtonPressed =
        typeof(SfPointerEventArgs).GetProperty("IsLeftButtonPressed", Any);

    private static readonly PropertyInfo? s_isRightButtonPressed =
        typeof(SfPointerEventArgs).GetProperty("IsRightButtonPressed", Any);

    private static int s_reported;

    /// <summary>Names of the members that did not resolve; empty when every input path is live.</summary>
    internal static IReadOnlyList<string> Missing
    {
        get
        {
            var missing = new List<string>();
            if (s_touchDetectorProperty == null) missing.Add("TouchListenerExtension.TouchDetectorProperty");
            if (s_gestureDetectorProperty == null) missing.Add("GestureListenerExtension.GestureDetectorProperty");
            if (s_onTouch == null) missing.Add("TouchDetector.OnTouchAction");
            if (s_onTapped == null) missing.Add("GestureDetector.OnTapped");
            if (s_onRightTapped == null) missing.Add("GestureDetector.OnRightTapped");
            if (s_onLongPress == null) missing.Add("GestureDetector.OnLongPress");
            if (s_pointerArgsCtor == null) missing.Add("PointerEventArgs(position, id, action, deviceType, point)");
            return missing;
        }
    }

    /// <summary>Logs the unresolved members once per process.</summary>
    internal static void ReportMissingOnce()
    {
        var missing = Missing;
        if (missing.Count == 0 || Interlocked.Exchange(ref s_reported, 1) == 1)
            return;
        DiagnosticLog.Warn("Syncfusion",
            $"This Syncfusion.Maui.Core release lacks members the Linux bridge drives ({string.Join(", ", missing)}); the matching input is disabled.");
    }

    internal static TouchDetector? TouchDetectorOf(View view) =>
        s_touchDetectorProperty == null ? null : view.GetValue(s_touchDetectorProperty) as TouchDetector;

    internal static GestureDetector? GestureDetectorOf(View view) =>
        s_gestureDetectorProperty == null ? null : view.GetValue(s_gestureDetectorProperty) as GestureDetector;

    internal static void Touch(TouchDetector detector, Func<IElement?, Point?> position, PointerActions action,
        Point point, bool leftPressed, bool rightPressed)
    {
        if (s_onTouch == null || s_pointerArgsCtor == null)
            return;
        var args = (SfPointerEventArgs)s_pointerArgsCtor.Invoke([position, 1L, action, PointerDeviceType.Mouse, point]);
        s_isLeftButtonPressed?.SetValue(args, leftPressed);
        if (rightPressed)
            s_isRightButtonPressed?.SetValue(args, true);
        s_onTouch(detector, args);
    }

    internal static void Tap(GestureDetector detector, Point point, int count) => s_onTapped?.Invoke(detector, point, count);

    internal static void RightTap(GestureDetector detector, Point point) =>
        s_onRightTapped?.Invoke(detector, point, PointerDeviceType.Mouse);

    internal static void LongPress(GestureDetector detector, Func<IElement?, Point?> position, Point point) =>
        s_onLongPress?.Invoke(detector, position, point);

    private static T? Bind<T>(Type type, string name, params Type[] parameters) where T : Delegate
    {
        var method = type.GetMethod(name, Any, null, parameters, null);
        if (method == null)
            return null;
        try
        {
            return (T)Delegate.CreateDelegate(typeof(T), method);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
