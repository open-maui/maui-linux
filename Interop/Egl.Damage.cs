// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// Buffer age (EGL_EXT_buffer_age) and swap-with-damage
/// (EGL_KHR_swap_buffers_with_damage / EGL_EXT_swap_buffers_with_damage):
/// what partial-damage presentation on the GPU target needs.
/// </summary>
internal static partial class Egl
{
    public const int EGL_BUFFER_AGE_EXT = 0x313D;

    [LibraryImport(LibEgl)]
    public static partial int eglQuerySurface(IntPtr display, IntPtr surface, int attribute, out int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int SwapBuffersWithDamageFn(IntPtr display, IntPtr surface, int[] rects, int nRects);

    /// <summary>
    /// eglSwapBuffersWithDamage{KHR,EXT} when the display offers either, else null.
    /// Rects are x, y, width, height quadruples with a BOTTOM-LEFT origin.
    /// </summary>
    public static SwapBuffersWithDamageFn? GetSwapBuffersWithDamage(IntPtr display)
    {
        if (HasDisplayExtension(display, "EGL_KHR_swap_buffers_with_damage"))
            return Proc<SwapBuffersWithDamageFn>("eglSwapBuffersWithDamageKHR");
        if (HasDisplayExtension(display, "EGL_EXT_swap_buffers_with_damage"))
            return Proc<SwapBuffersWithDamageFn>("eglSwapBuffersWithDamageEXT");
        return null;
    }
}
