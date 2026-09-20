// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>
/// GPU render target: an EGL window surface with an OpenGL ES context and a
/// Skia <see cref="GRContext"/> drawing straight into the default framebuffer.
/// Presentation is <c>eglSwapBuffers</c>, which on Wayland attaches and commits
/// a GPU buffer to the surface (zero copy) and on X11 goes through DRI3/Present.
/// Backend-specific native window handling lives in the subclasses.
/// </summary>
public abstract class EglRenderTarget : IRenderTarget
{
    private IntPtr _eglDisplay;
    private IntPtr _eglConfig;
    private IntPtr _eglContext;
    private IntPtr _eglSurface;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _backendTarget;
    private SKSurface? _skSurface;
    private int _stencilBits;
    private int _sampleCount;
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _swapFailureLogged;

    public string Name { get; }
    public bool IsGpuAccelerated => true;

    /// <summary>
    /// A swapped GPU buffer does not carry the previous frame, so the engine
    /// repaints the whole surface each frame. GPU fill rate makes that cheaper
    /// than the raster path's partial repaints.
    /// </summary>
    public bool PreservesContents => false;

    public int Width => _width;
    public int Height => _height;

    /// <summary>GL renderer string (driver + GPU), for diagnostics.</summary>
    public string? Renderer { get; private set; }

    /// <summary>EGL vendor/version string, for diagnostics.</summary>
    public string? EglVersion { get; private set; }

    protected EglRenderTarget(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Brings up display, config, context, window surface and the Skia GL
    /// context. Throws on any failure; the factory falls back to raster.
    /// </summary>
    /// <param name="requiredVisualId">
    /// X11 only: the window's visual id. The EGL config must match it or
    /// <c>eglCreateWindowSurface</c> fails with EGL_BAD_MATCH.
    /// </param>
    protected void Initialize(uint platform, IntPtr nativeDisplay, IntPtr nativeWindow,
        long? requiredVisualId, int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        _eglDisplay = Egl.eglGetPlatformDisplay(platform, nativeDisplay, IntPtr.Zero);
        if (_eglDisplay == Egl.EGL_NO_DISPLAY)
            throw Fail("eglGetPlatformDisplay");

        if (Egl.eglInitialize(_eglDisplay, out int major, out int minor) == Egl.EGL_FALSE)
            throw Fail("eglInitialize");
        EglVersion = $"{major}.{minor} {Egl.QueryString(_eglDisplay, Egl.EGL_VENDOR)}";

        if (Egl.eglBindAPI(Egl.EGL_OPENGL_ES_API) == Egl.EGL_FALSE)
            throw Fail("eglBindAPI(OpenGL ES)");

        _eglConfig = ChooseConfig(requiredVisualId, wantStencil: true);
        if (_eglConfig == IntPtr.Zero)
            _eglConfig = ChooseConfig(requiredVisualId, wantStencil: false);
        if (_eglConfig == IntPtr.Zero)
            throw new InvalidOperationException("No EGL config matches the window (RGB8, window surface, OpenGL ES 2)");

        Egl.eglGetConfigAttrib(_eglDisplay, _eglConfig, Egl.EGL_STENCIL_SIZE, out _stencilBits);
        Egl.eglGetConfigAttrib(_eglDisplay, _eglConfig, Egl.EGL_SAMPLES, out _sampleCount);

        // ES 3 preferred; ES 2 is enough for Skia's GL backend.
        _eglContext = Egl.eglCreateContext(_eglDisplay, _eglConfig, Egl.EGL_NO_CONTEXT,
            new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 3, Egl.EGL_NONE });
        if (_eglContext == Egl.EGL_NO_CONTEXT)
        {
            _eglContext = Egl.eglCreateContext(_eglDisplay, _eglConfig, Egl.EGL_NO_CONTEXT,
                new[] { Egl.EGL_CONTEXT_CLIENT_VERSION, 2, Egl.EGL_NONE });
        }
        if (_eglContext == Egl.EGL_NO_CONTEXT)
            throw Fail("eglCreateContext");

        _eglSurface = Egl.eglCreateWindowSurface(_eglDisplay, _eglConfig, nativeWindow, IntPtr.Zero);
        if (_eglSurface == Egl.EGL_NO_SURFACE)
            throw Fail("eglCreateWindowSurface");

        if (Egl.eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext) == Egl.EGL_FALSE)
            throw Fail("eglMakeCurrent");

