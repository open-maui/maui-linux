// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;
using SkiaSharp;

namespace OpenMaui.Benchmarks;

using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;
using MauiWindow = Microsoft.Maui.Controls.Window;

/// <summary>Where frames are rasterised.</summary>
internal enum TargetKind { Raster, Gpu }

/// <summary>
/// Fake monotonic clock (milliseconds) the platform <see cref="LinuxTicker"/>
/// reads, so animations advance by exactly one 60 Hz interval per frame no
/// matter how long the frame took to render.
/// </summary>
internal sealed class FakeClock
{
    public double Now { get; private set; }
    public void Advance(double ms) => Now += ms;
}

/// <summary>
/// A complete MAUI application hosted headlessly: <c>MauiApp</c> built with
/// <c>UseLinux</c> (every platform handler and service, as in an app), a real
/// <see cref="Microsoft.Maui.Controls.Application"/> and window, and a
/// <see cref="WindowContext"/> over a fake display window with a real
/// <see cref="SkiaRenderingEngine"/>. The engine draws into the production
/// <see cref="RasterRenderTarget"/> (frames land in <see cref="BenchWindow.Present"/>,
/// which copies them like the wl_shm path does) or into an offscreen EGL
/// target. Rendering is driven by <see cref="Frame"/>, which runs the body of
/// <c>LinuxApplication.RunEventLoop</c> minus the native event pump: ticker
/// pump, cursor-blink update, render.
/// </summary>
/// <remarks>Mirrors tests/Views/HeadlessMauiHost.cs and tests/HeadlessMaui.cs.</remarks>
internal sealed class BenchHost : IDisposable
{
    private static bool s_dispatcherInstalled;

    public FakeClock Clock { get; }
    public LinuxApplication LinuxApp { get; }
    public MauiApp MauiApp { get; }
    public BenchWindow Window { get; }
    public WindowContext Context { get; }
    public SkiaRenderingEngine Engine { get; }
    public SkiaView Root { get; }
    public Page Page { get; }
    public OffscreenGl? Gl { get; }

    /// <summary>Time spent in CreateBuilder..Build (the MAUI app build).</summary>
    public TimeSpan BuildTime { get; }

    /// <summary>Frames the target actually produced (nothing dirty = no frame).</summary>
    public int FramesRendered => Gl != null ? _gpuTarget!.Frames : Window.PresentCount;

    private readonly GpuRenderTarget? _gpuTarget;

    public BenchHost(Func<Page> pageFactory, int logicalWidth = 800, int logicalHeight = 600, float scale = 1f,
                     TargetKind target = TargetKind.Raster, OffscreenGl? gl = null)
    {
        if (!s_dispatcherInstalled)
        {
            // Single-threaded headless run: this thread is the UI thread and
            // every dispatch runs inline (as in tests/HeadlessMaui.cs).
            DispatcherProvider.SetCurrent(new InlineDispatcherProvider());
            s_dispatcherInstalled = true;
        }

        Clock = new FakeClock();
        var clock = Clock;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<BenchApplication>();
        // Registered before UseLinux, whose TryAdd then keeps these: the real
        // platform ticker and animation manager, reading the fake clock.
        var ticker = new LinuxTicker(() => clock.Now);
        builder.Services.AddSingleton<ITicker>(ticker);
        builder.Services.AddSingleton<IAnimationManager>(_ => new LinuxAnimationManager(ticker));
        builder.UseLinux(_ => { });
        MauiApp = builder.Build();
        BuildTime = sw.Elapsed;

        LinuxApp = new LinuxApplication();
        var mauiContext = new LinuxMauiContext(MauiApp.Services, LinuxApp);
        LinuxApp.MauiContext = mauiContext;

        var app = (BenchApplication)MauiApp.Services.GetRequiredService<IApplication>();
        app.UserAppTheme = AppTheme.Light; // do not follow the desktop theme
        Microsoft.Maui.Controls.Application.Current = app;

        Page = pageFactory();
        var mauiWindow = new MauiWindow(Page) { FlowDirection = FlowDirection.LeftToRight };
        app.StartupWindow = mauiWindow;
        ((IApplication)app).CreateWindow(null!);

        Window = new BenchWindow((int)Math.Round(logicalWidth * scale), (int)Math.Round(logicalHeight * scale), scale);
        if (target == TargetKind.Gpu)
        {
            Gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _gpuTarget = new GpuRenderTarget(Gl, Window.Width, Window.Height);
            Engine = new SkiaRenderingEngine(Window, _gpuTarget);
        }
        else
        {
            Engine = new SkiaRenderingEngine(Window);
        }

        Context = LinuxApp.AttachWindowContext(Window, Engine, raisesMauiLifecycle: false);
        Context.WireInput(); // applies the window scale to the engine
        Context.MauiWindow = mauiWindow;
        Root = new LinuxViewRenderer(mauiContext).RenderPage(Page)
            ?? throw new InvalidOperationException("page did not produce a SkiaView");
        Context.RootView = Root;
    }

