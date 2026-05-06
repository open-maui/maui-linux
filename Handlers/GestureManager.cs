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
    {
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
            cached.Invoke(recognizerInstance, args);
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
                // Position resolver: Func<IElement, Point?>
                Func<IElement, Point?> getPosition = (_) => new Point(x, y);
                args[i] = getPosition;
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

    // Cached reflection MethodInfo for internal MAUI methods
    private static MethodInfo? _sendTappedMethod;
    private static MethodInfo? _sendSwipedMethod;
    private static MethodInfo? _sendPanMethod;
    private static MethodInfo? _sendPinchMethod;
    private static MethodInfo? _sendDragStartingMethod;
    private static MethodInfo? _sendDragOverMethod;
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
            ProcessPanGesture(view, deltaX, deltaY, (GestureStatus)(state.IsPanning ? 1 : 0));
            state.IsPanning = true;
        }
        ProcessPointerEvent(view, x, y, PointerEventType.Moved);
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
    /// Initiates a drag operation from the specified view.
    /// </summary>
    public static void StartDrag(View? view, double x, double y)
    {
        if (view == null) return;

        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return;

        foreach (var item in recognizers)
        {
            if (item is not DragGestureRecognizer dragRecognizer) continue;

            DiagnosticLog.Debug(Tag, $"Starting drag from {view.GetType().Name}");

            try
            {
                bool invoked = InvokeGestureMethod(
                    ref _sendDragStartingMethod,
                    typeof(DragGestureRecognizer),
                    "SendDragStarting",
                    dragRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, x, y));
                if (invoked)
                    DiagnosticLog.Debug(Tag, "SendDragStarting invoked successfully");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendDragStarting failed", ex);
            }
        }
    }

    /// <summary>
    /// Processes a drag enter event on the specified view.
    /// </summary>
    public static void ProcessDragEnter(View? view, double x, double y, object? data)
    {
        if (view == null) return;

        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return;

        foreach (var item in recognizers)
        {
            if (item is not DropGestureRecognizer dropRecognizer) continue;

            DiagnosticLog.Debug(Tag, $"Drag enter on {view.GetType().Name}");

            try
            {
                InvokeGestureMethod(
                    ref _sendDragOverMethod,
                    typeof(DropGestureRecognizer),
                    "SendDragOver",
                    dropRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, x, y));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendDragOver failed", ex);
            }
        }
    }

    /// <summary>
    /// Processes a drop event on the specified view.
    /// </summary>
    public static void ProcessDrop(View? view, double x, double y, object? data)
    {
        if (view == null) return;

        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return;

        foreach (var item in recognizers)
        {
            if (item is not DropGestureRecognizer dropRecognizer) continue;

            DiagnosticLog.Debug(Tag, $"Drop on {view.GetType().Name}");

            try
            {
                InvokeGestureMethod(
                    ref _sendDropMethod,
                    typeof(DropGestureRecognizer),
                    "SendDrop",
                    dropRecognizer,
                    view,
                    method => BuildAdaptiveArgs(method, view, x, y));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendDrop failed", ex);
            }
        }
    }
}
