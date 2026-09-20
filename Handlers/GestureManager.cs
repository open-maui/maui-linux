using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Handlers;

// Inside the namespace so it beats Microsoft.Maui.Platform.SwipeDirection in lookup.
using SwipeDirection = Microsoft.Maui.SwipeDirection;

/// <summary>
/// Manages gesture recognition and processing for MAUI views on Linux.
/// Handles tap, pan, swipe, pinch, pointer and drag/drop gestures.
/// </summary>
/// <remarks>
/// Dispatch prefers MAUI's public surface: <see cref="IPanGestureController"/>,
/// <see cref="IPinchGestureController"/>, <see cref="SwipeGestureRecognizer.SendSwiped"/>
/// and <see cref="DropGestureRecognizer.SendDragOver"/>. The members MAUI keeps
/// internal (TapGestureRecognizer.SendTapped, PointerGestureRecognizer.SendPointer*,
/// DragGestureRecognizer.SendDragStarting/SendDropCompleted,
/// DropGestureRecognizer.SendDragLeave/SendDrop and the event-args constructors
/// that take a GetPosition resolver) are reached through <see cref="MauiInternals"/>,
/// which resolves them once against explicit signatures so a MAUI upgrade that
/// changes them shows up as a pinned test failure rather than a silent no-op.
/// </remarks>
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
        public int PanGestureId { get; set; }
        // Set once the press+move threshold has offered this gesture to the
        // drag path — whether or not a native drag actually started — so a
        // cancelled/empty DragStarting doesn't retrigger on every move.
        public bool DragStarted { get; set; }
    }

    internal enum PointerEventType
    {
        Entered,
        Exited,
        Pressed,
        Moved,
        Released
    }

    #region MAUI internal members (pinned)

    /// <summary>
    /// The internal MAUI members this manager depends on, resolved once with
    /// explicit signatures. Every member is nullable so a signature change
    /// degrades to a logged no-op at runtime; the test suite pins them so the
    /// change is caught at upgrade time.
    /// </summary>
    internal static class MauiInternals
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly Type PositionResolverType = typeof(Func<IElement?, Point?>);

        /// <summary>TapGestureRecognizer.SendTapped(View, Func&lt;IElement?, Point?&gt;)</summary>
        public static readonly MethodInfo? SendTapped = typeof(TapGestureRecognizer).GetMethod(
            "SendTapped", Instance, new[] { typeof(View), PositionResolverType });

        /// <summary>PointerGestureRecognizer.SendPointer{Entered,Exited,Pressed,Moved,Released}(View, Func, PlatformPointerEventArgs, ButtonsMask)</summary>
        public static readonly IReadOnlyDictionary<PointerEventType, MethodInfo?> SendPointer =
            new Dictionary<PointerEventType, MethodInfo?>
            {
                [PointerEventType.Entered] = ResolvePointer("SendPointerEntered"),
                [PointerEventType.Exited] = ResolvePointer("SendPointerExited"),
                [PointerEventType.Pressed] = ResolvePointer("SendPointerPressed"),
                [PointerEventType.Moved] = ResolvePointer("SendPointerMoved"),
                [PointerEventType.Released] = ResolvePointer("SendPointerReleased"),
            };

        /// <summary>DragGestureRecognizer.SendDragStarting(View, Func, PlatformDragStartingEventArgs) → DragStartingEventArgs</summary>
        public static readonly MethodInfo? SendDragStarting = typeof(DragGestureRecognizer).GetMethod(
            "SendDragStarting", Instance, new[] { typeof(View), PositionResolverType, typeof(PlatformDragStartingEventArgs) });

        /// <summary>DragGestureRecognizer.SendDropCompleted(DropCompletedEventArgs)</summary>
        public static readonly MethodInfo? SendDropCompleted = typeof(DragGestureRecognizer).GetMethod(
            "SendDropCompleted", Instance, new[] { typeof(DropCompletedEventArgs) });

        /// <summary>DropGestureRecognizer.SendDragLeave(DragEventArgs)</summary>
        public static readonly MethodInfo? SendDragLeave = typeof(DropGestureRecognizer).GetMethod(
            "SendDragLeave", Instance, new[] { typeof(Microsoft.Maui.Controls.DragEventArgs) });

        /// <summary>DropGestureRecognizer.SendDrop(DropEventArgs) → Task</summary>
        public static readonly MethodInfo? SendDrop = typeof(DropGestureRecognizer).GetMethod(
            "SendDrop", Instance, new[] { typeof(Microsoft.Maui.Controls.DropEventArgs) });

        /// <summary>DragEventArgs(DataPackage, Func, PlatformDragEventArgs) — the resolver-carrying constructor.</summary>
        public static readonly ConstructorInfo? DragEventArgsCtor = typeof(Microsoft.Maui.Controls.DragEventArgs).GetConstructor(
            Instance, new[] { typeof(DataPackage), PositionResolverType, typeof(PlatformDragEventArgs) });

        /// <summary>DropEventArgs(DataPackageView, Func, PlatformDropEventArgs) — the resolver-carrying constructor.</summary>
        public static readonly ConstructorInfo? DropEventArgsCtor = typeof(Microsoft.Maui.Controls.DropEventArgs).GetConstructor(
            Instance, new[] { typeof(DataPackageView), PositionResolverType, typeof(PlatformDropEventArgs) });

        private static MethodInfo? ResolvePointer(string name)
            => typeof(PointerGestureRecognizer).GetMethod(
                name, Instance, new[] { typeof(View), PositionResolverType, typeof(PlatformPointerEventArgs), typeof(ButtonsMask) });

        /// <summary>Names of the members that failed to resolve — empty on a supported MAUI.</summary>
        public static IEnumerable<string> Missing()
        {
            if (SendTapped == null) yield return "TapGestureRecognizer.SendTapped";
            foreach (var kv in SendPointer)
                if (kv.Value == null) yield return $"PointerGestureRecognizer.SendPointer{kv.Key}";
            if (SendDragStarting == null) yield return "DragGestureRecognizer.SendDragStarting";
            if (SendDropCompleted == null) yield return "DragGestureRecognizer.SendDropCompleted";
            if (SendDragLeave == null) yield return "DropGestureRecognizer.SendDragLeave";
            if (SendDrop == null) yield return "DropGestureRecognizer.SendDrop";
            if (DragEventArgsCtor == null) yield return "DragEventArgs(DataPackage, Func, PlatformDragEventArgs)";
            if (DropEventArgsCtor == null) yield return "DropEventArgs(DataPackageView, Func, PlatformDropEventArgs)";
        }
    }

    /// <summary>
    /// Invokes a pinned internal member, logging (never throwing) on failure.
    /// Returns false when the member is unavailable or the call threw.
    /// </summary>
    private static bool InvokeInternal(MethodInfo? method, object target, object?[] args, out object? result)
    {
        result = null;
        if (method == null)
        {
            DiagnosticLog.Warn(Tag, $"MAUI internal member unavailable on {target.GetType().Name}; gesture not delivered");
            return false;
        }
        try
        {
            result = method.Invoke(target, args);
            return true;
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            // The app's handler threw — surface that, not the reflection wrapper.
            DiagnosticLog.Error(Tag, $"{method.Name} handler failed", tie.InnerException);
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, $"{method.Name} invocation failed", ex);
            return false;
        }
    }

    private static bool InvokeInternal(MethodInfo? method, object target, params object?[] args)
        => InvokeInternal(method, target, args, out _);

    #endregion

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

    #endregion

    private static readonly Dictionary<View, (DateTime lastTap, int tapCount)> _tapTracking = new();
    private static readonly Dictionary<View, GestureTrackingState> _gestureState = new();
    private static int _nextPanGestureId;

    /// <summary>
    /// Minimum distance in pixels for a swipe gesture to be recognized when the
    /// recognizer's own <see cref="SwipeGestureRecognizer.Threshold"/> is zero.
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
    /// Maximum interval in milliseconds between the taps of a multi-tap gesture.
    /// </summary>
    public static double MultiTapInterval { get; set; } = 300.0;

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
    /// Processes a tap gesture on the specified view. Coordinates are in
    /// window-logical space (see <see cref="CreatePositionResolver"/>). Walks
    /// up the parent chain until a view (or one of its child gesture elements,
    /// e.g. a Label's Spans) handles the tap.
    /// </summary>
    public static bool ProcessTap(View? view, double x, double y)
        => ProcessTapCore(view, x, y) != null;

    /// <summary>
    /// The tap walk; returns the view whose recognizers (or child gesture
    /// elements) consumed the tap, or null.
    /// </summary>
    private static View? ProcessTapCore(View? view, double x, double y)
    {
        for (var current = view; current != null; current = current.Parent as View)
        {
            if (ProcessTapOnView(current, x, y))
            {
                return current;
            }
        }
        return null;
    }

    private static Microsoft.Maui.Graphics.Rect? ScreenBoundsOf(View view)
        => view.Handler?.PlatformView is Microsoft.Maui.Platform.SkiaView sv ? sv.ScreenBounds : null;

    /// <summary>
    /// Window-logical → view-local. A view without a realized platform view
    /// has no origin to subtract; its coordinates are taken as already local.
    /// </summary>
    private static Point ToLocal(View view, double x, double y)
        => ScreenBoundsOf(view) is { } b ? new Point(x - b.Left, y - b.Top) : new Point(x, y);

    private static bool ProcessTapOnView(View view, double x, double y)
    {
        bool result = false;

        // Child gesture elements first (Label.FormattedText spans carry their
        // own TapGestureRecognizers and report hit regions in view-local
        // coordinates). MAUI raises Tapped with the host view as sender.
        var children = view.GetChildElements(ToLocal(view, x, y));
        if (children != null)
        {
            foreach (var child in children)
            {
                foreach (var recognizer in child.GestureRecognizers)
                {
                    if (recognizer is TapGestureRecognizer childTap)
                        result |= ProcessTapRecognizer(childTap, view, child, x, y);
                }
            }
        }

        var recognizers = view.GestureRecognizers;
        if (recognizers == null || recognizers.Count == 0)
        {
            return result;
        }
        foreach (var item in recognizers)
        {
            if (item is TapGestureRecognizer tapRecognizer)
                result |= ProcessTapRecognizer(tapRecognizer, view, view, x, y);
        }
        return result;
    }

    /// <summary>
    /// Runs the multi-tap bookkeeping for one recognizer and raises Tapped
    /// when its NumberOfTapsRequired is satisfied. <paramref name="trackingKey"/>
    /// distinguishes a span's taps from its host label's.
    /// </summary>
    private static bool ProcessTapRecognizer(TapGestureRecognizer tapRecognizer, View sender, Element trackingKey, double x, double y)
    {
        DiagnosticLog.Debug(Tag,
            $"Processing TapGestureRecognizer on {sender.GetType().Name}, CommandParameter={tapRecognizer.CommandParameter}, NumberOfTapsRequired={tapRecognizer.NumberOfTapsRequired}");

        int numberOfTapsRequired = tapRecognizer.NumberOfTapsRequired;
        if (numberOfTapsRequired > 1)
        {
            // Multi-tap state is tracked per host view; spans share the label's
            // clock, which matches how a double-tap on a span reads to the user.
            var key = sender;
            DateTime utcNow = DateTime.UtcNow;
            if (!_tapTracking.TryGetValue(key, out var tracking)
                || (utcNow - tracking.lastTap).TotalMilliseconds >= MultiTapInterval)
            {
                _tapTracking[key] = (utcNow, 1);
                DiagnosticLog.Debug(Tag, $"Tap 1/{numberOfTapsRequired}");
                return false;
            }
            int tapCount = tracking.tapCount + 1;
            if (tapCount < numberOfTapsRequired)
            {
                _tapTracking[key] = (utcNow, tapCount);
                DiagnosticLog.Debug(Tag, $"Tap {tapCount}/{numberOfTapsRequired}, waiting for more taps");
                return false;
            }
            _tapTracking.Remove(key);
        }

        // SendTapped raises Tapped (with a lazily-resolved position) and runs
        // the Command itself; only fall back to the Command when the internal
        // member is unavailable.
        bool eventFired = InvokeInternal(MauiInternals.SendTapped, tapRecognizer, sender, CreatePositionResolver(x, y));
        if (!eventFired)
        {
            ICommand? command = tapRecognizer.Command;
            if (command != null && command.CanExecute(tapRecognizer.CommandParameter))
            {
                DiagnosticLog.Debug(Tag, "Executing TapGestureRecognizer Command");
                command.Execute(tapRecognizer.CommandParameter);
            }
        }

        _ = trackingKey;
        return true;
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
        if (state.IsPanning || Math.Sqrt(deltaX * deltaX + deltaY * deltaY) >= PanMinDistance)
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

            if (!state.IsPanning)
            {
                state.IsPanning = true;
                state.PanGestureId = ++_nextPanGestureId;
                ProcessPanGesture(view, deltaX, deltaY, GestureStatus.Started, state.PanGestureId);
            }
            ProcessPanGesture(view, deltaX, deltaY, GestureStatus.Running, state.PanGestureId);
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
            if (elapsed <= SwipeMaxTime && distance > 0)
            {
                ProcessSwipeGesture(view, deltaX, deltaY);
            }
            if (state.IsPanning)
            {
                ProcessPanGesture(view, deltaX, deltaY, GestureStatus.Completed, state.PanGestureId);
            }
            else if (distance < 15.0 && elapsed < SwipeMaxTime)
            {
                DiagnosticLog.Debug(Tag, $"Detected tap on {view.GetType().Name} (distance={distance:F1}, elapsed={elapsed:F0}ms)");
                if (ProcessTapCore(view, x, y) != null)
                {
                    // The tap was consumed by this view or an ancestor. Pointer
                    // events bubble (SkiaView.BubblePointerEvent calls
                    // ProcessPointerUp for every ancestor too), so drop the
                    // ancestors' press state or the same tap would be raised
                    // again when their own release arrives.
                    for (var ancestor = view.Parent as View; ancestor != null; ancestor = ancestor.Parent as View)
                        _gestureState.Remove(ancestor);
                }
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

    /// <summary>
    /// Classifies a displacement as one of the four swipe directions; the
    /// dominant axis wins, with <see cref="SwipeDirectionThreshold"/> deciding
    /// how dominant it has to be before the minor axis is ignored.
    /// </summary>
    internal static SwipeDirection DetermineSwipeDirection(double deltaX, double deltaY)
    {
        double absX = Math.Abs(deltaX);
        double absY = Math.Abs(deltaY);
        if (absX >= absY)
        {
            return deltaX > 0.0 ? SwipeDirection.Right : SwipeDirection.Left;
        }
        return deltaY > 0.0 ? SwipeDirection.Down : SwipeDirection.Up;
    }

    /// <summary>
    /// Raises Swiped on every SwipeGestureRecognizer whose Direction includes
    /// the detected direction and whose Threshold the displacement along that
    /// axis exceeds (a zero Threshold falls back to <see cref="SwipeMinDistance"/>).
    /// </summary>
    private static void ProcessSwipeGesture(View view, double deltaX, double deltaY)
    {
        var recognizers = view.GestureRecognizers;
        if (recognizers == null)
        {
            return;
        }
        var direction = DetermineSwipeDirection(deltaX, deltaY);
        double axisDistance = direction is SwipeDirection.Left or SwipeDirection.Right ? Math.Abs(deltaX) : Math.Abs(deltaY);

        foreach (var item in recognizers)
        {
            if (item is not SwipeGestureRecognizer swipeRecognizer || !swipeRecognizer.Direction.HasFlag(direction))
            {
                continue;
            }
            double threshold = swipeRecognizer.Threshold > 0 ? swipeRecognizer.Threshold : SwipeMinDistance;
            if (axisDistance < threshold)
            {
                continue;
            }
            DiagnosticLog.Debug(Tag, $"Swipe detected: {direction} ({axisDistance:F0}px, threshold {threshold:F0})");

            try
            {
                // Public API: raises Swiped and runs Command with CommandParameter.
                swipeRecognizer.SendSwiped(view, direction);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendSwiped failed", ex);
            }
        }
    }

    private static void ProcessPanGesture(View view, double totalX, double totalY, GestureStatus status, int gestureId)
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
                // Public API: IPanGestureController raises PanUpdated with the
                // matching GestureStatus.
                var controller = (IPanGestureController)panRecognizer;
                switch (status)
                {
                    case GestureStatus.Started:
                        controller.SendPanStarted(view, gestureId);
                        break;
                    case GestureStatus.Running:
                        controller.SendPan(view, totalX, totalY, gestureId);
                        break;
                    case GestureStatus.Completed:
                        controller.SendPanCompleted(view, gestureId);
                        break;
                    case GestureStatus.Canceled:
                        controller.SendPanCanceled(view, gestureId);
                        break;
                }
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
            // SendPointer*(View sender, Func<IElement?, Point?> getPosition,
            // PlatformPointerEventArgs platformArgs, ButtonsMask button) — the
            // platform args carry nothing on Linux, MAUI accepts null there.
            MauiInternals.SendPointer.TryGetValue(eventType, out var method);
            InvokeInternal(method, pointerRecognizer,
                view, CreatePositionResolver(x, y), null, ButtonsMask.Primary);
        }
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

        state.PinchScale = ComputePinchScale(state.PinchScale, deltaY);

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
    /// Applies one scroll step to a running pinch scale: each unit of
    /// <paramref name="scrollDelta"/> multiplies by (1 + <see cref="PinchScrollScale"/>),
    /// clamped to [0.1, 10]. Split out so the math is testable without a display.
    /// </summary>
    internal static double ComputePinchScale(double currentScale, double scrollDelta)
    {
        double scaleDelta = 1.0 + (scrollDelta * PinchScrollScale);
        return Math.Clamp(currentScale * scaleDelta, 0.1, 10.0);
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
            if (!state.IsPressed)
                _gestureState.Remove(view);
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
                // ScaleOrigin is normalised to the view's size, as on the other platforms.
                var local = ToLocal(view, originX, originY);
                var scaleOrigin = new Point(
                    view.Width > 0 ? local.X / view.Width : 0,
                    view.Height > 0 ? local.Y / view.Height : 0);

                // Public API: IPinchGestureController raises PinchUpdated.
                var controller = (IPinchGestureController)pinchRecognizer;
                switch (status)
                {
                    case GestureStatus.Started:
                        controller.SendPinchStarted(view, scaleOrigin);
                        controller.SendPinch(view, scale, scaleOrigin);
                        break;
                    case GestureStatus.Running:
                        controller.SendPinch(view, scale, scaleOrigin);
                        break;
                    case GestureStatus.Completed:
                        controller.SendPinchEnded(view);
                        break;
                    case GestureStatus.Canceled:
                        controller.SendPinchCanceled(view);
                        break;
                }
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

            // SendDragStarting(View, Func<IElement?, Point?>, PlatformDragStartingEventArgs)
            // raises DragStarting and returns its args; the platform args carry
            // nothing on Linux.
            if (!InvokeInternal(MauiInternals.SendDragStarting, dragRecognizer,
                    new object?[] { view, CreatePositionResolver(x, y), null }, out var result))
                continue;

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

            var payload = ExtractDragPayload(args.Data);
            if (payload == null || payload.IsEmpty)
            {
                DiagnosticLog.Debug(Tag, "DataPackage carried no representable payload; native drag not started");
                continue;
            }

            if (DragDropService.Default.TryStartDrag(payload))
            {
                DiagnosticLog.Debug(Tag, "Native drag session started");
                WatchDragSession(view);
                return true;
            }

            DiagnosticLog.Debug(Tag, "Native drag unavailable (backend not ready or no recent press serial)");
        }

        return false;
    }

    private static View? s_dragSourceView;
    private static bool s_dragSessionHooked;

    /// <summary>
    /// Remembers the drag source so DropCompleted reaches it when the native
    /// session ends (Wayland dnd_finished/cancelled, X11 cleanup).
    /// </summary>
    internal static void WatchDragSession(View view)
    {
        s_dragSourceView = view;
        if (s_dragSessionHooked) return;
        s_dragSessionHooked = true;
        DragDropService.Default.DragSessionEnded += (_, _) =>
        {
            var source = s_dragSourceView;
            s_dragSourceView = null;
            ProcessDropCompleted(source);
        };
    }

    /// <summary>
    /// Raises MAUI DropCompleted on the view's DragGestureRecognizers once the
    /// native drag session that <see cref="StartDrag"/> began has ended
    /// (dropped or cancelled). Never throws.
    /// </summary>
    public static void ProcessDropCompleted(View? view)
    {
        if (view?.GestureRecognizers == null) return;

        foreach (var item in view.GestureRecognizers)
        {
            if (item is not DragGestureRecognizer dragRecognizer) continue;
            InvokeInternal(MauiInternals.SendDropCompleted, dragRecognizer, new DropCompletedEventArgs());
        }
    }

    // Conventional Properties keys — DataPackage in MAUI 10.0.90 exposes only
    // Text, Image, Properties and View, so file lists and raw image bytes ride
    // in Properties.
    public const string ImageBytesPropertyKey = "ImageBytes";
    public const string ImageMimePropertyKey = "ImageMime";

    /// <summary>
    /// Map a MAUI <see cref="DataPackage"/> onto a backend <see cref="DragPayload"/>:
    /// text, a file list (from the <see cref="FilePathsPropertyKey"/> convention),
    /// and one image (a <see cref="FileImageSource"/>, or raw bytes supplied via
    /// the <see cref="ImageBytesPropertyKey"/>/<see cref="ImageMimePropertyKey"/>
    /// convention). Never throws — returns null on any failure.
    /// </summary>
    internal static DragPayload? ExtractDragPayload(DataPackage? data)
    {
        if (data == null) return null;
        try
        {
            var payload = new DragPayload { Text = ExtractDragText(data) };

            var files = ExtractFilePaths(data);
            if (files is { Length: > 0 }) payload.FilePaths = files;

            if (TryExtractImage(data, out var imageBytes, out var imageMime))
            {
                payload.ImageBytes = imageBytes;
                payload.ImageMime = imageMime;
            }
            else
            {
                // StreamImageSource is async-only: start reading NOW on a
                // background task and hand the in-flight task to the payload,
                // so the drag still starts synchronously (Wayland needs the
                // held press serial; X11 grabs the in-progress press). The
                // transfer path awaits it with a bounded timeout.
                payload.PendingImage = StartPendingImageRead(data);
            }

            return payload;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Debug(Tag, $"ExtractDragPayload failed: {ex.Message}");
            return null;
        }
    }

    // DataPackage.Text first, then Properties conventions — a "Text" key (any
    // casing), else the first string value.
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

    // File paths from the FilePaths convention key (matching the incoming
    // adapter); accepts string[] or IEnumerable<string>.
    private static string[]? ExtractFilePaths(DataPackage data)
    {
        foreach (var kvp in data.Properties)
        {
            if (!string.Equals(kvp.Key, FilePathsPropertyKey, StringComparison.OrdinalIgnoreCase))
                continue;
            return kvp.Value switch
            {
                string[] arr => arr,
                IEnumerable<string> seq => seq.ToArray(),
                string single => new[] { single },
                _ => null,
            };
        }
        return null;
    }

    // An image the native source can carry: raw bytes + MIME via Properties, or
    // a FileImageSource we can read off disk. StreamImageSource is async-only,
    // so it is skipped (the gesture path must not block).
    private static bool TryExtractImage(DataPackage data, out byte[]? bytes, out string? mime)
    {
        bytes = null;
        mime = null;

        // Convention: raw bytes + explicit mime in Properties.
        byte[]? propBytes = null;
        string? propMime = null;
        foreach (var kvp in data.Properties)
        {
            if (string.Equals(kvp.Key, ImageBytesPropertyKey, StringComparison.OrdinalIgnoreCase)
                && kvp.Value is byte[] b)
                propBytes = b;
            else if (string.Equals(kvp.Key, ImageMimePropertyKey, StringComparison.OrdinalIgnoreCase)
                && kvp.Value is string m)
                propMime = m;
        }
        if (propBytes is { Length: > 0 })
        {
            bytes = propBytes;
            mime = string.IsNullOrEmpty(propMime) ? "image/png" : propMime;
            return true;
        }

        // FileImageSource → read bytes; prefer the header-sniffed format over
        // the file extension (extensions lie, magic numbers don't).
        if (data.Image is FileImageSource { File: { Length: > 0 } file } && System.IO.File.Exists(file))
        {
            bytes = System.IO.File.ReadAllBytes(file);
            mime = DragPayload.SniffImageMime(bytes) ?? MimeFromExtension(file);
            return bytes.Length > 0;
        }

        return false;
    }

    // Kick off an async read of a StreamImageSource. Returns the in-flight
    // task (bytes + header-sniffed MIME; unrecognized headers default to
    // image/png), or null when the DataPackage carries no readable stream
    // image. The task never faults unobserved — failures resolve to null and
    // are logged.
    private static System.Threading.Tasks.Task<ResolvedImage?>? StartPendingImageRead(DataPackage data)
    {
        if (data.Image is not StreamImageSource { Stream: { } streamFunc })
            return null;

        return System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                using var stream = await streamFunc(System.Threading.CancellationToken.None).ConfigureAwait(false);
                if (stream == null) return null;

                using var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms).ConfigureAwait(false);
                var bytes = ms.ToArray();
                if (bytes.Length == 0) return null;

                var mime = DragPayload.SniffImageMime(bytes) ?? "image/png";
                return new ResolvedImage(bytes, mime);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Debug(Tag, $"Pending drag image read failed: {ex.Message}");
                return (ResolvedImage?)null;
            }
        });
    }

    private static string MimeFromExtension(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
    }

    #region Incoming native DnD → DropGestureRecognizer adapter

    // DropGestureRecognizer.SendDragOver is public; SendDragLeave / SendDrop
    // and the position-resolver constructors of DragEventArgs / DropEventArgs
    // are internal and reached through MauiInternals. Everything below degrades
    // to debug logs — it must NEVER throw into the input path.

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

            try
            {
                // Public API: raises DragOver and runs DragOverCommand.
                dropRecognizer.SendDragOver(args);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error(Tag, "SendDragOver handler failed", ex);
                continue;
            }

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

            InvokeInternal(MauiInternals.SendDragLeave, dropRecognizer, args);
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

            if (!InvokeInternal(MauiInternals.SendDrop, dropRecognizer, new object?[] { dropArgs }, out var result))
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

    /// <summary>
    /// Builds Controls.DragEventArgs carrying a GetPosition resolver. The
    /// payload isn't readable until drop on either backend, so over/leave args
    /// carry an empty DataPackage. Falls back to the public constructor (no
    /// position) when the internal one is unavailable.
    /// </summary>
    internal static Microsoft.Maui.Controls.DragEventArgs? BuildControlsDragEventArgs(double x, double y)
    {
        try
        {
            var package = BuildDataPackage(null, null);
            if (MauiInternals.DragEventArgsCtor != null)
                return (Microsoft.Maui.Controls.DragEventArgs)MauiInternals.DragEventArgsCtor.Invoke(
                    new object?[] { package, CreatePositionResolver(x, y), null });
            DiagnosticLog.Warn(Tag, "DragEventArgs resolver constructor unavailable; positions will be null");
            return new Microsoft.Maui.Controls.DragEventArgs(package);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, "BuildControlsDragEventArgs failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Builds Controls.DropEventArgs over a DataPackageView of the dropped text
    /// and file paths, with a GetPosition resolver when MAUI's internal
    /// constructor is available.
    /// </summary>
    internal static Microsoft.Maui.Controls.DropEventArgs? BuildControlsDropEventArgs(double x, double y, string? text, string[]? filePaths)
    {
        try
        {
            var package = BuildDataPackage(text, filePaths);
            var packageView = package.View;
            if (MauiInternals.DropEventArgsCtor != null)
                return (Microsoft.Maui.Controls.DropEventArgs)MauiInternals.DropEventArgsCtor.Invoke(
                    new object?[] { packageView, CreatePositionResolver(x, y), null });
            DiagnosticLog.Warn(Tag, "DropEventArgs resolver constructor unavailable; positions will be null");
            return new Microsoft.Maui.Controls.DropEventArgs(packageView);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(Tag, "BuildControlsDropEventArgs failed", ex);
            return null;
        }
    }

    #endregion
}
