// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// EGL pieces for importing DMA-BUFs as textures (the WebView's zero-copy
/// frame path) and for creating offscreen contexts: EGLImage (EGL 1.5 core),
/// native fence syncs (EGL_ANDROID_native_fence_sync), device queries
/// (EGL_EXT_device_query / _enumeration / _drm / _drm_render_node) and the
/// GLES entry points the importer needs, all resolved at runtime.
/// </summary>
internal static partial class Egl
{
    private const string LibEglDmaBuf = "libEGL.so.1";

    // EGL_EXT_image_dma_buf_import(_modifiers)
    public const int EGL_LINUX_DMA_BUF_EXT = 0x3270;
    public const int EGL_LINUX_DRM_FOURCC_EXT = 0x3271;
    public const int EGL_WIDTH = 0x3057;
    public const int EGL_HEIGHT = 0x3056;
    public const int EGL_IMAGE_PRESERVED_KHR = 0x30D2;

    public static readonly int[] EGL_DMA_BUF_PLANE_FD_EXT = { 0x3272, 0x3275, 0x3278, 0x3440 };
    public static readonly int[] EGL_DMA_BUF_PLANE_OFFSET_EXT = { 0x3273, 0x3276, 0x3279, 0x3441 };
    public static readonly int[] EGL_DMA_BUF_PLANE_PITCH_EXT = { 0x3274, 0x3277, 0x327A, 0x3442 };
    public static readonly int[] EGL_DMA_BUF_PLANE_MODIFIER_LO_EXT = { 0x3443, 0x3445, 0x3447, 0x3449 };
    public static readonly int[] EGL_DMA_BUF_PLANE_MODIFIER_HI_EXT = { 0x3444, 0x3446, 0x3448, 0x344A };

    // EGL_ANDROID_native_fence_sync
    public const int EGL_SYNC_NATIVE_FENCE_ANDROID = 0x3144;
    public const int EGL_SYNC_NATIVE_FENCE_FD_ANDROID = 0x3145;
    public const int EGL_NO_NATIVE_FENCE_FD_ANDROID = -1;

    // Current-surface query
    public const int EGL_DRAW = 0x3059;
    public const int EGL_READ = 0x305A;

    // Devices / platforms
    public const int EGL_DEVICE_EXT = 0x322C;
    public const int EGL_DRM_DEVICE_FILE_EXT = 0x3233;
    public const int EGL_DRM_RENDER_NODE_FILE_EXT = 0x3377;
    public const int EGL_PLATFORM_DEVICE_EXT = 0x313F;
    public const int EGL_PLATFORM_SURFACELESS_MESA = 0x31DD;

    // Pbuffer configs for offscreen contexts
    public const int EGL_PBUFFER_BIT = 0x0001;

    [LibraryImport(LibEglDmaBuf)]
    public static partial IntPtr eglGetCurrentDisplay();

    [LibraryImport(LibEglDmaBuf)]
    public static partial IntPtr eglGetCurrentContext();

    [LibraryImport(LibEglDmaBuf)]
    public static partial IntPtr eglGetCurrentSurface(int readDraw);

    /// <summary>EGL 1.5 core (EGLAttrib list).</summary>
    [LibraryImport(LibEglDmaBuf)]
    public static partial IntPtr eglCreateImage(IntPtr display, IntPtr context, uint target, IntPtr buffer, nint[] attribList);

    [LibraryImport(LibEglDmaBuf)]
    public static partial int eglDestroyImage(IntPtr display, IntPtr image);

    [LibraryImport(LibEglDmaBuf)]
    public static partial IntPtr eglCreateSync(IntPtr display, uint type, nint[] attribList);

    [LibraryImport(LibEglDmaBuf)]
    public static partial int eglDestroySync(IntPtr display, IntPtr sync);

    [LibraryImport(LibEglDmaBuf)]
    public static partial int eglWaitSync(IntPtr display, IntPtr sync, int flags);

