// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.ExceptionServices;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Input;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Native;
using Microsoft.Maui.Platform.Linux.Views;
using SkiaSharp;

namespace Microsoft.Maui.Controls.Linux.Tests.WebViewHost;

/// <summary>
/// Runs one named WebView scenario on the process main thread against the
/// WPE engine (headless, no display needed). Exit code 0 = passed, 1 = a
/// check failed (message on stderr), 2 = unknown scenario, 3 = WPE not
/// installed, 4 = this machine cannot run the scenario (e.g. no GPU EGL
/// context; reason on stderr). <c>--list</c> prints the scenario names.
/// </summary>
public static partial class Program
{
    private static readonly Dictionary<string, Action> s_scenarios = new(StringComparer.Ordinal)
    {
        ["html-source-navigation-events"] = HtmlSourceNavigationEvents,
        ["javascript-evaluation"] = JavaScriptEvaluation,
        ["javascript-mutation-and-eval"] = JavaScriptMutationAndEval,
        ["javascript-non-string-and-errors"] = JavaScriptNonStringAndErrors,
        ["navigation-decision-cancel"] = NavigationDecisionCancel,
        ["history-can-go-back"] = HistoryCanGoBack,
        ["user-agent-round-trip"] = UserAgentRoundTrip,
        ["frames-delivered-after-load"] = FramesDeliveredAfterLoad,
        ["cookies-round-trip"] = CookiesRoundTrip,
        ["reload-and-stop"] = ReloadAndStop,
        ["keyboard-event-code"] = KeyboardEventCode,
        ["frames-delivered-gpu"] = FramesDeliveredGpu,
        ["scale-change"] = ScaleChange,
        ["browser-status-and-failure"] = BrowserStatusAndFailure,
        ["browser-response-policy"] = BrowserResponsePolicy,
        ["browser-download-intercept"] = BrowserDownloadIntercept,
        ["browser-download-save-dialog"] = BrowserDownloadSaveDialog,
        ["browser-new-window-in-place"] = BrowserNewWindowInPlace,
        ["browser-url-changed"] = BrowserUrlChanged,
        ["browser-zoom-round-trip"] = BrowserZoomRoundTrip,
        ["browser-capture"] = BrowserCapture,
        ["browser-save-as-pdf"] = BrowserSaveAsPdf,
        ["browser-zoom-gestures"] = BrowserZoomGestures,
    };

    /// <summary>Exit code for a scenario this machine cannot run (reported, not failed).</summary>
    public const int NotSupportedExitCode = 4;

    /// <summary>Thrown by a scenario whose prerequisites (e.g. a GPU context) are missing here.</summary>
    private sealed class ScenarioNotSupportedException(string message) : Exception(message);

    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--list")
        {
            foreach (var name in s_scenarios.Keys)
                Console.WriteLine(name);
            return 0;
        }

        if (args.Length != 1 || !s_scenarios.TryGetValue(args[0], out var scenario))
        {
            Console.Error.WriteLine($"usage: WebViewHost <scenario>|--list (unknown: {string.Join(' ', args)})");
            return 2;
        }

        if (!WpeWebView.IsSupported)
        {
            Console.Error.WriteLine("WPE WebKit (libWPEWebKit-2.0) is not installed");
            return 3;
        }

