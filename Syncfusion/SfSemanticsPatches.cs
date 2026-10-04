// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Graphics.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Accessibility of what Syncfusion's views draw. An SfView describes its
/// drawn parts (a calendar's days, a rating's stars, a chip's close button)
/// as semantics nodes; on Windows its automation peer lists them as children
/// (<c>CustomAutomationPeer</c>: a button for a node that takes touch, text
/// for the others) and <c>SfViewHandler.InvalidateSemantics</c> makes the
/// peer read them again after the control changed them. The platform-neutral
/// handler's <c>InvalidateSemantics</c> is empty, and OpenMaui's bridge
/// handler is not that handler. Here the bridge's view lists the nodes as
/// accessible children (<see cref="SkiaSfLayout"/>), and SfView's
/// <c>InvalidateSemantics</c> drops the cached list so assistive technology
/// reads it again.
/// </summary>
internal static class SfSemanticsPatches
{
    private static readonly MethodInfo? s_getNodes = typeof(SfView).Assembly
        .GetType("Syncfusion.Maui.Core.Semantics.ISemanticsProvider")
        ?.GetMethod("GetSemanticsNodes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static int s_installed;

    internal static void Install()
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        try
        {
            var original = typeof(SfView).GetMethod("InvalidateSemantics", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (original == null)
                return;
            new Harmony("com.openmaui.syncfusion.semantics").Patch(original,
                postfix: new HarmonyMethod(typeof(SfSemanticsPatches).GetMethod(nameof(InvalidateSemantics_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "Patching SfView semantics failed", ex);
        }
    }

    private static void InvalidateSemantics_Postfix(SfView __instance)
    {
        if (__instance.Handler?.PlatformView is SkiaView view)
            view.InvalidateAccessibleChildren();
    }

    /// <summary>The view's semantics nodes as accessible children (none when it has no nodes).</summary>
    internal static List<IAccessible> NodesOf(SkiaView owner, View? view)
    {
        var result = new List<IAccessible>();
        if (view == null || s_getNodes == null || !s_getNodes.DeclaringType!.IsInstanceOfType(view))
            return result;
        try
        {
            if (s_getNodes.Invoke(view, [view.Width, view.Height]) is IEnumerable nodes)
            {
                foreach (var node in nodes.OfType<SemanticsNode>())
                    result.Add(new SfSemanticsAccessible(owner, node));
            }
        }
        catch (TargetInvocationException ex)
        {
            DiagnosticLog.Error("Syncfusion", $"Reading the semantics of {view.GetType().Name} failed", ex.InnerException ?? ex);
        }
        return result;
    }
}

/// <summary>
/// One drawn part of a Syncfusion view as assistive technology sees it: the
/// Windows build's <c>CustomButtonAutomationPeer</c> (a node that takes
/// touch; its click action runs the node's OnClick) or
/// <c>CustomTextAutomationPeer</c> (the others).
/// </summary>
internal sealed class SfSemanticsAccessible : IAccessible
{
    private readonly SkiaView _owner;
    private readonly SemanticsNode _node;
    private readonly string _id = Guid.NewGuid().ToString();

    internal SfSemanticsAccessible(SkiaView owner, SemanticsNode node)
    {
        _owner = owner;
        _node = node;
    }

    internal SemanticsNode Node => _node;

    public string AccessibleId => _id;
    public string AccessibleName => _node.Text ?? string.Empty;
    public string AccessibleDescription => string.Empty;
    public AccessibleRole Role => _node.IsTouchEnabled ? AccessibleRole.Button : AccessibleRole.Label;

    public AccessibleStates States
    {
        get
        {
            var states = AccessibleStates.Visible | AccessibleStates.Showing;
            if (_owner.IsEnabled)
                states |= AccessibleStates.Enabled | AccessibleStates.Sensitive;
            return states;
        }
    }

    public IAccessible? Parent => _owner;
    public IReadOnlyList<IAccessible> Children => Array.Empty<IAccessible>();

    public AccessibleRect Bounds
    {
        get
        {
            var origin = _owner.ScreenBounds;
            var b = _node.Bounds;
            return new AccessibleRect((int)(origin.Left + b.X), (int)(origin.Top + b.Y), (int)b.Width, (int)b.Height);
        }
    }

    public IReadOnlyList<AccessibleAction> Actions => _node.IsTouchEnabled
        ? [new AccessibleAction { Name = "click", Description = _node.Text ?? string.Empty }]
        : Array.Empty<AccessibleAction>();

    public double? Value => null;
    public double? MinValue => null;
    public double? MaxValue => null;

    public bool DoAction(string actionName)
    {
        if (actionName != "click" || !_node.IsTouchEnabled || _node.OnClick == null)
            return false;
        try
        {
            _node.OnClick(_node);
            SfInvalidation.InvalidateAll(drawingOnly: false);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Syncfusion", "A semantics node's click failed", ex);
            return false;
        }
    }

    public bool SetValue(double value) => false;
}
