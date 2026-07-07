using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Manages gesture recognition and processing for MAUI views on Linux.
/// Handles tap, pan, swipe, pinch, and pointer gestures.
/// </summary>
public static class GestureManager
{
    private const string Tag = "GestureManager";

    private class GestureTrackingState
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsPanning { get; set; }
        public bool IsPressed { get; set; }
        public bool IsPinching { get; set; }
        public double PinchScale { get; set; } = 1.0;
        // Set once the press+move threshold has offered this gesture to the
        // drag path — whether or not a native drag actually started — so a
        // cancelled/empty DragStarting doesn't retrigger on every move.
        public bool DragStarted { get; set; }
    }

    private enum PointerEventType
    {
        Entered,
        Exited,
        Pressed,
        Moved,
        Released
    }

    /// <summary>
    /// Finds and invokes an internal MAUI gesture method, handling signature changes across versions.
    /// Caches the resolved method for performance.
    /// </summary>
    private static bool InvokeGestureMethod(
        ref MethodInfo? cached,
        Type recognizerType,
        string methodName,
        object recognizerInstance,
        View view,
        Func<MethodInfo, object[]> buildArgs)
        => InvokeGestureMethod(ref cached, recognizerType, methodName, recognizerInstance, view, buildArgs, out _);

    /// <summary>
    /// Overload exposing the invoked method's return value — SendDragStarting
    /// returns the DragStartingEventArgs the drag path needs (Cancel + Data).
    /// </summary>
    private static bool InvokeGestureMethod(
        ref MethodInfo? cached,
        Type recognizerType,
        string methodName,
        object recognizerInstance,
        View view,
        Func<MethodInfo, object[]> buildArgs,
        out object? result)
    {
        result = null;

        if (cached == null)
        {
            var methods = recognizerType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == methodName)
                .ToArray();

            if (methods.Length == 0)
            {
                DiagnosticLog.Warn(Tag, $"No {methodName} method found on {recognizerType.Name}");
                return false;
            }

            if (methods.Length == 1)
            {
                cached = methods[0];
            }
            else
            {
                // Multiple overloads — prefer ones with Func parameters (newer MAUI)
                cached = methods.FirstOrDefault(m =>
                    m.GetParameters().Any(p => p.ParameterType.Name.Contains("Func")))
                    ?? methods[0];
            }
        }

        try
        {
            var args = buildArgs(cached);
            result = cached.Invoke(recognizerInstance, args);
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, $"{methodName} invocation failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Builds argument array matching the method's parameter types.
    /// Handles View, Func&lt;IElement, Point?&gt;, and other parameter types.
    /// </summary>
    private static object[] BuildAdaptiveArgs(MethodInfo method, View view, double x, double y, params (Type type, object value)[] extras)
    {
        var parameters = method.GetParameters();
        var args = new object[parameters.Length];
        int extraIdx = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;

            if (pType.IsAssignableFrom(typeof(View)) || pType.IsAssignableFrom(view.GetType()))
            {
                args[i] = view;
            }
            else if (pType.Name.Contains("Func"))
            {
                // GetPosition resolver — see CreatePositionResolver for the
                // coordinate-space contract and null semantics.
                args[i] = CreateResolverForParameter(pType, x, y)!;
            }
            else if (extraIdx < extras.Length && (pType == extras[extraIdx].type || pType.IsAssignableFrom(extras[extraIdx].type)))
            {
                args[i] = extras[extraIdx++].value;
            }
            else if (extraIdx < extras.Length)
            {
                // Try to convert
                args[i] = extras[extraIdx++].value;
            }
            else
            {
                args[i] = pType.IsValueType ? Activator.CreateInstance(pType)! : null!;
            }
        }

        return args;
    }

    #region GetPosition resolvers

    /// <summary>
    /// Builds the GetPosition resolver MAUI event args invoke lazily.
    ///
    /// COORDINATE-SPACE CONTRACT: every x/y GestureManager receives is in
    /// window-logical space — physical pixels divided by DpiScale with the
    /// Wayland CSD titlebar inset removed (ScalePointerArgs in
    /// LinuxApplication.Input) — which is the same space SkiaView Bounds,
    /// ScreenBounds and HitTest operate in, so no further DPI/CSD adjustment
    /// happens here.
    ///
    /// MAUI GetPosition semantics:
    ///   GetPosition(null)    → the point in window coordinates.
    ///   GetPosition(element) → the point relative to the element's top-left,
    ///                          via the platform view's ScreenBounds (not
    ///                          Bounds) so ancestor scroll offsets are
    ///                          respected.
    ///   unresolvable element → null (no handler / no platform SkiaView).
    /// Never throws — MAUI calls the resolver lazily from app handlers,
    /// possibly after the gesture completed.
    /// </summary>
    internal static Func<IElement?, Point?> CreatePositionResolver(double x, double y)
        => relativeTo => ResolvePosition(x, y, relativeTo);

    private static Point? ResolvePosition(double x, double y, IElement? relativeTo)
    {
        try
        {
            if (relativeTo == null)
                return new Point(x, y);

            if (relativeTo.Handler?.PlatformView is Microsoft.Maui.Platform.SkiaView platformView)
                return ResolvePositionCore(x, y, platformView.ScreenBounds);

            return null;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug(Tag, $"GetPosition resolver failed: {ex.Message}");
            return null;
        }
    }

    // Pure translation math, split out for headless tests: a window-logical
    // point becomes element-relative given the element's ScreenBounds; null
    // bounds = element not realized → null per the MAUI contract.
    internal static Point? ResolvePositionCore(double x, double y, Microsoft.Maui.Graphics.Rect? elementScreenBounds)
    {
        if (elementScreenBounds is not { } bounds)
            return null;
        return new Point(x - bounds.Left, y - bounds.Top);
    }

    /// <summary>
    /// Adapt the resolver to whatever Func&lt;TElement, Point?&gt; the reflected
    /// MAUI signature asks for (IElement? vs Element vs VisualElement across
    /// versions). Returns null when the parameter isn't a compatible Func —
    /// the invocation then proceeds with a null resolver, which MAUI treats
    /// as "position unavailable".
    /// </summary>
    private static object? CreateResolverForParameter(Type funcParameterType, double x, double y)
    {
        try
        {
            var resolver = CreatePositionResolver(x, y);
            if (funcParameterType.IsInstanceOfType(resolver))
                return resolver;

            if (!funcParameterType.IsGenericType
                || funcParameterType.GetGenericTypeDefinition() != typeof(Func<,>))
                return null;

            var genericArgs = funcParameterType.GetGenericArguments();
            if (genericArgs[1] != typeof(Point?))
                return null;

            return typeof(GestureManager)
                .GetMethod(nameof(MakeTypedResolver), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(genericArgs[0])
                .Invoke(null, new object[] { resolver });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug(Tag, $"Position resolver adaptation failed: {ex.Message}");
            return null;
        }
    }

    private static Func<T, Point?> MakeTypedResolver<T>(Func<IElement?, Point?> resolver) where T : class
        => arg => resolver(arg as IElement);

    #endregion

    // Cached reflection MethodInfo for internal MAUI methods
    private static MethodInfo? _sendTappedMethod;
    private static MethodInfo? _sendSwipedMethod;
    private static MethodInfo? _sendPanMethod;
    private static MethodInfo? _sendPinchMethod;
    private static MethodInfo? _sendDragStartingMethod;
    private static MethodInfo? _sendDragOverMethod;
    private static MethodInfo? _sendDragLeaveMethod;
    private static MethodInfo? _sendDropMethod;
    private static readonly Dictionary<PointerEventType, MethodInfo?> _pointerMethodCache = new();

    private static readonly Dictionary<View, (DateTime lastTap, int tapCount)> _tapTracking = new();
    private static readonly Dictionary<View, GestureTrackingState> _gestureState = new();

    /// <summary>
    /// Minimum distance in pixels for a swipe gesture to be recognized.
    /// </summary>
    public static double SwipeMinDistance { get; set; } = 50.0;

    /// <summary>
    /// Maximum time in milliseconds for a swipe gesture to be recognized.
    /// </summary>
    public static double SwipeMaxTime { get; set; } = 500.0;

    /// <summary>
    /// Ratio threshold for determining swipe direction dominance.
    /// </summary>
    public static double SwipeDirectionThreshold { get; set; } = 0.5;

    /// <summary>
    /// Minimum distance in pixels before a pan gesture is recognized.
    /// </summary>
    public static double PanMinDistance { get; set; } = 10.0;

    /// <summary>
    /// Scale factor per scroll unit for pinch-via-scroll gestures.
    /// </summary>
    public static double PinchScrollScale { get; set; } = 0.1;

    /// <summary>
    /// Removes tracking entries for the specified view, preventing memory leaks
    /// when views are disconnected from the visual tree.
    /// </summary>
    public static void CleanupView(View view)
    {
        if (view == null) return;
        _tapTracking.Remove(view);
        _gestureState.Remove(view);
    }

    /// <summary>
    /// Processes a tap gesture on the specified view.
    /// </summary>
    public static bool ProcessTap(View? view, double x, double y)
    {
        if (view == null)
        {
            return false;
        }
        var current = view;
        while (current != null)
        {
            var recognizers = current.GestureRecognizers;
            if (recognizers != null && recognizers.Count > 0 && ProcessTapOnView(current, x, y))
            {
                return true;
            }
            var parent = current.Parent;
            current = (parent is View parentView) ? parentView : null;
        }
        return false;
    }

    private static bool ProcessTapOnView(View view, double x, double y)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null || recognizers.Count == 0)
        {
            return false;
        }
        bool result = false;
        foreach (var item in recognizers)
        {
            if (item is not TapGestureRecognizer tapRecognizer)
            {
                continue;
            }
            DiagnosticLog.Debug(Tag,
                $"Processing TapGestureRecognizer on {view.GetType().Name}, CommandParameter={tapRecognizer.CommandParameter}, NumberOfTapsRequired={tapRecognizer.NumberOfTapsRequired}");

            int numberOfTapsRequired = tapRecognizer.NumberOfTapsRequired;
            if (numberOfTapsRequired > 1)
            {
                DateTime utcNow = DateTime.UtcNow;
                if (!_tapTracking.TryGetValue(view, out var tracking))
                {
                    _tapTracking[view] = (utcNow, 1);
                    DiagnosticLog.Debug(Tag, $"First tap 1/{numberOfTapsRequired}");
                    continue;
                }
                if (!((utcNow - tracking.lastTap).TotalMilliseconds < 300.0))
                {
                    _tapTracking[view] = (utcNow, 1);
                    DiagnosticLog.Debug(Tag, $"Tap timeout, reset to 1/{numberOfTapsRequired}");
                    continue;
                }
                int tapCount = tracking.tapCount + 1;
                if (tapCount < numberOfTapsRequired)
                {
                    _tapTracking[view] = (utcNow, tapCount);
                    DiagnosticLog.Debug(Tag, $"Tap {tapCount}/{numberOfTapsRequired}, waiting for more taps");
                    continue;
                }
                _tapTracking.Remove(view);
            }

            // Try to raise the Tapped event via cached reflection
            bool eventFired = false;
            try
            {
                eventFired = InvokeGestureMethod(
                    ref _sendTappedMethod,
                    typeof(TapGestureRecognizer),
                    "SendTapped",
                    tapRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, x, y,
                        (typeof(TappedEventArgs), new TappedEventArgs(tapRecognizer.CommandParameter))));
                if (eventFired)
                    DiagnosticLog.Debug(Tag, "SendTapped invoked successfully");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendTapped failed", ex);
            }

            // Always invoke the Command if available (SendTapped may or may not invoke it internally)
            if (!eventFired)
            {
                ICommand? command = tapRecognizer.Command;
                if (command != null && command.CanExecute(tapRecognizer.CommandParameter))
                {
                    DiagnosticLog.Debug(Tag, "Executing TapGestureRecognizer Command");
                    command.Execute(tapRecognizer.CommandParameter);
                }
            }

            result = true;
        }
        return result;
    }

    /// <summary>
    /// Checks if the view has any gesture recognizers.
    /// </summary>
    public static bool HasGestureRecognizers(View? view)
    {
        if (view == null)
        {
            return false;
        }
        return view.GestureRecognizers?.Count > 0;
    }

    /// <summary>
    /// Checks if the view has a tap gesture recognizer.
    /// </summary>
    public static bool HasTapGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is TapGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Processes a pointer down event.
    /// </summary>
    public static void ProcessPointerDown(View? view, double x, double y)
    {
        if (view != null)
        {
            _gestureState[view] = new GestureTrackingState
            {
                StartX = x,
                StartY = y,
                CurrentX = x,
                CurrentY = y,
                StartTime = DateTime.UtcNow,
                IsPanning = false,
                IsPressed = true
            };
            ProcessPointerEvent(view, x, y, PointerEventType.Pressed);
        }
    }

    /// <summary>
    /// Processes a pointer move event.
    /// </summary>
    public static void ProcessPointerMove(View? view, double x, double y)
    {
        if (view == null)
        {
            return;
        }
        if (!_gestureState.TryGetValue(view, out var state))
        {
            ProcessPointerEvent(view, x, y, PointerEventType.Moved);
            return;
        }
        state.CurrentX = x;
        state.CurrentY = y;
        if (!state.IsPressed)
        {
            ProcessPointerEvent(view, x, y, PointerEventType.Moved);
            return;
        }
        double deltaX = x - state.StartX;
        double deltaY = y - state.StartY;
        if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) >= PanMinDistance)
        {
            // Press-then-move on a view with an enabled DragGestureRecognizer
            // starts a native drag instead of a pan. Offered exactly once per
            // press; if the handler cancels or supplies no payload we fall
            // through to normal panning.
            if (!state.DragStarted && !state.IsPanning && HasEnabledDragRecognizer(view))
            {
                state.DragStarted = true;
                if (StartDrag(view, x, y))
                {
                    // The native session (compositor grab on Wayland, pointer
                    // grab on X11) now owns the pointer; motion and release
                    // stop reaching this view, so don't begin a pan.
                    return;
                }
            }

            ProcessPanGesture(view, deltaX, deltaY, (GestureStatus)(state.IsPanning ? 1 : 0));
            state.IsPanning = true;
        }
        ProcessPointerEvent(view, x, y, PointerEventType.Moved);
    }

    private static bool HasEnabledDragRecognizer(View view)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return false;
        foreach (var r in recognizers)
        {
            if (r is DragGestureRecognizer { CanDrag: true })
                return true;
        }
        return false;
    }

    /// <summary>
    /// Processes a pointer up event.
    /// </summary>
    public static void ProcessPointerUp(View? view, double x, double y)
    {
        if (view == null)
        {
            return;
        }
        if (_gestureState.TryGetValue(view, out var state))
        {
            state.CurrentX = x;
            state.CurrentY = y;
            double deltaX = x - state.StartX;
            double deltaY = y - state.StartY;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            double elapsed = (DateTime.UtcNow - state.StartTime).TotalMilliseconds;
            if (distance >= SwipeMinDistance && elapsed <= SwipeMaxTime)
            {
                var direction = DetermineSwipeDirection(deltaX, deltaY);
                if (direction != SwipeDirection.Right)
                {
                    ProcessSwipeGesture(view, direction);
                }
                else if (Math.Abs(deltaX) > Math.Abs(deltaY) * SwipeDirectionThreshold)
                {
                    ProcessSwipeGesture(view, (deltaX > 0.0) ? SwipeDirection.Right : SwipeDirection.Left);
                }
            }
            if (state.IsPanning)
            {
                ProcessPanGesture(view, deltaX, deltaY, (GestureStatus)2);
            }
            else if (distance < 15.0 && elapsed < SwipeMaxTime)
            {
                DiagnosticLog.Debug(Tag, $"Detected tap on {view.GetType().Name} (distance={distance:F1}, elapsed={elapsed:F0}ms)");
                ProcessTap(view, x, y);
            }
            _gestureState.Remove(view);
        }
        ProcessPointerEvent(view, x, y, PointerEventType.Released);
    }

    /// <summary>
    /// Processes a pointer entered event.
    /// </summary>
    public static void ProcessPointerEntered(View? view, double x, double y)
    {
        if (view != null)
        {
            ProcessPointerEvent(view, x, y, PointerEventType.Entered);
        }
    }

    /// <summary>
    /// Processes a pointer exited event.
    /// </summary>
    public static void ProcessPointerExited(View? view, double x, double y)
    {
        if (view != null)
        {
            ProcessPointerEvent(view, x, y, PointerEventType.Exited);
        }
    }

    private static SwipeDirection DetermineSwipeDirection(double deltaX, double deltaY)
    {
        double absX = Math.Abs(deltaX);
        double absY = Math.Abs(deltaY);
        if (absX > absY * SwipeDirectionThreshold)
        {
            if (deltaX > 0.0)
            {
                return SwipeDirection.Right;
            }
            return SwipeDirection.Left;
        }
        if (absY > absX * SwipeDirectionThreshold)
        {
            if (deltaY > 0.0)
            {
                return SwipeDirection.Down;
            }
            return SwipeDirection.Up;
        }
        if (deltaX > 0.0)
        {
            return SwipeDirection.Right;
        }
        return SwipeDirection.Left;
    }

    private static void ProcessSwipeGesture(View view, SwipeDirection direction)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null)
        {
            return;
        }
        foreach (var item in recognizers)
        {
            if (item is not SwipeGestureRecognizer swipeRecognizer || !swipeRecognizer.Direction.HasFlag(direction))
            {
                continue;
            }
            DiagnosticLog.Debug(Tag, $"Swipe detected: {direction}");

            try
            {
                bool invoked = InvokeGestureMethod(
                    ref _sendSwipedMethod,
                    typeof(SwipeGestureRecognizer),
                    "SendSwiped",
                    swipeRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, 0, 0,
                        (typeof(SwipeDirection), direction)));
                if (invoked)
                    DiagnosticLog.Debug(Tag, "SendSwiped invoked successfully");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendSwiped failed", ex);
            }

            ICommand? command = swipeRecognizer.Command;
            if (command != null && command.CanExecute(swipeRecognizer.CommandParameter))
            {
                command.Execute(swipeRecognizer.CommandParameter);
            }
        }
    }

    private static void ProcessPanGesture(View view, double totalX, double totalY, GestureStatus status)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null)
        {
            return;
        }
        foreach (var item in recognizers)
        {
            if (item is not PanGestureRecognizer panRecognizer)
            {
                continue;
            }
            DiagnosticLog.Debug(Tag, $"Pan gesture: status={status}, totalX={totalX:F1}, totalY={totalY:F1}");

            try
            {
                InvokeGestureMethod(
                    ref _sendPanMethod,
                    typeof(PanGestureRecognizer),
                    "SendPan",
                    panRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, totalX, totalY,
                        (typeof(double), totalX),
                        (typeof(double), totalY),
                        (typeof(int), (int)status),
                        (typeof(GestureStatus), status)));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendPan failed", ex);
            }
        }
    }

    private static void ProcessPointerEvent(View view, double x, double y, PointerEventType eventType)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null)
        {
            return;
        }
        foreach (var item in recognizers)
        {
            if (item is not PointerGestureRecognizer pointerRecognizer)
            {
                continue;
            }
            try
            {
                string? methodName = eventType switch
                {
                    PointerEventType.Entered => "SendPointerEntered",
                    PointerEventType.Exited => "SendPointerExited",
                    PointerEventType.Pressed => "SendPointerPressed",
                    PointerEventType.Moved => "SendPointerMoved",
                    PointerEventType.Released => "SendPointerReleased",
                    _ => null,
                };
                if (methodName == null)
                {
                    continue;
                }

                if (!_pointerMethodCache.TryGetValue(eventType, out var method))
                {
                    var methods = typeof(PointerGestureRecognizer).GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == methodName)
                        .ToArray();
                    method = methods.FirstOrDefault(m =>
                        m.GetParameters().Any(p => p.ParameterType.Name.Contains("Func")))
                        ?? methods.FirstOrDefault();
                    _pointerMethodCache[eventType] = method;
                }

                if (method != null)
                {
                    var pointerArgs = BuildAdaptiveArgs(method, view, x, y,
                        (typeof(object), CreatePointerEventArgs(view, x, y)));
                    method.Invoke(pointerRecognizer, pointerArgs);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, $"Pointer event {eventType} failed", ex);
            }
        }
    }

    private static object CreatePointerEventArgs(View view, double x, double y)
    {
        try
        {
            var type = typeof(PointerGestureRecognizer).Assembly.GetType("Microsoft.Maui.Controls.PointerEventArgs");
            if (type != null)
            {
                var ctor = type.GetConstructors().FirstOrDefault();
                if (ctor != null)
                {
                    return ctor.Invoke(new object[0]);
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug("GestureManager", "PointerEventArgs creation failed", ex);
        }
        return null!;
    }

    /// <summary>
    /// Processes a scroll event that may be a pinch gesture (Ctrl+Scroll).
    /// Returns true if the scroll was consumed as a pinch gesture.
    /// </summary>
    public static bool ProcessScrollAsPinch(View? view, double x, double y, double deltaY, bool isCtrlPressed)
    {
        if (view == null || !isCtrlPressed)
        {
            return false;
        }

        // Check if view has a pinch gesture recognizer
        if (!HasPinchGestureRecognizer(view))
        {
            return false;
        }

        // Get or create gesture state
        if (!_gestureState.TryGetValue(view, out var state))
        {
            state = new GestureTrackingState
            {
                StartX = x,
                StartY = y,
                CurrentX = x,
                CurrentY = y,
                StartTime = DateTime.UtcNow,
                PinchScale = 1.0
            };
            _gestureState[view] = state;
        }

        // Calculate new scale based on scroll delta
        double scaleDelta = 1.0 + (deltaY * PinchScrollScale);
        state.PinchScale *= scaleDelta;

        // Clamp scale to reasonable bounds
        state.PinchScale = Math.Clamp(state.PinchScale, 0.1, 10.0);

        GestureStatus status;
        if (!state.IsPinching)
        {
            state.IsPinching = true;
            status = GestureStatus.Started;
        }
        else
        {
            status = GestureStatus.Running;
        }

        ProcessPinchGesture(view, state.PinchScale, x, y, status);
        return true;
    }

    /// <summary>
    /// Ends an ongoing pinch gesture.
    /// </summary>
    public static void EndPinchGesture(View? view)
    {
        if (view == null) return;

        if (_gestureState.TryGetValue(view, out var state) && state.IsPinching)
        {
            ProcessPinchGesture(view, state.PinchScale, state.CurrentX, state.CurrentY, GestureStatus.Completed);
            state.IsPinching = false;
            state.PinchScale = 1.0;
        }
    }

    private static void ProcessPinchGesture(View view, double scale, double originX, double originY, GestureStatus status)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null)
        {
            return;
        }

        foreach (var item in recognizers)
        {
            if (item is not PinchGestureRecognizer pinchRecognizer)
            {
                continue;
            }

            DiagnosticLog.Debug(Tag, $"Pinch gesture: status={status}, scale={scale:F2}, origin=({originX:F0},{originY:F0})");

            try
            {
                var scaleOrigin = new Point(originX / view.Width, originY / view.Height);
                bool invoked = InvokeGestureMethod(
                    ref _sendPinchMethod,
                    typeof(PinchGestureRecognizer),
                    "SendPinch",
                    pinchRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, originX, originY,
                        (typeof(double), scale),
                        (typeof(Point), scaleOrigin),
                        (typeof(GestureStatus), status)));
                if (invoked)
                    DiagnosticLog.Debug(Tag, "SendPinch invoked successfully");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendPinch failed", ex);
            }
        }
    }

    /// <summary>
    /// Checks if the view has a pinch gesture recognizer.
    /// </summary>
    public static bool HasPinchGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is PinchGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the view has a swipe gesture recognizer.
    /// </summary>
    public static bool HasSwipeGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is SwipeGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the view has a pan gesture recognizer.
    /// </summary>
    public static bool HasPanGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is PanGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the view has a pointer gesture recognizer.
    /// </summary>
    public static bool HasPointerGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is PointerGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the view has a drag gesture recognizer.
    /// </summary>
    public static bool HasDragGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is DragGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the view has a drop gesture recognizer.
    /// </summary>
    public static bool HasDropGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null)
        {
            return false;
        }
        foreach (var recognizer in view.GestureRecognizers)
        {
            if (recognizer is DropGestureRecognizer)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Initiates a drag operation from the specified view: raises MAUI's
    /// DragStarting (DragGestureRecognizer.SendDragStarting), and — unless the
    /// handler cancelled — starts a native drag session carrying the
    /// DataPackage's text via <see cref="DragDropService.TryStartDrag(string)"/>
    /// (Wayland wl_data_device or X11 XDND, chosen by session type). Returns
    /// true when a native drag session actually started.
    /// </summary>
    public static bool StartDrag(View? view, double x, double y)
    {
        if (view == null) return false;

        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return false;

        foreach (var item in recognizers)
        {
            if (item is not DragGestureRecognizer dragRecognizer) continue;
            if (!dragRecognizer.CanDrag) continue;

            DiagnosticLog.Debug(Tag, $"Starting drag from {view.GetType().Name}");

            object? result;
            try
            {
                if (!InvokeGestureMethod(
                        ref _sendDragStartingMethod,
                        typeof(DragGestureRecognizer),
                        "SendDragStarting",
                        dragRecognizer,
                        view,
                        method => BuildAdaptiveArgs(method, view, x, y),
                        out result))
                    continue;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendDragStarting failed", ex);
                continue;
            }

            // SendDragStarting returns the DragStartingEventArgs — honor the
            // handler's cancellation, then feed the DataPackage into the
            // native drag path.
            if (result is not DragStartingEventArgs args)
            {
                DiagnosticLog.Debug(Tag, "SendDragStarting returned no DragStartingEventArgs; native drag not started");
                continue;
            }

            if (args.Cancel)
            {
                DiagnosticLog.Debug(Tag, "DragStarting cancelled by handler");
                continue;
            }

            var text = ExtractDragText(args.Data);
            if (string.IsNullOrEmpty(text))
            {
                // TODO: source file/image payloads (DataPackage.Image, file
                // lists) once the native sources offer more than text MIMEs.
                DiagnosticLog.Debug(Tag, "DataPackage has no text payload; native drag not started");
                continue;
            }

            if (DragDropService.Default.TryStartDrag(text))
            {
                DiagnosticLog.Debug(Tag, "Native drag session started");
                return true;
            }

            DiagnosticLog.Debug(Tag, "Native drag unavailable (backend not ready or no recent press serial)");
        }

        return false;
    }

    // Pull the text payload out of a DataPackage: DataPackage.Text first, then
    // common Properties conventions — a "Text" key (any casing), else the
    // first string value.
    private static string? ExtractDragText(DataPackage? data)
    {
        if (data == null) return null;
        if (!string.IsNullOrEmpty(data.Text)) return data.Text;

        string? firstString = null;
        foreach (var kvp in data.Properties)
        {
            if (kvp.Value is not string s || s.Length == 0) continue;
            if (string.Equals(kvp.Key, "Text", StringComparison.OrdinalIgnoreCase))
                return s;
            firstString ??= s;
        }
        return firstString;
    }

    #region Incoming native DnD → DropGestureRecognizer adapter

    // MAUI's DropGestureRecognizer plumbing is internal: SendDragOver /
    // SendDragLeave / SendDrop, plus the DragEventArgs/DropEventArgs and
    // DataPackageView construction paths, vary across versions. Everything
    // below goes through the same cached-reflection machinery as the rest of
    // this file and degrades to debug logs — it must NEVER throw into the
    // input path.

    private static ConstructorInfo? _controlsDragEventArgsCtor;
    private static ConstructorInfo? _controlsDropEventArgsCtor;
    private static PropertyInfo? _dataPackageViewProperty;

    /// <summary>
    /// Conventional key under which dropped file paths are exposed on
    /// <see cref="DataPackage.Properties"/> — MAUI's DataPackage has no
    /// first-class file member.
    /// </summary>
    public const string FilePathsPropertyKey = "FilePaths";

    /// <summary>
    /// Resolve the MAUI-level drop target for a hit-tested view: the view
    /// itself or its nearest ancestor carrying an enabled (AllowDrop)
    /// DropGestureRecognizer. Null when nothing in the chain accepts drops.
    /// </summary>
    public static View? FindDropTarget(View? view)
    {
        for (var current = view; current != null; current = current.Parent as View)
        {
            var recognizers = current.GestureRecognizers;
            if (recognizers == null) continue;
            foreach (var r in recognizers)
            {
                if (r is DropGestureRecognizer { AllowDrop: true })
                    return current;
            }
        }
        return null;
    }

    /// <summary>
    /// Raises MAUI DragOver on the view's enabled DropGestureRecognizers.
    /// Returns null when no recognizer participated (leave native accept
    /// unchanged), true when at least one left AcceptedOperation != None,
    /// false when every participating recognizer set None (explicit reject).
    /// </summary>
    public static bool? ProcessDragOver(View? view, double x, double y)
    {
        if (view?.GestureRecognizers == null) return null;

        bool sawRecognizer = false;
        bool accepted = false;

        foreach (var item in view.GestureRecognizers)
        {
            if (item is not DropGestureRecognizer { AllowDrop: true } dropRecognizer) continue;

            var args = BuildControlsDragEventArgs(x, y);
            if (args == null) return null; // MAUI internals shifted — degrade gracefully

            if (!InvokeGestureMethod(
                    ref _sendDragOverMethod,
                    typeof(DropGestureRecognizer),
                    "SendDragOver",
                    dropRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, x, y,
                        (typeof(Microsoft.Maui.Controls.DragEventArgs), args))))
                continue;

            sawRecognizer = true;
            if (args.AcceptedOperation != DataPackageOperation.None)
                accepted = true;
        }

        return sawRecognizer ? accepted : null;
    }

    /// <summary>
    /// Raises MAUI DragLeave on the view's enabled DropGestureRecognizers.
    /// </summary>
    public static void ProcessDragLeave(View? view)
    {
        if (view?.GestureRecognizers == null) return;

        foreach (var item in view.GestureRecognizers)
        {
            if (item is not DropGestureRecognizer { AllowDrop: true } dropRecognizer) continue;

            var args = BuildControlsDragEventArgs(0, 0);
            if (args == null) return;

            InvokeGestureMethod(
                ref _sendDragLeaveMethod,
                typeof(DropGestureRecognizer),
                "SendDragLeave",
                dropRecognizer,
                view,
                method => BuildAdaptiveArgs(method, view, 0, 0,
                    (typeof(Microsoft.Maui.Controls.DragEventArgs), args)));
        }
    }

    /// <summary>
    /// Raises MAUI Drop on the view's enabled DropGestureRecognizers with a
    /// DataPackage carrying the dropped text and file paths (the latter under
    /// <see cref="FilePathsPropertyKey"/> in Properties). SendDrop returns a
    /// Task in MAUI — faults are observed with a logged continuation; the
    /// input path is never blocked on it.
    /// </summary>
    public static void ProcessDrop(View? view, double x, double y, string? text, string[]? filePaths)
    {
        if (view?.GestureRecognizers == null) return;

        foreach (var item in view.GestureRecognizers)
        {
            if (item is not DropGestureRecognizer { AllowDrop: true } dropRecognizer) continue;

            DiagnosticLog.Debug(Tag, $"Drop on {view.GetType().Name}");

            var dropArgs = BuildControlsDropEventArgs(x, y, text, filePaths);
            if (dropArgs == null) return;

            if (!InvokeGestureMethod(
                    ref _sendDropMethod,
                    typeof(DropGestureRecognizer),
                    "SendDrop",
                    dropRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, x, y,
                        (typeof(Microsoft.Maui.Controls.DropEventArgs), dropArgs)),
                    out var result))
                continue;

            if (result is System.Threading.Tasks.Task task)
            {
                task.ContinueWith(
                    t => DiagnosticLog.Error(Tag, "Drop handler faulted", t.Exception!),
                    System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    private static DataPackage BuildDataPackage(string? text, string[]? filePaths)
    {
        var package = new DataPackage();
        if (!string.IsNullOrEmpty(text))
            package.Text = text;
        if (filePaths is { Length: > 0 })
            package.Properties[FilePathsPropertyKey] = filePaths;
        return package;
    }

    private static Microsoft.Maui.Controls.DragEventArgs? BuildControlsDragEventArgs(double x, double y)
    {
        try
        {
            // The payload isn't readable until drop on either backend, so the
            // over/leave args carry an empty DataPackage.
            var package = BuildDataPackage(null, null);
            _controlsDragEventArgsCtor ??= SelectCtor(typeof(Microsoft.Maui.Controls.DragEventArgs), typeof(DataPackage));
            if (_controlsDragEventArgsCtor == null)
            {
                DiagnosticLog.Warn(Tag, "No usable Controls.DragEventArgs constructor found");
                return null;
            }
            return (Microsoft.Maui.Controls.DragEventArgs?)InvokeCtorAdaptive(_controlsDragEventArgsCtor, package, x, y);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, "BuildControlsDragEventArgs failed", ex);
            return null;
        }
    }

    private static Microsoft.Maui.Controls.DropEventArgs? BuildControlsDropEventArgs(double x, double y, string? text, string[]? filePaths)
    {
        try
        {
            var package = BuildDataPackage(text, filePaths);

            // DropEventArgs wants a DataPackageView; both its constructor and
            // DataPackage.View are internal.
            _dataPackageViewProperty ??= typeof(DataPackage).GetProperty(
                "View", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var packageView = _dataPackageViewProperty?.GetValue(package);
            if (packageView == null)
            {
                DiagnosticLog.Warn(Tag, "DataPackage.View unavailable; drop not delivered to recognizer");
                return null;
            }

            _controlsDropEventArgsCtor ??= SelectCtor(typeof(Microsoft.Maui.Controls.DropEventArgs), packageView.GetType());
            if (_controlsDropEventArgsCtor == null)
            {
                DiagnosticLog.Warn(Tag, "No usable Controls.DropEventArgs constructor found");
                return null;
            }
            return (Microsoft.Maui.Controls.DropEventArgs?)InvokeCtorAdaptive(_controlsDropEventArgsCtor, packageView, x, y);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, "BuildControlsDropEventArgs failed", ex);
            return null;
        }
    }

    // Pick a constructor whose first parameter accepts the payload type,
    // PREFERRING one with a Func parameter — that's the GetPosition resolver
    // slot (MAUI 10: DragEventArgs(DataPackage, Func<IElement, Point?>,
    // PlatformDragEventArgs)); the resolver-less overload would silently lose
    // positions. Among equals, fewest parameters wins.
    private static ConstructorInfo? SelectCtor(Type type, Type firstArgType)
    {
        return type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(c =>
            {
                var p = c.GetParameters();
                return p.Length > 0 && p[0].ParameterType.IsAssignableFrom(firstArgType);
            })
            .OrderByDescending(c => c.GetParameters().Any(p => p.ParameterType.Name.Contains("Func")))
            .ThenBy(c => c.GetParameters().Length)
            .FirstOrDefault();
    }

    // First param = payload; Func params get a position resolver; anything
    // else gets null/default — the same adaptive shape BuildAdaptiveArgs uses.
    private static object? InvokeCtorAdaptive(ConstructorInfo ctor, object firstArg, double x, double y)
    {
        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;
            if (i == 0)
            {
                args[i] = firstArg;
            }
            else if (pType.Name.Contains("Func"))
            {
                // Same shared GetPosition resolver as BuildAdaptiveArgs.
                args[i] = CreateResolverForParameter(pType, x, y);
            }
            else
            {
                args[i] = pType.IsValueType ? Activator.CreateInstance(pType) : null;
            }
        }
        return ctor.Invoke(args);
    }

    #endregion
}
