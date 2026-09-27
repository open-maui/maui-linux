# Performance benchmarks and regression gate

`tools/Benchmarks` is a repeatable, headless benchmark suite for the platform. It drives the real
rendering stack (a MAUI app built with `UseLinux`, the platform handlers, `WindowContext`,
`SkiaRenderingEngine`, the production `RasterRenderTarget`, the platform ticker and animation
manager) with no display server. Where a GPU is available it also runs the rendering scenarios on
a headless EGL context. The same tool compares a run against a committed baseline
(`docs/perf-baseline.json`) and fails when a gated metric regresses beyond its threshold.

## Running

```bash
# Full suite, one run (about 3 minutes: each scenario runs in 3 fresh processes)
dotnet run --project tools/Benchmarks -c Release

# Some scenarios only (prefix match), JSON output too
dotnet run --project tools/Benchmarks -c Release -- --scenario scroll,text-1.0x --json /tmp/bench.json

# Regression gate against the committed baseline (exit code 1 on a regression)
dotnet run --project tools/Benchmarks -c Release -- --baseline docs/perf-baseline.json --compare

# Same, the way a release is gated: median of 3 runs
dotnet run --project tools/Benchmarks -c Release -- --baseline docs/perf-baseline.json --compare --runs 3

# Re-record the baseline (median of 3 runs by default; keeps the gates already in the file)
dotnet run --project tools/Benchmarks -c Release -- --baseline docs/perf-baseline.json --update-baseline
```

| Option | Meaning |
|--------|---------|
| `--runs N` | Run the suite N times; every reported statistic is the median over the runs. Default 1, or 3 with `--update-baseline` |
| `--trials N` | Processes per scenario within a run (default 3). For timing distributions the trial with the lowest p50 is kept; counts and memory take the median |
| `--scenario a,b` | Only scenarios whose id starts with one of the prefixes |
| `--no-gpu` | Skip the GPU variants |
| `--render-node PATH` | DRM render node for the GPU variants (default `/dev/dri/renderD128`; falls back to `EGL_MESA_platform_surfaceless`) |
| `--json FILE` | Also write all metrics as JSON |
| `--baseline FILE --compare` | Compare with the baseline, print the gate table, exit 1 on a regression |
| `--warn-only` | With `--compare`: report regressions but exit 0 |
| `--update-baseline` | Write this run's metrics to the baseline file |
| `--list` | List the scenarios |