        try
        {
            scenario();
            Console.WriteLine($"ok {args[0]}");
            return 0;
        }
        catch (ScenarioNotSupportedException ex)
        {
            Console.Error.WriteLine($"not supported {args[0]}: {ex.Message}");
            return NotSupportedExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL {args[0]}: {ex.Message}");
            return 1;
        }
    }

    // ---- scenarios ------------------------------------------------------

    private static void HtmlSourceNavigationEvents()
    {
        string? started = null; (string Url, bool Success)? completed = null;
        using var view = new WpeWebView();
        view.NavigationStarted += (_, u) => started = u;
        view.NavigationCompleted += (_, e) => completed = e;
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        view.LoadHtml("<html><head><title>Hello WPE</title></head><body>hi</body></html>", "app://localhost/");

        Pump(() => completed != null);
        Check(completed != null, "NavigationCompleted was not raised");
        Check(completed!.Value.Success, "navigation did not succeed");
        Check(started != null, "NavigationStarted was not raised");
        // WebKit may finish the load before the title property is updated
        // (notify::title arrives separately), so wait for it briefly.
        Pump(() => view.Title == "Hello WPE", 3000);
        Check(view.Title == "Hello WPE", $"title was '{view.Title}'");
    }

    private static void JavaScriptEvaluation()
    {
        using var view = Load("<html><body><div id='x'>forty-two</div></body></html>", out var loaded);
        Pump(loaded);

        var text = Await(view.EvaluateJavaScriptAsync("document.getElementById('x').textContent + '!'"));
        Check(text == "forty-two!", $"textContent evaluation returned '{text}'");

        var arith = Await(view.EvaluateJavaScriptAsync("String(6 * 7)"));
        Check(arith == "42", $"arithmetic evaluation returned '{arith}'");
    }

    private static void JavaScriptMutationAndEval()
    {
        using var view = Load("<html><body><p id='p'>before</p></body></html>", out var loaded);
        Pump(loaded);

        view.Eval("document.getElementById('p').textContent = 'after'");
        var read = Await(view.EvaluateJavaScriptAsync("document.getElementById('p').textContent"));
        Check(read == "after", $"Eval did not mutate the document (read '{read}')");
    }

    private static void JavaScriptNonStringAndErrors()
    {
        using var view = Load("<html><body></body></html>", out var loaded);
        Pump(loaded);

        var obj = Await(view.EvaluateJavaScriptAsync("({a:1})"));
        Check(obj == null, $"object result should be null, was '{obj}'");

        var err = Await(view.EvaluateJavaScriptAsync("throw new Error('nope')"));
        Check(err == null, $"thrown error should yield null, was '{err}'");
    }

    private static void NavigationDecisionCancel()
    {
        using var view = Load("<html><body>first</body></html>", out var loaded);
        Pump(loaded);

        bool asked = false;
        view.NavigationDecision += (_, e) => { asked = true; e.Cancel = e.Url.Contains("blocked"); };
        int completions = 0;
        view.NavigationCompleted += (_, _) => completions++;

        view.Navigate("app://localhost/blocked");
        Pump(() => asked, 3000);
        Check(asked, "NavigationDecision was not raised");
        Pump(() => false, 300);
        Check(completions == 0, $"cancelled navigation still completed {completions} time(s)");
    }

    private static void HistoryCanGoBack()
    {
        using var server = new LocalServer(("/one", "<html><body>one</body></html>"), ("/two", "<html><body>two</body></html>"));
        using var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        int completions = 0;
        view.NavigationCompleted += (_, e) => { if (e.Success) completions++; };

        view.Navigate(server.Url("/one"));
        Pump(() => completions >= 1);
        Check(completions >= 1, "first navigation did not complete");
        Check(!view.CanGoBack, "CanGoBack should be false after the first load");

        view.Navigate(server.Url("/two"));
        Pump(() => completions >= 2);
        Check(completions >= 2, "second navigation did not complete");
        Check(view.CanGoBack, "CanGoBack should be true after two loads");
        Check(!view.CanGoForward, "CanGoForward should be false at the newest entry");
        Check(view.CurrentUri != null && view.CurrentUri.EndsWith("/two"), $"Url should report the current page, was '{view.CurrentUri}'");

        view.GoBack();
        Pump(() => completions >= 3);
        Check(completions >= 3, "GoBack did not complete");
        Check(view.CanGoForward, "CanGoForward should be true after GoBack");
        Check(view.CurrentUri != null && view.CurrentUri.EndsWith("/one"), $"GoBack should land on /one, was '{view.CurrentUri}'");
    }

    private static void UserAgentRoundTrip()
    {
        using var view = new WpeWebView();
        view.UserAgent = "OpenMauiTest/1.0";
        Check(view.UserAgent == "OpenMauiTest/1.0", $"UserAgent read back '{view.UserAgent}'");
    }

    /// <summary>Stand-in render context: only the scale matters to the view.</summary>
    private sealed class ScaleContext : Microsoft.Maui.Platform.Linux.Rendering.IRenderContext
    {
        public float DpiScale { get; set; } = 1f;
        public Microsoft.Maui.Platform.Linux.Rendering.ResourceCache Resources { get; } = new();
        public void Invalidate() { }
        public void InvalidateRegion(SKRect rect) { }
    }

    /// <summary>
    /// The window moves to a monitor with another scale: the page's
    /// devicePixelRatio follows on the next draw, and its CSS size is kept.
    /// </summary>
    private static void ScaleChange()
    {
        var context = new ScaleContext { DpiScale = 1f };
        using var view = Load("<html><body>scale</body></html>", out var loaded);
        view.RenderContext = context;
        Pump(loaded);

        using var bitmap = new SKBitmap(new SKImageInfo(400, 300, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        view.Draw(canvas);
        Pump(() => false, 200);
        var before = Await(view.EvaluateJavaScriptAsync("String(window.devicePixelRatio) + '|' + window.innerWidth"));
        Check(before == "1|400", $"at 1x the page reported '{before}'");

        context.DpiScale = 2f;
        view.Draw(canvas);
        string? after = null;
        Pump(() =>
        {
            var t = view.EvaluateJavaScriptAsync("String(window.devicePixelRatio) + '|' + window.innerWidth");
            Pump(() => t.IsCompleted, 1000);
            after = t.IsCompleted ? t.Result : null;
            return after == "2|400";
        }, 4000);
        Check(after == "2|400", $"after the scale change the page reported '{after}' (want devicePixelRatio 2, CSS width 400)");
    }

    private static void FramesDeliveredAfterLoad()
    {
        using var view = Load("<html><body style='background:#ff0000'></body></html>", out var loaded);
        Pump(loaded);
        // The compositor delivers the first buffer shortly after load.
        Pump(() => false, 300);

        using var bitmap = new SKBitmap(new SKImageInfo(200, 150, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 200, 150);
            view.Draw(canvas);
        }
        var c = bitmap.GetPixel(100, 75);
        Check(c.Red > 200 && c.Green < 60 && c.Blue < 60, $"expected a red frame, centre pixel was {c}");
    }

    private static void CookiesRoundTrip()
    {
        // Cookies need an http(s) origin; app:// and data: loads have no jar.
        using var server = new LocalServer(("/", "<html><body>cookies</body></html>"));
        using var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        bool loaded = false;
        view.NavigationCompleted += (_, e) => loaded = e.Success;
        view.Navigate(server.Url("/"));
        Pump(() => loaded);
        Check(loaded, "page did not load");

        view.Eval("document.cookie = 'flavour=oatmeal'");
        var read = Await(view.EvaluateJavaScriptAsync("document.cookie"));
        Check(read != null && read.Contains("flavour=oatmeal"), $"cookie did not round-trip (read '{read}')");

        // A second request from the same origin carries the cookie back to the server.
        view.Navigate(server.Url("/"));
        Pump(() => server.RequestCount >= 2);
        Check(server.LastCookieHeader?.Contains("flavour=oatmeal") == true, $"server saw Cookie header '{server.LastCookieHeader}'");
    }

    private static void ReloadAndStop()
    {
        using var server = new LocalServer(("/", "<html><body><p id='p'>original</p></body></html>"));
        using var view = new WpeWebView();
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        int completions = 0;
        view.NavigationCompleted += (_, e) => { if (e.Success) completions++; };
        view.Navigate(server.Url("/"));
        Pump(() => completions >= 1);
        Check(completions >= 1, "page did not load");

        view.Eval("document.getElementById('p').textContent = 'changed'");
        Check(Await(view.EvaluateJavaScriptAsync("document.getElementById('p').textContent")) == "changed", "mutation before reload");

        view.Reload();
        Pump(() => completions >= 2);
        Check(completions >= 2, "Reload did not complete");
        Check(server.RequestCount >= 2, "Reload did not hit the server again");
        var after = Await(view.EvaluateJavaScriptAsync("document.getElementById('p').textContent"));
        Check(after == "original", $"Reload should restore the document (read '{after}')");

        // StopLoading on an idle view must not throw or fire a completion.
        int before = completions;
        view.StopLoading();
        Pump(() => false, 100);
        Check(completions == before, "StopLoading on an idle view raised NavigationCompleted");
    }

    private static void KeyboardEventCode()
    {
        using var view = Load(
            "<html><body><script>window.__keys = '';" +
            "document.addEventListener('keydown', e => { window.__keys += e.code + '|' + e.key + ';'; });" +
            "</script></body></html>", out var loaded);
        Pump(loaded);
        view.OnFocusGained();

        // 'a' as the Wayland backend delivers it: evdev KEY_A (30) -> XKB keycode 38,
        // a key-down (printable: held for the text) followed by the text input.
        uint keyA = KeyMapping.EvdevToXkbKeycode(30);
        Check(keyA == 38, $"evdev 30 should map to XKB keycode 38, got {keyA}");
        view.OnKeyDown(new KeyEventArgs(Key.A) { HardwareKeycode = keyA });
        view.OnTextInput(new TextInputEventArgs("a"));
        view.OnKeyUp(new KeyEventArgs(Key.A) { HardwareKeycode = keyA });

        // A non-printable key goes straight through with its keycode (evdev KEY_LEFT 105).
        uint left = KeyMapping.EvdevToXkbKeycode(105);
        view.OnKeyDown(new KeyEventArgs(Key.Left) { HardwareKeycode = left });
        view.OnKeyUp(new KeyEventArgs(Key.Left) { HardwareKeycode = left });

        string? keys = null;
        Pump(() =>
        {
            var t = view.EvaluateJavaScriptAsync("window.__keys");
            Pump(() => t.IsCompleted, 1000);
            keys = t.IsCompletedSuccessfully ? t.Result : null;
            return keys != null && keys.Contains("ArrowLeft");
        }, 5000);
        Console.WriteLine($"page saw: {keys}");
        Check(keys != null && keys.Contains("KeyA|a;"), $"page should observe code 'KeyA' / key 'a', saw '{keys}'");
        Check(keys!.Contains("ArrowLeft|ArrowLeft;"), $"page should observe code 'ArrowLeft', saw '{keys}'");
    }

    /// <summary>
    /// The on-screen GPU path, offscreen: a surfaceless EGL context on the GPU
    /// WebKit renders with, a Skia GL context and GPU surface, the view drawn
    /// through the same <c>Draw</c> call the render target makes. Asserts the
    /// colour read back and that the zero-copy import (not a pixel copy) fed it,
    /// then times steady-state frames. With OPENMAUI_WEBVIEW_ZEROCOPY=0 the
    /// same run asserts the copy path instead (before/after comparison).
    /// </summary>
    private static void FramesDeliveredGpu()
    {
        bool forcedCopy = Environment.GetEnvironmentVariable("OPENMAUI_WEBVIEW_ZEROCOPY") == "0";
        using var view = Load("<html><body style='margin:0;background:#ff0000'></body></html>", out var loaded);
        Pump(loaded);
        Pump(() => view.FramesReceived > 0, 3000);
        Check(view.FramesReceived > 0, "no frame was rendered after load");

        using var gpu = OffscreenGl.Create(WpeWebView.RenderNode);
        Console.WriteLine($"GL context: {gpu.Description}");

        var red = DrawUntil(view, gpu, 200, 150, c => c.Red > 200 && c.Green < 60 && c.Blue < 60);
        Check(red.Red > 200 && red.Green < 60 && red.Blue < 60, $"expected a red frame, centre pixel was {red}");

        if (forcedCopy)
        {
            Check(view.ActiveFramePath == "PixelCopy", $"override should force the copy path, was {view.ActiveFramePath}");
            Check(view.ZeroCopyFrameCount == 0, "override set but frames were imported zero-copy");
        }
        else
        {
            Check(view.ActiveFramePath == "ZeroCopy", $"expected the zero-copy path on a GPU canvas, was {view.ActiveFramePath}");
            Check(view.ZeroCopyFrameCount >= 1, "no frame was imported zero-copy");
        }

        // A new frame replaces the held one (lifetime/release path).
        long copiesBefore = view.PixelCopyFrameCount;
        view.Eval("document.body.style.background = '#0000ff'");
        var blue = DrawUntil(view, gpu, 200, 150, c => c.Blue > 200 && c.Red < 60 && c.Green < 60);
        Check(blue.Blue > 200 && blue.Red < 60 && blue.Green < 60, $"expected a blue frame after the update, centre pixel was {blue}");
        if (!forcedCopy)
        {
            Check(view.ZeroCopyFrameCount >= 2, $"the updated frame was not imported zero-copy ({view.ZeroCopyFrameCount} imports)");
            Check(view.PixelCopyFrameCount == copiesBefore, "a zero-copy frame was also copied to pixels");
        }

        // Steady-state cost at a desktop-sized view: frame import + draw/flush.
        const int W = 1280, H = 800, Frames = 30;
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, W, H);
        DrawUntil(view, gpu, W, H, _ => true);
        Pump(() => false, 300);
        long importStart = view.FrameImportTicks;
        long drawTicks = 0;
        int measured = 0;
        for (int i = 0; i < Frames; i++)
        {
            long received = view.FramesReceived;
            view.Eval($"document.body.style.background = 'rgb({i * 8 % 256},{(255 - i * 8) % 256},128)'");
            Pump(() => view.FramesReceived > received, 2000);
            if (view.FramesReceived == received) continue;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            gpu.Draw(view, W, H);
            drawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            measured++;
        }
        Check(measured > Frames / 2, $"only {measured}/{Frames} frames arrived");
        double ms(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / measured;
        Console.WriteLine($"path={view.ActiveFramePath} {W}x{H} frames={measured} " +
            $"import={ms(view.FrameImportTicks - importStart):F3} ms/frame draw+flush={ms(drawTicks):F3} ms/frame " +
            $"zeroCopy={view.ZeroCopyFrameCount} pixelCopies={view.PixelCopyFrameCount}");

        // Dispose while the GL context is alive: GL objects deleted in their context, buffers released.
        view.Dispose();
    }

    /// <summary>Draws repeatedly (pumping WebKit between) until the centre pixel satisfies <paramref name="done"/>.</summary>
    private static SKColor DrawUntil(WpeWebView view, OffscreenGl gpu, int w, int h, Func<SKColor, bool> done, int maxMs = 5000)
    {
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, w, h);
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        SKColor c;
        while (true)
        {
            c = gpu.Draw(view, w, h);
            if (done(c) || DateTime.UtcNow >= deadline) return c;
            Pump(() => false, 50);
        }
    }

    /// <summary>
    /// A headless EGL + GLES + Skia GL context on the GPU WebKit renders with:
    /// EGL_EXT_platform_device (the device whose DRM node matches), else
    /// EGL_MESA_platform_surfaceless. Contexts are surfaceless
    /// (EGL_KHR_surfaceless_context), with a 1x1 pbuffer as a fallback.
    /// </summary>
    private sealed class OffscreenGl : IDisposable
    {
        private IntPtr _display, _context, _surface;
        private GRContext? _gr;
        private SKSurface? _target;
        private int _targetW, _targetH;
        public string Description { get; private set; } = string.Empty;

        public static OffscreenGl Create(string? renderNode)
        {
            var gl = new OffscreenGl();
            try
            {
                gl.Initialize(renderNode);
                return gl;
            }
            catch
            {
                gl.Dispose();
                throw;
            }
        }

        private void Initialize(string? renderNode)
        {
            string platform = "none";
            if (Egl.HasClientExtension("EGL_EXT_platform_device") && renderNode != null)
            {
                foreach (var device in Egl.QueryDevices())
                {
                    var nodes = Egl.GetDeviceDrmNodes(device);
                    if (!nodes.Any(n => Microsoft.Maui.Platform.Linux.Rendering.DmaBufFrameImporter.SameGpu(n, renderNode)))
                        continue;
                    _display = Egl.eglGetPlatformDisplay(Egl.EGL_PLATFORM_DEVICE_EXT, device, IntPtr.Zero);
                    platform = $"device {string.Join("/", nodes)}";
                    break;
                }
            }
            if (_display == IntPtr.Zero && Egl.HasClientExtension("EGL_MESA_platform_surfaceless"))
            {
                _display = Egl.eglGetPlatformDisplay(Egl.EGL_PLATFORM_SURFACELESS_MESA, Egl.EGL_DEFAULT_DISPLAY, IntPtr.Zero);
                platform = "surfaceless";
            }
            if (_display == IntPtr.Zero)
                throw new ScenarioNotSupportedException("no EGL display without a window (need EGL_EXT_platform_device or EGL_MESA_platform_surfaceless)");
            if (Egl.eglInitialize(_display, out _, out _) == Egl.EGL_FALSE)
                throw new ScenarioNotSupportedException($"eglInitialize ({platform}) failed: {Egl.ErrorName(Egl.eglGetError())}");
            if (Egl.eglBindAPI(Egl.EGL_OPENGL_ES_API) == Egl.EGL_FALSE)
                throw new ScenarioNotSupportedException("eglBindAPI(OpenGL ES) failed");

            bool surfaceless = Egl.HasDisplayExtension(_display, "EGL_KHR_surfaceless_context");
            var configs = new IntPtr[1];
            var attribs = new[]
            {
                Egl.EGL_SURFACE_TYPE, surfaceless ? 0 : Egl.EGL_PBUFFER_BIT,
                Egl.EGL_RENDERABLE_TYPE, Egl.EGL_OPENGL_ES2_BIT,
                Egl.EGL_RED_SIZE, 8, Egl.EGL_GREEN_SIZE, 8, Egl.EGL_BLUE_SIZE, 8, Egl.EGL_ALPHA_SIZE, 8,
                Egl.EGL_NONE,
            };
            if (Egl.eglChooseConfig(_display, attribs, configs, 1, out int count) == Egl.EGL_FALSE || count == 0)
                throw new ScenarioNotSupportedException($"no RGBA8 OpenGL ES config on the {platform} display");

            _context = Egl.eglCreateContext(_display, configs[0], Egl.EGL_NO_CONTEXT, new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 3, Egl.EGL_NONE });
            if (_context == IntPtr.Zero)
                _context = Egl.eglCreateContext(_display, configs[0], Egl.EGL_NO_CONTEXT, new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 2, Egl.EGL_NONE });
            if (_context == IntPtr.Zero)
                throw new ScenarioNotSupportedException($"eglCreateContext failed: {Egl.ErrorName(Egl.eglGetError())}");

            if (!surfaceless)
            {
                _surface = Egl.eglCreatePbufferSurface(_display, configs[0], new[] { Egl.EGL_WIDTH, 1, Egl.EGL_HEIGHT, 1, Egl.EGL_NONE });
                if (_surface == IntPtr.Zero)
                    throw new ScenarioNotSupportedException("neither EGL_KHR_surfaceless_context nor a pbuffer surface is available");
            }
            if (Egl.eglMakeCurrent(_display, _surface, _surface, _context) == Egl.EGL_FALSE)
                throw new ScenarioNotSupportedException($"eglMakeCurrent failed: {Egl.ErrorName(Egl.eglGetError())}");

            var glInterface = GRGlInterface.Create(name => Egl.eglGetProcAddress(name))
                ?? throw new ScenarioNotSupportedException("GRGlInterface.Create failed");
            _gr = GRContext.CreateGl(glInterface) ?? throw new ScenarioNotSupportedException("GRContext.CreateGl failed");
            Description = $"{platform}, {Egl.GetGlRenderer()}";
        }

        /// <summary>Draws the view into a GPU surface of the given size and returns the centre pixel.</summary>
        public SKColor Draw(WpeWebView view, int w, int h)
        {
            Egl.eglMakeCurrent(_display, _surface, _surface, _context);
            if (_target == null || _targetW != w || _targetH != h)
            {
                _target?.Dispose();
                (_targetW, _targetH) = (w, h);
                _target = SKSurface.Create(_gr!, false, new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul))
                    ?? throw new ScenarioNotSupportedException("GPU SKSurface creation failed");
            }
            var canvas = _target.Canvas;
            canvas.Clear(SKColors.White);
            view.Draw(canvas);
            _gr!.Flush(submit: true, synchronous: true);

            using var pixel = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!_target.ReadPixels(pixel.Info, pixel.GetPixels(), pixel.RowBytes, w / 2, h / 2))
                throw new InvalidOperationException("GPU read-back failed");
            return pixel.GetPixel(0, 0);
        }

        public void Dispose()
        {
            if (_display == IntPtr.Zero) return;
            if (_context != IntPtr.Zero)
                Egl.eglMakeCurrent(_display, _surface, _surface, _context);
            _target?.Dispose();
            _gr?.Dispose();
            Egl.eglMakeCurrent(_display, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
            if (_surface != IntPtr.Zero) Egl.eglDestroySurface(_display, _surface);
            if (_context != IntPtr.Zero) Egl.eglDestroyContext(_display, _context);
            Egl.eglTerminate(_display);
            _display = IntPtr.Zero;
        }
    }

    /// <summary>Tiny HTTP server so http-only behaviour (cookies, history, reload) is exercised for real.</summary>
    private sealed class LocalServer : IDisposable
    {
        private readonly System.Net.HttpListener _listener = new();
        private readonly Dictionary<string, string> _pages;
        private readonly int _port;
        private int _requests;

        public LocalServer(params (string Path, string Html)[] pages)
        {
            _pages = pages.ToDictionary(p => p.Path, p => p.Html, StringComparer.Ordinal);
            _port = FreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public int RequestCount => Volatile.Read(ref _requests);
        public string? LastCookieHeader { get; private set; }
        public string Url(string path) => $"http://127.0.0.1:{_port}{path}";

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                System.Net.HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }

                LastCookieHeader = ctx.Request.Headers["Cookie"];
                Interlocked.Increment(ref _requests);
                var html = _pages.TryGetValue(ctx.Request.Url?.AbsolutePath ?? "/", out var page) ? page : null;
                ctx.Response.StatusCode = html == null ? 404 : 200;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                var bytes = System.Text.Encoding.UTF8.GetBytes(html ?? "<html><body>not found</body></html>");
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        }

        private static int FreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { }
        }
    }

    // ---- helpers --------------------------------------------------------

    private static WpeWebView Load(string html, out Func<bool> loaded)
    {
        var view = new WpeWebView();
        bool done = false;
        view.NavigationCompleted += (_, e) => done = true;
        view.Bounds = new Microsoft.Maui.Graphics.Rect(0, 0, 400, 300);
        view.LoadHtml(html, "app://localhost/");
        loaded = () => done;
        return view;
    }

    private static void Pump(Func<bool> until, int maxMs = 8000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (!until() && DateTime.UtcNow < deadline)
        {
            GLibNative.ProcessPendingEvents(50);
            Thread.Sleep(5);
        }
    }

    private static T Await<T>(Task<T> task)
    {
        Pump(() => task.IsCompleted);
        Check(task.IsCompleted, "asynchronous operation did not complete in time");
        if (task.IsFaulted)
            ExceptionDispatchInfo.Capture(task.Exception!.GetBaseException()).Throw();
        return task.Result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
