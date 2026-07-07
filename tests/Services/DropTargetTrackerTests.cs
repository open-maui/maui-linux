// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;
using DragData = Microsoft.Maui.Platform.Linux.Services.DragData;
using DropEventArgs = Microsoft.Maui.Platform.Linux.Services.DropEventArgs;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Tests;

// The enter/leave transition state machine that routes native drag positions
// to per-view MAUI DragOver/DragLeave. Factored out of LinuxApplication.Input
// precisely so it can be verified without a display server.
public class DropTargetTrackerTests
{
    [Fact]
    public void Update_FirstTarget_EntersWithoutLeaving()
    {
        var tracker = new DropTargetTracker<string>();

        var (left, target) = tracker.Update("A");

        left.Should().BeNull();
        target.Should().Be("A");
        tracker.Current.Should().Be("A");
    }

    [Fact]
    public void Update_SameTarget_RepeatsWithoutLeaving()
    {
        var tracker = new DropTargetTracker<string>();
        tracker.Update("A");

        var (left, target) = tracker.Update("A");

        // MAUI fires DragOver repeatedly on an unchanged target — the tracker
        // must keep returning it without generating a leave.
        left.Should().BeNull();
        target.Should().Be("A");
    }

    [Fact]
    public void Update_TargetChange_LeavesOldAndEntersNew()
    {
        var tracker = new DropTargetTracker<string>();
        tracker.Update("A");

        var (left, target) = tracker.Update("B");

        left.Should().Be("A");
        target.Should().Be("B");
        tracker.Current.Should().Be("B");
    }

    [Fact]
    public void Update_TargetToNothing_LeavesOld()
    {
        var tracker = new DropTargetTracker<string>();
        tracker.Update("A");

        var (left, target) = tracker.Update(null);

        left.Should().Be("A");
        target.Should().BeNull();
        tracker.Current.Should().BeNull();
    }

    [Fact]
    public void Update_NothingToNothing_NoTransitions()
    {
        var tracker = new DropTargetTracker<string>();

        var (left, target) = tracker.Update(null);

        left.Should().BeNull();
        target.Should().BeNull();
    }

    [Fact]
    public void Clear_ReturnsCurrentAndResets()
    {
        var tracker = new DropTargetTracker<string>();
        tracker.Update("A");

        tracker.Clear().Should().Be("A");
        tracker.Current.Should().BeNull();

        // Idempotent: a second clear has nothing to leave.
        tracker.Clear().Should().BeNull();
    }

    [Fact]
    public void FullDragSequence_ProducesExpectedTransitions()
    {
        var tracker = new DropTargetTracker<string>();

        tracker.Update(null).Should().Be(((string?)null, (string?)null)); // over empty space
        tracker.Update("A").Should().Be(((string?)null, (string?)"A"));   // enter A
        tracker.Update("A").Should().Be(((string?)null, (string?)"A"));   // still over A
        tracker.Update("B").Should().Be(((string?)"A", (string?)"B"));    // A → B
        tracker.Clear().Should().Be("B");                                 // drop/leave
    }
}

public class DropEventArgsCoordinateTests
{
    [Fact]
    public void Constructor_WithCoordinates_ExposesThem()
    {
        var data = new DragData();

        var args = new DropEventArgs(data, "text", 120, 45);

        args.X.Should().Be(120);
        args.Y.Should().Be(45);
        args.Data.Should().BeSameAs(data);
        args.DroppedData.Should().Be("text");
    }

    [Fact]
    public void Constructor_LegacyTwoArg_DefaultsToOrigin()
    {
        var args = new DropEventArgs(new DragData(), null);

        args.X.Should().Be(0);
        args.Y.Should().Be(0);
    }
}
