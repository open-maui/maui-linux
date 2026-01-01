using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Manages gesture recognition and processing for MAUI views on Linux.
/// Handles tap, pan, swipe, and pointer gestures.
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
    private static readonly Dictionary<View, (DateTime lastTap, int tapCount)> _tapTracking = new();
    private static readonly Dictionary<View, GestureTrackingState> _gestureState = new();

    private const double SwipeMinDistance = 50.0;
    private const double SwipeMaxTime = 500.0;
    private const double SwipeDirectionThreshold = 0.5;
    private const double PanMinDistance = 10.0;

    /// <summary>
    /// Processes a tap gesture on the specified view.
    /// </summary>
    public static bool ProcessTap(View? view, double x, double y)
    {
        if (view == null) return false;

        var current = view;
        while (current != null)
        {
            var recognizers = current.GestureRecognizers;
            if (recognizers?.Count > 0 && ProcessTapOnView(current, x, y))
            {
                return true;
            }
            current = current.Parent as View;
        }
        return false;
    }

    private static bool ProcessTapOnView(View view, double x, double y)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null || recognizers.Count == 0)
            return false;

        bool result = false;
        foreach (var recognizer in recognizers)
        {
            if (recognizer is not TapGestureRecognizer tapRecognizer)
                continue;

            Console.WriteLine($"[GestureManager] Processing TapGestureRecognizer on {view.GetType().Name}, CommandParameter={tapRecognizer.CommandParameter}, NumberOfTapsRequired={tapRecognizer.NumberOfTapsRequired}");

            int requiredTaps = tapRecognizer.NumberOfTapsRequired;
            if (requiredTaps > 1)
            {
                var now = DateTime.UtcNow;
                if (!_tapTracking.TryGetValue(view, out var tracking))
                {
                    _tapTracking[view] = (now, 1);
                    Console.WriteLine($"[GestureManager] First tap 1/{requiredTaps}");
                    continue;
                }

                if ((now - tracking.lastTap).TotalMilliseconds >= 300.0)
                {
                    _tapTracking[view] = (now, 1);
                    Console.WriteLine($"[GestureManager] Tap timeout, reset to 1/{requiredTaps}");
                    continue;
                }

                int tapCount = tracking.tapCount + 1;
                if (tapCount < requiredTaps)
                {
                    _tapTracking[view] = (now, tapCount);
                    Console.WriteLine($"[GestureManager] Tap {tapCount}/{requiredTaps}, waiting for more taps");
                    continue;
                }

                _tapTracking.Remove(view);
            }

            bool eventFired = false;

            // Try SendTapped method
            try
            {
                _sendTappedMethod ??= typeof(TapGestureRecognizer).GetMethod("SendTapped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

            // Try direct event invocation
            if (!eventFired)
            {
                try
                {
                    var field = typeof(TapGestureRecognizer).GetField("Tapped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? typeof(TapGestureRecognizer).GetField("_tapped", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field?.GetValue(tapRecognizer) is EventHandler<TappedEventArgs> handler)
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

            // Execute command if available
            if (tapRecognizer.Command?.CanExecute(tapRecognizer.CommandParameter) == true)
            {
                Console.WriteLine("[GestureManager] Executing Command");
                tapRecognizer.Command.Execute(tapRecognizer.CommandParameter);
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
        return view?.GestureRecognizers?.Count > 0;
    }

    /// <summary>
    /// Checks if the view has a tap gesture recognizer.
    /// </summary>
    public static bool HasTapGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null) return false;
        return view.GestureRecognizers.Any(g => g is TapGestureRecognizer);
    }

    /// <summary>
    /// Processes a pointer down event.
    /// </summary>
    public static void ProcessPointerDown(View? view, double x, double y)
    {
        if (view == null) return;

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

    /// <summary>
    /// Processes a pointer move event.
    /// </summary>
    public static void ProcessPointerMove(View? view, double x, double y)
    {
        if (view == null) return;

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
        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        if (distance >= PanMinDistance)
        {
            ProcessPanGesture(view, deltaX, deltaY, state.IsPanning ? GestureStatus.Running : GestureStatus.Started);
            state.IsPanning = true;
        }

        ProcessPointerEvent(view, x, y, PointerEventType.Moved);
    }

    /// <summary>
    /// Processes a pointer up event.
    /// </summary>
    public static void ProcessPointerUp(View? view, double x, double y)
    {
        if (view == null) return;

        if (_gestureState.TryGetValue(view, out var state))
        {
            state.CurrentX = x;
            state.CurrentY = y;

            double deltaX = x - state.StartX;
            double deltaY = y - state.StartY;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            double elapsed = (DateTime.UtcNow - state.StartTime).TotalMilliseconds;

            // Check for swipe
            if (distance >= SwipeMinDistance && elapsed <= SwipeMaxTime)
            {
                var direction = DetermineSwipeDirection(deltaX, deltaY);
                ProcessSwipeGesture(view, direction);
            }

            // Complete pan or detect tap
            if (state.IsPanning)
            {
                ProcessPanGesture(view, deltaX, deltaY, GestureStatus.Completed);
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
            ProcessPointerEvent(view, x, y, PointerEventType.Entered);
    }

    /// <summary>
    /// Processes a pointer exited event.
    /// </summary>
    public static void ProcessPointerExited(View? view, double x, double y)
    {
        if (view != null)
            ProcessPointerEvent(view, x, y, PointerEventType.Exited);
    }

    private static SwipeDirection DetermineSwipeDirection(double deltaX, double deltaY)
    {
        double absX = Math.Abs(deltaX);
        double absY = Math.Abs(deltaY);

        if (absX > absY * SwipeDirectionThreshold)
            return deltaX > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        if (absY > absX * SwipeDirectionThreshold)
            return deltaY > 0 ? SwipeDirection.Down : SwipeDirection.Up;
        return deltaX > 0 ? SwipeDirection.Right : SwipeDirection.Left;
    }

    private static void ProcessSwipeGesture(View view, SwipeDirection direction)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return;

        foreach (var recognizer in recognizers)
        {
            if (recognizer is not SwipeGestureRecognizer swipeRecognizer)
                continue;

            if (!swipeRecognizer.Direction.HasFlag(direction))
                continue;

            Console.WriteLine($"[GestureManager] Swipe detected: {direction}");

            try
            {
                var method = typeof(SwipeGestureRecognizer).GetMethod("SendSwiped", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(swipeRecognizer, new object[] { view, direction });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GestureManager] SendSwiped failed: " + ex.Message);
            }

            if (swipeRecognizer.Command?.CanExecute(swipeRecognizer.CommandParameter) == true)
                swipeRecognizer.Command.Execute(swipeRecognizer.CommandParameter);
        }
    }

    private static void ProcessPanGesture(View view, double totalX, double totalY, GestureStatus status)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null) return;

        foreach (var recognizer in recognizers)
        {
            if (recognizer is not PanGestureRecognizer panRecognizer)
                continue;

            Console.WriteLine($"[GestureManager] Pan gesture: status={status}, totalX={totalX:F1}, totalY={totalY:F1}");

            try
            {
                var method = typeof(PanGestureRecognizer).GetMethod("SendPan", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(panRecognizer, new object[] { view, totalX, totalY, (int)status });
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
        if (recognizers == null) return;

        foreach (var recognizer in recognizers)
        {
            if (recognizer is not PointerGestureRecognizer pointerRecognizer)
                continue;

            try
            {
                string? methodName = eventType switch
                {
                    PointerEventType.Entered => "SendPointerEntered",
                    PointerEventType.Exited => "SendPointerExited",
                    PointerEventType.Pressed => "SendPointerPressed",
                    PointerEventType.Moved => "SendPointerMoved",
                    PointerEventType.Released => "SendPointerReleased",
                    _ => null
                };

                if (methodName != null)
                {
                    var method = typeof(PointerGestureRecognizer).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        var args = CreatePointerEventArgs(view, x, y);
                        method.Invoke(pointerRecognizer, new object[] { view, args! });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GestureManager] Pointer event failed: " + ex.Message);
            }
        }
    }

    private static object? CreatePointerEventArgs(View view, double x, double y)
    {
        try
        {
            var type = typeof(PointerGestureRecognizer).Assembly.GetType("Microsoft.Maui.Controls.PointerEventArgs");
            if (type != null)
            {
                var ctor = type.GetConstructors().FirstOrDefault();
                if (ctor != null)
                    return ctor.Invoke(Array.Empty<object>());
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Checks if the view has a swipe gesture recognizer.
    /// </summary>
    public static bool HasSwipeGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null) return false;
        return view.GestureRecognizers.Any(g => g is SwipeGestureRecognizer);
    }

    /// <summary>
    /// Checks if the view has a pan gesture recognizer.
    /// </summary>
    public static bool HasPanGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null) return false;
        return view.GestureRecognizers.Any(g => g is PanGestureRecognizer);
    }

    /// <summary>
    /// Checks if the view has a pointer gesture recognizer.
    /// </summary>
    public static bool HasPointerGestureRecognizer(View? view)
    {
        if (view?.GestureRecognizers == null) return false;
        return view.GestureRecognizers.Any(g => g is PointerGestureRecognizer);
    }
}
