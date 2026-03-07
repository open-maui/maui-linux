using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Native;

public static class GLibNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GSourceFunc(IntPtr userData);

    private const string Lib = "libglib-2.0.so.0";

    private static readonly List<GSourceFunc> _callbacks = new List<GSourceFunc>();
    private static readonly object _callbackLock = new object();

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_idle_add")]
    private static extern uint g_idle_add_native(GSourceFunc function, IntPtr data);

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_timeout_add")]
    private static extern uint g_timeout_add_native(uint interval, GSourceFunc function, IntPtr data);

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_source_remove")]
    public static extern bool SourceRemove(uint sourceId);

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_get_monotonic_time")]
    public static extern long GetMonotonicTime();

    public static uint IdleAdd(Func<bool> callback)
    {
        GSourceFunc wrapper = null!;
        wrapper = delegate
        {
            bool flag = false;
            try
            {
                flag = callback();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("GLibNative", "Error in idle callback", ex);
            }
            if (!flag)
            {
                lock (_callbackLock)
                {
                    _callbacks.Remove(wrapper);
                }
            }
            return flag;
        };
        lock (_callbackLock)
        {
            _callbacks.Add(wrapper);
        }
        return g_idle_add_native(wrapper, IntPtr.Zero);
    }

    public static uint TimeoutAdd(uint intervalMs, Func<bool> callback)
    {
        GSourceFunc wrapper = null!;
        wrapper = delegate
        {
            bool flag = false;
            try
            {
                flag = callback();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Error("GLibNative", "Error in timeout callback", ex);
            }
            if (!flag)
            {
                lock (_callbackLock)
                {
                    _callbacks.Remove(wrapper);
                }
            }
            return flag;
        };
        lock (_callbackLock)
        {
            _callbacks.Add(wrapper);
        }
        return g_timeout_add_native(intervalMs, wrapper, IntPtr.Zero);
    }

    public static void ClearCallbacks()
    {
        lock (_callbackLock)
        {
            _callbacks.Clear();
        }
    }

    public static uint g_idle_add(GSourceFunc func, IntPtr data)
    {
        return g_idle_add_native(func, data);
    }

    public static uint g_timeout_add(uint intervalMs, GSourceFunc func, IntPtr data)
    {
        return g_timeout_add_native(intervalMs, func, data);
    }

    public static bool g_source_remove(uint tag)
    {
        return SourceRemove(tag);
    }
}
