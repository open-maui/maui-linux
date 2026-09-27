// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>Outcome of one doctor check.</summary>
public enum DoctorStatus
{
    /// <summary>Present / working.</summary>
    Ok,
    /// <summary>Informational value, nothing to fix.</summary>
    Info,
    /// <summary>Works, but degraded or with a caveat.</summary>
    Warn,
    /// <summary>Not present. Fatal only when <see cref="DoctorCheck.Required"/>.</summary>
    Missing,
}

/// <summary>One row of the doctor report.</summary>
/// <param name="Name">Short label.</param>
/// <param name="Status">Outcome.</param>
/// <param name="Detail">What was found.</param>
/// <param name="Fix">Command (or instruction) that fixes a Warn/Missing row, when known.</param>
/// <param name="Required">True when the app cannot start without it.</param>
public sealed record DoctorCheck(string Name, DoctorStatus Status, string Detail, string? Fix = null, bool Required = false);

/// <summary>A titled group of checks.</summary>
public sealed class DoctorSection
{
    public DoctorSection(string title) => Title = title;

    public string Title { get; }

    public List<DoctorCheck> Checks { get; } = new();

    internal DoctorSection Add(DoctorCheck check)
    {
        Checks.Add(check);
        return this;
    }
}

/// <summary>Structured result of <see cref="OpenMauiDoctor.Run()"/>.</summary>
public sealed class DoctorReport
{
    public List<DoctorSection> Sections { get; } = new();

    public IEnumerable<DoctorCheck> AllChecks => Sections.SelectMany(s => s.Checks);

    /// <summary>True when any <see cref="DoctorCheck.Required"/> item is <see cref="DoctorStatus.Missing"/>.</summary>
    public bool HasMissingRequired => AllChecks.Any(c => c.Required && c.Status == DoctorStatus.Missing);

    /// <summary>Process exit code for the doctor run: 1 when a required item is missing, else 0.</summary>
    public int ExitCode => HasMissingRequired ? 1 : 0;
}

/// <summary>
/// <c>openmaui doctor</c>: reports the runtime, session, compositor globals,
/// GPU/renderer selection, scale detection and optional native dependencies,
/// with the exact install command for the detected distro. It runs inside the
/// platform (set <c>OPENMAUI_DOCTOR=1</c> or pass <c>--openmaui-doctor</c> to
/// any OpenMaui app) and calls the platform's own probes, so it reports what
/// the platform will actually select. Every probe is isolated: a missing
/// library becomes a Missing row, never a crash.
/// </summary>
public static class OpenMauiDoctor
{
    /// <summary>Environment variable that triggers the report at startup.</summary>
    public const string EnvironmentVariable = "OPENMAUI_DOCTOR";

    /// <summary>Command-line switch that triggers the report at startup.</summary>
    public const string CommandLineSwitch = "--openmaui-doctor";

    private const int WrapWidth = 120;

    // ------------------------------------------------------------------
    // Trigger
    // ------------------------------------------------------------------

    /// <summary>True when the doctor was requested via environment or command line.</summary>
    public static bool IsRequested(string[]? args)
        => IsRequested(Environment.GetEnvironmentVariable(EnvironmentVariable), args);