    /// <summary>One run-loop iteration: ticker, cursor blink, render.</summary>
    public void Frame()
    {
        LinuxTicker.PumpAll();
        Context.UpdateAnimations();
        Context.Render();
    }

    /// <summary>Marks the whole window dirty (a full repaint on the next frame).</summary>
    public void InvalidateAll() => Engine.InvalidateAll();

    public void Dispose()
    {
        LinuxApp.Dispose();
        MauiApp.Dispose();
    }

    public sealed class BenchApplication : Microsoft.Maui.Controls.Application
    {
        public MauiWindow? StartupWindow { get; set; }

        protected override MauiWindow CreateWindow(IActivationState? activationState)
            => StartupWindow ?? throw new InvalidOperationException("StartupWindow not set");
    }

    private sealed class InlineDispatcherProvider : IDispatcherProvider
    {
        private readonly InlineDispatcher _dispatcher = new();
        public IDispatcher? GetForCurrentThread() => _dispatcher;
    }

    private sealed class InlineDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
        public IDispatcherTimer CreateTimer() => new InlineTimer();
    }

    private sealed class InlineTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsRepeating { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler? Tick;
        public void Start() { IsRunning = true; _ = Tick; }
        public void Stop() => IsRunning = false;
    }
}

/// <summary>
/// Display window without a display: size and scale for the engine, raise
/// helpers for native events, and a Present that copies the frame into a
/// buffer the way the Wayland wl_shm path copies into its pool.
/// </summary>
internal sealed class BenchWindow : IDisplayWindow, IScaleAwareDisplayWindow
{
    private byte[] _shm = Array.Empty<byte>();

