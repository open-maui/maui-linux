// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// Minimal EGL 1.5 surface (libEGL.so.1) for the GPU render targets, plus the
/// wayland-egl bridge (libwayland-egl.so.1) that turns a wl_surface into an
/// EGLNativeWindowType. Only what the render targets need is bound.
/// </summary>
internal static partial class Egl
{
    private const string LibEgl = "libEGL.so.1";
    private const string LibWaylandEgl = "libwayland-egl.so.1";

    // Platform enums (EGL 1.5 / EGL_KHR_platform_*)
    public const int EGL_PLATFORM_X11_KHR = 0x31D5;
    public const int EGL_PLATFORM_WAYLAND_KHR = 0x31D8;

    // Booleans / sentinels
    public const int EGL_FALSE = 0;
    public const int EGL_TRUE = 1;
    public const int EGL_NONE = 0x3038;
    public static readonly IntPtr EGL_NO_DISPLAY = IntPtr.Zero;
    public static readonly IntPtr EGL_NO_CONTEXT = IntPtr.Zero;
    public static readonly IntPtr EGL_NO_SURFACE = IntPtr.Zero;
    public static readonly IntPtr EGL_DEFAULT_DISPLAY = IntPtr.Zero;

    // Config attributes
    public const int EGL_ALPHA_SIZE = 0x3021;
    public const int EGL_BLUE_SIZE = 0x3022;
    public const int EGL_GREEN_SIZE = 0x3023;
    public const int EGL_RED_SIZE = 0x3024;
    public const int EGL_DEPTH_SIZE = 0x3025;
    public const int EGL_STENCIL_SIZE = 0x3026;
    public const int EGL_SAMPLES = 0x3031;
    public const int EGL_SURFACE_TYPE = 0x3033;
    public const int EGL_NATIVE_VISUAL_ID = 0x302E;
    public const int EGL_RENDERABLE_TYPE = 0x3040;
    public const int EGL_WINDOW_BIT = 0x0004;
    public const int EGL_OPENGL_ES2_BIT = 0x0004;
    public const int EGL_OPENGL_ES3_BIT = 0x0040;

    // API binding / context attributes
    public const int EGL_OPENGL_ES_API = 0x30A0;
    public const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;

    // Surface attributes
    public const int EGL_SWAP_BEHAVIOR = 0x3093;
    public const int EGL_BUFFER_PRESERVED = 0x3094;
    public const int EGL_BUFFER_DESTROYED = 0x3095;

    // Query strings
    public const int EGL_VENDOR = 0x3053;
    public const int EGL_VERSION = 0x3054;
    public const int EGL_EXTENSIONS = 0x3055;

    // GL constants Skia needs for the framebuffer description
    public const uint GL_RGBA8 = 0x8058;
    public const uint GL_RENDERER = 0x1F01;

    [LibraryImport(LibEgl)]
    public static partial int eglGetError();

    [LibraryImport(LibEgl)]
    public static partial IntPtr eglGetPlatformDisplay(uint platform, IntPtr nativeDisplay, IntPtr attribList);

    [LibraryImport(LibEgl)]
    public static partial IntPtr eglGetDisplay(IntPtr nativeDisplay);

    [LibraryImport(LibEgl)]
    public static partial int eglInitialize(IntPtr display, out int major, out int minor);

    [LibraryImport(LibEgl)]
    public static partial int eglTerminate(IntPtr display);

    [LibraryImport(LibEgl)]
    public static partial int eglBindAPI(uint api);

    [LibraryImport(LibEgl)]
    public static partial int eglChooseConfig(IntPtr display, int[] attribList, IntPtr[] configs, int configSize, out int numConfig);

    [LibraryImport(LibEgl)]
    public static partial int eglGetConfigAttrib(IntPtr display, IntPtr config, int attribute, out int value);

    [LibraryImport(LibEgl)]
    public static partial IntPtr eglCreateWindowSurface(IntPtr display, IntPtr config, IntPtr nativeWindow, IntPtr attribList);

    [LibraryImport(LibEgl)]
    public static partial int eglDestroySurface(IntPtr display, IntPtr surface);

    [LibraryImport(LibEgl)]
    public static partial IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int[] attribList);

    [LibraryImport(LibEgl)]
    public static partial int eglDestroyContext(IntPtr display, IntPtr context);

    [LibraryImport(LibEgl)]
    public static partial int eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);

    [LibraryImport(LibEgl)]
    public static partial int eglSwapBuffers(IntPtr display, IntPtr surface);

    [LibraryImport(LibEgl)]
    public static partial int eglSwapInterval(IntPtr display, int interval);

    [LibraryImport(LibEgl)]
    public static partial int eglSurfaceAttrib(IntPtr display, IntPtr surface, int attribute, int value);

    [LibraryImport(LibEgl, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr eglGetProcAddress(string procName);

    [LibraryImport(LibEgl)]
    private static partial IntPtr eglQueryString(IntPtr display, int name);

    public static string? QueryString(IntPtr display, int name)
    {
        var p = eglQueryString(display, name);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }

    // wayland-egl: wraps a wl_surface in a resizable native window for EGL.
    [LibraryImport(LibWaylandEgl)]
    public static partial IntPtr wl_egl_window_create(IntPtr surface, int width, int height);

    [LibraryImport(LibWaylandEgl)]
    public static partial void wl_egl_window_destroy(IntPtr eglWindow);

    [LibraryImport(LibWaylandEgl)]
    public static partial void wl_egl_window_resize(IntPtr eglWindow, int width, int height, int dx, int dy);

    /// <summary>
    /// glGetString(GL_RENDERER) through the EGL proc loader, for diagnostics.
    /// Only valid with a current context.
    /// </summary>
    public static string? GetGlRenderer()
    {
        var fn = eglGetProcAddress("glGetString");
        if (fn == IntPtr.Zero) return null;
        var getString = Marshal.GetDelegateForFunctionPointer<GlGetStringDelegate>(fn);
        var p = getString(GL_RENDERER);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GlGetStringDelegate(uint name);

    public static string ErrorName(int error) => error switch
    {
        0x3000 => "EGL_SUCCESS",
        0x3001 => "EGL_NOT_INITIALIZED",
        0x3002 => "EGL_BAD_ACCESS",
        0x3003 => "EGL_BAD_ALLOC",
        0x3004 => "EGL_BAD_ATTRIBUTE",
        0x3005 => "EGL_BAD_CONFIG",
        0x3006 => "EGL_BAD_CONTEXT",
        0x3007 => "EGL_BAD_CURRENT_SURFACE",
        0x3008 => "EGL_BAD_DISPLAY",
        0x3009 => "EGL_BAD_MATCH",
        0x300A => "EGL_BAD_NATIVE_PIXMAP",
        0x300B => "EGL_BAD_NATIVE_WINDOW",
        0x300C => "EGL_BAD_PARAMETER",
        0x300D => "EGL_BAD_SURFACE",
        0x300E => "EGL_CONTEXT_LOST",
        _ => $"0x{error:X}",
    };
}
