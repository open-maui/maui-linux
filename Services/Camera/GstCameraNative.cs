// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services.Camera;

/// <summary>
/// The slice of GStreamer 1.x the camera capture needs (core + appsink). Loaded on first use;
/// a machine without GStreamer has no camera capture (IsCaptureSupported is false).
/// </summary>
internal static partial class GstCameraNative
{
    private const string LibGst = "libgstreamer-1.0.so.0";
    private const string LibGstApp = "libgstapp-1.0.so.0";

    internal const int StateNull = 1;
    internal const int StatePlaying = 4;
    internal const int StateChangeFailure = 0;

    internal const uint MessageEos = 1 << 0;
    internal const uint MessageError = 1 << 1;

    internal const ulong Second = 1_000_000_000UL;

    [LibraryImport(LibGst, EntryPoint = "gst_init_check")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool gst_init_check(IntPtr argc, IntPtr argv, out IntPtr error);

    [LibraryImport(LibGst, EntryPoint = "gst_is_initialized")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool gst_is_initialized();

    [LibraryImport(LibGst, EntryPoint = "gst_element_factory_find", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gst_element_factory_find(string name);

    [LibraryImport(LibGst, EntryPoint = "gst_parse_launch", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gst_parse_launch(string description, out IntPtr error);

    [LibraryImport(LibGst, EntryPoint = "gst_bin_get_by_name", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gst_bin_get_by_name(IntPtr bin, string name);

    [LibraryImport(LibGst, EntryPoint = "gst_element_set_state")]
    internal static partial int gst_element_set_state(IntPtr element, int state);

    [LibraryImport(LibGst, EntryPoint = "gst_element_get_bus")]
    internal static partial IntPtr gst_element_get_bus(IntPtr element);

    [LibraryImport(LibGst, EntryPoint = "gst_element_send_event")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool gst_element_send_event(IntPtr element, IntPtr evt);

    [LibraryImport(LibGst, EntryPoint = "gst_event_new_eos")]
    internal static partial IntPtr gst_event_new_eos();

    [LibraryImport(LibGst, EntryPoint = "gst_bus_timed_pop_filtered")]
    internal static partial IntPtr gst_bus_timed_pop_filtered(IntPtr bus, ulong timeout, uint types);

    [LibraryImport(LibGst, EntryPoint = "gst_message_parse_error")]
    internal static partial void gst_message_parse_error(IntPtr message, out IntPtr error, out IntPtr debug);

    [LibraryImport(LibGst, EntryPoint = "gst_mini_object_unref")]
    internal static partial void gst_mini_object_unref(IntPtr obj);

    [LibraryImport(LibGst, EntryPoint = "gst_object_unref")]
    internal static partial void gst_object_unref(IntPtr obj);

    [LibraryImport(LibGst, EntryPoint = "gst_sample_get_buffer")]
    internal static partial IntPtr gst_sample_get_buffer(IntPtr sample);

    [LibraryImport(LibGst, EntryPoint = "gst_sample_get_caps")]
    internal static partial IntPtr gst_sample_get_caps(IntPtr sample);

    [LibraryImport(LibGst, EntryPoint = "gst_caps_get_structure")]
    internal static partial IntPtr gst_caps_get_structure(IntPtr caps, uint index);

    [LibraryImport(LibGst, EntryPoint = "gst_structure_get_int", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool gst_structure_get_int(IntPtr structure, string field, out int value);

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_map")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool gst_buffer_map(IntPtr buffer, out MapInfo info, uint flags);

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_unmap")]
    internal static partial void gst_buffer_unmap(IntPtr buffer, ref MapInfo info);

    [LibraryImport(LibGstApp, EntryPoint = "gst_app_sink_try_pull_sample")]
    internal static partial IntPtr gst_app_sink_try_pull_sample(IntPtr appsink, ulong timeout);

    [LibraryImport("libglib-2.0.so.0", EntryPoint = "g_error_free")]
    internal static partial void g_error_free(IntPtr error);

    [LibraryImport("libglib-2.0.so.0", EntryPoint = "g_free")]
    internal static partial void g_free(IntPtr ptr);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MapInfo
    {
        public IntPtr Memory;
        public uint Flags;
        public IntPtr Data;
        public nuint Size;
        public nuint MaxSize;
        public IntPtr User0;
        public IntPtr User1;
        public IntPtr User2;
        public IntPtr User3;
        public IntPtr Reserved0;
        public IntPtr Reserved1;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
    }

    /// <summary>The message of a GError ({ GQuark domain; gint code; gchar *message; }).</summary>
    internal static string? ErrorMessage(IntPtr error)
        => error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, 8));

    private static readonly Lazy<bool> s_initialized = new(() =>
    {
        try
        {
            if (gst_is_initialized())
                return true;
            if (gst_init_check(IntPtr.Zero, IntPtr.Zero, out var error))
                return true;
            DiagnosticLog.Warn("Camera", $"GStreamer init failed: {ErrorMessage(error)}");
            if (error != IntPtr.Zero)
                g_error_free(error);
            return false;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            DiagnosticLog.Debug("Camera", $"GStreamer not available: {ex.Message}");
            return false;
        }
    });

    /// <summary>Initialises GStreamer once; false when it is not installed.</summary>
    internal static bool EnsureInitialized() => s_initialized.Value;

    /// <summary>Whether the element factory <paramref name="name"/> is installed.</summary>
    internal static bool HasElement(string name)
    {
        if (!EnsureInitialized())
            return false;
        var factory = gst_element_factory_find(name);
        if (factory == IntPtr.Zero)
            return false;
        gst_object_unref(factory);
        return true;
    }
}
