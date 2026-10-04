// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// CommunityToolkit.Maui's <c>TouchBehavior</c> and <c>ImageTouchBehavior</c> on Linux. The
/// toolkit's platform-neutral build, the one a Linux app runs, never sets the behavior's element
/// nor feeds it pointer events (its Windows build does both in <c>OnAttachedTo</c>), so there were
/// no pressed or hover states (colours, scale, opacity, translation, rotation, images) and neither
/// <c>Command</c> nor <c>LongPressCommand</c> ran. Here the behavior is attached as on Windows once
/// MAUI's platform behavior has attached it (the view is loaded and has its Skia view): its element
/// is set, then the view's routed pointer events drive the toolkit's own touch, hover and
/// interaction state machine with the same transitions as the Windows pointer handlers. No
/// compile-time reference: the toolkit is optional.
/// </summary>
internal static class ToolkitTouchBehavior
{
    private const string TouchBehaviorTypeName = "CommunityToolkit.Maui.Behaviors.TouchBehavior";
    private const string ImageTouchBehaviorTypeName = "CommunityToolkit.Maui.Behaviors.ImageTouchBehavior";

    // CommunityToolkit.Maui.Core enums: TouchStatus, HoverStatus, TouchInteractionStatus.
    private const int TouchStarted = 0, TouchCompleted = 1, TouchCanceled = 2;
    private const int HoverEntered = 0, HoverExited = 1;
    private const int InteractionStarted = 0, InteractionCompleted = 1;

    private static int s_installed;

    /// <summary>The live link of each attached behavior (its view and pointer feed).</summary>
    private static readonly ConditionalWeakTable<BindableObject, Link> s_links = new();

    private sealed class Link
    {
        public required BindableObject Behavior;
        public required VisualElement Element;
        public required Contract Contract;
        public SkiaView? PlatformView;
        public EventHandler<RoutedPointerEventArgs>? OnPointer;
        public EventHandler? OnLoaded, OnUnloaded, OnHandlerChanged;
        public bool IsPressed;
        public bool Connected;
    }

