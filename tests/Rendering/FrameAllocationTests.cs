// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Maui.Controls.Shapes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// What redrawing and resizing a MAUI page costs per frame, through the handlers and the
/// rendering engine. Each budget is several times today's cost and well under the regressions it
/// guards: a MAUI canvas made for every shape draw instead of the shared one (MauiCanvasCache)
/// took a frame of this page from 1 ms to 20 ms, and an allocation per view per frame shows as GC
/// pauses while a window is resized or animated.
/// </summary>
[Collection("LinuxApplication.Current")]
public class FrameAllocationTests
{
    // About 43 KB and 1-2 ms per frame of this page when the budgets were set.
    private const long BytesPerFrameBudget = 120_000;
    private const double MillisecondsPerFrameBudget = 10;

    private readonly ITestOutputHelper _output;

    public FrameAllocationTests(ITestOutputHelper output) => _output = output;

    private static ContentPage MixedPage()
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        for (int i = 0; i < 10; i++)
            stack.Add(new Label { Text = $"Label {i}" });
        stack.Add(new Ellipse { Fill = Colors.Red, WidthRequest = 40, HeightRequest = 40 });
        stack.Add(new Rectangle { Fill = Colors.Blue, Stroke = Colors.Black, StrokeThickness = 2, WidthRequest = 80, HeightRequest = 30 });
        stack.Add(new Line { X1 = 0, Y1 = 0, X2 = 100, Y2 = 0, Stroke = Colors.Green, StrokeThickness = 2 });
        stack.Add(new Microsoft.Maui.Controls.Shapes.Path { Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M0,0 L20,20 L40,0 Z")!, Fill = Colors.Orange });
        stack.Add(new Border { Content = new Label { Text = "In a border" }, Stroke = Colors.Gray, StrokeThickness = 1, Padding = 8 });
        stack.Add(new Button { Text = "Button" });
        return new ContentPage { Content = stack };
    }

    private (long Bytes, double Milliseconds) PerFrame(bool resize)
    {
        using var host = new HeadlessMauiHost(MixedPage(), withEngine: true);
        host.DisplayWindow.KeepFrames = false;
        int frame = 0;
        void Frame()
        {
            // A drag resize: every frame is at a new size, so the page is laid out again.
            if (resize)
                host.DisplayWindow.RaiseResized(++frame % 2 == 0 ? 800 : 780, 600);
            host.Context.RenderingEngine!.InvalidateAll();
            host.Context.Render();
        }

        for (int i = 0; i < 5; i++)
            Frame(); // caches, first-use paths, JIT
        const int Frames = 20;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < Frames; i++)
            Frame();
        stopwatch.Stop();
        var result = ((GC.GetAllocatedBytesForCurrentThread() - before) / Frames, stopwatch.Elapsed.TotalMilliseconds / Frames);
        _output.WriteLine($"{(resize ? "resize" : "redraw")}: {result.Item1} bytes, {result.Item2:0.00} ms per frame");
        return result;
    }

    [Fact]
    public void Redrawing_a_page_stays_within_budget()
    {
        var (bytes, milliseconds) = PerFrame(resize: false);
        bytes.Should().BeLessThan(BytesPerFrameBudget);
        milliseconds.Should().BeLessThan(MillisecondsPerFrameBudget);
    }

    [Fact]
    public void Resizing_a_page_stays_within_budget()
    {
        var (bytes, milliseconds) = PerFrame(resize: true);
        bytes.Should().BeLessThan(BytesPerFrameBudget);
        milliseconds.Should().BeLessThan(MillisecondsPerFrameBudget);
    }
}