    internal static bool IsRequested(string? environmentValue, string[]? args)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            switch (environmentValue.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
            }
        }

        if (args != null)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, CommandLineSwitch, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Startup hook: runs the doctor, writes the report to stdout and returns
    /// the exit code. Never throws.
    /// </summary>
    internal static int RunAndPrint(LinuxApplicationOptions? options)
    {
        try
        {
            // Keep stdout to the report: probes log through DiagnosticLog, and
            // the process exits right after, so there is nothing to restore.
            DiagnosticLog.IsEnabled = false;
            var report = Run(options);
            bool color = !Console.IsOutputRedirected
                         && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            Console.Out.Write(Format(report, color));
            Console.Out.Flush();
            return report.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Out.WriteLine($"openmaui doctor failed: {ex}");
            return 1;
        }
    }

    // ------------------------------------------------------------------
    // Run
    // ------------------------------------------------------------------

    /// <summary>Runs every probe against this machine and session.</summary>
    public static DoctorReport Run() => Run(null);

    /// <summary>Runs every probe, honouring the app's configured options (renderer, display server, GTK mode).</summary>
    public static DoctorReport Run(LinuxApplicationOptions? options)
        => Run(options, DoctorProbes.CreateDefault());

    internal static DoctorReport Run(LinuxApplicationOptions? options, DoctorProbes probes)
    {
        var report = new DoctorReport();
        string? osRelease = Safe(probes.ReadOsRelease, null);
        var distro = DetectDistro(osRelease);

        report.Sections.Add(BuildRuntimeSection(osRelease, distro));

        var session = new DoctorSection("Session");
        report.Sections.Add(session);
        var serverType = Guard(session, "Display server", () => ProbeSession(session, options));

        WaylandWindow.WaylandProbeResult? wayland = null;
        EglProbe? egl = null;
        bool hasDisplayFallback = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

        if (serverType == DisplayServerType.Wayland)
        {
            var section = new DoctorSection("Wayland compositor");
            report.Sections.Add(section);
            wayland = Guard(section, "Wayland connection", () => ProbeWayland(section, distro, hasDisplayFallback, out egl));
        }
        else if (serverType == DisplayServerType.X11)
        {
            var section = new DoctorSection("X11");
            report.Sections.Add(section);
            Guard(section, "X11 connection", () => { ProbeX11(section, distro, out egl); return 0; });
        }

        var graphics = new DoctorSection("GPU and renderer");
        report.Sections.Add(graphics);
        Guard(graphics, "Renderer", () => { BuildGraphicsSection(graphics, options, serverType, egl, probes, distro); return 0; });

        var scale = new DoctorSection("Display scale");
        report.Sections.Add(scale);
        Guard(scale, "Scale factor", () => { BuildScaleSection(scale, serverType, wayland); return 0; });

        var input = new DoctorSection("Input method");
        report.Sections.Add(input);
        Guard(input, "IME backend", () =>
        {
            bool tiv3 = serverType == DisplayServerType.Wayland
                        && wayland != null
                        && wayland.Globals.Any(g => g.Interface == "zwp_text_input_manager_v3");
            input.Add(new DoctorCheck("IME backend", DoctorStatus.Info, InputMethodServiceFactory.DescribeSelection(tiv3)));
            return 0;
        });

        report.Sections.Add(BuildDependencySection(probes, distro));
        report.Sections.Add(BuildFontSection(probes, distro));

        return report;
    }

    // ------------------------------------------------------------------
    // Sections
    // ------------------------------------------------------------------

    private static DoctorSection BuildRuntimeSection(string? osRelease, DistroFamily distro)
    {
        var s = new DoctorSection("Runtime");
        Guard(s, ".NET", () => s.Add(new DoctorCheck(".NET",
            DoctorStatus.Ok,
            $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture}, {RuntimeInformation.RuntimeIdentifier})")));

        Guard(s, "OpenMaui", () =>
        {
            var asm = typeof(OpenMauiDoctor).Assembly;
            var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? asm.GetName().Version?.ToString();
            var plus = version?.IndexOf('+') ?? -1;
            if (plus > 0) version = version![..plus];
            if (version is null or "0.0.0.0") version = null;
            var location = string.IsNullOrEmpty(asm.Location) ? "" : $" ({asm.Location})";
            return s.Add(new DoctorCheck("OpenMaui", DoctorStatus.Info, $"OpenMaui.Controls.Linux{(version != null ? " " + version : "")}{location}"));
        });

        Guard(s, "OS", () =>
        {
            var pretty = ParseOsReleaseValue(osRelease, "PRETTY_NAME") ?? RuntimeInformation.OSDescription;
            return s.Add(new DoctorCheck("OS", DoctorStatus.Info,
                $"{pretty}, kernel {Safe(() => File.ReadAllText("/proc/sys/kernel/osrelease").Trim(), "?")}; install hints for: {DistroLabel(distro)}"));
        });

        Guard(s, "SkiaSharp native", () =>
        {
            var native = SkiaSharp.SkiaSharpVersion.Native;
            return s.Add(new DoctorCheck("SkiaSharp native", DoctorStatus.Ok, $"libSkiaSharp {native}", Required: true));
        }, failStatus: DoctorStatus.Missing, required: true,
           fix: "restore the SkiaSharp.NativeAssets.Linux package (libSkiaSharp.so must ship next to the app)");

        return s;
    }

    private static DisplayServerType ProbeSession(DoctorSection s, LinuxApplicationOptions? options)
    {
        string Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : "(unset)";

        s.Add(new DoctorCheck("Session type", DoctorStatus.Info, $"XDG_SESSION_TYPE={Env("XDG_SESSION_TYPE")}"));
        s.Add(new DoctorCheck("Desktop", DoctorStatus.Info,
            $"XDG_CURRENT_DESKTOP={Env("XDG_CURRENT_DESKTOP")}"));
        s.Add(new DoctorCheck("Display variables", DoctorStatus.Info,
            $"WAYLAND_DISPLAY={Env("WAYLAND_DISPLAY")}  DISPLAY={Env("DISPLAY")}  MAUI_PREFER_X11={Env("MAUI_PREFER_X11")}"));

        var configured = options?.DisplayServer ?? DisplayServerType.Auto;
        var detected = configured == DisplayServerType.Auto ? DisplayServerFactory.DetectDisplayServer() : configured;
        bool anyDisplay = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                          || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

        if (!anyDisplay && configured == DisplayServerType.Auto)
        {
            s.Add(new DoctorCheck("Display server", DoctorStatus.Missing,
                "neither WAYLAND_DISPLAY nor DISPLAY is set; run inside a graphical session",
                Required: true));
            return DisplayServerType.Auto;
        }

        var how = configured == DisplayServerType.Auto ? "auto-detected" : "LinuxApplicationOptions.DisplayServer";
        s.Add(new DoctorCheck("Display server", DoctorStatus.Ok,
            $"{DisplayServerFactory.GetDisplayServerName(detected)} ({how})"));

        if (options?.UseGtk == true)
            s.Add(new DoctorCheck("Window host", DoctorStatus.Info, "GTK-hosted (LinuxApplicationOptions.UseGtk = true)"));

        return detected;
    }

    private static readonly (string Interface, string Purpose, bool Required)[] s_waylandGlobals =
    {
        ("wl_compositor", "surfaces", true),
        ("wl_shm", "raster buffers", true),
        ("xdg_wm_base", "toplevel windows", true),
        ("wl_seat", "pointer / keyboard input", false),
        ("wl_output", "monitor enumeration / integer scale", false),
        ("wp_fractional_scale_manager_v1", "fractional scaling (wp_fractional_scale_v1)", false),
        ("wp_viewporter", "fractional-scale buffer mapping", false),
        ("zxdg_decoration_manager_v1", "server-side decorations (absent: client-side titlebar)", false),
        ("zwp_text_input_manager_v3", "native IME (zwp_text_input_v3)", false),
        ("zwp_linux_dmabuf_v1", "dmabuf buffer sharing", false),
        ("wl_data_device_manager", "clipboard and drag-and-drop", false),
        ("zwp_primary_selection_device_manager_v1", "primary selection (middle-click paste)", false),
        ("wp_cursor_shape_manager_v1", "compositor-drawn cursors", false),
    };

    private static WaylandWindow.WaylandProbeResult ProbeWayland(DoctorSection s, DistroFamily distro, bool hasX11Fallback, out EglProbe? egl)
    {
        // Protocol shim first: without it the Wayland backend cannot start.
        try
        {
            var shim = WaylandWindow.ProbeProtocolShim(out var missingSymbols);
            if (shim == null)
            {
                s.Add(new DoctorCheck("libopenmaui_wl.so", hasX11Fallback ? DoctorStatus.Warn : DoctorStatus.Missing,
                    "Wayland protocol shim not found next to OpenMaui.Controls.Linux.dll"
                    + (hasX11Fallback ? "; the app will fall back to X11 (XWayland)" : ""),
                    InstallHintOrAll(Dependency.ProtocolShim, distro), Required: !hasX11Fallback));
            }
            else if (missingSymbols.Count > 0)
            {
                s.Add(new DoctorCheck("libopenmaui_wl.so", DoctorStatus.Warn,
                    $"{shim} is missing {string.Join(", ", missingSymbols)} (stale build?)",
                    InstallHintOrAll(Dependency.ProtocolShim, distro)));
            }
            else
            {
                s.Add(new DoctorCheck("libopenmaui_wl.so", DoctorStatus.Ok, shim));
            }
        }
        catch (Exception ex)
        {
            s.Add(new DoctorCheck("libopenmaui_wl.so", DoctorStatus.Warn, $"probe failed: {ex.Message}"));
        }

        EglProbe? eglLocal = null;
        WaylandWindow.WaylandProbeResult result;
        try
        {
            result = WaylandWindow.ProbeCompositor(display =>
            {
                eglLocal = EglProbe.Run(Egl.EGL_PLATFORM_WAYLAND_KHR, display);
            });
        }
        catch (Exception ex)
        {
            egl = null;
            bool dllMissing = ex is DllNotFoundException;
            s.Add(new DoctorCheck("Wayland connection",
                hasX11Fallback ? DoctorStatus.Warn : DoctorStatus.Missing,
                (dllMissing ? "libwayland-client.so.0 not found" : ex.Message)
                + (hasX11Fallback ? "; the app will fall back to X11 (XWayland)" : ""),
                dllMissing ? InstallHintOrAll(Dependency.WaylandClient, distro) : null,
                Required: !hasX11Fallback));
            throw new DoctorSkipException();
        }

        egl = eglLocal;

        var compositor = result.CompositorProcess != null
            ? $"{result.CompositorProcess} (pid {result.CompositorPid})"
            : $"unknown process; XDG_CURRENT_DESKTOP={Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "(unset)"}";
        s.Add(new DoctorCheck("Compositor", DoctorStatus.Ok, compositor));

        var byName = result.Globals
            .GroupBy(g => g.Interface)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Version));

        foreach (var (iface, purpose, required) in s_waylandGlobals)
        {
            if (byName.TryGetValue(iface, out var version))
            {
                s.Add(new DoctorCheck(iface, DoctorStatus.Ok, $"v{version}: {purpose}"));
            }
            else
            {
                var status = required ? DoctorStatus.Missing
                    : iface is "zxdg_decoration_manager_v1" or "zwp_linux_dmabuf_v1" or "wp_cursor_shape_manager_v1"
                        ? DoctorStatus.Info : DoctorStatus.Warn;
                s.Add(new DoctorCheck(iface, status, $"not advertised: {purpose}", Required: required && !hasX11Fallback));
            }
        }

        var known = new HashSet<string>(s_waylandGlobals.Select(g => g.Interface));
        var others = result.Globals
            .Where(g => !known.Contains(g.Interface))
            .Select(g => g.Interface)
            .Distinct()
            .ToList();
        s.Add(new DoctorCheck("Other globals", DoctorStatus.Info,
            others.Count == 0 ? "(none)" : $"{others.Count}: {string.Join(", ", others)}"));

        return result;
    }

    private static void ProbeX11(DoctorSection s, DistroFamily distro, out EglProbe? egl)
    {
        egl = null;
        IntPtr display;
        try
        {
            display = X11.XOpenDisplay(IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
            s.Add(new DoctorCheck("libX11", DoctorStatus.Missing, "libX11.so.6 not found",
                InstallHintOrAll(Dependency.X11, distro), Required: true));
            throw new DoctorSkipException();
        }

        if (display == IntPtr.Zero)
        {
            s.Add(new DoctorCheck("X11 connection", DoctorStatus.Missing,
                $"XOpenDisplay failed for DISPLAY={Environment.GetEnvironmentVariable("DISPLAY") ?? "(unset)"}",
                Required: true));
            throw new DoctorSkipException();
        }

        try
        {
            s.Add(new DoctorCheck("X11 connection", DoctorStatus.Ok,
                $"DISPLAY={Environment.GetEnvironmentVariable("DISPLAY")}"));
            egl = EglProbe.Run(Egl.EGL_PLATFORM_X11_KHR, display);
        }
        finally
        {
            X11.XCloseDisplay(display);
        }

        try
        {
            var monitors = MonitorService.Instance.Monitors;
            if (monitors.Count == 0)
            {
                s.Add(new DoctorCheck("XRandR outputs", DoctorStatus.Warn, "no outputs reported (XRandR missing?)",
                    InstallHintOrAll(Dependency.X11, distro)));
            }
            foreach (var m in monitors)
            {
                s.Add(new DoctorCheck($"Output {m.Name}", DoctorStatus.Info,
                    string.Create(CultureInfo.InvariantCulture,
                        $"{m.Width}x{m.Height}+{m.X}+{m.Y} @ {m.RefreshRate:0.##} Hz, {m.PhysicalWidthMm}x{m.PhysicalHeightMm} mm, {m.Dpi:0} dpi{(m.IsPrimary ? ", primary" : "")}")));
            }
        }
        catch (Exception ex)
        {
            s.Add(new DoctorCheck("XRandR outputs", DoctorStatus.Warn, $"probe failed: {ex.Message}",
                InstallHintOrAll(Dependency.X11, distro)));
        }

        var xrdb = HiDpiService.RunCommand("xrdb", "-query");
        var xft = xrdb == null ? null : Regex.Match(xrdb, @"Xft\.dpi:\s*(\d+(?:\.\d+)?)");
        s.Add(new DoctorCheck("Xft.dpi", DoctorStatus.Info,
            xrdb == null ? "xrdb not available"
            : xft is { Success: true } ? xft.Groups[1].Value
            : "not set in the X resource database"));
    }

    private static void BuildGraphicsSection(DoctorSection s, LinuxApplicationOptions? options,
        DisplayServerType serverType, EglProbe? egl, DoctorProbes probes, DistroFamily distro)
    {
        var configured = options?.Renderer ?? RendererPreference.Auto;
        var preference = RenderTargetFactory.ResolvePreference(configured);
        var env = Environment.GetEnvironmentVariable(RenderTargetFactory.EnvironmentVariable);
        s.Add(new DoctorCheck("Preference", DoctorStatus.Info,
            $"{preference} ({(string.IsNullOrWhiteSpace(env) ? "LinuxApplicationOptions.Renderer / default" : $"{RenderTargetFactory.EnvironmentVariable}={env}")})"));

        bool libEgl = Safe(() => probes.HasLibrary("libEGL.so.1"), false);
        if (!libEgl)
        {
            s.Add(new DoctorCheck("libEGL", DoctorStatus.Warn, "libEGL.so.1 not found; GPU rendering unavailable",
                InstallHintOrAll(Dependency.Egl, distro)));
        }

        if (serverType == DisplayServerType.Wayland && !Safe(() => probes.HasLibrary("libwayland-egl.so.1"), false))
        {
            s.Add(new DoctorCheck("libwayland-egl", DoctorStatus.Warn, "libwayland-egl.so.1 not found; GPU rendering on Wayland unavailable",
                InstallHintOrAll(Dependency.WaylandEgl, distro)));
        }

        if (egl != null)
        {
            if (egl.Version != null)
                s.Add(new DoctorCheck("EGL", DoctorStatus.Ok, $"{egl.Version} {egl.Vendor} (client APIs: {egl.ClientApis?.Trim() ?? "?"})"));
            if (egl.GlRenderer != null)
                s.Add(new DoctorCheck("GL renderer", egl.IsSoftware ? DoctorStatus.Warn : DoctorStatus.Ok,
                    $"{egl.GlRenderer}{(egl.GlVersion != null ? $" ({egl.GlVersion})" : "")}"
                    + (egl.IsSoftware ? "; software rasteriser, the GPU path will run on the CPU" : "")));
            if (egl.Error != null)
                s.Add(new DoctorCheck("EGL probe", DoctorStatus.Warn, egl.Error));
        }

        string selection;
        DoctorStatus status;
        if (options?.UseGtk == true)
        {
            selection = "GTK-hosted surface (RenderTargetFactory not used)";
            status = DoctorStatus.Info;
        }
        else if (preference == RendererPreference.Raster)
        {
            selection = "raster (by configuration)";
            status = DoctorStatus.Info;
        }
        else if (egl is { Usable: true } && serverType is DisplayServerType.Wayland or DisplayServerType.X11)
        {
            selection = serverType == DisplayServerType.Wayland ? "egl-wayland (GPU)" : "egl-x11 (GPU; config must also match the window visual)";
            status = egl.IsSoftware ? DoctorStatus.Warn : DoctorStatus.Ok;
        }
        else
        {
            var why = egl?.Error ?? (libEgl ? "EGL not probed" : "libEGL missing");
            selection = $"raster fallback ({why})";
            status = preference == RendererPreference.Gpu ? DoctorStatus.Warn : DoctorStatus.Info;
        }

        s.Add(new DoctorCheck("RenderTargetFactory", status, selection));
    }

    private static void BuildScaleSection(DoctorSection s, DisplayServerType serverType, WaylandWindow.WaylandProbeResult? wayland)
    {
        var hiDpi = new HiDpiService();
        hiDpi.Initialize();
        s.Add(new DoctorCheck("Startup scale", DoctorStatus.Info,
            string.Create(CultureInfo.InvariantCulture, $"{hiDpi.ScaleFactor:0.###} ({hiDpi.Dpi:0} dpi)")));
        s.Add(new DoctorCheck("Detected by", DoctorStatus.Info, $"HiDpiService: {hiDpi.DetectionSource}"));

        if (serverType == DisplayServerType.Wayland)
        {
            bool fractional = wayland?.Globals.Any(g => g.Interface == "wp_fractional_scale_manager_v1") == true;
            bool viewporter = wayland?.Globals.Any(g => g.Interface == "wp_viewporter") == true;
            s.Add(new DoctorCheck("Per-window scale",
                fractional && viewporter ? DoctorStatus.Ok : DoctorStatus.Warn,
                fractional && viewporter
                    ? "compositor-driven via wp_fractional_scale_v1 + wp_viewporter (overrides the startup value per window)"
                    : "integer wl_output scale only; fractional scales will be rounded up and downsampled"));
        }

        foreach (var name in new[] { "GDK_SCALE", "GDK_DPI_SCALE", "QT_SCALE_FACTOR", "QT_SCREEN_SCALE_FACTORS" })
        {
            var v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(v))
                s.Add(new DoctorCheck(name, DoctorStatus.Info, v));
        }
    }

    internal static DoctorSection BuildDependencySection(DoctorProbes probes, DistroFamily distro)
    {
        var s = new DoctorSection("Native dependencies");

        bool Has(string lib) => Safe(() => probes.HasLibrary(lib), false);

        // GTK 3: gtk_init_check runs unconditionally at startup.
        bool gtk = Has("libgtk-3.so.0");
        bool glib = Has("libglib-2.0.so.0");
        if (gtk && glib)
        {
            bool printDialog = Safe(probes.HasGtkPrintDialog, false);
            s.Add(new DoctorCheck("GTK 3", DoctorStatus.Ok,
                $"libgtk-3.so.0: file chooser, print dialog{(printDialog ? "" : " (unavailable: GTK built without printing)")}"));
        }
        else
        {
            s.Add(new DoctorCheck("GTK 3", DoctorStatus.Missing,
                $"{(gtk ? "libglib-2.0.so.0" : "libgtk-3.so.0")} not found; loaded at startup, also file chooser / print dialog",
                InstallHintOrAll(Dependency.Gtk3, distro), Required: true));
        }

        AddLib(s, "GStreamer", new[] { "libgstreamer-1.0.so.0", "libgstapp-1.0.so.0" }, Has,
            "MediaElement playback", Dependency.GStreamer, distro);
        AddLib(s, "libcups", new[] { "libcups.so.2" }, Has, "printing", Dependency.Cups, distro);
        AddLib(s, "AT-SPI2", new[] { "libatspi.so.0" }, Has, "accessibility / screen readers", Dependency.AtSpi2, distro);

        // Tray: the backend TrayIconService actually probed.
        var tray = Safe(probes.TrayBackend, null);
        string? indicatorLib = new[] { "libayatana-appindicator3.so.1", "libappindicator3.so.1" }.FirstOrDefault(Has);
        if (indicatorLib != null)
            s.Add(new DoctorCheck("Tray (AppIndicator)", DoctorStatus.Ok, $"{indicatorLib}: StatusNotifierItem{(tray != null ? $", backend {tray}" : "")}"));
        else if (tray is "XEmbed")
            s.Add(new DoctorCheck("Tray (AppIndicator)", DoctorStatus.Warn,
                "no AppIndicator library; using the legacy XEmbed tray (X11 only)", InstallHintOrAll(Dependency.AppIndicator, distro)));
        else
            s.Add(new DoctorCheck("Tray (AppIndicator)", DoctorStatus.Missing,
                "libayatana-appindicator3 / libappindicator3 not found; TrayIcon is a no-op", InstallHintOrAll(Dependency.AppIndicator, distro)));

        AddLib(s, "WebKitGTK", new[] { "libwebkit2gtk-4.1.so.0" }, Has,
            "GTK-hosted WebView in UseGtk mode", Dependency.WebKitGtk, distro,
            alternatives: new[] { "libwebkit2gtk-4.0.so.37" });
        AddLib(s, "WPE WebKit 2.54+", new[] { WpeNative.LibWpeWebKit }, Has,
            "composited WebView / BlazorWebView", Dependency.WpeWebKit, distro);

        var webView = Safe(probes.WebViewBackend, null);
        if (webView != null)
            s.Add(new DoctorCheck("WebView backend", DoctorStatus.Info,
                webView == "wpe" ? "wpe (composited in the Skia tree)" : "webkitgtk (requires UseGtk = true to embed)"));

        AddLib(s, "fontconfig", new[] { "libfontconfig.so.1" }, Has, "system font discovery", Dependency.Fontconfig, distro);
        return s;
    }

    internal static DoctorSection BuildFontSection(DoctorProbes probes, DistroFamily distro)
    {
        var s = new DoctorSection("Fallback fonts");
        bool HasFont(string f) => Safe(() => probes.HasFont(f), false);

        var cjk = new[] { "Noto Sans CJK SC", "Noto Sans CJK JP", "Noto Sans CJK TC", "Noto Sans CJK KR" }.FirstOrDefault(HasFont);
        s.Add(cjk != null
            ? new DoctorCheck("Noto CJK", DoctorStatus.Ok, cjk)
            : new DoctorCheck("Noto CJK", DoctorStatus.Missing, "Chinese / Japanese / Korean text will render as boxes",
                InstallHintOrAll(Dependency.NotoCjk, distro)));

        s.Add(HasFont("Noto Color Emoji")
            ? new DoctorCheck("Noto Color Emoji", DoctorStatus.Ok, "Noto Color Emoji")
            : new DoctorCheck("Noto Color Emoji", DoctorStatus.Missing, "emoji will render monochrome or as boxes",
                InstallHintOrAll(Dependency.NotoColorEmoji, distro)));
        return s;
    }

    private static void AddLib(DoctorSection s, string name, string[] libs, Func<string, bool> has,
        string purpose, Dependency dep, DistroFamily distro, string[]? alternatives = null)
    {
        var missing = libs.Where(l => !has(l)).ToList();
        if (missing.Count == 0)
        {
            s.Add(new DoctorCheck(name, DoctorStatus.Ok, $"{string.Join(", ", libs)}: {purpose}"));
            return;
        }

        var alt = alternatives?.FirstOrDefault(has);
        if (alt != null)
        {
            s.Add(new DoctorCheck(name, DoctorStatus.Ok, $"{alt}: {purpose}"));
            return;
        }

        s.Add(new DoctorCheck(name, DoctorStatus.Missing,
            $"{string.Join(", ", missing)} not found; {purpose} unavailable",
            InstallHintOrAll(dep, distro)));
    }

    // ------------------------------------------------------------------
    // Distro detection and install hints
    // ------------------------------------------------------------------

    internal enum DistroFamily { Unknown, Debian, Fedora, Arch }

    internal enum Dependency
    {
        GStreamer,
        Cups,
        AtSpi2,
        AppIndicator,
        Gtk3,
        WebKitGtk,
        WpeWebKit,
        WaylandClient,
        WaylandEgl,
        Egl,
        Fontconfig,
        NotoCjk,
        NotoColorEmoji,
        X11,
        ProtocolShim,
    }

    /// <summary>Classifies /etc/os-release content by package manager family (ID, then ID_LIKE).</summary>
    internal static DistroFamily DetectDistro(string? osReleaseText)
    {
        if (string.IsNullOrWhiteSpace(osReleaseText)) return DistroFamily.Unknown;

        var ids = new List<string>();
        if (ParseOsReleaseValue(osReleaseText, "ID") is { } id) ids.Add(id);
        if (ParseOsReleaseValue(osReleaseText, "ID_LIKE") is { } like)
            ids.AddRange(like.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var raw in ids)
        {
            switch (raw.ToLowerInvariant())
            {
                case "debian":
                case "ubuntu":
                    return DistroFamily.Debian;
                case "fedora":
                case "rhel":
                case "centos":
                    return DistroFamily.Fedora;
                case "arch":
                case "archlinux":
                    return DistroFamily.Arch;
            }
        }

        return DistroFamily.Unknown;
    }

    internal static string? ParseOsReleaseValue(string? osReleaseText, string key)
    {
        if (string.IsNullOrEmpty(osReleaseText)) return null;
        foreach (var rawLine in osReleaseText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var eq = line.IndexOf('=');
            if (eq <= 0 || !string.Equals(line[..eq], key, StringComparison.Ordinal)) continue;
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];
            return value;
        }
        return null;
    }

    private static string DistroLabel(DistroFamily d) => d switch
    {
        DistroFamily.Debian => "Debian/Ubuntu (apt)",
        DistroFamily.Fedora => "Fedora (dnf)",
        DistroFamily.Arch => "Arch (pacman)",
        _ => "unknown distro (showing all)",
    };

    private const string ShimHint =
        "restore OpenMaui.Controls.Linux (libopenmaui_wl.so ships next to OpenMaui.Controls.Linux.dll); from source run native/build.sh";

    // Exact install commands. null = no package the doctor can vouch for.
    private static readonly Dictionary<Dependency, (string? Apt, string? Dnf, string? Pacman)> s_hints = new()
    {
        [Dependency.GStreamer] = (
            "sudo apt install libgstreamer1.0-0 gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-libav",
            "sudo dnf install gstreamer1 gstreamer1-plugins-base gstreamer1-plugins-good gstreamer1-plugins-bad-free gstreamer1-plugin-libav",
            "sudo pacman -S gstreamer gst-plugins-base gst-plugins-good gst-plugins-bad gst-libav"),
        [Dependency.Cups] = ("sudo apt install libcups2", "sudo dnf install cups-libs", "sudo pacman -S libcups"),
        [Dependency.AtSpi2] = ("sudo apt install at-spi2-core libatspi2.0-0", "sudo dnf install at-spi2-core", "sudo pacman -S at-spi2-core"),
        [Dependency.AppIndicator] = (
            "sudo apt install libayatana-appindicator3-1",
            "sudo dnf install libayatana-appindicator-gtk3",
            "sudo pacman -S libayatana-appindicator"),
        [Dependency.Gtk3] = ("sudo apt install libgtk-3-0", "sudo dnf install gtk3", "sudo pacman -S gtk3"),
        [Dependency.WebKitGtk] = ("sudo apt install libwebkit2gtk-4.1-0", "sudo dnf install webkit2gtk4.1", "sudo pacman -S webkit2gtk-4.1"),
        // Debian testing/sid carry 2.54; Debian 13 has 2.48 (too old for the
        // WPEPlatform API) and Ubuntu has had no WPE package since 22.04.
        [Dependency.WpeWebKit] = (
            "sudo apt install libwpewebkit-2.0-1 (Debian testing/sid; Debian 13 and Ubuntu ship no WPE 2.54: use the WebKitGTK WebView with UseGtk = true)",
            "sudo dnf copr enable philn/wpewebkit && sudo dnf install wpewebkit",
            null),
        [Dependency.WaylandClient] = ("sudo apt install libwayland-client0", "sudo dnf install libwayland-client", "sudo pacman -S wayland"),
        [Dependency.WaylandEgl] = ("sudo apt install libwayland-egl1", "sudo dnf install libwayland-egl", "sudo pacman -S wayland"),
        [Dependency.Egl] = ("sudo apt install libegl1 libegl-mesa0 libgles2", "sudo dnf install libglvnd-egl mesa-libEGL", "sudo pacman -S libglvnd mesa"),
        [Dependency.Fontconfig] = ("sudo apt install libfontconfig1", "sudo dnf install fontconfig", "sudo pacman -S fontconfig"),
        [Dependency.NotoCjk] = ("sudo apt install fonts-noto-cjk", "sudo dnf install google-noto-sans-cjk-fonts", "sudo pacman -S noto-fonts-cjk"),
        [Dependency.NotoColorEmoji] = ("sudo apt install fonts-noto-color-emoji", "sudo dnf install google-noto-color-emoji-fonts", "sudo pacman -S noto-fonts-emoji"),
        [Dependency.X11] = ("sudo apt install libx11-6 libxrandr2", "sudo dnf install libX11 libXrandr", "sudo pacman -S libx11 libxrandr"),
        [Dependency.ProtocolShim] = (ShimHint, ShimHint, ShimHint),
    };

    /// <summary>The install command for one dependency on one distro family, or null when unknown.</summary>
    internal static string? InstallHint(Dependency dependency, DistroFamily distro)
    {
        if (!s_hints.TryGetValue(dependency, out var h)) return null;
        return distro switch
        {
            DistroFamily.Debian => h.Apt,
            DistroFamily.Fedora => h.Dnf,
            DistroFamily.Arch => h.Pacman,
            _ => null,
        };
    }

    /// <summary>
    /// Hint for the detected distro; for an unknown distro, every known
    /// variant labelled by family so the user can pick.
    /// </summary>
    internal static string? InstallHintOrAll(Dependency dependency, DistroFamily distro)
    {
        if (distro != DistroFamily.Unknown)
            return InstallHint(dependency, distro);

        if (dependency == Dependency.ProtocolShim)
            return ShimHint;

        var parts = new List<string>();
        foreach (var family in new[] { DistroFamily.Debian, DistroFamily.Fedora, DistroFamily.Arch })
        {
            if (InstallHint(dependency, family) is { } cmd)
                parts.Add($"{DistroLabel(family)}: {cmd}");
        }
        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    // ------------------------------------------------------------------
    // Formatting
    // ------------------------------------------------------------------

    /// <summary>Plain-text rendering of the report (no colours).</summary>
    public static string Format(DoctorReport report) => Format(report, color: false);

    internal static string Format(DoctorReport report, bool color)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.Append("OpenMaui doctor\n");
        sb.Append("===============\n");

        int nameWidth = Math.Min(40, report.AllChecks.Select(c => c.Name.Length).DefaultIfEmpty(10).Max());
        const int tagWidth = 9; // "[MISSING]"
        string indent = new(' ', 2 + tagWidth + 1 + nameWidth + 2);

        foreach (var section in report.Sections)
        {
            if (section.Checks.Count == 0) continue;
            sb.Append('\n').Append(section.Title).Append('\n');

            foreach (var check in section.Checks)
            {
                var tag = StatusTag(check.Status).PadRight(tagWidth);
                if (color) tag = Colorize(tag, check.Status);
                var name = check.Name.Length > nameWidth ? check.Name[..(nameWidth - 1)] + "~" : check.Name.PadRight(nameWidth);
                var detail = check.Required && check.Status == DoctorStatus.Missing ? check.Detail + " (required)" : check.Detail;

                var lines = Wrap(detail, Math.Max(30, WrapWidth - indent.Length));
                sb.Append("  ").Append(tag).Append(' ').Append(name).Append("  ").Append(lines[0]).Append('\n');
                for (int i = 1; i < lines.Count; i++)
                    sb.Append(indent).Append(lines[i]).Append('\n');

                if (check.Fix != null && check.Status is DoctorStatus.Warn or DoctorStatus.Missing)
                    sb.Append(indent).Append("fix: ").Append(check.Fix).Append('\n');
            }
        }

        var all = report.AllChecks.ToList();
        int ok = all.Count(c => c.Status == DoctorStatus.Ok);
        int warn = all.Count(c => c.Status == DoctorStatus.Warn);
        int missing = all.Count(c => c.Status == DoctorStatus.Missing);
        int requiredMissing = all.Count(c => c.Required && c.Status == DoctorStatus.Missing);
        sb.Append('\n')
          .Append($"Summary: {ok} ok, {warn} warning(s), {missing} missing ({requiredMissing} required)")
          .Append(requiredMissing > 0 ? "; the app cannot start until the required items are fixed.\n" : ".\n");
        return sb.ToString();
    }

    internal static string StatusTag(DoctorStatus status) => status switch
    {
        DoctorStatus.Ok => "[ok]",
        DoctorStatus.Info => "[info]",
        DoctorStatus.Warn => "[warn]",
        DoctorStatus.Missing => "[MISSING]",
        _ => "[?]",
    };

    private static string Colorize(string text, DoctorStatus status) => status switch
    {
        DoctorStatus.Ok => $"\u001b[32m{text}\u001b[0m",
        DoctorStatus.Warn => $"\u001b[33m{text}\u001b[0m",
        DoctorStatus.Missing => $"\u001b[31m{text}\u001b[0m",
        _ => $"\u001b[2m{text}\u001b[0m",
    };

    internal static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        lines.Add(current.ToString());
        return lines;
    }

    // ------------------------------------------------------------------
    // Guards
    // ------------------------------------------------------------------

    /// <summary>Thrown by a probe that already recorded its own failure row.</summary>
    private sealed class DoctorSkipException : Exception
    {
    }

    private static T? Guard<T>(DoctorSection section, string name, Func<T> probe,
        DoctorStatus failStatus = DoctorStatus.Warn, bool required = false, string? fix = null)
    {
        try
        {
            return probe();
        }
        catch (DoctorSkipException)
        {
            return default;
        }
        catch (Exception ex)
        {
            var detail = ex is DllNotFoundException or TypeInitializationException { InnerException: DllNotFoundException }
                ? $"native library not found ({(ex.InnerException ?? ex).Message})"
                : $"probe failed: {ex.GetType().Name}: {ex.Message}";
            section.Add(new DoctorCheck(name, failStatus, detail, fix, required));
            return default;
        }
    }

    private static T Safe<T>(Func<T> f, T fallback)
    {
        try
        {
            return f();
        }
        catch
        {
            return fallback;
        }
    }

    // ------------------------------------------------------------------
    // EGL
    // ------------------------------------------------------------------

    /// <summary>
    /// Short-lived EGL bring-up on the given native display, mirroring what
    /// EglRenderTarget needs (ES2-renderable window config, ES3/ES2 context),
    /// made current surfaceless to read the GL renderer string.
    /// </summary>
    internal sealed class EglProbe
    {
        private const int EGL_CLIENT_APIS = 0x308D;
        private const uint GL_VERSION = 0x1F02;

        public string? Vendor { get; private set; }
        public string? Version { get; private set; }
        public string? ClientApis { get; private set; }
        public string? GlRenderer { get; private set; }
        public string? GlVersion { get; private set; }
        public bool Usable { get; private set; }
        public string? Error { get; private set; }

        public bool IsSoftware => GlRenderer != null
            && (GlRenderer.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase)
                || GlRenderer.Contains("softpipe", StringComparison.OrdinalIgnoreCase)
                || GlRenderer.Contains("Software Rasterizer", StringComparison.OrdinalIgnoreCase));

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GlGetString(uint name);

        public static EglProbe Run(int platform, IntPtr nativeDisplay)
        {
            var p = new EglProbe();
            IntPtr display = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;
            bool initialized = false;
            try
            {
                display = Egl.eglGetPlatformDisplay((uint)platform, nativeDisplay, IntPtr.Zero);
                if (display == Egl.EGL_NO_DISPLAY)
                    return p.Fail("eglGetPlatformDisplay");
                if (Egl.eglInitialize(display, out int major, out int minor) == Egl.EGL_FALSE)
                    return p.Fail("eglInitialize");
                initialized = true;

                p.Version = $"EGL {major}.{minor}";
                p.Vendor = Egl.QueryString(display, Egl.EGL_VENDOR);
                p.ClientApis = Egl.QueryString(display, EGL_CLIENT_APIS);
                var extensions = Egl.QueryString(display, Egl.EGL_EXTENSIONS) ?? string.Empty;

                if (Egl.eglBindAPI(Egl.EGL_OPENGL_ES_API) == Egl.EGL_FALSE)
                    return p.Fail("eglBindAPI(OpenGL ES)");

                var attribs = new[]
                {
                    Egl.EGL_SURFACE_TYPE, Egl.EGL_WINDOW_BIT,
                    Egl.EGL_RENDERABLE_TYPE, Egl.EGL_OPENGL_ES2_BIT,
                    Egl.EGL_RED_SIZE, 8, Egl.EGL_GREEN_SIZE, 8, Egl.EGL_BLUE_SIZE, 8,
                    Egl.EGL_NONE,
                };
                var configs = new IntPtr[1];
                if (Egl.eglChooseConfig(display, attribs, configs, 1, out int count) == Egl.EGL_FALSE || count == 0)
                {
                    p.Error = "no EGL config matches (RGB8, window surface, OpenGL ES 2)";
                    return p;
                }

                context = Egl.eglCreateContext(display, configs[0], Egl.EGL_NO_CONTEXT, new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 3, Egl.EGL_NONE });
                if (context == Egl.EGL_NO_CONTEXT)
                    context = Egl.eglCreateContext(display, configs[0], Egl.EGL_NO_CONTEXT, new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 2, Egl.EGL_NONE });
                if (context == Egl.EGL_NO_CONTEXT)
                    return p.Fail("eglCreateContext");

                p.Usable = true;

                if (extensions.Contains("EGL_KHR_surfaceless_context", StringComparison.Ordinal)
                    && Egl.eglMakeCurrent(display, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, context) != Egl.EGL_FALSE)
                {
                    p.GlRenderer = Egl.GetGlRenderer();
                    var fn = Egl.eglGetProcAddress("glGetString");
                    if (fn != IntPtr.Zero)
                    {
                        var ptr = Marshal.GetDelegateForFunctionPointer<GlGetString>(fn)(GL_VERSION);
                        p.GlVersion = ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
                    }
                    Egl.eglMakeCurrent(display, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
                }
            }
            catch (DllNotFoundException)
            {
                p.Error = "libEGL.so.1 not found";
            }
            catch (Exception ex)
            {
                p.Error = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                try
                {
                    if (context != IntPtr.Zero) Egl.eglDestroyContext(display, context);
                    if (initialized) Egl.eglTerminate(display);
                }
                catch
                {
                    // Teardown is best-effort.
                }
            }
            return p;
        }

        private EglProbe Fail(string call)
        {
            Error = $"{call} failed: {Egl.ErrorName(Egl.eglGetError())}";
            return this;
        }
    }
}

