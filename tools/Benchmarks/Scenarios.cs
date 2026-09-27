// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Platform;
using GridLength = Microsoft.Maui.GridLength;
using FlexWrap = Microsoft.Maui.Layouts.FlexWrap;
using FlexJustify = Microsoft.Maui.Layouts.FlexJustify;
using FlexAlignItems = Microsoft.Maui.Layouts.FlexAlignItems;
using Switch = Microsoft.Maui.Controls.Switch;

namespace OpenMaui.Benchmarks;

/// <summary>A scenario: runs in its own process and returns named metrics.</summary>
internal sealed record Scenario(string Id, string Description, bool SupportsGpu, Func<TargetKind, OffscreenGl?, Dictionary<string, Metric>> Run);

internal static class Scenarios
{
    // Fixed iteration counts: every run does exactly the same work.
    private const int WarmupFrames = 60;
    private const int MeasuredFrames = 400;

    public static readonly Scenario[] All =
    {
        new("startup", "Cold start in a fresh process: MAUI app build, then page, handlers and first frame (12 processes, 2 discarded)", false, (_, _) => Startup()),
        new("idle", "2 s of run-loop iterations with nothing dirty: frames must be zero; loop cost and CPU", false, (t, gl) => Idle(t, gl)),
        new("memory", "Working set and GC heap after the first frame and after 1,000 full repaints", false, (t, gl) => Memory(t, gl)),
        new("scroll-1k", "CollectionView, 1,000 items: wheel-scroll 20 px per frame for 400 frames", true, (t, gl) => Scroll(1_000, t, gl)),
        new("scroll-10k", "CollectionView, 10,000 items: wheel-scroll 20 px per frame for 400 frames", true, (t, gl) => Scroll(10_000, t, gl)),
        new("resize", "Resize through 8 sizes, 30 cycles: configure to presented frame", true, (t, gl) => Resize(t, gl)),
        new("animation", "FadeTo + TranslateTo (500 ms each way, 10 round trips) on the platform ticker and animation manager, fake 60 Hz clock", true, (t, gl) => Animation(t, gl)),
        new("text-1.0x", "Page of 40 labels (wrapping, sizes 11-28), full repaint at scale 1.0", true, (t, gl) => Text(1.0f, update: false, t, gl)),
        new("text-1.75x", "Page of 40 labels, full repaint at scale 1.75", true, (t, gl) => Text(1.75f, update: false, t, gl)),
        new("text-update", "Page of 40 labels, every label gets new text each frame (measure + shape + draw), scale 1.0", true, (t, gl) => Text(1.0f, update: true, t, gl)),
        new("layout-grid", "Grid 25 x 20 = 500 labels: measure + arrange of the layout at 800 x 600", false, (t, gl) => Layout(GridPage, t, gl)),
        new("layout-flex", "FlexLayout (wrap) with 500 labels: measure + arrange of the layout at 800 x 600", false, (t, gl) => Layout(FlexPage, t, gl)),
    };

    // ---------------------------------------------------------------- pages

