using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Manages gesture recognition and processing for MAUI views on Linux.
/// Handles tap, pan, swipe, pinch, and pointer gestures.
/// </summary>
public static class GestureManager
{
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

    private static MethodInfo? _sendTappedMethod;
    private static MethodInfo? _sendPinchMethod;
    private static readonly Dictionary<View, (DateTime lastTap, int tapCount)> _tapTracking = new Dictionary<View, (DateTime, int)>();
    private static readonly Dictionary<View, GestureTrackingState> _gestureState = new Dictionary<View, GestureTrackingState>();

    private const double SwipeMinDistance = 50.0;
    private const double SwipeMaxTime = 500.0;
    private const double SwipeDirectionThreshold = 0.5;
    private const double PanMinDistance = 10.0;
    private const double PinchScrollScale = 0.1; // Scale factor per scroll unit

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
            var tapRecognizer = (item is TapGestureRecognizer) ? (TapGestureRecognizer)item : null;
            if (tapRecognizer == null)
            {
                continue;
            }
            Console.WriteLine($"[GestureManager] Processing TapGestureRecognizer on {view.GetType().Name}, CommandParameter={tapRecognizer.CommandParameter}, NumberOfTapsRequired={tapRecognizer.NumberOfTapsRequired}");
            int numberOfTapsRequired = tapRecognizer.NumberOfTapsRequired;
            if (numberOfTapsRequired > 1)
            {
                DateTime utcNow = DateTime.UtcNow;
                if (!_tapTracking.TryGetValue(view, out var tracking))
                {
                    _tapTracking[view] = (utcNow, 1);
                    Console.WriteLine($"[GestureManager] First tap 1/{numberOfTapsRequired}");
                    continue;
                }
                if (!((utcNow - tracking.lastTap).TotalMilliseconds < 300.0))
                {
                    _tapTracking[view] = (utcNow, 1);
                    Console.WriteLine($"[GestureManager] Tap timeout, reset to 1/{numberOfTapsRequired}");
                    continue;
                }
                int tapCount = tracking.tapCount + 1;
                if (tapCount < numberOfTapsRequired)
                {
                    _tapTracking[view] = (utcNow, tapCount);
                    Console.WriteLine($"[GestureManager] Tap {tapCount}/{numberOfTapsRequired}, waiting for more taps");
                    continue;
                }
                _tapTracking.Remove(view);
            }
            bool eventFired = false;
            try
            {
                if (_sendTappedMethod == null)
                {
                    _sendTappedMethod = typeof(TapGestureRecognizer).GetMethod("SendTapped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_sendTappedMethod != null)
                {
                    Console.WriteLine($"[GestureManager] Found SendTapped method with {_sendTappedMethod.GetParameters().Length} params");
                    var args = new TappedEventArgs(tapRecognizer.CommandParameter);
                    _sendTappedMethod.Invoke(tapRecognizer, new object[] { view, args });
                    Console.WriteLine("[GestureManager] SendTapped invoked successfully");
                    eventFired = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GestureManager] SendTapped failed: " + ex.Message);
            }
            if (!eventFired)
            {
                try
                {
                    var field = typeof(TapGestureRecognizer).GetField("Tapped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? typeof(TapGestureRecognizer).GetField("_tapped", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && field.GetValue(tapRecognizer) is EventHandler<TappedEventArgs> handler)
                    {
                        Console.WriteLine("[GestureManager] Invoking Tapped event directly");
                        var args = new TappedEventArgs(tapRecognizer.CommandParameter);
                        handler(tapRecognizer, args);
                        eventFired = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GestureManager] Direct event invoke failed: " + ex.Message);
                }
            }
            if (!eventFired)
            {
                try
                {
                    string[] fieldNames = new string[] { "TappedEvent", "_TappedHandler", "<Tapped>k__BackingField" };
                    foreach (string fieldName in fieldNames)
                    {
                        var field = typeof(TapGestureRecognizer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                        if (field != null)
                        {
                            Console.WriteLine("[GestureManager] Found field: " + fieldName);
                            if (field.GetValue(tapRecognizer) is EventHandler<TappedEventArgs> handler)
                            {
                                var args = new TappedEventArgs(tapRecognizer.CommandParameter);
                                handler(tapRecognizer, args);
                                Console.WriteLine("[GestureManager] Event fired via " + fieldName);
                                eventFired = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GestureManager] Backing field approach failed: " + ex.Message);
                }
            }
            if (!eventFired)
            {
                Console.WriteLine("[GestureManager] Could not fire event, dumping type info...");
                var methods = typeof(TapGestureRecognizer).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var method in methods)
                {
                    if (method.Name.Contains("Tap", StringComparison.OrdinalIgnoreCase) || method.Name.Contains("Send", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[GestureManager]   Method: {method.Name}({string.Join(", ", from p in method.GetParameters() select p.ParameterType.Name)})");
                    }
                }
            }
            ICommand? command = tapRecognizer.Command;
            if (command != null && command.CanExecute(tapRecognizer.CommandParameter))
            {
                Console.WriteLine("[GestureManager] Executing Command");
                command.Execute(tapRecognizer.CommandParameter);
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
        if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) >= 10.0)
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
            if (distance >= 50.0 && elapsed <= 500.0)
            {
                var direction = DetermineSwipeDirection(deltaX, deltaY);
                if (direction != SwipeDirection.Right)
                {
                    ProcessSwipeGesture(view, direction);
                }
                else if (Math.Abs(deltaX) > Math.Abs(deltaY) * 0.5)
                {
                    ProcessSwipeGesture(view, (deltaX > 0.0) ? SwipeDirection.Right : SwipeDirection.Left);
                }
            }
            if (state.IsPanning)
            {
                ProcessPanGesture(view, deltaX, deltaY, (GestureStatus)2);
            }
            else if (distance < 15.0 && elapsed < 500.0)
            {
                Console.WriteLine($"[GestureManager] Detected tap on {view.GetType().Name} (distance={distance:F1}, elapsed={elapsed:F0}ms)");
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
        if (absX > absY * 0.5)
        {
            if (deltaX > 0.0)
            {
                return SwipeDirection.Right;
            }
            return SwipeDirection.Left;
        }
        if (absY > absX * 0.5)
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
            var swipeRecognizer = (item is SwipeGestureRecognizer) ? (SwipeGestureRecognizer)item : null;
            if (swipeRecognizer == null || !swipeRecognizer.Direction.HasFlag(direction))
            {
                continue;
            }
            Console.WriteLine($"[GestureManager] Swipe detected: {direction}");
            try
            {
                var method = typeof(SwipeGestureRecognizer).GetMethod("SendSwiped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(swipeRecognizer, new object[] { view, direction });
                    Console.WriteLine("[GestureManager] SendSwiped invoked successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GestureManager] SendSwiped failed: " + ex.Message);
            }
            ICommand? command = swipeRecognizer.Command;
            if (command != null && command.CanExecute(swipeRecognizer.CommandParameter))
            {
                swipeRecognizer.Command.Execute(swipeRecognizer.CommandParameter);
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
            var panRecognizer = (item is PanGestureRecognizer) ? (PanGestureRecognizer)item : null;
            if (panRecognizer == null)
            {
                continue;
            }
            Console.WriteLine($"[GestureManager] Pan gesture: status={status}, totalX={totalX:F1}, totalY={totalY:F1}");
            try
            {
                var method = typeof(PanGestureRecognizer).GetMethod("SendPan", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(panRecognizer, new object[]
                    {
                        view,
                        totalX,
                        totalY,
                        (int)status
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GestureManager] SendPan failed: " + ex.Message);
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
            var pointerRecognizer = (item is PointerGestureRecognizer) ? (PointerGestureRecognizer)item : null;
            if (pointerRecognizer == null)
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
                if (methodName != null)
                {
                    var method = typeof(PointerGestureRecognizer).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        var args = CreatePointerEventArgs(view, x, y);
                        method.Invoke(pointerRecognizer, new object[] { view, args });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GestureManager] Pointer event failed: " + ex.Message);
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
        catch
        {
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
            var pinchRecognizer = item as PinchGestureRecognizer;
            if (pinchRecognizer == null)
            {
                continue;
            }

            Console.WriteLine($"[GestureManager] Pinch gesture: status={status}, scale={scale:F2}, origin=({originX:F0},{originY:F0})");

            try
            {
                // Cache the method lookup
                if (_sendPinchMethod == null)
                {
                    _sendPinchMethod = typeof(PinchGestureRecognizer).GetMethod("SendPinch",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (_sendPinchMethod != null)
                {
                    // SendPinch(IView sender, double scale, Point scaleOrigin, GestureStatus status)
                    var scaleOrigin = new Point(originX / view.Width, originY / view.Height);
                    _sendPinchMethod.Invoke(pinchRecognizer, new object[]
                    {
                        view,
                        scale,
                        scaleOrigin,
                        status
                    });
                    Console.WriteLine("[GestureManager] SendPinch invoked successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GestureManager] SendPinch failed: {ex.Message}");
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
            var dragRecognizer = item as DragGestureRecognizer;
            if (dragRecognizer == null) continue;

            Console.WriteLine($"[GestureManager] Starting drag from {view.GetType().Name}");

            try
            {
                // Create DragStartingEventArgs and invoke SendDragStarting
                var method = typeof(DragGestureRecognizer).GetMethod("SendDragStarting",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (method != null)
                {
                    method.Invoke(dragRecognizer, new object[] { view });
                    Console.WriteLine("[GestureManager] SendDragStarting invoked successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GestureManager] SendDragStarting failed: {ex.Message}");
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
            var dropRecognizer = item as DropGestureRecognizer;
            if (dropRecognizer == null) continue;

            Console.WriteLine($"[GestureManager] Drag enter on {view.GetType().Name}");

            try
            {
                var method = typeof(DropGestureRecognizer).GetMethod("SendDragOver",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (method != null)
                {
                    method.Invoke(dropRecognizer, new object[] { view });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GestureManager] SendDragOver failed: {ex.Message}");
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
            var dropRecognizer = item as DropGestureRecognizer;
            if (dropRecognizer == null) continue;

            Console.WriteLine($"[GestureManager] Drop on {view.GetType().Name}");

            try
            {
                var method = typeof(DropGestureRecognizer).GetMethod("SendDrop",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (method != null)
                {
                    method.Invoke(dropRecognizer, new object[] { view });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GestureManager] SendDrop failed: {ex.Message}");
            }
        }
    }
}
