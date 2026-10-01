// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Native;

/// <summary>
/// The WebKitGTK (4.1, GTK 3) calls the WPE port has no counterpart for: page snapshots (a cairo
/// image surface) and printing to a PDF file without a dialog. Resolved lazily; each returns
/// null or false when a symbol is missing.
/// </summary>
internal static unsafe class WebKitGtkExtras
{
    private const int SnapshotRegionVisible = 0;
    private const int SnapshotRegionFullDocument = 1;
    private const int SnapshotOptionsNone = 0;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AsyncReadyCallback(IntPtr source, IntPtr result, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PrintFinishedCallback(IntPtr operation, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PrintFailedCallback(IntPtr operation, IntPtr error, IntPtr userData);

    private static readonly List<object> s_inFlight = new();
    private static readonly Dictionary<string, IntPtr> s_symbols = new();

    private static IntPtr Sym(string library, string name)
    {
        lock (s_symbols)
        {
            var key = library + ":" + name;
            if (s_symbols.TryGetValue(key, out var cached))
                return cached;
            IntPtr p = IntPtr.Zero;
            if (NativeLibrary.TryLoad(library, out var handle))
                NativeLibrary.TryGetExport(handle, name, out p);
            s_symbols[key] = p;
            return p;
        }
    }

    private static IntPtr WebKit(string library, string name) => Sym(library, name);
    private static IntPtr Cairo(string name) => Sym("libcairo.so.2", name);
    private static IntPtr Gtk(string name) => Sym("libgtk-3.so.0", name);
    private static IntPtr GObject(string name) => Sym("libgobject-2.0.so.0", name);

    /// <summary>The page as a bitmap: what is in view, or the whole document. Null when it cannot be taken.</summary>
    internal static Task<SKBitmap?> SnapshotAsync(string library, IntPtr webView, bool fullDocument)
    {
        var get = WebKit(library, "webkit_web_view_get_snapshot");
        var finish = WebKit(library, "webkit_web_view_get_snapshot_finish");
        if (get == IntPtr.Zero || finish == IntPtr.Zero || webView == IntPtr.Zero)
            return Task.FromResult<SKBitmap?>(null);

        var tcs = new TaskCompletionSource<SKBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        AsyncReadyCallback? callback = null;
        callback = (source, result, _) =>
        {
            try
            {
                IntPtr error = IntPtr.Zero;
                var surface = ((delegate* unmanaged<IntPtr, IntPtr, IntPtr*, IntPtr>)finish)(source, result, &error);
                if (surface == IntPtr.Zero)
                {
                    DiagnosticLog.Warn("WebKitGtkExtras", $"Snapshot failed: {WebKitContentApi.GErrorMessage(error)}");
                    tcs.TrySetResult(null);
                    return;
                }
                tcs.TrySetResult(CopySurface(surface));
                var destroy = Cairo("cairo_surface_destroy");
                if (destroy != IntPtr.Zero)
                    ((delegate* unmanaged<IntPtr, void>)destroy)(surface);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                lock (s_inFlight) s_inFlight.Remove(callback!);
            }
        };
        lock (s_inFlight) s_inFlight.Add(callback);
        ((delegate* unmanaged<IntPtr, int, int, IntPtr, IntPtr, IntPtr, void>)get)(
            webView,
            fullDocument ? SnapshotRegionFullDocument : SnapshotRegionVisible,
            SnapshotOptionsNone,
            IntPtr.Zero,
            Marshal.GetFunctionPointerForDelegate(callback),
            IntPtr.Zero);
        return tcs.Task;
    }

    /// <summary>A cairo ARGB32 image surface (premultiplied, native byte order: BGRA here) as an SKBitmap.</summary>
    private static SKBitmap? CopySurface(IntPtr surface)
    {
        var flush = Cairo("cairo_surface_flush");
        var getWidth = Cairo("cairo_image_surface_get_width");
        var getHeight = Cairo("cairo_image_surface_get_height");
        var getStride = Cairo("cairo_image_surface_get_stride");
        var getData = Cairo("cairo_image_surface_get_data");
        if (getWidth == IntPtr.Zero || getHeight == IntPtr.Zero || getStride == IntPtr.Zero || getData == IntPtr.Zero)
            return null;
        if (flush != IntPtr.Zero)
            ((delegate* unmanaged<IntPtr, void>)flush)(surface);
        int width = ((delegate* unmanaged<IntPtr, int>)getWidth)(surface);
        int height = ((delegate* unmanaged<IntPtr, int>)getHeight)(surface);
        int stride = ((delegate* unmanaged<IntPtr, int>)getStride)(surface);
        var data = ((delegate* unmanaged<IntPtr, IntPtr>)getData)(surface);
        if (width <= 0 || height <= 0 || data == IntPtr.Zero)
            return null;
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var dst = (byte*)bitmap.GetPixels();
        int row = width * 4;
        for (int y = 0; y < height; y++)
            Buffer.MemoryCopy((byte*)data + (long)y * stride, dst + (long)y * bitmap.RowBytes, row, row);
        bitmap.NotifyPixelsChanged();
        return bitmap;
    }

    /// <summary>
    /// Prints the page to <paramref name="path"/> as a PDF through GTK's "Print to File" backend, with no
    /// dialog. False when printing is unavailable or fails (the caller can fall back to an image).
    /// </summary>
    internal static Task<bool> PrintToPdfAsync(string library, IntPtr webView, string path)
    {
        var opNew = WebKit(library, "webkit_print_operation_new");
        var setSettings = WebKit(library, "webkit_print_operation_set_print_settings");
        var print = WebKit(library, "webkit_print_operation_print");
        var settingsNew = Gtk("gtk_print_settings_new");
        var settingsSet = Gtk("gtk_print_settings_set");
        var setPrinter = Gtk("gtk_print_settings_set_printer");
        var connect = GObject("g_signal_connect_data");
        var unref = GObject("g_object_unref");
        if (opNew == IntPtr.Zero || setSettings == IntPtr.Zero || print == IntPtr.Zero || settingsNew == IntPtr.Zero
            || settingsSet == IntPtr.Zero || connect == IntPtr.Zero || unref == IntPtr.Zero || webView == IntPtr.Zero)
            return Task.FromResult(false);

        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        if (File.Exists(full))
            File.Delete(full);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = ((delegate* unmanaged<IntPtr, IntPtr>)opNew)(webView);
        var settings = ((delegate* unmanaged<IntPtr>)settingsNew)();

        void Set(string key, string value)
        {
            var k = Marshal.StringToCoTaskMemUTF8(key);
            var v = Marshal.StringToCoTaskMemUTF8(value);
            try { ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)settingsSet)(settings, k, v); }
            finally { Marshal.FreeCoTaskMem(k); Marshal.FreeCoTaskMem(v); }
        }

        if (setPrinter != IntPtr.Zero)
        {
            var printer = Marshal.StringToCoTaskMemUTF8("Print to File");
            try { ((delegate* unmanaged<IntPtr, IntPtr, void>)setPrinter)(settings, printer); }
            finally { Marshal.FreeCoTaskMem(printer); }
        }
        Set("output-file-format", "pdf");
        Set("output-uri", new Uri(full).AbsoluteUri);
        ((delegate* unmanaged<IntPtr, IntPtr, void>)setSettings)(operation, settings);

        PrintFinishedCallback? finished = null;
        PrintFailedCallback? failed = null;
        void Done(bool ok)
        {
            if (!tcs.TrySetResult(ok)) return;
            lock (s_inFlight) { s_inFlight.Remove(finished!); s_inFlight.Remove(failed!); }
            ((delegate* unmanaged<IntPtr, void>)unref)(settings);
            ((delegate* unmanaged<IntPtr, void>)unref)(operation);
        }
        finished = (_, _) => Done(File.Exists(full) && new FileInfo(full).Length > 0);
        failed = (_, error, _) =>
        {
            DiagnosticLog.Warn("WebKitGtkExtras", $"Printing to PDF failed: {WebKitContentApi.GErrorMessage(error)}");
            Done(false);
        };
        lock (s_inFlight) { s_inFlight.Add(finished); s_inFlight.Add(failed); }

        var finishedName = Marshal.StringToCoTaskMemUTF8("finished");
        var failedName = Marshal.StringToCoTaskMemUTF8("failed");
        try
        {
            ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, nuint>)connect)(operation, finishedName, Marshal.GetFunctionPointerForDelegate(finished), IntPtr.Zero, IntPtr.Zero, 0);
            ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int, nuint>)connect)(operation, failedName, Marshal.GetFunctionPointerForDelegate(failed), IntPtr.Zero, IntPtr.Zero, 0);
        }
        finally
        {
            Marshal.FreeCoTaskMem(finishedName);
            Marshal.FreeCoTaskMem(failedName);
        }

        ((delegate* unmanaged<IntPtr, void>)print)(operation);
        return tcs.Task;
    }
}