/// <summary>
/// Injectable probes so the doctor's pure logic can be tested without
/// depending on the machine. <see cref="CreateDefault"/> routes to the
/// platform's own availability checks.
/// </summary>
internal sealed class DoctorProbes
{
    public Func<string, bool> HasLibrary { get; init; } = _ => false;
    public Func<string, bool> HasFont { get; init; } = _ => false;
    public Func<string?> ReadOsRelease { get; init; } = () => null;
    public Func<bool> HasGtkPrintDialog { get; init; } = () => false;

    /// <summary>"AppIndicator", "XEmbed" or "none"; null when unknown.</summary>
    public Func<string?> TrayBackend { get; init; } = () => null;

    /// <summary>"wpe" or "webkitgtk"; null when unknown.</summary>
    public Func<string?> WebViewBackend { get; init; } = () => null;

    public static DoctorProbes CreateDefault() => new()
    {
        HasLibrary = lib => lib switch
        {
            // Reuse the platform's own probes where they exist.
            WpeNative.LibWpeWebKit => WpeNative.IsAvailable,
            "libcups.so.2" => PrintService.IsAvailable,
            _ => NativeLibrary.TryLoad(lib, out _),
        },
        HasFont = family => FontFallbackManager.Instance.IsFontAvailable(family),
        ReadOsRelease = () =>
        {
            foreach (var path in new[] { "/etc/os-release", "/usr/lib/os-release" })
                if (File.Exists(path)) return File.ReadAllText(path);
            return null;
        },
        HasGtkPrintDialog = () => GtkPrintDialog.IsAvailable,
        TrayBackend = () => TrayIconService.Backend switch
        {
            AppIndicatorBackend => "AppIndicator",
            XEmbedTrayBackend => "XEmbed",
            _ => "none",
        },
        WebViewBackend = () => Handlers.WebViewBackend.Name,
    };
}