    /// <summary>A typical form page: headings, paragraph, entries, toggles, buttons, a card.</summary>
    public static Page RepresentativePage()
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            RowSpacing = 6,
            ColumnSpacing = 12,
        };
        string[] settings = { "Notifications", "Dark mode", "Sync over mobile data", "Send usage statistics" };
        for (int i = 0; i < settings.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(new Label { Text = settings[i], VerticalOptions = LayoutOptions.Center }, 0, i);
            grid.Add(i % 2 == 0 ? new Switch { IsToggled = i == 0 } : new CheckBox { IsChecked = true }, 1, i);
        }

        return new ContentPage
        {
            Title = "Settings",
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Account settings", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label
                    {
                        Text = "Changes are saved as you make them. Your profile is visible to the members of your workspace; " +
                               "the e-mail address is used for sign-in and notifications only.",
                        LineBreakMode = LineBreakMode.WordWrap,
                    },
                    new Entry { Placeholder = "Display name" },
                    new Entry { Text = "someone@example.com" },
                    grid,
                    new Slider { Minimum = 0, Maximum = 100, Value = 40 },
                    new ProgressBar { Progress = 0.6 },
                    new Border
                    {
                        Padding = new Thickness(12),
                        Stroke = Colors.LightGray,
                        Content = new Label { Text = "Storage: 3.2 GB of 15 GB used" },
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { new Button { Text = "Save" }, new Button { Text = "Cancel" }, new Button { Text = "Sign out" } },
                    },
                },
            },
        };
    }

    private sealed record Row(string Title, string Detail);

    private static Page CollectionPage(int count, out CollectionView view)
    {
        var items = new List<Row>(count);
        for (int i = 0; i < count; i++)
            items.Add(new Row($"Item {i}: {(i % 3 == 0 ? "quarterly report" : i % 3 == 1 ? "meeting notes" : "invoice")}", $"{i * 7 % 1000} KB"));

        view = new CollectionView
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() =>
            {
                var g = new Grid
                {
                    Padding = new Thickness(12, 8),
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                };
                var title = new Label();
                title.SetBinding(Label.TextProperty, nameof(Row.Title));
                var detail = new Label { TextColor = Colors.Gray };
                detail.SetBinding(Label.TextProperty, nameof(Row.Detail));
                g.Add(title, 0, 0);
                g.Add(detail, 1, 0);
                return g;
            }),
        };
        return new ContentPage { Content = view };
    }

    private static Page TextPage(out List<Label> labels)
    {
        labels = new List<Label>();
        var grid = new Grid
        {
            Padding = new Thickness(12),
            ColumnSpacing = 12,
            RowSpacing = 2,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
        };
        double[] sizes = { 11, 12, 13, 14, 15, 16, 18, 20, 24, 28 };
        for (int i = 0; i < 40; i++)
        {
            int row = i / 2, col = i % 2;
            if (col == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new Label
            {
                Text = i % 5 == 4
                    ? $"Paragraph {i}: the quick brown fox jumps over the lazy dog, then naïve café owners serve crème brûlée."
                    : $"Label {i} — Sphinx of black quartz, judge my vow",
                FontSize = sizes[i % sizes.Length] * 0.7,
                FontAttributes = i % 7 == 0 ? FontAttributes.Bold : i % 11 == 0 ? FontAttributes.Italic : FontAttributes.None,
                LineBreakMode = LineBreakMode.WordWrap,
            };
            labels.Add(label);
            grid.Add(label, col, row);
        }
        return new ContentPage { Content = grid };
    }

    private static Page GridPage(out View layout)
    {
        var grid = new Grid { RowSpacing = 1, ColumnSpacing = 1 };
        for (int c = 0; c < 20; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int r = 0; r < 25; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < 20; c++)
                grid.Add(new Label { Text = $"{r},{c}", FontSize = 10 }, c, r);
        }
        layout = grid;
        return new ContentPage { Content = grid };
    }

    private static Page FlexPage(out View layout)
    {
        var flex = new FlexLayout { Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.SpaceBetween, AlignItems = FlexAlignItems.Center };
        for (int i = 0; i < 500; i++)
            flex.Children.Add(new Label { Text = i % 4 == 0 ? $"tag-{i}-longer" : $"t{i}", FontSize = 10, Margin = new Thickness(2) });
        layout = flex;
        return new ContentPage { Content = flex };
    }

    // ------------------------------------------------------------ scenarios

    /// <summary>
    /// Runs in each startup child process. Everything before the first frame
    /// is on the clock: assembly load and JIT, the MAUI app build, page
    /// construction, handler creation, layout and the first render.
    /// </summary>
    private static Dictionary<string, Metric> Startup()
    {
        var host = new BenchHost(RepresentativePage);
        host.Frame();
        var mainToFrame = Program.SinceMain.Elapsed.TotalMilliseconds;
        double processToFrame;
        using (var self = Process.GetCurrentProcess())
            processToFrame = (DateTime.Now - self.StartTime).TotalMilliseconds;
        if (host.FramesRendered != 1)
            throw new InvalidOperationException($"expected one first frame, got {host.FramesRendered}");
        var result = new Dictionary<string, Metric>
        {
            ["build_ms"] = Metric.Scalar(host.BuildTime.TotalMilliseconds, "ms"),
            ["first_frame_ms"] = Metric.Scalar(mainToFrame, "ms"),
            ["process_to_frame_ms"] = Metric.Scalar(processToFrame, "ms"),
        };
        host.Dispose();
        return result;
    }

    private static Dictionary<string, Metric> Idle(TargetKind target, OffscreenGl? gl)
    {
        using var host = new BenchHost(RepresentativePage, target: target, gl: gl);
        // Settle for 2 s: the first frame, anything the first layout legitimately
        // dirties, and the runtime's background tier-up of startup code (which
        // would otherwise show up as idle CPU).
        var settle = Stopwatch.StartNew();
        while (settle.Elapsed < TimeSpan.FromSeconds(2)) { host.Frame(); Thread.Sleep(16); }

        int before = host.FramesRendered;
        var loop = new List<double>();
        using var self = Process.GetCurrentProcess();
        self.Refresh();
        var cpu0 = self.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        while (wall.Elapsed < TimeSpan.FromSeconds(2))
        {
            long t0 = Stopwatch.GetTimestamp();
            host.Frame();
            loop.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds * 1000.0);
            Thread.Sleep(16); // the run loop's poll() timeout when no events arrive
        }
        self.Refresh();
        var cpuMs = (self.TotalProcessorTime - cpu0).TotalMilliseconds;
        return new Dictionary<string, Metric>
        {
            ["frames"] = Metric.Scalar(host.FramesRendered - before, "frames"),
            ["iterations"] = Metric.Scalar(loop.Count, "count"),
            ["loop_us"] = Metric.FromBlocks(loop, "us"),
            ["cpu_pct"] = Metric.Scalar(100.0 * cpuMs / wall.Elapsed.TotalMilliseconds, "%"),
        };
    }

    private static Dictionary<string, Metric> Memory(TargetKind target, OffscreenGl? gl)
    {
        using var host = new BenchHost(RepresentativePage, target: target, gl: gl);
        host.Frame();
        var (ws0, heap0) = Snapshot();
        for (int i = 0; i < 1000; i++)
        {
            host.InvalidateAll();
            host.Frame();
        }
        var (ws1, heap1) = Snapshot();
        return new Dictionary<string, Metric>
        {
            ["ws_first_frame_mb"] = Metric.Scalar(ws0, "MB"),
            ["gc_heap_first_frame_mb"] = Metric.Scalar(heap0, "MB"),
            ["ws_after_1000_mb"] = Metric.Scalar(ws1, "MB"),
            ["gc_heap_after_1000_mb"] = Metric.Scalar(heap1, "MB"),
            ["gc_heap_growth_kb"] = Metric.Scalar((heap1 - heap0) * 1024, "KB"),
        };

        static (double Ws, double Heap) Snapshot()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            double heap = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
            using var p = Process.GetCurrentProcess();
            p.Refresh();
            return (p.WorkingSet64 / (1024.0 * 1024.0), heap);
        }
    }

    private static Dictionary<string, Metric> Scroll(int count, TargetKind target, OffscreenGl? gl)
    {
        CollectionView? view = null;
        using var host = new BenchHost(() => CollectionPage(count, out view), target: target, gl: gl);
        var skia = view!.Handler?.PlatformView as SkiaCollectionView
            ?? throw new InvalidOperationException("CollectionView has no SkiaCollectionView");
        int scrolledEvents = 0;
        skia.Scrolled += (_, _) => scrolledEvents++;
        host.Frame();

        float x = host.Window.Width / 2f, y = host.Window.Height / 2f;
        // Warm-up: down and back up again, so the measured pass starts at the top.
        for (int i = 0; i < WarmupFrames; i++) { host.Window.RaiseScroll(x, y, 0, 1); host.Frame(); }
        for (int i = 0; i < WarmupFrames; i++) { host.Window.RaiseScroll(x, y, 0, -1); host.Frame(); }
        GC.Collect();

        int framesBefore = host.FramesRendered;
        scrolledEvents = 0;
        var gc = GcWindow.Start();
        var samples = new List<double>(MeasuredFrames);
        for (int i = 0; i < MeasuredFrames; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            host.Window.RaiseScroll(x, y, 0, 1);
            host.Frame();
            samples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        var gcMetrics = gc.Stop();
        if (scrolledEvents != MeasuredFrames)
            throw new InvalidOperationException($"scroll moved on {scrolledEvents} of {MeasuredFrames} frames (content too short or hit-test missed)");
        int rendered = host.FramesRendered - framesBefore;

        int realised = 0;
        for (int i = 0; i < count; i++)
            if (skia.GetItemView(i) != null) realised++;

        return gcMetrics
            .With("frame_ms", Metric.FromBlocks(samples, "ms"))
            .With("frames_rendered", Metric.Scalar(rendered, "frames"))
            .With("realised", Metric.Scalar(realised, "views"));
    }

    private static Dictionary<string, Metric> Resize(TargetKind target, OffscreenGl? gl)
    {
        using var host = new BenchHost(RepresentativePage, target: target, gl: gl);
        host.Frame();
        (int W, int H)[] sizes = { (1024, 768), (1280, 800), (640, 480), (1920, 1080), (900, 700), (1366, 768), (720, 1280), (800, 600) };
        for (int c = 0; c < 2; c++)
            foreach (var (w, h) in sizes) { host.Window.RaiseResized(w, h); host.Frame(); }
        GC.Collect();

        var samples = new List<double>();
        int before = host.FramesRendered;
        var gc = GcWindow.Start();
        for (int c = 0; c < 30; c++)
        {
            foreach (var (w, h) in sizes)
            {
                long t0 = Stopwatch.GetTimestamp();
                host.Window.RaiseResized(w, h);
                host.Frame();
                samples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
        }
        var gcMetrics = gc.Stop();
        int rendered = host.FramesRendered - before;
        if (rendered != samples.Count)
            throw new InvalidOperationException($"{samples.Count} resizes produced {rendered} frames");
        return gcMetrics.With("latency_ms", Metric.FromBlocks(samples, "ms"));
    }

    private static Dictionary<string, Metric> Animation(TargetKind target, OffscreenGl? gl)
    {
        BoxView? box = null;
        using var host = new BenchHost(() =>
        {
            box = new BoxView { Color = Colors.CornflowerBlue, WidthRequest = 200, HeightRequest = 120, HorizontalOptions = LayoutOptions.Start };
            return new ContentPage
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20),
                    Spacing = 12,
                    Children = { new Label { Text = "Animated", FontSize = 20 }, box, new Label { Text = "Below the box" } },
                },
            };
        }, target: target, gl: gl);
        host.Frame();

        const double FrameMs = 1000.0 / 60.0;
        var samples = new List<double>();
        int stalled = 0, ticks = 0;
        var perCycle = new List<double>();
        GcWindow? gc = null;

        for (int cycle = 0; cycle < 12; cycle++)
        {
            bool measured = cycle >= 2; // two round trips of warm-up
            if (cycle == 2) { GC.Collect(); gc = GcWindow.Start(); }
            bool outward = cycle % 2 == 0;
            var fade = box!.FadeToAsync(outward ? 0.2 : 1.0, 500);
            var move = box!.TranslateToAsync(outward ? 300 : 0, 0, 500);
            int frames = 0;
            while (!(fade.IsCompleted && move.IsCompleted))
            {
                if (++frames > 600) throw new InvalidOperationException("animation did not finish in 600 frames");
                host.Clock.Advance(FrameMs);
                int before = host.FramesRendered;
                long t0 = Stopwatch.GetTimestamp();
                host.Frame();
                double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                if (!measured) continue;
                ticks++;
                if (host.FramesRendered == before) stalled++; // a tick that changed nothing on screen
                samples.Add(ms);
            }
            if (measured) perCycle.Add(frames);
        }

        return gc!.Stop()
            .With("frame_ms", Metric.FromBlocks(samples, "ms"))
            .With("ticks", Metric.Scalar(ticks, "ticks"))
            .With("stalled_ticks", Metric.Scalar(stalled, "ticks"))
            .With("frames_per_500ms", Metric.FromSamples(perCycle, "frames"));
    }

    private static Dictionary<string, Metric> Text(float scale, bool update, TargetKind target, OffscreenGl? gl)
    {
        List<Label>? labels = null;
        using var host = new BenchHost(() => TextPage(out labels), scale: scale, target: target, gl: gl);
        host.Frame();

        int frame = 0;
        void Step()
        {
            frame++;
            if (update)
            {
                for (int i = 0; i < labels!.Count; i++)
                    labels[i].Text = i % 5 == 4
                        ? $"Paragraph {i}, revision {frame}: the quick brown fox jumps over the lazy dog, then naïve café owners serve crème brûlée."
                        : $"Label {i} — value {frame * 31 + i}";
            }
            else
            {
                host.InvalidateAll();
            }
            host.Frame();
        }

        for (int i = 0; i < WarmupFrames; i++) Step();
        GC.Collect();
        int before = host.FramesRendered;
        var gc = GcWindow.Start();
        var samples = new List<double>(MeasuredFrames);
        for (int i = 0; i < MeasuredFrames; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            Step();
            samples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        var gcMetrics = gc.Stop();
        int rendered = host.FramesRendered - before;
        if (rendered != samples.Count)
            throw new InvalidOperationException($"{samples.Count} text frames produced {rendered} frames");
        return gcMetrics.With("frame_ms", Metric.FromBlocks(samples, "ms"));
    }

    private delegate Page LayoutPageFactory(out View layout);

    /// <summary>
    /// Measure + arrange of the layout's platform view, exactly what the page
    /// does for its content before drawing each frame (SkiaView keeps no
    /// measure cache, so every frame pays this).
    /// </summary>
    private static Dictionary<string, Metric> Layout(LayoutPageFactory page, TargetKind target, OffscreenGl? gl)
    {
        View? layout = null;
        using var host = new BenchHost(() => page(out layout), target: target, gl: gl);
        host.Frame();
        var view = layout!.Handler?.PlatformView as SkiaView
            ?? throw new InvalidOperationException("layout has no SkiaView");
        var size = new Size(800, 600);
        var rect = new Rect(0, 0, 800, 600);
        for (int i = 0; i < 20; i++) { view.Measure(size); view.Arrange(rect); }
        GC.Collect();
        var samples = new List<double>(MeasuredFrames);
        for (int i = 0; i < MeasuredFrames; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            view.Measure(size);
            view.Arrange(rect);
            samples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }
        return new Dictionary<string, Metric> { ["ms"] = Metric.FromBlocks(samples, "ms") };
    }
}

