// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Maui.Controls.Linux.Tests.Rendering;

/// <summary>
/// Minimal concrete SkiaView for benchmarking. Uses base MeasureOverride
/// (respects WidthRequest/HeightRequest) and does trivial drawing.
/// </summary>
internal class BenchView : SkiaView
{
    protected override void OnDraw(SKCanvas canvas, SKRect bounds)
    {
        // Minimal draw — fill rect to simulate real view cost
        using var paint = new SKPaint { Color = SKColors.Gray };
        canvas.DrawRect(bounds, paint);
    }
}

/// <summary>
/// Performance benchmarks for the rendering pipeline.
/// These tests verify that critical paths complete within acceptable time budgets.
/// Times are measured with Stopwatch and validated against generous upper bounds
/// to avoid flaky failures on slow CI machines, while still catching regressions.
/// </summary>
public class MeasureArrangePerformanceTests : ITestOutputHelper
{
    private readonly ITestOutputHelper _output;

    public MeasureArrangePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ITestOutputHelper implementation for direct construction in theory tests
    void ITestOutputHelper.WriteLine(string message) => _output.WriteLine(message);
    void ITestOutputHelper.WriteLine(string format, params object[] args) => _output.WriteLine(format, args);

    [Fact]
    public void Measure_FlatLayout_100Children_Under5ms()
    {
        // Arrange — flat stack with 100 children
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };
        for (int i = 0; i < 100; i++)
            stack.AddChild(new BenchView { WidthRequest = 200, HeightRequest = 30 });

        // Warmup
        stack.Measure(new Size(800, 10000));

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            stack.InvalidateMeasure();
            stack.Measure(new Size(800, 10000));
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Measure 100-child flat stack: {avgMs:F3} ms/iteration ({iterations} iterations)");

        // Assert — should be well under 5ms per measure
        avgMs.Should().BeLessThan(5.0, "measuring a 100-child flat layout should be fast");
    }

    [Fact]
    public void Arrange_FlatLayout_100Children_Under5ms()
    {
        // Arrange
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };
        for (int i = 0; i < 100; i++)
            stack.AddChild(new BenchView { WidthRequest = 200, HeightRequest = 30 });

        stack.Measure(new Size(800, 10000));

        // Warmup
        stack.Arrange(new Rect(0, 0, 800, 10000));

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            stack.Arrange(new Rect(0, 0, 800, 10000));
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Arrange 100-child flat stack: {avgMs:F3} ms/iteration ({iterations} iterations)");

        avgMs.Should().BeLessThan(5.0, "arranging a 100-child flat layout should be fast");
    }

    [Fact]
    public void Measure_DeepNesting_20Levels_Under5ms()
    {
        // Arrange — deeply nested layout (20 levels, 1 child each)
        var root = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        var current = root;
        for (int i = 0; i < 19; i++)
        {
            var child = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
            current.AddChild(child);
            current = child;
        }
        current.AddChild(new BenchView { WidthRequest = 100, HeightRequest = 50 });

        // Warmup
        root.Measure(new Size(800, 600));

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            root.InvalidateMeasure();
            root.Measure(new Size(800, 600));
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Measure 20-deep nested layout: {avgMs:F3} ms/iteration ({iterations} iterations)");

        avgMs.Should().BeLessThan(5.0, "measuring a 20-level deep layout should be fast");
    }

    [Fact]
    public void MeasureArrange_Grid_10x10_Under10ms()
    {
        // Arrange — 10x10 grid (100 cells)
        var grid = new SkiaGrid();
        for (int r = 0; r < 10; r++)
            grid.RowDefinitions.Add(new Microsoft.Maui.Platform.GridLength(40));
        for (int c = 0; c < 10; c++)
            grid.ColumnDefinitions.Add(new Microsoft.Maui.Platform.GridLength(80));

        for (int r = 0; r < 10; r++)
            for (int c = 0; c < 10; c++)
                grid.AddChild(new BenchView { WidthRequest = 70, HeightRequest = 30 }, r, c);

        // Warmup
        grid.Measure(new Size(800, 600));
        grid.Arrange(new Rect(0, 0, 800, 600));

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 50;
        for (int i = 0; i < iterations; i++)
        {
            grid.InvalidateMeasure();
            grid.Measure(new Size(800, 600));
            grid.Arrange(new Rect(0, 0, 800, 600));
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Measure+Arrange 10x10 grid: {avgMs:F3} ms/iteration ({iterations} iterations)");

        avgMs.Should().BeLessThan(10.0, "measure+arrange of a 10x10 grid should complete quickly");
    }
}

