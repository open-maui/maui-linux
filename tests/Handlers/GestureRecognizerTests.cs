// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

// Inside the namespace so they beat the platform's own types in lookup.
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using SwipeDirection = Microsoft.Maui.SwipeDirection;
using Rect = Microsoft.Maui.Graphics.Rect;

/// <summary>
/// Gesture recognizers driven end-to-end without a display: a MAUI view with
/// a real handler (so Handler.PlatformView is the SkiaView and GetPosition can
/// resolve against ScreenBounds), pointer events injected through
/// SkiaView.OnPointerPressed/Moved/Released in window-logical coordinates,
/// and the MAUI recognizer events observed on the other side.
/// </summary>
[Collection("GestureManager")]
public class GestureRecognizerTests
{
    // A Label hosted by its handler and arranged at a known window position.
    private static (Label view, SkiaLabel platform) Host(double x = 100, double y = 50, double w = 200, double h = 100)
    {
        var label = new Label { Text = "tap me" };
        var handler = new LabelHandler();
        handler.SetVirtualView(label);
        var platform = handler.PlatformView;
        platform.Arrange(new Rect(x, y, w, h));
        return (label, platform);
    }

    private static void Tap(SkiaView v, float x, float y)
    {
        v.OnPointerPressed(new PointerEventArgs(x, y, PointerButton.Left));
        v.OnPointerReleased(new PointerEventArgs(x, y, PointerButton.Left));
    }

