// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

// The project sets <GenerateAssemblyInfo>false</GenerateAssemblyInfo>, so the
// <InternalsVisibleTo> MSBuild item doesn't get materialized into the assembly
// — we declare it directly here. Tests need to see internal helpers (text
// encoding conversion, etc.) without having to widen the public surface.
[assembly: InternalsVisibleTo("OpenMaui.Controls.Linux.Tests")]

// The third-party compatibility suite (tests/Compat) hosts pages the same way.
[assembly: InternalsVisibleTo("OpenMaui.Compat.Tests")]

// The out-of-process WebView scenario host (tests/WebViewHost) drives the
// render interop (EGL) and reads WpeWebView's frame-path diagnostics.
[assembly: InternalsVisibleTo("WebViewHost")]

// The headless benchmark suite (tools/Benchmarks) hosts windows through
// WindowContext and drives LinuxTicker with a fake clock, like the tests.
[assembly: InternalsVisibleTo("OpenMaui.Benchmarks")]

// The MediaElement package (shipped in lockstep with this one) reuses the
// DMA-BUF texture importer and EGL interop for zero-copy video frames.
[assembly: InternalsVisibleTo("OpenMaui.Controls.Linux.MediaElement")]

// The Syncfusion bridge package (also lockstep) observes routed pointer
// events and animation ticks to drive controls whose native input and
// invalidation are stubbed in Syncfusion's platform-neutral build.
[assembly: InternalsVisibleTo("OpenMaui.Controls.Linux.Syncfusion")]