    public BenchWindow(int width, int height, float scale)
    {
        Width = width;
        Height = height;
        Scale = scale;
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public float Scale { get; }
    public bool IsRunning => true;
    public int PresentCount { get; private set; }

    public event EventHandler<float>? ScaleChanged;
    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<TextInputEventArgs>? TextInput;
    public event EventHandler<PointerEventArgs>? PointerMoved;
    public event EventHandler<PointerEventArgs>? PointerPressed;
    public event EventHandler<PointerEventArgs>? PointerReleased;
    public event EventHandler<ScrollEventArgs>? Scroll;
    public event EventHandler? Exposed;
    public event EventHandler<(int Width, int Height)>? Resized;
    public event EventHandler? CloseRequested;
    public event EventHandler? FocusGained;
    public event EventHandler? FocusLost;

    public void Show() { }
    public void Hide() { }
    public void SetTitle(string title) { }
    public void Resize(int width, int height) { Width = width; Height = height; }
    public void SetCursor(CursorType cursorType) { }
    public void SetIcon(string iconPath) { }
    public void SetWMClass(string resName, string resClass) { }
    public void ProcessEvents() { }
    public void Stop() { }
    public int GetFileDescriptor() => -1;
    public void FlushDeferredResize() { }
    public void AcknowledgeSync() { }
    public void Dispose() { }

    public unsafe void Present(IntPtr pixels, int width, int height, int stride)
    {
        PresentCount++;
        int bytes = stride * height;
        if (_shm.Length < bytes) _shm = new byte[bytes];
        fixed (byte* dst = _shm)
            Buffer.MemoryCopy((void*)pixels, dst, _shm.Length, bytes);
    }

    /// <summary>A compositor configure: new physical size, then the Resized event.</summary>
    public void RaiseResized(int w, int h) { Width = w; Height = h; Resized?.Invoke(this, (w, h)); }

    /// <summary>A wheel event at physical coordinates (x, y).</summary>
    public void RaiseScroll(float x, float y, float dx, float dy) => Scroll?.Invoke(this, new ScrollEventArgs(x, y, dx, dy));

    private void Touch()
    {
        _ = ScaleChanged; _ = KeyDown; _ = KeyUp; _ = TextInput; _ = PointerMoved; _ = PointerPressed;
        _ = PointerReleased; _ = Exposed; _ = CloseRequested; _ = FocusGained; _ = FocusLost;
    }
}

/// <summary>
/// GPU target over an offscreen EGL context: a Skia GPU surface the size of
/// the window. Like the EGL swapchain target it does not preserve contents,
/// so every frame is a full repaint. EndFrame flushes and waits for the GPU
/// (the frame is "ready" when the GPU has finished it), which is what a frame
/// time should measure without a compositor to pace the swap.
/// </summary>
internal sealed class GpuRenderTarget : IRenderTarget
{
    private readonly OffscreenGl _gl;
    private SKSurface? _surface;
    private int _width, _height;

    public GpuRenderTarget(OffscreenGl gl, int width, int height)
    {
        _gl = gl;
        Resize(width, height);
    }

    public int Frames { get; private set; }
    public string Name => "egl-offscreen";
    public bool IsGpuAccelerated => true;
    public bool PreservesContents => false;
    public int Width => _width;
    public int Height => _height;

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_surface != null && width == _width && height == _height) return;
        _gl.MakeCurrent();
        _surface?.Dispose();
        (_width, _height) = (width, height);
        _surface = SKSurface.Create(_gl.Context, false, new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("GPU SKSurface creation failed");
    }

    public SKCanvas? BeginFrame()
    {
        _gl.MakeCurrent();
        return _surface?.Canvas;
    }

    public void EndFrame()
    {
        _gl.Context.Flush(submit: true, synchronous: true);
        Frames++;
    }

    public void Dispose()
    {
        _gl.MakeCurrent();
        _surface?.Dispose();
        _surface = null;
    }
}

/// <summary>
/// Headless OpenGL ES context: EGL_EXT_platform_device on the chosen DRM
/// render node, else EGL_MESA_platform_surfaceless; surfaceless context or a
/// 1x1 pbuffer. Same approach as tests/WebViewHost's OffscreenGl.
/// </summary>
internal sealed class OffscreenGl : IDisposable
{
    private IntPtr _display, _context, _surface;
    private GRContext? _gr;

    public GRContext Context => _gr!;
    public string Renderer { get; private set; } = "";
    public string Platform { get; private set; } = "";

    public static OffscreenGl? TryCreate(string? renderNode, out string? error)
    {
        var gl = new OffscreenGl();
        try
        {
            gl.Initialize(renderNode);
            error = null;
            return gl;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            gl.Dispose();
            error = ex.Message;
            return null;
        }
    }

