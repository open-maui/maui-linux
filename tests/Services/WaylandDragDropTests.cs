// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;
using DragData = Microsoft.Maui.Platform.Linux.Services.DragData;
using DragDropService = Microsoft.Maui.Platform.Linux.Services.DragDropService;
using DragEventArgs = Microsoft.Maui.Platform.Linux.Services.DragEventArgs;
using DropEventArgs = Microsoft.Maui.Platform.Linux.Services.DropEventArgs;

namespace Microsoft.Maui.Platform.Tests;

public class WaylandDragDropServiceTests
{
    // The Wayland and X11 backend code is exercised only with a live display
    // server, so these tests focus on the protocol-agnostic event surface that
    // both backends raise into.

    [Fact]
    public void Default_ReturnsSingleton()
    {
        DragDropService.Default.Should().BeSameAs(DragDropService.Default);
    }

    [Fact]
    public void RaiseDragEnter_FiresEventAndTracksIsDragging()
    {
        var svc = new DragDropService();
        DragEventArgs? seen = null;
        svc.DragEnter += (s, e) => seen = e;

        var data = new DragData { Text = "hello" };
        var args = TestRaise.DragEnter(svc, data, x: 10, y: 20);

        seen.Should().BeSameAs(args);
        svc.IsDragging.Should().BeTrue();
        args.Data.Should().BeSameAs(data);
        args.X.Should().Be(10);
        args.Y.Should().Be(20);
    }

    [Fact]
    public void RaiseDrop_FiresEventAndClearsIsDragging()
    {
        var svc = new DragDropService();
        TestRaise.DragEnter(svc, new DragData(), 0, 0);
        svc.IsDragging.Should().BeTrue();

        DropEventArgs? seen = null;
        svc.Drop += (s, e) => seen = e;
        var data = new DragData();

        var args = TestRaise.Drop(svc, data, "dropped text");

        seen.Should().BeSameAs(args);
        svc.IsDragging.Should().BeFalse();
        args.DroppedData.Should().Be("dropped text");
    }

    [Fact]
    public void RaiseDragLeave_ClearsIsDragging()
    {
        var svc = new DragDropService();
        TestRaise.DragEnter(svc, new DragData(), 0, 0);

        bool fired = false;
        svc.DragLeave += (s, e) => fired = true;
        TestRaise.DragLeave(svc);

        fired.Should().BeTrue();
        svc.IsDragging.Should().BeFalse();
    }

    // Bridge to the internal RaiseXxx methods. The Wayland window is the real
    // caller; here we call them via reflection so tests don't need to grow the
    // public surface.
    private static class TestRaise
    {
        private static readonly System.Reflection.MethodInfo MEnter =
            typeof(DragDropService).GetMethod("RaiseDragEnter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        private static readonly System.Reflection.MethodInfo MDrop =
            typeof(DragDropService).GetMethod("RaiseDrop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        private static readonly System.Reflection.MethodInfo MLeave =
            typeof(DragDropService).GetMethod("RaiseDragLeave", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        public static DragEventArgs DragEnter(DragDropService s, DragData d, int x, int y)
            => (DragEventArgs)MEnter.Invoke(s, new object[] { d, x, y })!;
        public static DropEventArgs Drop(DragDropService s, DragData d, string? text)
            => (DropEventArgs)MDrop.Invoke(s, new object?[] { d, text })!;
        public static void DragLeave(DragDropService s)
            => MLeave.Invoke(s, Array.Empty<object>());
    }
}
