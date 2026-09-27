// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// EGL pieces the video (MediaElement) DMA-BUF path needs on top of
/// <c>Egl.DmaBuf.cs</c>: YUV import hints (EGL_EXT_image_dma_buf_import),
/// the importable format/modifier query
/// (EGL_EXT_image_dma_buf_import_modifiers) and EGL 1.5 fence syncs used to
/// know when the GPU has finished sampling a decoder buffer.
/// </summary>
internal static partial class Egl
{
    // EGL_EXT_image_dma_buf_import: YUV colour hints.
    public const int EGL_YUV_COLOR_SPACE_HINT_EXT = 0x327B;
    public const int EGL_SAMPLE_RANGE_HINT_EXT = 0x327C;
    public const int EGL_ITU_REC601_EXT = 0x327F;
    public const int EGL_ITU_REC709_EXT = 0x3280;
    public const int EGL_ITU_REC2020_EXT = 0x3281;
    public const int EGL_YUV_FULL_RANGE_EXT = 0x3282;
    public const int EGL_YUV_NARROW_RANGE_EXT = 0x3283;

    // EGL 1.5 core fence syncs.
    public const int EGL_SYNC_FENCE = 0x30F9;
    public const int EGL_SYNC_FLUSH_COMMANDS_BIT = 0x0001;
    public const int EGL_CONDITION_SATISFIED = 0x30F6;
    public const int EGL_TIMEOUT_EXPIRED = 0x30F5;

    [LibraryImport("libEGL.so.1")]
    public static partial int eglClientWaitSync(IntPtr display, IntPtr sync, int flags, ulong timeoutNs);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int QueryDmaBufModifiersDelegate(IntPtr display, int format, int maxModifiers,
        [Out] ulong[]? modifiers, [Out] int[]? externalOnly, out int numModifiers);

    private static readonly Lazy<QueryDmaBufModifiersDelegate?> s_queryDmaBufModifiers =
        new(() => eglGetProcAddress("eglQueryDmaBufModifiersEXT") is var p && p != IntPtr.Zero
            ? Marshal.GetDelegateForFunctionPointer<QueryDmaBufModifiersDelegate>(p)
            : null);

    /// <summary>
    /// The modifiers <paramref name="display"/> can import for DRM format
    /// <paramref name="fourcc"/> (eglQueryDmaBufModifiersEXT), each with its
    /// external-only flag. Null when the query is unavailable; empty when the
    /// format is not importable with explicit modifiers.
    /// </summary>
    public static IReadOnlyList<(ulong Modifier, bool ExternalOnly)>? QueryDmaBufModifiers(IntPtr display, uint fourcc)
    {
        if (display == EGL_NO_DISPLAY
            || !HasDisplayExtension(display, "EGL_EXT_image_dma_buf_import_modifiers")
            || s_queryDmaBufModifiers.Value is not { } query)
            return null;

        if (query(display, unchecked((int)fourcc), 0, null, null, out int count) == EGL_FALSE)
        {
            eglGetError();
            return Array.Empty<(ulong, bool)>();
        }
        if (count <= 0)
            return Array.Empty<(ulong, bool)>();

        var modifiers = new ulong[count];
        var external = new int[count];
        if (query(display, unchecked((int)fourcc), count, modifiers, external, out count) == EGL_FALSE)
        {
            eglGetError();
            return Array.Empty<(ulong, bool)>();
        }

        var result = new (ulong, bool)[count];
        for (int i = 0; i < count; i++)
            result[i] = (modifiers[i], external[i] != 0);
        return result;
    }
}