        // Never block in eglSwapBuffers waiting for a vblank: the run loop
        // already renders only when something is dirty, and on Wayland a
        // swap-interval of 1 blocks indefinitely while the surface is hidden.
        Egl.eglSwapInterval(_eglDisplay, 0);

        var glInterface = GRGlInterface.Create(name => Egl.eglGetProcAddress(name));
        if (glInterface == null)
            throw new InvalidOperationException("Skia could not assemble a GL interface over EGL (GRGlInterface.Create returned null)");

        _grContext = GRContext.CreateGl(glInterface);
        if (_grContext == null)
            throw new InvalidOperationException("GRContext.CreateGl failed");

        Renderer = Egl.GetGlRenderer();
    }

    private IntPtr ChooseConfig(long? requiredVisualId, bool wantStencil)
    {
        var attribs = new List<int>
        {
            Egl.EGL_SURFACE_TYPE, Egl.EGL_WINDOW_BIT,
            Egl.EGL_RENDERABLE_TYPE, Egl.EGL_OPENGL_ES2_BIT,
            Egl.EGL_RED_SIZE, 8,
            Egl.EGL_GREEN_SIZE, 8,
            Egl.EGL_BLUE_SIZE, 8,
        };
        if (wantStencil)
        {
            attribs.Add(Egl.EGL_STENCIL_SIZE);
            attribs.Add(8);
        }
        attribs.Add(Egl.EGL_NONE);

        var configs = new IntPtr[64];
        if (Egl.eglChooseConfig(_eglDisplay, attribs.ToArray(), configs, configs.Length, out int count) == Egl.EGL_FALSE || count == 0)
            return IntPtr.Zero;

        IntPtr firstAlpha = IntPtr.Zero;
        for (int i = 0; i < count; i++)
        {
            var cfg = configs[i];
            if (requiredVisualId is long visual)
            {
                Egl.eglGetConfigAttrib(_eglDisplay, cfg, Egl.EGL_NATIVE_VISUAL_ID, out int id);
                if (id == visual)
                    return cfg;
                continue;
            }

            // Wayland: prefer an ARGB config so the surface keeps the alpha the
            // wl_shm path (ARGB8888) always had.
            Egl.eglGetConfigAttrib(_eglDisplay, cfg, Egl.EGL_ALPHA_SIZE, out int alpha);
            if (alpha >= 8)
                return cfg;
            if (firstAlpha == IntPtr.Zero)
                firstAlpha = cfg;
        }

        return requiredVisualId is null ? firstAlpha : IntPtr.Zero;
    }

    private static InvalidOperationException Fail(string call)
        => new($"{call} failed: {Egl.ErrorName(Egl.eglGetError())}");

    /// <summary>Called after the native window must take a new size (Wayland: wl_egl_window_resize).</summary>
    protected abstract void ResizeNativeWindow(int width, int height);

    /// <summary>Called during dispose, after the EGL surface is destroyed.</summary>
    protected abstract void DestroyNativeWindow();

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (width == _width && height == _height) return;

        _width = width;
        _height = height;
        ResizeNativeWindow(width, height);

        // The Skia surface wraps the default framebuffer at a fixed size;
        // recreate it lazily in BeginFrame once the context is current.
        _skSurface?.Dispose();
        _skSurface = null;
        _backendTarget?.Dispose();
        _backendTarget = null;
    }

    public SKCanvas? BeginFrame()
    {
        if (_disposed || _grContext == null) return null;

        // Several windows share the UI thread, each with its own EGL context:
        // make ours current before touching GL.
        if (Egl.eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext) == Egl.EGL_FALSE)
        {
            DiagnosticLog.Error("EglRenderTarget", $"eglMakeCurrent failed: {Egl.ErrorName(Egl.eglGetError())}");
            return null;
        }

        if (_skSurface == null)
        {
            _backendTarget = new GRBackendRenderTarget(_width, _height, _sampleCount, _stencilBits,
                new GRGlFramebufferInfo(0, Egl.GL_RGBA8));
            _skSurface = SKSurface.Create(_grContext, _backendTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
            if (_skSurface == null)
            {
                DiagnosticLog.Error("EglRenderTarget", "SKSurface.Create over the EGL framebuffer failed");
                return null;
            }
        }

        return _skSurface.Canvas;
    }

    public void EndFrame()
    {
        if (_disposed || _skSurface == null || _grContext == null) return;

        _skSurface.Canvas.Flush();
        _grContext.Flush();

        if (Egl.eglSwapBuffers(_eglDisplay, _eglSurface) == Egl.EGL_FALSE && !_swapFailureLogged)
        {
            _swapFailureLogged = true;
            DiagnosticLog.Error("EglRenderTarget", $"eglSwapBuffers failed: {Egl.ErrorName(Egl.eglGetError())}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_eglDisplay != Egl.EGL_NO_DISPLAY)
        {
            // Skia objects must go while the context is still current.
            if (_eglContext != Egl.EGL_NO_CONTEXT)
                Egl.eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext);

            _skSurface?.Dispose();
            _backendTarget?.Dispose();
            _grContext?.Dispose();
            _skSurface = null;
            _backendTarget = null;
            _grContext = null;

            Egl.eglMakeCurrent(_eglDisplay, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
            if (_eglSurface != Egl.EGL_NO_SURFACE)
                Egl.eglDestroySurface(_eglDisplay, _eglSurface);
            if (_eglContext != Egl.EGL_NO_CONTEXT)
                Egl.eglDestroyContext(_eglDisplay, _eglContext);
            _eglSurface = Egl.EGL_NO_SURFACE;
            _eglContext = Egl.EGL_NO_CONTEXT;
        }

        DestroyNativeWindow();

        if (_eglDisplay != Egl.EGL_NO_DISPLAY)
        {
            // Each window owns its display connection, so terminating here
            // cannot pull the rug from another window's context.
            Egl.eglTerminate(_eglDisplay);
            _eglDisplay = Egl.EGL_NO_DISPLAY;
        }
    }
}

/// <summary>
/// EGL over a native Wayland surface via <c>wl_egl_window</c>. The window is
/// switched to external presentation first so its wl_shm buffer is released
/// and <c>Show</c>/<c>Present</c> no longer attach it; <c>eglSwapBuffers</c>
/// owns attach/damage/commit from then on. The wp_viewporter destination the
/// window already maintains keeps applying, so buffer size stays physical and
/// the compositor still sees the logical size.
/// </summary>
public sealed class WaylandEglRenderTarget : EglRenderTarget
{
    private IntPtr _eglWindow;

    public WaylandEglRenderTarget(Services.IWaylandSurface surface, int width, int height)
        : base("egl-wayland")
    {
        _eglWindow = Egl.wl_egl_window_create(surface.Surface, Math.Max(1, width), Math.Max(1, height));
        if (_eglWindow == IntPtr.Zero)
            throw new InvalidOperationException("wl_egl_window_create failed");

        try
        {
            Initialize(Egl.EGL_PLATFORM_WAYLAND_KHR, surface.Display, _eglWindow, requiredVisualId: null, width, height);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    protected override void ResizeNativeWindow(int width, int height)
    {
        if (_eglWindow != IntPtr.Zero)
            Egl.wl_egl_window_resize(_eglWindow, width, height, 0, 0);
    }

    protected override void DestroyNativeWindow()
    {
        if (_eglWindow != IntPtr.Zero)
        {
            Egl.wl_egl_window_destroy(_eglWindow);
            _eglWindow = IntPtr.Zero;
        }
    }
}

/// <summary>
/// EGL over an X11 window. The EGL config is matched to the window's visual;
/// the X server resizes the drawable with the window, so only the Skia surface
/// is recreated on resize.
/// </summary>
public sealed class X11EglRenderTarget : EglRenderTarget
{
    public X11EglRenderTarget(Services.IX11Surface surface, int width, int height)
        : base("egl-x11")
    {
        int screen = X11.XDefaultScreen(surface.Display);
        var visual = X11.XDefaultVisual(surface.Display, screen);
        long visualId = (long)X11.XVisualIDFromVisual(visual);

        try
        {
            Initialize(Egl.EGL_PLATFORM_X11_KHR, surface.Display, surface.Handle, visualId, width, height);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    protected override void ResizeNativeWindow(int width, int height) { /* the X server resizes the drawable */ }

    protected override void DestroyNativeWindow() { /* the window owns the X11 drawable */ }
}