Close other heavy work before recording a baseline or gating a release. A laptop CPU's clock drops
when all cores are busy, and that slows every scenario by the same factor. The suite reduces the
effect but cannot remove it (see [Repeatability](#repeatability)).

## How it works

- **Process isolation.** The parent process only orchestrates. Every scenario runs in a child
  process (the same executable with `--child`), so the JIT state, caches and GC heap of one
  scenario never affect another, and startup is always measured cold. `WAYLAND_DISPLAY` and
  `DISPLAY` are removed from the child environment, so nothing can reach a desktop.
- **Headless host.** `BenchHost` (in `Harness.cs`) wires the application the way
  `LinuxApplication.Run` does, minus the native window and event loop. It builds a `MauiApp` with
  `UseLinux`, creates a real `Application` and `Window`, attaches a `WindowContext` to a fake
  display window with a `SkiaRenderingEngine`, and renders the page through `LinuxViewRenderer`.
  One frame is the body of the run loop: `LinuxTicker.PumpAll()`, the cursor-blink update, then
  `WindowContext.Render()`. The pieces are copied from `tests/Views/HeadlessMauiHost.cs` and
  `tests/HeadlessMaui.cs`; the tool does not reference the test project.
- **Targets.** The raster variant uses the production `RasterRenderTarget`. The fake window's
  `Present` copies each frame into a buffer, as the wl_shm path does. The GPU variant uses an
  offscreen EGL context (`EGL_EXT_platform_device` on the render node, else surfaceless Mesa)
  with a Skia GPU surface. Like the EGL swapchain target, it does not preserve contents, so every
  frame is a full repaint. `EndFrame` flushes and waits for the GPU, so a GPU frame time is the
  time until the frame is finished on the GPU. There is no compositor or vsync pacing.
- **Frames that were really rendered.** A frame counts only when the target received it (the
  raster `Present`, or the GPU `EndFrame`). The engine skips frames when nothing is dirty. The
  scenarios check that each measured iteration produced exactly one frame, and the idle scenario
  checks that none did.
- **Fake clock.** The animation scenario registers the platform `LinuxTicker` and
  `LinuxAnimationManager` with a fake clock that advances exactly 1/60 s per frame. Animation
  progress therefore does not depend on how long a frame took, and the frames per animation are
  identical in every run.
- **Fixed work.** Iteration counts are fixed: 60 warm-up and 400 measured frames for scroll and
  text, 240 resizes, 300 animation ticks, and 400 layout passes. A `GC.Collect()` runs before
  each measured loop.
- **Statistics.** avg, p50, p95, p99 and max use nearest-rank percentiles, the same definition as
  `OPENMAUI_RENDER_STATS`. A distribution is computed over 5 consecutive blocks of samples, and
  each statistic is the median over the blocks (max stays the overall maximum). A burst of outside
  load that lands in one block is voted out. The p99 and max still show GC pauses, because a pause
  hits every block.

## Scenarios

Metric names are `scenario/target/measure`, for example `scroll-10k/raster/frame_ms`.

| Scenario | Targets | What it measures |
|----------|---------|------------------|
| `startup` | raster | 12 cold processes, of which the first 2 are discarded. `build_ms` covers `MauiApp.CreateBuilder().UseMauiApp().UseLinux().Build()`. `first_frame_ms` runs from entering `Main` to the first presented frame of a representative settings page (a heading, a paragraph, 2 entries, switches and checkboxes in a grid, a slider, a progress bar, a border and 3 buttons). It includes assembly load and JIT. `process_to_frame_ms` is measured from the process start time |
| `idle` | raster | After a 2 s settle, 2 s of run-loop iterations at the loop's 16 ms idle cadence with nothing dirty. `frames` must be 0. Also reports `loop_us` (cost of one idle iteration; the engine still measures and arranges the root) and `cpu_pct` (process CPU over wall time, not gated) |
| `memory` | raster | Working set and GC heap (after a full collection) after the first frame and after 1,000 full repaints of the representative page. Also reports `gc_heap_growth_kb` |
| `scroll-1k`, `scroll-10k` | raster, gpu | A `CollectionView` with 1,000 or 10,000 items, each a two-label `Grid` from a `DataTemplate` with bindings. The scenario sends a wheel event (20 logical px) through the window's input path, then renders; the frame time covers both. Warm-up scrolls down and back to the top, then 400 frames are measured. `realised` counts the item views that exist afterwards, which shows how many item views were created |
| `resize` | raster, gpu | Cycles through 8 window sizes (640x480 to 1920x1080, plus portrait), 30 times. `latency_ms` runs from the resize event (the compositor configure) to the finished frame |
| `animation` | raster, gpu | `FadeToAsync` together with `TranslateToAsync` (500 ms each), 12 legs, the first 2 discarded. Reports `frame_ms` (ticker pump plus render), `frames_per_500ms` (must be 30), and `stalled_ticks` (ticks during a running animation that produced no frame; must be 0) |
| `text-1.0x`, `text-1.75x` | raster, gpu | A page of 40 labels (sizes 7.7 to 19.6 pt, wrapping paragraphs, bold and italic, accented text), fully repainted each frame at scale 1.0 or 1.75 |
| `text-update` | raster, gpu | The same page, but every label gets new text each frame (measure, shape and draw), at scale 1.0 |
| `layout-grid` | raster | A `Grid` of 25 x 20 = 500 labels: `Measure` plus `Arrange` of the layout's platform view at 800 x 600 |
| `layout-flex` | raster | A wrapping `FlexLayout` with 500 labels, measured the same way |

The frame-based scenarios also report `gc_count`, `gc_gen2_count` and `gc_pause_ms` for the
measured loop. The logical window is 800 x 600.

## Current numbers

Recorded on 2026-09-26 on an Intel Core i7-10875H (8 cores, 16 threads, 31 GB) with Mesa Intel UHD
Graphics (CML GT2) on `/dev/dri/renderD128`, running Fedora Linux 44 (KDE Plasma), kernel 7.1.5,
and .NET 10.0.10. Values are the median of 3 runs x 3 trials (`docs/perf-baseline.json`). Times
are in ms unless noted.

| Metric | avg | p50 | p95 | p99 | max |
|--------|-----|-----|-----|-----|-----|
| startup: MAUI app build | 508.9 | 506.8 | 518.3 | | |
| startup: `Main` to first frame | 1065.3 | 1059.8 | 1091.9 | | |
| startup: process start to first frame | 1085.5 | 1077.7 | 1113.0 | | |
| idle: frames in 2 s | 0 | | | | |
| idle: loop iteration (µs) | 3.7 | 4.1 | 4.8 | 5.0 | 6.8 |
| scroll-1k raster | 2.60 | 2.31 | 3.22 | 3.90 | 38.5 |
| scroll-1k gpu | 2.97 | 2.52 | 3.52 | 4.23 | 40.3 |
| scroll-10k raster | 2.46 | 1.75 | 2.42 | 4.53 | 61.0 |
| scroll-10k gpu | 2.62 | 1.96 | 2.74 | 3.80 | 61.6 |
| resize raster | 3.38 | 3.08 | 4.98 | 5.39 | 8.80 |
| resize gpu | 1.67 | 1.65 | 1.90 | 2.13 | 6.01 |
| animation raster | 0.63 | 0.63 | 0.66 | 0.74 | 4.39 |
| animation gpu | 0.74 | 0.74 | 0.87 | 0.91 | 6.74 |
| text 1.0x raster | 4.95 | 3.56 | 6.18 | 52.7 | 54.4 |
| text 1.0x gpu | 5.36 | 3.54 | 6.52 | 52.7 | 61.5 |
| text 1.75x raster | 5.67 | 4.07 | 7.14 | 54.5 | 67.7 |
| text 1.75x gpu | 5.43 | 3.71 | 6.47 | 54.6 | 63.4 |
| text-update raster | 4.12 | 3.21 | 5.56 | 49.9 | 52.8 |
| text-update gpu | 4.51 | 3.33 | 5.99 | 53.9 | 54.9 |
| layout-grid (500 labels) | 2.43 | 1.90 | 2.34 | 18.2 | 38.4 |
| layout-flex (500 labels) | 2.84 | 2.06 | 3.50 | 9.78 | 26.5 |

| Scalar | Value |
|--------|-------|
| Working set after the first frame / after 1,000 frames | 121.4 MB / 145.5 MB |
| GC heap after the first frame / after 1,000 frames | 3.72 MB / 3.78 MB (+59 KB) |
| Item views realised after 400 scroll frames (8,000 px), 1,000 items / 10,000 items | 197 / 197 |
| Animation: frames per 500 ms leg / ticks without a frame | 30 / 0 |
| GC pauses per 400 text frames | 6 to 8 gen0 collections, 280 to 400 ms in total |

What the numbers show:

- **Rendering stops when idle.** The idle loop renders no frames. An idle iteration costs about
  4 µs, and process CPU stays under 1 % (most of that is runtime background work).
- **Item creation is virtualised, but item views are never recycled.** 197 item views exist after
  scrolling 8,000 px, whether the list has 1,000 or 10,000 items. Scroll frame time does not grow
  with the item count. However, `SkiaItemsView._itemViewCache` is only cleared when the items
  source or the theme changes, so a list scrolled to its end holds one view per item.
- **The text p99 is garbage collection, not drawing.** Each text frame takes 3.5 to 4 ms at p50.
  The roughly 50 ms spikes at p99 are gen0 collections, and each one finalises about 230,000
  `SkiaSharp.SKString` objects (found with a `GCFinalizeObject` event listener). They come from
  `FontFallbackManager.ShapeTextWithFallback`, which compares
  `typeface.FamilyName != currentTypeface.FamilyName` for every rune. Each `FamilyName` read
  allocates a finalizable native string wrapper, so this costs two per character per frame.
  Comparing the typefaces by reference first would remove almost all of them. The same
  collections cause the 40 to 60 ms scroll maximum.
- **Startup is dominated by the app build.** In a cold process, `MauiApp.CreateBuilder()` through
  `Build()` with `UseLinux` takes about 500 ms of the roughly 1,060 ms to the first frame.

## Repeatability

The same code was measured repeatedly on the baseline machine while other builds and test runs
were active (load average 1.2 to 4). Spread is `(max - min) / median` across 3 runs:

| Configuration | Median p50 spread | Median p95 spread |
|---------------|-------------------|-------------------|
| 1 process per scenario, one distribution over all samples (load average 9 to 11) | 18 % | 51 % |
| Block medians, 1 process per scenario (load average falling from 11 to 4) | 22 % | 47 % |
| Block medians, 1 process per scenario (load average 3 to 10) | 10 % | 34 % |
| Block medians, best of 3 trials per scenario: the committed configuration (load average 1.2 to 4) | 4.6 % | 12 % |

The load differed between these measurements, so the rows do not isolate the effect of each
technique. Keeping the best of 3 trials made the largest difference: a trial slowed by a burst of
outside load is discarded instead of averaged in.

In the committed configuration, most CPU-side metrics agree within 1 to 8 %. Examples: startup
3.7 %, animation raster 2.9 %, text 1.4 to 6 %, scroll-10k gpu 3.5 %, resize raster 6.8 %, memory
0 to 3.5 %.

The noisiest metrics are:

- GPU timings, which share the GPU with the desktop compositor: animation gpu 33 %, scroll-1k
  gpu 24 %.
- `layout-flex` (32 %).
- `idle/loop_us`, which measures single microseconds.

The gate thresholds allow for this (next section). A single `--compare` run is noisier than the
median of 3. Use `--runs 3` for release decisions.

## The regression gate

`docs/perf-baseline.json` contains:

- `machine`: CPU, thread count, memory, GPU renderer, OS, kernel, runtime, and a `fingerprint`.
  The fingerprint is a hash of the CPU model, thread count and memory size.
- `gates`: an ordered list of `{metric, stat, maxIncreasePct, minAbsDelta, maxAbsolute}`.
  `metric` is a pattern where `*` matches anything. For each baseline metric, the first gate
  whose pattern matches decides; a metric with no matching gate is reported only.
- `metrics`: every metric's avg, p50, p95, p99 and max, as the median over the recorded runs.

A gated metric fails when:

- `maxAbsolute` is set and the current value is above it. This is how "0 idle frames" and
  "0 stalled animation ticks" are enforced.
- Otherwise, the current value is above
  `max(baseline * (1 + maxIncreasePct / 100), baseline + minAbsDelta)`. The absolute floor keeps
  sub-millisecond metrics from failing on scheduler jitter.

Default gates:

| Pattern | Statistic | Limit |
|---------|-----------|-------|
| `idle/*/frames` | value | must be 0 |
| `animation/*/stalled_ticks` | value | must be 0 |
| `scroll-*/*/realised` | value | +10 % (at least +5): catches virtualisation breaking |
| `*/gpu/frame_ms` | p95 | +30 % (at least +0.5 ms) |
| `*/frame_ms` (raster) | p95 | +15 % (at least +0.25 ms) |
| `resize/gpu/latency_ms` | p95 | +30 % (at least +0.5 ms) |
| `resize/*/latency_ms` | p95 | +15 % (at least +0.25 ms) |
| `layout-*/*/ms` | p95 | +25 % (at least +0.25 ms) |
| `startup/*/first_frame_ms`, `startup/*/build_ms` | p50 | +20 % (at least +10 ms) |
| `memory/*/*_mb` | value | +20 % (at least +2 MB) |
| `idle/*/loop_us` | p50 | +25 % (at least +20 µs) |

`--compare` prints each gated metric with its baseline value, current value, change, limit and
result. It exits with 1 when any gated metric regressed, or 0 with `--warn-only`. Metrics missing
from the current run, because they were filtered out with `--scenario` or had no GPU, are listed
as `MISSING` and do not fail. New metrics that are not in the baseline are listed as ungated.

If the machine fingerprint differs from the baseline's, `--compare` prints a warning with both
machine descriptions. Numbers from different hardware are not comparable, so record a local
baseline with `--update-baseline` before relying on the gate. A different GPU renderer gets its own
warning for the `*/gpu/*` metrics. `--no-gpu` does not change the fingerprint.

`--update-baseline` rewrites `metrics`, `machine`, `timestamp` and `runs`, and keeps the `gates`
already in the file, so hand-tuned thresholds survive a re-record.

### Demonstration

To check the gate, a regression was injected temporarily and then reverted. The idle loop marked
the window dirty every 10th iteration, and each scroll frame gained a `Thread.Sleep(1)`. The
comparison was run with
`--baseline docs/perf-baseline.json --compare --scenario scroll-1k,idle`:

```
idle/raster/frames                         value      0.000      13.00       new <= 0.000                 REGRESSED
scroll-1k/gpu/frame_ms                     p95        3.518      3.720     +5.7% +30% (min +0.500) = 4.574 ok
scroll-1k/raster/frame_ms                  p95        3.220      3.918    +21.7% +15% (min +0.250) = 3.703 REGRESSED

Performance regression detected.          (exit code 1; with --warn-only: exit code 0)
```

In the same session, an unmodified single-run `--compare` of the full suite passed. Every gated
metric was within its limit; the largest change was `layout-flex` p95 at +16 % against a +25 %
limit.

### CI

`.gitea/workflows/ci.yml` runs
`--baseline docs/perf-baseline.json --compare --warn-only --no-gpu` after the tests. Shared runners
are noisy, have different hardware from the baseline machine (so the fingerprint warning is
expected), and may only have a software GL, so the step reports but never fails the build.
Release gating runs `--compare --runs 3` on the baseline machine.

## Limitations

- There is no compositor, so frame times do not include compositor latency, vsync, or wl_shm
  buffer release. The raster `Present` copies into memory instead of a shared-memory pool.
- GPU frame times use a synchronous flush on an offscreen surface. They measure GPU work, not
  swap or presentation.
- Power draw is not measured. The roadmap's "power" item needs RAPL (`/sys/class/powercap`)
  access, which normally requires root.
- Each target runs one fixed set of pages. A new platform feature needs a scenario before the
  gate covers it.
