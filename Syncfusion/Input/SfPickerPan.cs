// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Dragging a picker column with the mouse (SfPicker, SfDatePicker,
/// SfTimePicker, SfDateTimePicker: Syncfusion's internal <c>PickerView</c>).
/// The Windows build makes each column a pan listener
/// (<c>IPanGestureListener.OnPan</c>): the column follows the drag, and on
/// release the item nearest the centre (<c>GetCenterItemIndex</c>) is
/// selected, or the column settles back on the selected item. The
/// platform-neutral build has no pan listener there (touch platforms scroll
/// the column natively), so a mouse could only click items. The same
/// handling is driven from OpenMaui's pointer routing.
/// </summary>
internal static class SfPickerPan
{
    private const string PickerViewType = "Syncfusion.Maui.Picker.PickerView";

    private sealed class Drag
    {
        public Point Start;
        public bool Started;
    }

    private static readonly ConditionalWeakTable<View, Drag> s_drags = new();
    private static int s_installed;

    /// <summary>
    /// The column's scroll view puts the column back on its selected item
    /// after each measure (<c>PickerScrollView.UpdateSelectedIndexPosition</c>,
    /// dispatched from <c>MeasureOverride</c>). WinUI measures only when
    /// something asks for it; OpenMaui lays the tree out for every frame, so
    /// during a drag that would undo the drag frame by frame. It waits while
    /// the column is being dragged.
    /// </summary>
    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        if (Type.GetType("Syncfusion.Maui.Picker.PickerScrollView, Syncfusion.Maui.Picker") is not { } scrollView)
            return; // the picker is not part of the app
        try
        {
            var original = scrollView.GetMethod("UpdateSelectedIndexPosition", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (original != null)
                new HarmonyLib.Harmony("com.openmaui.syncfusion.picker-pan").Patch(original,
                    new HarmonyLib.HarmonyMethod(typeof(SfPickerPan).GetMethod(nameof(UpdateSelectedIndexPosition_Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching the picker columns failed", ex);
        }
    }

    private static bool UpdateSelectedIndexPosition_Prefix(ScrollView __instance) =>
        __instance.Content is not View column || SfMembers.Get(column, "IsPanScrolling") is not true;

    internal static bool IsPickerColumn(View view) => view.GetType().FullName == PickerViewType;

    internal static void OnPointerRouted(View view, SkiaView.RoutedPointerKind kind, PointerEventArgs e)
    {
        try
        {
            var origin = SfInputBridge.OriginOf(view);
            var local = new Point(e.X - origin.X, e.Y - origin.Y);
            switch (kind)
            {
                case SkiaView.RoutedPointerKind.Pressed when e.Button == PointerButton.Left:
                    s_drags.AddOrUpdate(view, new Drag { Start = local });
                    break;

                case SkiaView.RoutedPointerKind.Moved when s_drags.TryGetValue(view, out var drag):
                    if (!drag.Started)
                    {
                        // Started (the first move), as the Windows pan begins.
                        if (Math.Abs(local.Y - drag.Start.Y) < 1)
                            break;
                        drag.Started = true;
                        SfMembers.Set(view, "IsPanScrolling", true);
                    }
                    Running(view, drag, local);
                    break;

                case SkiaView.RoutedPointerKind.Released when s_drags.TryGetValue(view, out var ended):
                    s_drags.Remove(view);
                    if (ended.Started)
                        Completed(view);
                    break;
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Dragging a picker column failed", ex);
        }
    }

    /// <summary>
    /// A Running pan: the column scrolls by the drag since its start. The
    /// point is in the column's own coordinates, which move with the scroll,
    /// so each move scrolls by what the pointer moved since the last one.
    /// </summary>
    private static void Running(View view, Drag drag, Point local)
    {
        if (SfMembers.Get(view, "pickerLayoutInfo") is not { } layout || view.Parent is not ScrollView scrollView)
            return;
        double deltaY = local.Y - drag.Start.Y;
        double offset = SfMembers.Get(layout, "ScrollOffset") is double scroll ? scroll : scrollView.ScrollY;
        _ = scrollView.ScrollToAsync(0.0, offset - deltaY, false);
        // A repaint, not a layout pass: the column's measure puts it back on
        // its selected item.
        (view.Handler?.PlatformView as SkiaView)?.Invalidate();
    }

    /// <summary>The Completed pan: select the item at the centre, or settle on the selected one.</summary>
    private static void Completed(View view)
    {
        SfMembers.Set(view, "IsPanScrolling", false);
        if (SfMembers.Get(view, "pickerLayoutInfo") is not { } layout || SfMembers.Get(layout, "PickerInfo") is not { } info)
            return;
        double itemHeight = SfMembers.Get(info, "ItemHeight") is double height && height > 0 ? height : 40;
        int center = CenterItemIndex(view, layout, info, itemHeight);
        int selected = SfMembers.Get(SfMembers.Get(layout, "Column"), "SelectedIndex") is int index ? index : -1;
        if (center == selected)
        {
            if (view.Parent is ScrollView scrollView)
                _ = scrollView.ScrollToAsync(0.0, center * itemHeight, false);
        }
        else if (InterfaceMethod(layout, "UpdateSelectedIndexValue") is { } update)
        {
            var parameters = update.GetParameters();
            var args = new object?[parameters.Length];
            args[0] = center;
            args[1] = false;
            for (int i = 2; i < args.Length; i++)
                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : false;
            update.Invoke(layout, args);
        }
        SfInvalidation.InvalidateAll(drawingOnly: false);
    }

    /// <summary>The Windows build's <c>GetCenterItemIndex</c>.</summary>
    private static int CenterItemIndex(View view, object layout, object info, double itemHeight)
    {
        double scrollOffset = SfMembers.Get(layout, "ScrollOffset") is double offset ? offset : 0;
        int count = SfMembers.Get(view, "itemsSource") is System.Collections.ICollection items ? items.Count : 0;
        double viewport = SfMembers.Call(view, "GetViewPortHeight") is double h ? h : view.Height;
        double shown = Math.Round(viewport / itemHeight);
        bool looping = SfMembers.Get(info, "EnableLooping") is true && count > shown;
        int index = (int)Math.Round(scrollOffset / itemHeight);
        if (looping && count > 0)
        {
            index %= count;
            if (index < 0)
                index += count;
        }
        return Math.Max(0, Math.Min(index, count - 1));
    }

    private static MethodInfo? InterfaceMethod(object target, string name)
    {
        foreach (var type in target.GetType().GetInterfaces().Prepend(target.GetType()))
        {
            if (type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == name || m.Name.EndsWith("." + name)) is { } method)
                return method;
        }
        return null;
    }
}