public class HitTestPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public HitTestPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HitTest_FlatLayout_100Children_Under1ms()
    {
        // Arrange — layout with 100 children arranged vertically
        var stack = new SkiaStackLayout
        {
            Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical
        };
        for (int i = 0; i < 100; i++)
            stack.AddChild(new BenchView { WidthRequest = 400, HeightRequest = 30 });

        stack.Measure(new Size(400, 3000));
        stack.Arrange(new Rect(0, 0, 400, 3000));

        // Warmup
        stack.HitTest(200, 1500);

        // Act — hit test at various points
        var sw = Stopwatch.StartNew();
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            float y = (i % 3000);
            stack.HitTest(200, y);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"HitTest 100-child flat stack: {avgMs:F4} ms/hit ({iterations} iterations)");

        avgMs.Should().BeLessThan(1.0, "hit testing a 100-child flat layout should be sub-millisecond");
    }

    [Fact]
    public void HitTest_DeepNesting_20Levels_Under1ms()
    {
        // Arrange — deeply nested layout
        var root = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        var current = root;
        for (int i = 0; i < 19; i++)
        {
            var child = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
            current.AddChild(child);
            current = child;
        }
        current.AddChild(new BenchView { WidthRequest = 100, HeightRequest = 50 });

        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));

        // Warmup
        root.HitTest(50, 25);

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            root.HitTest(50, 25);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"HitTest 20-deep nested layout: {avgMs:F4} ms/hit ({iterations} iterations)");

        avgMs.Should().BeLessThan(1.0, "hit testing a 20-level deep layout should be sub-millisecond");
    }

    [Fact]
    public void HitTest_Miss_Outside_Under01ms()
    {
        // Arrange
        var stack = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        for (int i = 0; i < 50; i++)
            stack.AddChild(new BenchView { WidthRequest = 400, HeightRequest = 30 });

        stack.Measure(new Size(400, 1500));
        stack.Arrange(new Rect(0, 0, 400, 1500));

        // Act — hit test outside bounds (should short-circuit)
        var sw = Stopwatch.StartNew();
        const int iterations = 10000;
        for (int i = 0; i < iterations; i++)
        {
            stack.HitTest(999, 999);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"HitTest miss (outside bounds): {avgMs:F5} ms/hit ({iterations} iterations)");

        avgMs.Should().BeLessThan(0.1, "hit test miss should short-circuit extremely fast");
    }
}

public class DirtyRegionPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public DirtyRegionPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InvalidateRegion_ManySmallRegions_MergesEfficiently()
    {
        // Arrange — simulate rapid invalidation of small adjacent regions
        // We test the merge logic directly on SkiaView's Invalidate which
        // calls through to the rendering engine. Since we can't create a
        // real rendering engine without X11, test the view invalidation path.
        var views = new List<BenchView>();
        var stack = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        for (int i = 0; i < 50; i++)
        {
            var v = new BenchView { WidthRequest = 400, HeightRequest = 20 };
            views.Add(v);
            stack.AddChild(v);
        }

        stack.Measure(new Size(400, 1000));
        stack.Arrange(new Rect(0, 0, 400, 1000));

        // Act — invalidate each child rapidly
        var sw = Stopwatch.StartNew();
        const int iterations = 100;
        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var v in views)
                v.Invalidate();
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Invalidate 50 views: {avgMs:F3} ms/batch ({iterations} iterations)");

        avgMs.Should().BeLessThan(5.0, "invalidating 50 views in a batch should be fast");
    }

    [Fact]
    public void InvalidateMeasure_Propagation_Under1ms()
    {
        // Arrange — deep tree, invalidate at leaf, check propagation speed
        var root = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        var current = root;
        SkiaStackLayout? leaf = null;
        for (int i = 0; i < 15; i++)
        {
            var child = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
            current.AddChild(child);
            current = child;
            leaf = child;
        }
        var leafView = new BenchView { WidthRequest = 100, HeightRequest = 30 };
        current.AddChild(leafView);

        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));

        // Act — invalidate from the leaf repeatedly
        var sw = Stopwatch.StartNew();
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            leafView.InvalidateMeasure();
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"InvalidateMeasure propagation (15 deep): {avgMs:F4} ms/call ({iterations} iterations)");

        avgMs.Should().BeLessThan(1.0, "invalidation propagation through 15 levels should be sub-millisecond");
    }
}