    /// <summary>
    /// Hooks behavior attach and detach (MAUI's <c>Behavior</c>, the non-generic base every
    /// behavior shares). Only the toolkit's touch behaviors are acted on.
    /// </summary>
    internal static void Install(Harmony harmony)
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
            return;
        if (Contract.TouchBehaviorType == null)
            return; // the toolkit is not part of the app

        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var attach = typeof(Behavior).GetMethods(Instance).FirstOrDefault(m => m.Name.EndsWith("AttachTo", StringComparison.Ordinal) && m.GetParameters().Length == 1);
        var detach = typeof(Behavior).GetMethods(Instance).FirstOrDefault(m => m.Name.EndsWith("DetachFrom", StringComparison.Ordinal) && m.GetParameters().Length == 1);
        if (attach == null || detach == null)
        {
            DiagnosticLog.Warn("ToolkitTouchBehavior", "Behavior.AttachTo/DetachFrom not found; TouchBehavior is not bridged");
            return;
        }
        harmony.Patch(attach, postfix: new HarmonyMethod(typeof(ToolkitTouchBehavior).GetMethod(nameof(AttachTo_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
        harmony.Patch(detach, postfix: new HarmonyMethod(typeof(ToolkitTouchBehavior).GetMethod(nameof(DetachFrom_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
    }

    private static void AttachTo_Postfix(Behavior __instance, BindableObject bindable)
    {
        try
        {
            if (bindable is VisualElement element && Contract.For(__instance.GetType()) is { } contract)
                Track(__instance, element, contract);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ToolkitTouchBehavior", "Attaching TouchBehavior failed", ex);
        }
    }

    private static void DetachFrom_Postfix(Behavior __instance)
    {
        try
        {
            if (s_links.TryGetValue(__instance, out var link))
                Untrack(link);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("ToolkitTouchBehavior", "Detaching TouchBehavior failed", ex);
        }
    }

    private static void Track(BindableObject behavior, VisualElement element, Contract contract)
    {
        if (s_links.TryGetValue(behavior, out var old))
            Untrack(old);

        var link = new Link { Behavior = behavior, Element = element, Contract = contract };
        // Subscribed after MAUI's PlatformBehavior (it subscribed in AttachTo, before this
        // postfix), so the toolkit's own attach (it sets the behavior's view) has run first.
        link.OnLoaded = (_, _) => Connect(link);
        link.OnUnloaded = (_, _) => Disconnect(link);
        link.OnHandlerChanged = (_, _) =>
        {
            if (!link.Connected)
                return;
            Disconnect(link);
            Connect(link);
        };
        element.Loaded += link.OnLoaded;
        element.Unloaded += link.OnUnloaded;
        element.HandlerChanged += link.OnHandlerChanged;
        s_links.AddOrUpdate(behavior, link);

        if (element.IsLoaded && element.Handler?.PlatformView is SkiaView)
            Connect(link);
    }

    private static void Untrack(Link link)
    {
        Disconnect(link);
        link.Element.Loaded -= link.OnLoaded;
        link.Element.Unloaded -= link.OnUnloaded;
        link.Element.HandlerChanged -= link.OnHandlerChanged;
        s_links.Remove(link.Behavior);
    }

    /// <summary>What the Windows build's <c>OnAttachedTo</c> does after the shared attach.</summary>
    private static void Connect(Link link)
    {
        if (link.Connected || link.Element.Handler?.PlatformView is not SkiaView platformView)
            return;
        var c = link.Contract;
        if (c.IsImageTouchBehavior && link.Element is not (Image or ImageButton))
        {
            // Windows throws InvalidOperationException from OnAttachedTo (inside Loaded, so the app
            // crashes). Reported instead; the behavior stays inert as the misuse makes it.
            DiagnosticLog.Error("ToolkitTouchBehavior", "ImageTouchBehavior can only be attached to an Image or ImageButton");
            return;
        }

        link.Connected = true;
        link.PlatformView = platformView;
        link.IsPressed = false;
        // Element = bindable: resets the gesture manager, makes a layout's children input
        // transparent when asked, and applies the default state without animation.
        c.SetElement(link.Behavior, link.Element);
        link.OnPointer = (_, e) => OnPointer(link, e);
        platformView.PointerRouted += link.OnPointer;
    }

    /// <summary>What the Windows build's <c>OnDetachedFrom</c> does.</summary>
    private static void Disconnect(Link link)
    {
        if (!link.Connected)
            return;
        link.Connected = false;
        if (link.PlatformView != null && link.OnPointer != null)
            link.PlatformView.PointerRouted -= link.OnPointer;
        link.PlatformView = null;
        link.OnPointer = null;
        if (link.Contract.IsEnabled(link.Behavior))
            link.Contract.SetElement(link.Behavior, null);
        link.IsPressed = false;
    }

    /// <summary>
    /// The Windows pointer handlers (PointerEntered/Exited/Pressed/Released/Canceled), fed from the
    /// view's routed pointer events. The window keeps the pointer on the pressed view (no enter or
    /// leave while pressed), so leaving and re-entering the view during a press is followed from
    /// the pointer position, as WinUI raises PointerExited/PointerEntered for a captured pointer.
    /// </summary>
    private static void OnPointer(Link link, RoutedPointerEventArgs e)
    {
        var c = link.Contract;
        var behavior = link.Behavior;
        if (!link.Connected || !c.HasElement(behavior) || !c.IsEnabled(behavior))
            return;

        try
        {
            bool inside = IsInside(link, e.Pointer);
            switch (e.Kind)
            {
                case SkiaView.RoutedPointerKind.Entered:
                    if (inside && !IsHovered(link))
                        PointerEntered(link);
                    break;

                case SkiaView.RoutedPointerKind.Exited:
                    // A child taking the pointer over is not leaving the view.
                    if (!inside && IsHovered(link))
                        PointerExited(link);
                    break;

                case SkiaView.RoutedPointerKind.Moved:
                    if (inside && !IsHovered(link))
                        PointerEntered(link);
                    else if (!inside && IsHovered(link))
                        PointerExited(link);
                    break;

                case SkiaView.RoutedPointerKind.Pressed:
                    // A press arrives over the view (a touch, or a click without a prior move):
                    // WinUI raises PointerEntered first.
                    if (!IsHovered(link))
                        PointerEntered(link);
                    link.IsPressed = true;
                    c.HandleUserInteraction(behavior, InteractionStarted);
                    c.HandleTouch(behavior, TouchStarted);
                    break;

                case SkiaView.RoutedPointerKind.Released:
                    if (!inside && IsHovered(link))
                        PointerExited(link);
                    if (link.IsPressed && c.GetHoverStatus(behavior) == HoverEntered)
                        c.HandleTouch(behavior, TouchCompleted);
                    else if (c.GetHoverStatus(behavior) != HoverExited)
                        c.HandleTouch(behavior, TouchCanceled);
                    c.HandleUserInteraction(behavior, InteractionCompleted);
                    link.IsPressed = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            // An exception out of a pointer observer would take the input pipeline down.
            DiagnosticLog.Error("ToolkitTouchBehavior", $"TouchBehavior failed handling {e.Kind}", ex);
        }
    }

    private static void PointerEntered(Link link)
    {
        link.Contract.HandleHover(link.Behavior, HoverEntered);
        if (link.IsPressed)
            link.Contract.HandleTouch(link.Behavior, TouchStarted);
    }

    private static void PointerExited(Link link)
    {
        if (link.IsPressed)
            link.Contract.HandleTouch(link.Behavior, TouchCanceled);
        link.Contract.HandleHover(link.Behavior, HoverExited);
    }

    private static bool IsHovered(Link link) => link.Contract.GetHoverStatus(link.Behavior) == HoverEntered;

    private static bool IsInside(Link link, PointerEventArgs e)
    {
        if (link.PlatformView is not { } view || float.IsNaN(e.X) || float.IsNaN(e.Y))
            return false;
        var bounds = view.ScreenBounds;
        return e.X >= bounds.Left && e.X <= bounds.Right && e.Y >= bounds.Top && e.Y <= bounds.Bottom;
    }

    /// <summary>The TouchBehavior members the bridge drives, bound by name once per type.</summary>
    internal sealed class Contract
    {
        private static readonly Dictionary<Type, Contract?> s_cache = new();

        internal static Type? TouchBehaviorType { get; } = ResolveTouchBehaviorType();

        private readonly PropertyInfo _element, _isEnabled, _hoverStatus;
        private readonly MethodInfo _handleTouch, _handleHover, _handleInteraction;
        private readonly Type _touchStatus, _hoverStatusType, _interactionStatus;

        public bool IsImageTouchBehavior { get; }

        private Contract(Type behaviorType, Type touchBehavior)
        {
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _element = touchBehavior.GetProperty("Element", All)!;
            _isEnabled = touchBehavior.GetProperty("IsEnabled", All)!;
            _hoverStatus = touchBehavior.GetProperty("CurrentHoverStatus", All)!;
            _handleTouch = touchBehavior.GetMethod("HandleTouch", All)!;
            _handleHover = touchBehavior.GetMethod("HandleHover", All)!;
            _handleInteraction = touchBehavior.GetMethod("HandleUserInteraction", All)!;
            _touchStatus = _handleTouch.GetParameters()[0].ParameterType;
            _hoverStatusType = _handleHover.GetParameters()[0].ParameterType;
            _interactionStatus = _handleInteraction.GetParameters()[0].ParameterType;
            for (var t = behaviorType; t != null; t = t.BaseType)
            {
                if (t.FullName == ImageTouchBehaviorTypeName)
                    IsImageTouchBehavior = true;
            }
        }

        private static Type? ResolveTouchBehaviorType()
        {
            try
            {
                return Type.GetType($"{TouchBehaviorTypeName}, CommunityToolkit.Maui", throwOnError: false);
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException)
            {
                return null;
            }
        }

        /// <summary>The contract for a behavior type, or null when it is not a toolkit TouchBehavior.</summary>
        public static Contract? For(Type behaviorType)
        {
            if (TouchBehaviorType is not { } touchBehavior || !touchBehavior.IsAssignableFrom(behaviorType))
                return null;
            lock (s_cache)
            {
                if (s_cache.TryGetValue(behaviorType, out var cached))
                    return cached;
                Contract? contract = null;
                try
                {
                    contract = new Contract(behaviorType, touchBehavior);
                }
                catch (Exception ex) when (ex is NullReferenceException or AmbiguousMatchException or IndexOutOfRangeException)
                {
                    DiagnosticLog.Error("ToolkitTouchBehavior", $"{behaviorType.FullName} does not have the expected TouchBehavior members; it is not bridged");
                }
                s_cache[behaviorType] = contract;
                return contract;
            }
        }

        public void SetElement(object behavior, VisualElement? element) => _element.SetValue(behavior, element);

        public bool HasElement(object behavior) => _element.GetValue(behavior) != null;

        public bool IsEnabled(object behavior) => _isEnabled.GetValue(behavior) is true;

        public int GetHoverStatus(object behavior) => Convert.ToInt32(_hoverStatus.GetValue(behavior), System.Globalization.CultureInfo.InvariantCulture);

        public void HandleTouch(object behavior, int status) => Invoke(_handleTouch, behavior, Enum.ToObject(_touchStatus, status));

        public void HandleHover(object behavior, int status) => Invoke(_handleHover, behavior, Enum.ToObject(_hoverStatusType, status));

        public void HandleUserInteraction(object behavior, int status) => Invoke(_handleInteraction, behavior, Enum.ToObject(_interactionStatus, status));

        private static void Invoke(MethodInfo method, object behavior, object argument)
        {
            try
            {
                method.Invoke(behavior, new[] { argument });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex.InnerException);
            }
        }
    }
}
