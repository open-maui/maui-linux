// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Layout code Syncfusion ships only in its platform builds, supplied for the
/// platform-neutral build Linux gets. Found by diffing the layout overrides of
/// the Windows and net10.0 assemblies (34.2.9); only
/// <c>SfHorizontalContent.MeasureContent</c> matters for SfView controls (the
/// others are native WinUI panels).
/// </summary>
/// <remarks>
/// Bound by name because the controls live in optional Syncfusion packages
/// this bridge does not reference; a member that is missing disables only
/// its shim.
/// </remarks>
internal static class SfPlatformShims
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private sealed class HorizontalContent
    {
        public required Type Type { get; init; }
        public required PropertyInfo Width { get; init; }
        public required PropertyInfo Height { get; init; }
        public required MethodInfo UpdateSize { get; init; }
        public required MethodInfo UpdatePosition { get; init; }
        public FieldInfo? TabBar { get; init; }
    }

    private static readonly Lazy<HorizontalContent?> s_horizontalContent = new(BindHorizontalContent);

    /// <summary>
    /// Runs before an SfView is measured. SfTabView's content pages learn their
    /// width here (the Windows build's <c>SfHorizontalContent.MeasureContent</c>):
    /// without it every page is as wide as the screen, since SfListView falls
    /// back to the screen width when measured unconstrained.
    /// </summary>
    internal static void BeforeMeasure(object? view, double widthConstraint, double heightConstraint)
    {
        if (view == null || s_horizontalContent.Value is not { } hc || !hc.Type.IsInstanceOfType(view))
            return;

        bool changed = false;
        if (widthConstraint > 0 && double.IsFinite(widthConstraint) && (double)hc.Width.GetValue(view)! != widthConstraint)
        {
            hc.Width.SetValue(view, widthConstraint);
            changed = true;
        }
        if (heightConstraint > 0 && double.IsFinite(heightConstraint) && (double)hc.Height.GetValue(view)! != heightConstraint)
        {
            hc.Height.SetValue(view, heightConstraint);
            changed = true;
        }
        if (!changed)
            return;

        hc.UpdateSize.Invoke(view, null);
        hc.UpdatePosition.Invoke(view, null);
        if (hc.TabBar?.GetValue(view) is { } tabBar)
            tabBar.GetType().GetMethod("UpdateTabIndicatorWidth", Any, Type.EmptyTypes)?.Invoke(tabBar, null);
    }

    private static HorizontalContent? BindHorizontalContent()
    {
        var type = Type.GetType("Syncfusion.Maui.TabView.SfHorizontalContent, Syncfusion.Maui.TabView");
        if (type == null)
            return null; // TabView is not part of the app

        var width = type.GetProperty("ContentWidth", Any);
        var height = type.GetProperty("ContentHeight", Any);
        var size = type.GetMethod("UpdateTabItemContentSize", Any, Type.EmptyTypes);
        var position = type.GetMethod("UpdateTabItemContentPosition", Any, Type.EmptyTypes);
        if (width?.PropertyType != typeof(double) || height?.PropertyType != typeof(double) || size == null || position == null)
        {
            Services.DiagnosticLog.Warn("Syncfusion",
                "This Syncfusion.Maui.TabView release lacks members the Linux bridge sizes tab content with; tab pages may size to the screen.");
            return null;
        }

        return new HorizontalContent
        {
            Type = type,
            Width = width,
            Height = height,
            UpdateSize = size,
            UpdatePosition = position,
            TabBar = type.GetField("tabBar", Any),
        };
    }
}