public class DrawPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public DrawPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Draw_FlatLayout_100Children_Under10ms()
    {
        // Arrange
        var stack = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        for (int i = 0; i < 100; i++)
            stack.AddChild(new BenchView { WidthRequest = 400, HeightRequest = 30 });

        stack.Measure(new Size(400, 3000));
        stack.Arrange(new Rect(0, 0, 400, 3000));

        using var bitmap = new SKBitmap(400, 3000);
        using var canvas = new SKCanvas(bitmap);

        // Warmup
        stack.Draw(canvas);

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 50;
        for (int i = 0; i < iterations; i++)
        {
            canvas.Clear();
            stack.Draw(canvas);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Draw 100-child flat stack: {avgMs:F3} ms/frame ({iterations} iterations)");

        avgMs.Should().BeLessThan(10.0, "drawing 100 simple views should be fast");
    }

    [Fact]
    public void Draw_Labels_50Items_Under20ms()
    {
        // Arrange — labels are more expensive (text shaping)
        var stack = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };
        for (int i = 0; i < 50; i++)
            stack.AddChild(new SkiaLabel { Text = $"Item {i}: Sample label text", FontSize = 14 });

        stack.Measure(new Size(400, 2000));
        stack.Arrange(new Rect(0, 0, 400, 2000));

        using var bitmap = new SKBitmap(400, 2000);
        using var canvas = new SKCanvas(bitmap);

        // Warmup
        stack.Draw(canvas);

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 20;
        for (int i = 0; i < iterations; i++)
        {
            canvas.Clear();
            stack.Draw(canvas);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Draw 50 labels with text: {avgMs:F3} ms/frame ({iterations} iterations)");

        avgMs.Should().BeLessThan(20.0, "drawing 50 labels should complete within frame budget");
    }

    [Fact]
    public void Draw_MixedControls_Under15ms()
    {
        // Arrange — realistic mix of controls
        var stack = new SkiaStackLayout { Orientation = Microsoft.Maui.Platform.StackOrientation.Vertical };

        for (int i = 0; i < 10; i++)
        {
            stack.AddChild(new SkiaLabel { Text = $"Section {i}", FontSize = 18, FontAttributes = FontAttributes.Bold });
            stack.AddChild(new SkiaEntry { Text = $"Input {i}", Placeholder = "Enter text..." });
            stack.AddChild(new SkiaButton { Text = $"Button {i}" });
            stack.AddChild(new SkiaCheckBox { IsChecked = i % 2 == 0 });
            stack.AddChild(new SkiaProgressBar { Progress = i / 10.0 });
        }

        stack.Measure(new Size(400, 5000));
        stack.Arrange(new Rect(0, 0, 400, 5000));

        using var bitmap = new SKBitmap(400, 5000);
        using var canvas = new SKCanvas(bitmap);

        // Warmup
        stack.Draw(canvas);

        // Act
        var sw = Stopwatch.StartNew();
        const int iterations = 20;
        for (int i = 0; i < iterations; i++)
        {
            canvas.Clear();
            stack.Draw(canvas);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"Draw 50 mixed controls: {avgMs:F3} ms/frame ({iterations} iterations)");

        avgMs.Should().BeLessThan(15.0, "drawing a realistic mix of 50 controls should fit in a frame budget");
    }
}

public class ResourceCachePerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ResourceCachePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TypefaceCache_HitRate_Under01ms()
    {
        // Arrange
        using var cache = new Microsoft.Maui.Platform.Linux.Rendering.ResourceCache();

        // Warmup — populate cache
        var style = new SKFontStyle(400, 5, SKFontStyleSlant.Upright);
        cache.GetTypeface("Sans", style);
        cache.GetTypeface("Serif", style);
        cache.GetTypeface("Monospace", style);

        // Act — repeated cache hits
        var sw = Stopwatch.StartNew();
        const int iterations = 10000;
        for (int i = 0; i < iterations; i++)
        {
            string family = (i % 3) switch
            {
                0 => "Sans",
                1 => "Serif",
                _ => "Monospace"
            };
            cache.GetTypeface(family, style);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        _output.WriteLine($"ResourceCache typeface lookup: {avgMs:F5} ms/lookup ({iterations} iterations)");

        avgMs.Should().BeLessThan(0.1, "cached typeface lookup should be extremely fast");
    }
}