    [LibraryImport(LibEglDmaBuf)]
    public static partial IntPtr eglCreatePbufferSurface(IntPtr display, IntPtr config, int[] attribList);

    /// <summary>True when the space-separated extension list contains <paramref name="name"/>.</summary>
    public static bool HasExtension(string? extensions, string name)
    {
        if (string.IsNullOrEmpty(extensions)) return false;
        foreach (var token in extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (token == name) return true;
        return false;
    }

    public static bool HasDisplayExtension(IntPtr display, string name)
        => HasExtension(QueryString(display, EGL_EXTENSIONS), name);

    public static bool HasClientExtension(string name)
        => HasExtension(QueryString(EGL_NO_DISPLAY, EGL_EXTENSIONS), name);

    private static T? Proc<T>(string name) where T : Delegate
    {
        var p = eglGetProcAddress(name);
        return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DupNativeFenceFdDelegate(IntPtr display, IntPtr sync);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int QueryDisplayAttribDelegate(IntPtr display, int attribute, out nint value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryDeviceStringDelegate(IntPtr device, int name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int QueryDevicesDelegate(int maxDevices, [Out] IntPtr[]? devices, out int numDevices);

    private static readonly Lazy<DupNativeFenceFdDelegate?> s_dupNativeFenceFd = new(() => Proc<DupNativeFenceFdDelegate>("eglDupNativeFenceFDANDROID"));
    private static readonly Lazy<QueryDisplayAttribDelegate?> s_queryDisplayAttrib = new(() => Proc<QueryDisplayAttribDelegate>("eglQueryDisplayAttribEXT"));
    private static readonly Lazy<QueryDeviceStringDelegate?> s_queryDeviceString = new(() => Proc<QueryDeviceStringDelegate>("eglQueryDeviceStringEXT"));
    private static readonly Lazy<QueryDevicesDelegate?> s_queryDevices = new(() => Proc<QueryDevicesDelegate>("eglQueryDevicesEXT"));

    /// <summary>eglDupNativeFenceFDANDROID, or -1 when unavailable or failed.</summary>
    public static int DupNativeFenceFd(IntPtr display, IntPtr sync)
        => s_dupNativeFenceFd.Value is { } fn ? fn(display, sync) : EGL_NO_NATIVE_FENCE_FD_ANDROID;

    /// <summary>
    /// The DRM node files (render node first, then primary node) of the device
    /// behind <paramref name="display"/>, via EGL_EXT_device_query. Empty when
    /// the implementation cannot tell.
    /// </summary>
    public static IReadOnlyList<string> GetDisplayDrmNodes(IntPtr display)
    {
        if (!HasClientExtension("EGL_EXT_device_query") || s_queryDisplayAttrib.Value is not { } query)
            return Array.Empty<string>();
        if (query(display, EGL_DEVICE_EXT, out var device) == EGL_FALSE || device == 0)
            return Array.Empty<string>();
        return GetDeviceDrmNodes(device);
    }

    /// <summary>DRM node files of an EGLDeviceEXT (render node first).</summary>
    public static IReadOnlyList<string> GetDeviceDrmNodes(IntPtr device)
    {
        var result = new List<string>(2);
        if (s_queryDeviceString.Value is not { } queryString)
            return result;
        foreach (var name in new[] { EGL_DRM_RENDER_NODE_FILE_EXT, EGL_DRM_DEVICE_FILE_EXT })
        {
            var p = queryString(device, name);
            if (p == IntPtr.Zero)
            {
                eglGetError(); // clear EGL_BAD_PARAMETER for an unsupported name
                continue;
            }
            var s = Marshal.PtrToStringUTF8(p);
            if (!string.IsNullOrEmpty(s)) result.Add(s);
        }
        return result;
    }

    /// <summary>All EGL devices (EGL_EXT_device_enumeration); empty when unsupported.</summary>
    public static IntPtr[] QueryDevices()
    {
        if (s_queryDevices.Value is not { } query)
            return Array.Empty<IntPtr>();
        if (query(0, null, out int count) == EGL_FALSE || count <= 0)
            return Array.Empty<IntPtr>();
        var devices = new IntPtr[count];
        if (query(count, devices, out count) == EGL_FALSE)
            return Array.Empty<IntPtr>();
        return devices.AsSpan(0, count).ToArray();
    }
}

/// <summary>
/// The handful of OpenGL ES entry points the DMA-BUF importer calls itself
/// (Skia owns everything else), resolved through <c>eglGetProcAddress</c> for
/// the current context's client API.
/// </summary>
internal static unsafe class Gles
{
    public const uint GL_TEXTURE_2D = 0x0DE1;
    public const uint GL_TEXTURE_EXTERNAL_OES = 0x8D65;
    public const uint GL_TEXTURE_MIN_FILTER = 0x2801;
    public const uint GL_TEXTURE_MAG_FILTER = 0x2800;
    public const uint GL_TEXTURE_WRAP_S = 0x2802;
    public const uint GL_TEXTURE_WRAP_T = 0x2803;
    public const int GL_LINEAR = 0x2601;
    public const int GL_CLAMP_TO_EDGE = 0x812F;
    public const uint GL_EXTENSIONS = 0x1F03;
    public const uint GL_NO_ERROR = 0;
    public const uint GL_RGBA8 = 0x8058;

    private static IntPtr P(string name) => Egl.eglGetProcAddress(name);

    private static readonly Lazy<IntPtr> s_genTextures = new(() => P("glGenTextures"));
    private static readonly Lazy<IntPtr> s_deleteTextures = new(() => P("glDeleteTextures"));
    private static readonly Lazy<IntPtr> s_bindTexture = new(() => P("glBindTexture"));
    private static readonly Lazy<IntPtr> s_texParameteri = new(() => P("glTexParameteri"));
    private static readonly Lazy<IntPtr> s_getError = new(() => P("glGetError"));
    private static readonly Lazy<IntPtr> s_finish = new(() => P("glFinish"));
    private static readonly Lazy<IntPtr> s_flush = new(() => P("glFlush"));
    private static readonly Lazy<IntPtr> s_getString = new(() => P("glGetString"));
    private static readonly Lazy<IntPtr> s_eglImageTargetTexture2D = new(() => P("glEGLImageTargetTexture2DOES"));

    public static bool HasEglImageTarget => s_eglImageTargetTexture2D.Value != IntPtr.Zero;

    public static uint GenTexture()
    {
        uint id = 0;
        ((delegate* unmanaged<int, uint*, void>)s_genTextures.Value)(1, &id);
        return id;
    }

    public static void DeleteTexture(uint id)
    {
        if (id == 0) return;
        ((delegate* unmanaged<int, uint*, void>)s_deleteTextures.Value)(1, &id);
    }

    public static void BindTexture(uint target, uint id)
        => ((delegate* unmanaged<uint, uint, void>)s_bindTexture.Value)(target, id);

    public static void TexParameteri(uint target, uint name, int value)
        => ((delegate* unmanaged<uint, uint, int, void>)s_texParameteri.Value)(target, name, value);

    public static uint GetError()
        => ((delegate* unmanaged<uint>)s_getError.Value)();

    public static void Finish()
        => ((delegate* unmanaged<void>)s_finish.Value)();

    public static void Flush()
        => ((delegate* unmanaged<void>)s_flush.Value)();

    public static void EglImageTargetTexture2D(uint target, IntPtr image)
        => ((delegate* unmanaged<uint, IntPtr, void>)s_eglImageTargetTexture2D.Value)(target, image);

    /// <summary>GL_EXTENSIONS of the current context (GLES 2/3 single string).</summary>
    public static string? Extensions()
    {
        var p = ((delegate* unmanaged<uint, IntPtr>)s_getString.Value)(GL_EXTENSIONS);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }

    /// <summary>Drains the GL error queue.</summary>
    public static void ClearErrors()
    {
        for (int i = 0; i < 16 && GetError() != GL_NO_ERROR; i++) { }
    }
}