    private static void Drag(SkiaView v, float x0, float y0, float x1, float y1, int steps = 4)
    {
        v.OnPointerPressed(new PointerEventArgs(x0, y0, PointerButton.Left));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            v.OnPointerMoved(new PointerEventArgs(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, PointerButton.Left));
        }
        v.OnPointerReleased(new PointerEventArgs(x1, y1, PointerButton.Left));
    }

    // ---- MAUI internal surface pin ------------------------------------------

    [Fact]
    public void Every_internal_MAUI_member_GestureManager_depends_on_resolves()
    {
        // A MAUI upgrade that renames or re-signatures one of these must fail
        // here, not silently drop gestures at runtime.
        GestureManager.MauiInternals.Missing().Should().BeEmpty();
    }

    [Fact]
    public void Public_MAUI_surface_used_for_pan_pinch_swipe_and_drag_over_is_present()
    {
        new PanGestureRecognizer().Should().BeAssignableTo<IPanGestureController>();
        new PinchGestureRecognizer().Should().BeAssignableTo<IPinchGestureController>();
        typeof(SwipeGestureRecognizer).GetMethod(nameof(SwipeGestureRecognizer.SendSwiped), new[] { typeof(View), typeof(SwipeDirection) })
            .Should().NotBeNull();
        typeof(DropGestureRecognizer).GetMethod(nameof(DropGestureRecognizer.SendDragOver), new[] { typeof(DragEventArgs) })
            .Should().NotBeNull();
    }

    // ---- Tap ------------------------------------------------------------------

    [Fact]
    public void Single_tap_raises_Tapped_with_window_and_element_positions()
    {
        var (label, platform) = Host(100, 50);
        var tap = new TapGestureRecognizer();
        TappedEventArgs? args = null;
        object? sender = null;
        tap.Tapped += (s, e) => { sender = s; args = e; };
        label.GestureRecognizers.Add(tap);

        Tap(platform, 150, 80);

        args.Should().NotBeNull();
        sender.Should().BeSameAs(label);
        args!.GetPosition(null).Should().Be(new Point(150, 80), "GetPosition(null) is the window point");
        args.GetPosition(label).Should().Be(new Point(50, 30), "GetPosition(view) is relative to the view's origin");
    }

    [Fact]
    public void Tap_runs_the_command_with_its_parameter()
    {
        var (label, platform) = Host();
        object? received = null;
        label.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command<object>(p => received = p),
            CommandParameter = "payload",
        });

        Tap(platform, 120, 70);

        received.Should().Be("payload");
    }

    [Fact]
    public void Tap_with_a_command_that_cannot_execute_is_not_run()
    {
        var (label, platform) = Host();
        int runs = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => runs++, () => false) });

        Tap(platform, 120, 70);

        runs.Should().Be(0);
    }

    [Fact]
    public void Double_tap_recognizer_fires_once_for_two_quick_taps()
    {
        var (label, platform) = Host();
        int fired = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 2 }.With(t => t.Tapped += (_, _) => fired++));

        Tap(platform, 120, 70);
        fired.Should().Be(0, "the first tap only arms the double-tap");
        Tap(platform, 121, 71);
        fired.Should().Be(1);
        Tap(platform, 120, 70);
        fired.Should().Be(1, "a third tap starts a new sequence");
    }

    [Fact]
    public void Double_tap_times_out_between_taps()
    {
        var (label, platform) = Host();
        int fired = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 2 }.With(t => t.Tapped += (_, _) => fired++));

        var saved = GestureManager.MultiTapInterval;
        try
        {
            GestureManager.MultiTapInterval = 1;
            Tap(platform, 120, 70);
            System.Threading.Thread.Sleep(20);
            Tap(platform, 120, 70);
            fired.Should().Be(0, "the second tap arrived after the multi-tap interval");
        }
        finally
        {
            GestureManager.MultiTapInterval = saved;
        }
    }

    [Fact]
    public void Single_and_double_tap_recognizers_on_the_same_view_both_work()
    {
        var (label, platform) = Host();
        int single = 0, dbl = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => single++));
        label.GestureRecognizers.Add(new TapGestureRecognizer { NumberOfTapsRequired = 2 }.With(t => t.Tapped += (_, _) => dbl++));

        Tap(platform, 120, 70);
        Tap(platform, 120, 70);

        single.Should().Be(2);
        dbl.Should().Be(1);
    }

    [Fact]
    public void Press_and_release_far_apart_is_not_a_tap()
    {
        var (label, platform) = Host();
        int fired = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => fired++));

        platform.OnPointerPressed(new PointerEventArgs(120, 70, PointerButton.Left));
        platform.OnPointerReleased(new PointerEventArgs(160, 70, PointerButton.Left));

        fired.Should().Be(0);
    }

    [Fact]
    public void Tap_on_a_child_bubbles_to_the_parent_recognizer_exactly_once()
    {
        // Parent (a layout) with the recognizer, child label without.
        var parent = new VerticalStackLayout();
        var parentHandler = new LayoutHandler();
        parentHandler.SetVirtualView(parent);
        parentHandler.PlatformView.Arrange(new Rect(0, 0, 400, 400));

        var (child, childPlatform) = Host(100, 50);
        parent.Add(child);

        int fired = 0;
        TappedEventArgs? args = null;
        parent.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, e) => { fired++; args = e; }));

        Tap(childPlatform, 150, 80);

        fired.Should().Be(1, "pointer bubbling and the tap walk must not both raise the parent's Tapped");
        args!.GetPosition(parent).Should().Be(new Point(150, 80));
        args.GetPosition(child).Should().Be(new Point(50, 30));
    }

    [Fact]
    public void ProcessTap_returns_false_for_a_view_without_recognizers()
    {
        var (label, _) = Host();
        GestureManager.ProcessTap(label, 1, 1).Should().BeFalse();
        GestureManager.ProcessTap(null, 1, 1).Should().BeFalse();
    }

    // ---- Pan ------------------------------------------------------------------

    [Fact]
    public void Pan_reports_Started_Running_and_Completed_with_totals()
    {
        var (label, platform) = Host();
        var pan = new PanGestureRecognizer();
        var updates = new List<PanUpdatedEventArgs>();
        pan.PanUpdated += (_, e) => updates.Add(e);
        label.GestureRecognizers.Add(pan);

        Drag(platform, 120, 70, 180, 110);

        updates.Select(u => u.StatusType).Should().StartWith(GestureStatus.Started);
        updates.Select(u => u.StatusType).Should().EndWith(GestureStatus.Completed);
        updates.Should().Contain(u => u.StatusType == GestureStatus.Running);

        var lastRunning = updates.Last(u => u.StatusType == GestureStatus.Running);
        lastRunning.TotalX.Should().BeApproximately(60, 0.01);
        lastRunning.TotalY.Should().BeApproximately(40, 0.01);
        updates.Select(u => u.GestureId).Distinct().Should().HaveCount(1, "one gesture id per pan");
    }

    [Fact]
    public void Pan_does_not_start_below_the_minimum_distance()
    {
        var (label, platform) = Host();
        var updates = new List<PanUpdatedEventArgs>();
        label.GestureRecognizers.Add(new PanGestureRecognizer().With(p => p.PanUpdated += (_, e) => updates.Add(e)));

        platform.OnPointerPressed(new PointerEventArgs(120, 70, PointerButton.Left));
        platform.OnPointerMoved(new PointerEventArgs(123, 72, PointerButton.Left));
        platform.OnPointerReleased(new PointerEventArgs(123, 72, PointerButton.Left));

        updates.Should().BeEmpty();
    }

    [Fact]
    public void Two_consecutive_pans_get_distinct_gesture_ids()
    {
        var (label, platform) = Host();
        var ids = new List<int>();
        label.GestureRecognizers.Add(new PanGestureRecognizer().With(p => p.PanUpdated += (_, e) => ids.Add(e.GestureId)));

        Drag(platform, 120, 70, 180, 70);
        Drag(platform, 120, 70, 180, 70);

        ids.Distinct().Should().HaveCount(2);
    }

    // ---- Swipe ----------------------------------------------------------------

    [Theory]
    [InlineData(150, 0, SwipeDirection.Right)]
    [InlineData(-150, 0, SwipeDirection.Left)]
    [InlineData(0, 150, SwipeDirection.Down)]
    [InlineData(0, -150, SwipeDirection.Up)]
    [InlineData(150, 40, SwipeDirection.Right)]
    [InlineData(-30, -150, SwipeDirection.Up)]
    public void Swipe_direction_is_detected_from_the_dominant_axis(double dx, double dy, SwipeDirection expected)
    {
        GestureManager.DetermineSwipeDirection(dx, dy).Should().Be(expected);

        var (label, platform) = Host(0, 0, 800, 800);
        SwipedEventArgs? args = null;
        label.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down,
            CommandParameter = "p",
        }.With(s => s.Swiped += (_, e) => args = e));

        Drag(platform, 400, 400, 400 + (float)dx, 400 + (float)dy);

        args.Should().NotBeNull();
        args!.Direction.Should().Be(expected);
        args.Parameter.Should().Be("p");
    }

    [Fact]
    public void Swipe_honours_the_recognizer_threshold()
    {
        var (label, platform) = Host(0, 0, 800, 800);
        int fired = 0;
        label.GestureRecognizers.Add(new SwipeGestureRecognizer { Direction = SwipeDirection.Right, Threshold = 200 }
            .With(s => s.Swiped += (_, _) => fired++));

        Drag(platform, 100, 100, 250, 100);
        fired.Should().Be(0, "150px is under the 200px threshold");

        Drag(platform, 100, 100, 350, 100);
        fired.Should().Be(1);
    }

    [Fact]
    public void Swipe_only_fires_for_the_recognizers_direction()
    {
        var (label, platform) = Host(0, 0, 800, 800);
        int left = 0, right = 0;
        label.GestureRecognizers.Add(new SwipeGestureRecognizer { Direction = SwipeDirection.Left }.With(s => s.Swiped += (_, _) => left++));
        label.GestureRecognizers.Add(new SwipeGestureRecognizer { Direction = SwipeDirection.Right }.With(s => s.Swiped += (_, _) => right++));

        Drag(platform, 400, 100, 100, 100);

        left.Should().Be(1);
        right.Should().Be(0);
    }

    [Fact]
    public void Swipe_runs_the_command()
    {
        var (label, platform) = Host(0, 0, 800, 800);
        object? received = null;
        label.GestureRecognizers.Add(new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Down,
            Command = new Command<object>(p => received = p),
            CommandParameter = 7,
        });

        Drag(platform, 100, 100, 100, 400);

        received.Should().Be(7);
    }

    // ---- Pinch ----------------------------------------------------------------

    [Fact]
    public void Pinch_scale_helper_grows_shrinks_and_clamps()
    {
        double step = GestureManager.PinchScrollScale;
        GestureManager.ComputePinchScale(1.0, 1).Should().BeApproximately(1.0 + step, 1e-9);
        GestureManager.ComputePinchScale(1.0, -1).Should().BeApproximately(1.0 - step, 1e-9);
        GestureManager.ComputePinchScale(9.99, 100).Should().Be(10.0, "clamped high");
        GestureManager.ComputePinchScale(0.11, -100).Should().Be(0.1, "clamped low");
    }

    [Fact]
    public void Ctrl_scroll_drives_a_pinch_with_Started_Running_and_Completed()
    {
        var (label, _) = Host(100, 50, 200, 100);
        // Give the MAUI view a size so ScaleOrigin can be normalised.
        label.Frame = new Rect(100, 50, 200, 100);
        var updates = new List<PinchGestureUpdatedEventArgs>();
        label.GestureRecognizers.Add(new PinchGestureRecognizer().With(p => p.PinchUpdated += (_, e) => updates.Add(e)));

        GestureManager.ProcessScrollAsPinch(label, 200, 100, 1, isCtrlPressed: true).Should().BeTrue();
        GestureManager.ProcessScrollAsPinch(label, 200, 100, 1, isCtrlPressed: true).Should().BeTrue();
        GestureManager.EndPinchGesture(label);

        updates.Select(u => u.Status).Should().ContainInOrder(GestureStatus.Started, GestureStatus.Running, GestureStatus.Completed);
        var running = updates.Last(u => u.Status == GestureStatus.Running);
        running.Scale.Should().BeGreaterThan(1.0);
        running.ScaleOrigin.X.Should().BeApproximately(0.5, 0.01, "origin is normalised to the view");
        running.ScaleOrigin.Y.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void Scroll_without_ctrl_or_without_a_pinch_recognizer_is_not_consumed()
    {
        var (label, _) = Host();
        GestureManager.ProcessScrollAsPinch(label, 10, 10, 1, isCtrlPressed: true).Should().BeFalse("no recognizer");
        label.GestureRecognizers.Add(new PinchGestureRecognizer());
        GestureManager.ProcessScrollAsPinch(label, 10, 10, 1, isCtrlPressed: false).Should().BeFalse("no ctrl");
    }

    // ---- Pointer --------------------------------------------------------------

    [Fact]
    public void Pointer_recognizer_receives_entered_moved_pressed_released_and_exited()
    {
        var (label, platform) = Host(100, 50);
        var events = new List<string>();
        Microsoft.Maui.Controls.PointerEventArgs? pressedArgs = null;
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => events.Add("entered");
        pointer.PointerMoved += (_, _) => events.Add("moved");
        pointer.PointerPressed += (_, e) => { events.Add("pressed"); pressedArgs = e; };
        pointer.PointerReleased += (_, _) => events.Add("released");
        pointer.PointerExited += (_, _) => events.Add("exited");
        label.GestureRecognizers.Add(pointer);

        platform.OnPointerEntered(new PointerEventArgs(110, 60));
        platform.OnPointerMoved(new PointerEventArgs(120, 70));
        platform.OnPointerPressed(new PointerEventArgs(120, 70, PointerButton.Left));
        platform.OnPointerReleased(new PointerEventArgs(120, 70, PointerButton.Left));
        platform.OnPointerExited(new PointerEventArgs(400, 400));

        events.Should().Equal("entered", "moved", "pressed", "released", "exited");
        pressedArgs!.GetPosition(null).Should().Be(new Point(120, 70));
        pressedArgs.GetPosition(label).Should().Be(new Point(20, 20));
    }

    [Fact]
    public void Pointer_recognizer_commands_run_with_their_parameters()
    {
        var (label, platform) = Host();
        var received = new List<object?>();
        label.GestureRecognizers.Add(new PointerGestureRecognizer
        {
            PointerPressedCommand = new Command<object>(p => received.Add(p)),
            PointerPressedCommandParameter = "down",
            PointerReleasedCommand = new Command<object>(p => received.Add(p)),
            PointerReleasedCommandParameter = "up",
        });

        Tap(platform, 120, 70);

        received.Should().Equal("down", "up");
    }

    // ---- Drag / drop ----------------------------------------------------------

    [Fact]
    public void Drop_target_is_the_nearest_ancestor_that_allows_drop()
    {
        var parent = new VerticalStackLayout();
        var child = new Label();
        parent.Add(child);
        parent.GestureRecognizers.Add(new DropGestureRecognizer { AllowDrop = true });

        GestureManager.FindDropTarget(child).Should().BeSameAs(parent);
        GestureManager.FindDropTarget(new Label()).Should().BeNull();
        GestureManager.FindDropTarget(null).Should().BeNull();
    }

    [Fact]
    public void DragOver_reports_acceptance_and_carries_a_position()
    {
        var (label, _) = Host(100, 50);
        DragEventArgs? over = null;
        label.GestureRecognizers.Add(new DropGestureRecognizer { AllowDrop = true }
            .With(d => d.DragOver += (_, e) => { over = e; e.AcceptedOperation = DataPackageOperation.Copy; }));

        GestureManager.ProcessDragOver(label, 150, 80).Should().BeTrue();
        over.Should().NotBeNull();
        over!.GetPosition(null).Should().Be(new Point(150, 80));
        over.GetPosition(label).Should().Be(new Point(50, 30));
    }

    [Fact]
    public void DragOver_reports_rejection_when_every_recognizer_sets_None()
    {
        var (label, _) = Host();
        label.GestureRecognizers.Add(new DropGestureRecognizer { AllowDrop = true }
            .With(d => d.DragOver += (_, e) => e.AcceptedOperation = DataPackageOperation.None));

        GestureManager.ProcessDragOver(label, 1, 1).Should().BeFalse();
        GestureManager.ProcessDragOver(new Label(), 1, 1).Should().BeNull("no recognizer participated");
    }

    [Fact]
    public void DragLeave_reaches_the_recognizer()
    {
        var (label, _) = Host();
        int fired = 0;
        label.GestureRecognizers.Add(new DropGestureRecognizer { AllowDrop = true }.With(d => d.DragLeave += (_, _) => fired++));

        GestureManager.ProcessDragLeave(label);

        fired.Should().Be(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task Drop_delivers_text_and_file_paths_with_a_position()
    {
        var (label, _) = Host(100, 50);
        DropEventArgs? drop = null;
        label.GestureRecognizers.Add(new DropGestureRecognizer { AllowDrop = true }.With(d => d.Drop += (_, e) => drop = e));

        GestureManager.ProcessDrop(label, 150, 80, "hello", new[] { "/tmp/a.txt", "/tmp/b.txt" });

        drop.Should().NotBeNull();
        (await drop!.Data.GetTextAsync()).Should().Be("hello");
        drop.Data.Properties[GestureManager.FilePathsPropertyKey].Should().BeEquivalentTo(new[] { "/tmp/a.txt", "/tmp/b.txt" });
        drop.GetPosition(label).Should().Be(new Point(50, 30));
    }

    [Fact]
    public void Drop_recognizer_with_AllowDrop_false_is_skipped()
    {
        var (label, _) = Host();
        int fired = 0;
        label.GestureRecognizers.Add(new DropGestureRecognizer { AllowDrop = false }.With(d => d.Drop += (_, _) => fired++));

        GestureManager.ProcessDrop(label, 1, 1, "x", null);

        fired.Should().Be(0);
    }

    [Fact]
    public void DragStarting_is_raised_once_per_press_when_the_pointer_moves()
    {
        var (label, platform) = Host();
        int starting = 0;
        DragStartingEventArgs? args = null;
        label.GestureRecognizers.Add(new DragGestureRecognizer { CanDrag = true }
            .With(d => d.DragStarting += (_, e) => { starting++; args = e; e.Cancel = true; }));

        Drag(platform, 120, 70, 200, 70, steps: 6);

        starting.Should().Be(1, "a cancelled DragStarting must not retrigger on every move");
        args!.GetPosition(label).Should().NotBeNull();
        args.Data.Should().NotBeNull();
    }

    [Fact]
    public void DragStarting_with_CanDrag_false_is_not_raised()
    {
        var (label, platform) = Host();
        int starting = 0;
        label.GestureRecognizers.Add(new DragGestureRecognizer { CanDrag = false }.With(d => d.DragStarting += (_, _) => starting++));

        Drag(platform, 120, 70, 200, 70);

        starting.Should().Be(0);
    }

    [Fact]
    public void DropCompleted_reaches_the_drag_recognizer_after_DragStarting()
    {
        var (label, platform) = Host();
        int completed = 0;
        label.GestureRecognizers.Add(new DragGestureRecognizer { CanDrag = true }
            .With(d =>
            {
                d.DragStarting += (_, e) => e.Data.Text = "payload";
                d.DropCompleted += (_, _) => completed++;
            }));

        // MAUI only accepts DropCompleted for a drag it saw start.
        GestureManager.ProcessDropCompleted(label);
        completed.Should().Be(0, "no drag has started yet");

        Drag(platform, 120, 70, 200, 70);
        GestureManager.ProcessDropCompleted(label);

        completed.Should().Be(1);
    }

    [Fact]
    public void DropCompleted_fires_when_the_native_drag_session_ends()
    {
        var (label, platform) = Host();
        int completed = 0;
        label.GestureRecognizers.Add(new DragGestureRecognizer { CanDrag = true }
            .With(d =>
            {
                d.DragStarting += (_, e) => e.Data.Text = "payload";
                d.DropCompleted += (_, _) => completed++;
            }));
        Drag(platform, 120, 70, 200, 70); // MAUI has seen DragStarting

        // What StartDrag does once a backend accepted the payload, then the
        // backend reporting the session's end (dnd_finished / cancelled).
        GestureManager.WatchDragSession(label);
        Microsoft.Maui.Platform.Linux.Services.DragDropService.Default.RaiseDragSessionEnded(dropped: true);

        completed.Should().Be(1);

        // The source is forgotten after one completion: a later session end
        // (from another drag) must not complete this recognizer again.
        Microsoft.Maui.Platform.Linux.Services.DragDropService.Default.RaiseDragSessionEnded(dropped: false);
        completed.Should().Be(1);
    }

    [Fact]
    public void Drag_payload_maps_text_and_file_paths_from_the_data_package()
    {
        var package = new DataPackage { Text = "hello" };
        package.Properties[GestureManager.FilePathsPropertyKey] = new[] { "/tmp/x" };

        var payload = GestureManager.ExtractDragPayload(package);

        payload.Should().NotBeNull();
        payload!.Text.Should().Be("hello");
        payload.FilePaths.Should().BeEquivalentTo(new[] { "/tmp/x" });
        GestureManager.ExtractDragPayload(null).Should().BeNull();
    }

    // ---- Cleanup --------------------------------------------------------------

    [Fact]
    public void CleanupView_forgets_a_pressed_view_so_its_release_is_not_a_tap()
    {
        var (label, platform) = Host();
        int fired = 0;
        label.GestureRecognizers.Add(new TapGestureRecognizer().With(t => t.Tapped += (_, _) => fired++));

        platform.OnPointerPressed(new PointerEventArgs(120, 70, PointerButton.Left));
        GestureManager.CleanupView(label);
        platform.OnPointerReleased(new PointerEventArgs(120, 70, PointerButton.Left));

        fired.Should().Be(0);
    }
}

internal static class GestureTestExtensions
{
    public static T With<T>(this T recognizer, Action<T> configure)
    {
        configure(recognizer);
        return recognizer;
    }
}