/// <summary>Garbage collections and total GC pause over a measured loop.</summary>
internal sealed class GcWindow
{
    private int _gen0, _gen2;
    private TimeSpan _pause;

    public static GcWindow Start() => new()
    {
        _gen0 = GC.CollectionCount(0),
        _gen2 = GC.CollectionCount(2),
        _pause = GC.GetTotalPauseDuration(),
    };

    public Dictionary<string, Metric> Stop()
    {
        if (Environment.GetEnvironmentVariable("BENCH_TRACE") == "1")
        {
            var i = GC.GetGCMemoryInfo(GCKind.Ephemeral);
            Console.Error.WriteLine($"last ephemeral GC: gen{i.Generation} pause {string.Join(",", i.PauseDurations.ToArray().Select(p => p.TotalMilliseconds.ToString("0.0")))} ms, " +
                $"heap {i.HeapSizeBytes / 1024} KB, promoted {i.PromotedBytes / 1024} KB, pinned {i.PinnedObjectsCount}, finalization pending {i.FinalizationPendingCount}, " +
                $"fragmented {i.FragmentedBytes / 1024} KB, committed {i.TotalCommittedBytes / 1024} KB");
        }
        return StopCore();
    }

    private Dictionary<string, Metric> StopCore() => new()
    {
        ["gc_count"] = Metric.Scalar(GC.CollectionCount(0) - _gen0, "GCs"),
        ["gc_gen2_count"] = Metric.Scalar(GC.CollectionCount(2) - _gen2, "GCs"),
        ["gc_pause_ms"] = Metric.Scalar((GC.GetTotalPauseDuration() - _pause).TotalMilliseconds, "ms"),
    };
}

internal static class MetricDictionaryExtensions
{
    public static Dictionary<string, Metric> With(this Dictionary<string, Metric> d, string key, Metric m)
    {
        d[key] = m;
        return d;
    }
}
