// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Core.Internals;
using static Microsoft.Maui.Platform.Linux.Syncfusion.SfDyn;
using SfPointerEventArgs = Syncfusion.Maui.Core.Internals.PointerEventArgs;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// SfToolbar's mouse and keyboard handling, which the platform-neutral build
/// leaves out and the Windows build implements:
/// <list type="bullet">
/// <item><b>Hover.</b> The item under the mouse gets its hover background and
/// its tool tip (<c>SfToolbarLayout.HandleTouch</c>, the multi-row layout's
/// <c>OnTouch</c>, <c>ToolbarHelper.UpdateItemHover</c> and
/// <c>UpdateGroupItemHover</c>); leaving it, pressing or releasing clears
/// both. An overlay toolbar's back icon shows its tool tip on hover
/// (<c>SfOverlayToolbar.OnTouchInteraction</c>).</item>
/// <item><b>The "more" menu.</b> Its items get a highlight effect under the
/// mouse (<c>MoreItemsLayout.OnTouch</c>, <c>UpdateMouseHover</c>,
/// <c>MoreItemView.InitializeEffectsView</c>, <c>UpdateMouseOver</c>,
/// <c>RemoveEffects</c>).</item>
/// <item><b>Keyboard.</b> Items, custom-view items, "more" menu items and the
/// navigation and "more" buttons are tab stops (their Windows
/// <c>OnHandlerChanged</c> sets <c>IsTabStop</c>; see
/// <see cref="SfLayoutBridgeHandler"/>), and the item views listen to the
/// keyboard, so Enter activates the focused item.</item>
/// </list>
/// The toolbar is optional, so every type is looked up by name. A patch is
/// applied only over the neutral body; a build that implements a method keeps
/// it.
/// </summary>
internal static class SfToolbarPatches
{
    private const string Asm = "Syncfusion.Maui.Toolbar";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConditionalWeakTable<object, StrongBox<object?>> s_moreHover = new();
    private static readonly ConditionalWeakTable<object, SfEffectsView> s_moreEffects = new();
    private static readonly ConditionalWeakTable<SfEffectsView, StrongBox<(Rect Rect, double Radius)>> s_effectsClips = new();
    private static Type? s_helper;
    private static Type? s_viewHelper;
    private static Type? s_defaultItemView;
    private static Type? s_groupLayout;
    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        var layout = Type("Syncfusion.Maui.Toolbar.SfToolbarLayout", Asm);
        if (layout == null)
            return; // SfToolbar is not part of the app
        s_helper = Type("Syncfusion.Maui.Toolbar.ToolbarHelper", Asm);
        s_viewHelper = Type("Syncfusion.Maui.Toolbar.ToolbarViewHelper", Asm);
        s_defaultItemView = Type("Syncfusion.Maui.Toolbar.DefaultToolbarItemView", Asm);
        s_groupLayout = Type("Syncfusion.Maui.Toolbar.ToolbarGroupLayout", Asm);
        if (s_helper == null || s_defaultItemView == null)
            return;
        var harmony = new Harmony("com.openmaui.syncfusion.toolbar");

        // Hover over the items (and their tool tips).
        if (s_helper.GetMethod("UpdateItemHover", Any) == null)
        {
            Patch(harmony, layout.GetMethod("HandleTouch", Any), nameof(HandleTouch_Postfix), postfix: true);
            Patch(harmony, OnTouchOf(Type("Syncfusion.Maui.Toolbar.MultiRowLayout", Asm)), nameof(MultiRowOnTouch_Postfix), postfix: true);
        }
        var overlay = Type("Syncfusion.Maui.Toolbar.SfOverlayToolbar", Asm);
        var onTouchInteraction = overlay?.GetMethod("OnTouchInteraction", Any);
        if (onTouchInteraction != null && !Calls(onTouchInteraction, "get_PointerDeviceType"))
            Patch(harmony, onTouchInteraction, nameof(OverlayOnTouchInteraction_Prefix), postfix: false);
        // A custom-view item's tool tip is placed as a multi-row item's (Windows' UpdatePopup).
        Patch(harmony, Type("Syncfusion.Maui.Toolbar.ToolbarTooltip", Asm)?.GetMethod("UpdatePopup", Any), nameof(UpdatePopup_Prefix), postfix: false);
        Patch(harmony, s_helper.GetMethod("ShowTooltip", Any), nameof(ShowTooltip_Prefix), postfix: false);