    private void Initialize(string? renderNode)
    {
        if (renderNode != null && Egl.HasClientExtension("EGL_EXT_platform_device"))
        {
            foreach (var device in Egl.QueryDevices())
            {
                var nodes = Egl.GetDeviceDrmNodes(device);
                if (!nodes.Contains(renderNode)) continue;
                _display = Egl.eglGetPlatformDisplay(Egl.EGL_PLATFORM_DEVICE_EXT, device, IntPtr.Zero);
                Platform = $"device {renderNode}";
                break;
            }
        }
        if (_display == IntPtr.Zero && Egl.HasClientExtension("EGL_MESA_platform_surfaceless"))
        {
            _display = Egl.eglGetPlatformDisplay(Egl.EGL_PLATFORM_SURFACELESS_MESA, Egl.EGL_DEFAULT_DISPLAY, IntPtr.Zero);
            Platform = "surfaceless";
        }
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("no windowless EGL display (need EGL_EXT_platform_device or EGL_MESA_platform_surfaceless)");
        if (Egl.eglInitialize(_display, out _, out _) == Egl.EGL_FALSE)
            throw new InvalidOperationException($"eglInitialize ({Platform}) failed: {Egl.ErrorName(Egl.eglGetError())}");
        if (Egl.eglBindAPI(Egl.EGL_OPENGL_ES_API) == Egl.EGL_FALSE)
            throw new InvalidOperationException("eglBindAPI(OpenGL ES) failed");

        bool surfaceless = Egl.HasDisplayExtension(_display, "EGL_KHR_surfaceless_context");
        var configs = new IntPtr[1];
        var attribs = new[]
        {
            Egl.EGL_SURFACE_TYPE, surfaceless ? 0 : Egl.EGL_PBUFFER_BIT,
            Egl.EGL_RENDERABLE_TYPE, Egl.EGL_OPENGL_ES2_BIT,
            Egl.EGL_RED_SIZE, 8, Egl.EGL_GREEN_SIZE, 8, Egl.EGL_BLUE_SIZE, 8, Egl.EGL_ALPHA_SIZE, 8,
            Egl.EGL_STENCIL_SIZE, 8,
            Egl.EGL_NONE,
        };
        if (Egl.eglChooseConfig(_display, attribs, configs, 1, out int count) == Egl.EGL_FALSE || count == 0)
            throw new InvalidOperationException($"no RGBA8 OpenGL ES config on the {Platform} display");

        _context = Egl.eglCreateContext(_display, configs[0], Egl.EGL_NO_CONTEXT, new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 3, Egl.EGL_NONE });
        if (_context == IntPtr.Zero)
            _context = Egl.eglCreateContext(_display, configs[0], Egl.EGL_NO_CONTEXT, new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 2, Egl.EGL_NONE });
        if (_context == IntPtr.Zero)
            throw new InvalidOperationException($"eglCreateContext failed: {Egl.ErrorName(Egl.eglGetError())}");

        if (!surfaceless)
        {
            _surface = Egl.eglCreatePbufferSurface(_display, configs[0], new[] { Egl.EGL_WIDTH, 1, Egl.EGL_HEIGHT, 1, Egl.EGL_NONE });
            if (_surface == IntPtr.Zero)
                throw new InvalidOperationException("neither EGL_KHR_surfaceless_context nor a pbuffer surface is available");
        }
        MakeCurrent();

        var glInterface = GRGlInterface.Create(name => Egl.eglGetProcAddress(name))
            ?? throw new InvalidOperationException("GRGlInterface.Create failed");
        _gr = GRContext.CreateGl(glInterface) ?? throw new InvalidOperationException("GRContext.CreateGl failed");
        Renderer = Egl.GetGlRenderer() ?? "unknown";
    }

    public void MakeCurrent()
    {
        if (Egl.eglMakeCurrent(_display, _surface, _surface, _context) == Egl.EGL_FALSE)
            throw new InvalidOperationException($"eglMakeCurrent failed: {Egl.ErrorName(Egl.eglGetError())}");
    }

    public void Dispose()
    {
        if (_display == IntPtr.Zero) return;
        if (_context != IntPtr.Zero)
            Egl.eglMakeCurrent(_display, _surface, _surface, _context);
        _gr?.Dispose();
        Egl.eglMakeCurrent(_display, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
        if (_surface != IntPtr.Zero) Egl.eglDestroySurface(_display, _surface);
        if (_context != IntPtr.Zero) Egl.eglDestroyContext(_display, _context);
        Egl.eglTerminate(_display);
        _display = IntPtr.Zero;
    }
}