        // The "more" menu's hover effect.
        var moreView = Type("Syncfusion.Maui.Toolbar.MoreItemView", Asm);
        var moreLayout = Type("Syncfusion.Maui.Toolbar.MoreItemsLayout", Asm);
        if (moreView != null && moreLayout != null && moreView.GetMethod("UpdateMouseOver", Any) == null)
        {
            foreach (var ctor in moreView.GetConstructors(Any))
                Patch(harmony, ctor, nameof(MoreItemViewCtor_Postfix), postfix: true);
            Patch(harmony, moreView.GetMethod("OnDraw", Any), nameof(MoreItemViewOnDraw_Postfix), postfix: true);
            Patch(harmony, moreView.GetMethod("Dispose", Any), nameof(MoreItemViewDispose_Postfix), postfix: true);
            Patch(harmony, OnTouchOf(moreLayout), nameof(MoreItemsOnTouch_Postfix), postfix: true);
            Patch(harmony, moreLayout.GetMethod("UpdateMoreItemView", Any), nameof(UpdateMoreItemView_Postfix), postfix: true);
        }

        // Item views listen to the keyboard (Enter activates the focused item).
        foreach (var name in new[] { "DefaultToolbarItemView", "CustomToolbarItemView", "MoreItemView" })
        {
            var type = Type("Syncfusion.Maui.Toolbar." + name, Asm);
            if (type == null || !typeof(IKeyboardListener).IsAssignableFrom(type))
                continue;
            foreach (var ctor in type.GetConstructors(Any))
            {
                if (!Calls(ctor, "AddKeyboardListener"))
                    Patch(harmony, ctor, nameof(AddKeyboardListener_Postfix), postfix: true);
            }
            var dispose = type.GetMethod("Dispose", Any);
            if (dispose != null && !Calls(dispose, "RemoveKeyboardListener"))
                Patch(harmony, dispose, nameof(RemoveKeyboardListener_Postfix), postfix: true);
        }
    }

    /// <summary>The explicit <c>ITouchListener.OnTouch</c> implementation of <paramref name="type"/>.</summary>
    private static MethodInfo? OnTouchOf(Type? type)
    {
        if (type == null || !typeof(ITouchListener).IsAssignableFrom(type))
            return null;
        var map = type.GetInterfaceMap(typeof(ITouchListener));
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (map.InterfaceMethods[i].Name == nameof(ITouchListener.OnTouch) && map.TargetMethods[i].DeclaringType == type)
                return map.TargetMethods[i];
        }
        return null;
    }

    private static void Patch(Harmony harmony, MethodBase? target, string patch, bool postfix)
    {
        if (target == null)
            return;
        try
        {
            var hm = new HarmonyMethod(typeof(SfToolbarPatches).GetMethod(patch, BindingFlags.Static | BindingFlags.NonPublic));
            if (postfix)
                harmony.Patch(target, postfix: hm);
            else
                harmony.Patch(target, prefix: hm);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Patching SfToolbar {target.DeclaringType?.Name}.{target.Name} failed", ex);
        }
    }

    private static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", $"SfToolbar {what} failed", ex);
        }
    }

    #region Item hover

    /// <summary>
    /// The hover half of the Windows <c>SfToolbarLayout.HandleTouch</c>, after
    /// the neutral body (which is the rest of it) has run: a disabled item
    /// under the pointer clears the hover; a mouse move hovers the item under
    /// it and shows its tool tip; leaving, cancelling or releasing clears both.
    /// </summary>
    private static void HandleTouch_Postfix(object __instance, SfPointerEventArgs e, View view) => Guard("hover", () =>
    {
        if (Get(__instance, "toolbar") is not { } toolbar)
            return;
        var item = CallStatic(s_helper!, "GetSelectedToolbarItem", e.TouchPoint, toolbar, view, false);
        var group = Is(__instance, "IsGroupingEnabled") && s_groupLayout?.IsInstanceOfType(view) == true ? view : null;
        void Hover(object? hovered)
        {
            if (group != null)
                UpdateGroupItemHover(hovered, group);
            else
                UpdateItemHover(hovered, __instance);
        }
        ApplyHover(__instance, e, item, Hover, hovered => UpdatePopup(__instance, hovered, false, 0.0));
    });

    /// <summary>The same for the multi-row (extended overflow) layout's <c>OnTouch</c>.</summary>
    private static void MultiRowOnTouch_Postfix(View __instance, SfPointerEventArgs e) => Guard("hover", () =>
    {
        if (Get(__instance, "toolbar") is not { } toolbar)
            return;
        var point = e.TouchPoint;
        if (Is(__instance, "IsRTL"))
            point = new Point(__instance.Width - point.X, point.Y); // as the neutral body hit-tests
        var item = CallStatic(s_helper!, "GetSelectedToolbarItem", point, toolbar, __instance, true);
        double spacing = Get(toolbar, "ItemSpacing") is double s ? s / 2.0 : 0.0;
        ApplyHover(__instance, e, item, hovered => UpdateItemHover(hovered, __instance),
            hovered => UpdatePopup(__instance, hovered, hovered != null, hovered != null ? spacing : 0.0));
    });

    private static void ApplyHover(object layout, SfPointerEventArgs e, object? item, Action<object?> hover, Action<object?> popup)
    {
        if (CallStatic(s_helper!, "GetView", item) is View { IsEnabled: false })
        {
            hover(null);
            return;
        }
        if (e.PointerDeviceType == PointerDeviceType.Mouse)
        {
            if (e.Action == PointerActions.Moved)
            {
                hover(item);
                popup(item);
                Set(layout, "hoverToolbarItem", item);
            }
            else if (e.Action == PointerActions.Exited)
            {
                popup(null);
                hover(null);
                Set(layout, "hoverToolbarItem", null);
            }
        }
        if (e.Action is PointerActions.Exited or PointerActions.Cancelled or PointerActions.Released)
        {
            popup(null);
            hover(null);
            Set(layout, "hoverToolbarItem", null);
        }
    }

    private static void UpdatePopup(object layout, object? item, bool fromMultiRow, double itemSpacing)
    {
        if (Get(layout, "toolbarTooltip") is { } tooltip)
            Call(tooltip, "UpdatePopup", item, fromMultiRow, itemSpacing);
    }

    /// <summary>The Windows <c>ToolbarHelper.UpdateItemHover</c>: the hovered item view of the layout changes to <paramref name="item"/>'s.</summary>
    private static void UpdateItemHover(object? item, object layout)
    {
        var views = Children(layout);
        if (views.FirstOrDefault(v => Is(v, "IsHovered")) is { } hovered)
            Call(hovered, "ClearHover");
        if (Get(item, "DefaultView") is { } view && s_defaultItemView!.IsInstanceOfType(view) && views.Contains(view))
            Call(view, "UpdateHover");
    }

    /// <summary>The Windows <c>ToolbarHelper.UpdateGroupItemHover</c>.</summary>
    private static void UpdateGroupItemHover(object? item, object group)
    {
        if (Children(group).FirstOrDefault(v => Is(v, "IsHovered")) is { } hovered)
            Call(hovered, "ClearHover");
        if (Get(item, "DefaultView") is { } view && s_defaultItemView!.IsInstanceOfType(view))
            Call(view, "UpdateHover");
    }

    private static List<object> Children(object layout) =>
        layout is IEnumerable children ? children.Cast<object>().Where(c => s_defaultItemView!.IsInstanceOfType(c)).ToList() : new List<object>();

    /// <summary>
    /// The Windows <c>SfOverlayToolbar.OnTouchInteraction</c>: the back icon's
    /// tool tip follows the mouse, and a release activates the back icon only
    /// over it.
    /// </summary>
    private static bool OverlayOnTouchInteraction_Prefix(object __instance, SfPointerEventArgs e)
    {
        try
        {
            if (Get(__instance, "BackIcon") is not View backIcon)
                return false;
            bool over = backIcon.Bounds.Contains(e.TouchPoint);
            if (e.Action == PointerActions.Pressed && over)
                Set(__instance, "isPressed", true);
            if (e.PointerDeviceType == PointerDeviceType.Mouse && e.Action is PointerActions.Moved or PointerActions.Exited)
                Call(__instance, "UpdatePopup", over);
            if (e.Action is PointerActions.Exited or PointerActions.Cancelled or PointerActions.Released)
            {
                if (e.Action == PointerActions.Released && Is(__instance, "isPressed") && over)
                {
                    Call(__instance, "OnBackIconTapped");
                    Set(__instance, "isPressed", false);
                }
                Call(__instance, "UpdatePopup", false);
            }
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "SfOverlayToolbar touch failed", ex);
            return true;
        }
    }

    /// <summary>A custom-view item's tool tip is placed as a multi-row item's (the Windows <c>ToolbarTooltip.UpdatePopup</c>).</summary>
    private static void UpdatePopup_Prefix(object? item, ref bool fromMultiRow)
    {
        if (!fromMultiRow && item != null && Get(item, "View") != null)
            fromMultiRow = true;
    }

    /// <summary>
    /// <c>ShowTooltip</c> measures the tool tip before its popup has shown it,
    /// and gives up when that measures nothing. A view without a handler
    /// measures nothing, so the tool tip (hover and long press alike) never
    /// showed; it gets its handler from the toolbar's context first.
    /// </summary>
    private static void ShowTooltip_Prefix(View tooltip, View tooltipView) => Guard("tool tip", () =>
    {
        if (tooltip.Handler == null && tooltipView.Handler?.MauiContext is { } context)
            tooltip.ToHandler(context);
    });

    #endregion

    #region "More" menu hover

    /// <summary>The Windows <c>MoreItemView.InitializeEffectsView</c>, run by its constructor.</summary>
    private static void MoreItemViewCtor_Postfix(SfView __instance) => Guard("more item effects", () =>
    {
        var effects = new SfEffectsView
        {
            ShouldIgnoreTouches = true,
            TouchDownEffects = SfEffects.Selection,
        };
        effects.ClipToBounds = true;
        __instance.Children.Add(effects);
        s_moreEffects.AddOrUpdate(__instance, effects);
    });

    /// <summary>
    /// The clip the Windows <c>MoreItemView.OnDraw</c> gives its effects view
    /// (the item's rounded selection shape), set only when it changes.
    /// </summary>
    private static void MoreItemViewOnDraw_Postfix(object __instance, RectF dirtyRect) => Guard("more item clip", () =>
    {
        if (!s_moreEffects.TryGetValue(__instance, out var effects) || Get(__instance, "toolbarItem") is not { } item)
            return;
        if (Get(item, "IsEnabled") is not true
            || (string.IsNullOrEmpty(Get(item, "Text") as string) && string.IsNullOrEmpty(Get(item, "Name") as string)))
            return;
        double radius = 0;
        if (Get(__instance, "toolbar") is { } toolbar && s_viewHelper != null && Get(toolbar, "SelectionCornerRadius") is double corner)
            radius = CallStatic(s_viewHelper, "GetSelectionCornerRadius", corner) is double r ? r : 0;
        var rect = new Rect(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
        var last = s_effectsClips.GetOrCreateValue(effects);
        if (effects.Clip != null && last.Value == (rect, radius))
            return;
        last.Value = (rect, radius);
        effects.Clip = new RoundRectangleGeometry { CornerRadius = new CornerRadius(radius), Rect = rect };
    });

    private static void MoreItemViewDispose_Postfix(SfView __instance) => Guard("more item dispose", () =>
    {
        if (!s_moreEffects.TryGetValue(__instance, out var effects))
            return;
        s_moreEffects.Remove(__instance);
        if (__instance.Children.Contains(effects))
        {
            __instance.Children.Remove(effects);
            effects.Handler?.DisconnectHandler();
        }
    });

    /// <summary>The mouse half of the Windows <c>MoreItemsLayout.OnTouch</c>: the item under the mouse is highlighted.</summary>
    private static void MoreItemsOnTouch_Postfix(object __instance, SfPointerEventArgs e) => Guard("more menu hover", () =>
    {
        if (e.PointerDeviceType != PointerDeviceType.Mouse)
            return;
        var previous = s_moreHover.GetOrCreateValue(__instance);
        if (e.Action == PointerActions.Moved)
        {
            var item = Call(__instance, "GetSelectedMoreItem", e.TouchPoint);
            if (!ReferenceEquals(item, previous.Value))
            {
                if (previous.Value != null)
                    MoreItemHover(__instance, previous.Value, false);
                if (item != null)
                    MoreItemHover(__instance, item, true);
            }
            previous.Value = item;
        }
        else if (e.Action == PointerActions.Exited)
        {
            MoreItemHover(__instance, previous.Value, false);
            previous.Value = null;
        }
    });

    /// <summary>The Windows <c>MoreItemsLayout.UpdateMoreItemView</c> also resets the effects of both items.</summary>
    private static void UpdateMoreItemView_Postfix(object __instance, object? item, object? previousItem) => Guard("more menu selection", () =>
    {
        foreach (var each in new[] { item, previousItem })
        {
            if (MoreItemViewOf(__instance, each) is { } view && s_moreEffects.TryGetValue(view, out var effects))
                effects.Reset();
        }
    });

    private static object? MoreItemViewOf(object layout, object? item) =>
        item != null && Get(layout, "itemViewMap") is IDictionary map && map.Contains(item) ? map[item] : null;

    /// <summary>The Windows <c>MoreItemView.UpdateMouseOver</c>.</summary>
    private static void MoreItemHover(object layout, object? item, bool over)
    {
        if (MoreItemViewOf(layout, item) is not SfView view)
            return;
        Call(view, "InvalidateDrawable");
        if (!s_moreEffects.TryGetValue(view, out var effects))
            return;
        if (over)
            effects.ApplyEffects(SfEffects.Highlight, RippleStartPosition.Default, null, false);
        else
            effects.Reset();
    }

    #endregion

    #region Keyboard

    private static void AddKeyboardListener_Postfix(View __instance) => Guard("keyboard", () =>
    {
        if (__instance is IKeyboardListener listener)
            __instance.AddKeyboardListener(listener);
    });

    private static void RemoveKeyboardListener_Postfix(View __instance) => Guard("keyboard", () =>
    {
        if (__instance is IKeyboardListener listener)
            __instance.RemoveKeyboardListener(listener);
    });

    #endregion
}
