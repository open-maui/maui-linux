// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Native;

/// <summary>
/// P/Invoke bindings for the slice of GStreamer 1.x we need to drive a
/// playbin → appsink pipeline. Three native libraries:
///   - libgstreamer-1.0.so.0  → core element/bus/state APIs
///   - libgstapp-1.0.so.0     → appsink-specific (set_callbacks, pull_sample, ...)
///   - libgobject-2.0.so.0    → g_object_set / g_signal_connect for property tweaks
///
/// All present on every modern desktop Linux as part of GStreamer + GLib. Plugins
/// (codec support) live in separate packages — see README for distro install
/// commands. The pipeline below uses playbin's auto-decoder selection, so HW
/// decoders (vaapi/nvdec) are picked automatically when their plugin packages
/// are installed; software fallback otherwise.
/// </summary>
public static partial class GStreamerInterop
{
    private const string LibGst = "libgstreamer-1.0.so.0";
    private const string LibGstApp = "libgstapp-1.0.so.0";
    private const string LibGObject = "libgobject-2.0.so.0";

    #region GstState enum

    public enum GstState
    {
        VoidPending = 0,
        Null = 1,
        Ready = 2,
        Paused = 3,
        Playing = 4,
    }

    public enum GstStateChangeReturn
    {
        Failure = 0,
        Success = 1,
        Async = 2,
        NoPreroll = 3,
    }

    public enum GstFlowReturn
    {
        Ok = 0,
        NotLinked = -1,
        Flushing = -2,
        Eos = -3,
        NotNegotiated = -4,
        Error = -5,
        NotSupported = -6,
    }

    [Flags]
    public enum GstMapFlags : uint
    {
        Read = 1,
        Write = 2,
    }

    [Flags]
    public enum GstMessageType : uint
    {
        Eos = 1 << 0,
        Error = 1 << 1,
        Warning = 1 << 2,
        Info = 1 << 3,
        StateChanged = 1 << 5,
        AsyncDone = 1 << 23,
        DurationChanged = 1 << 25,
    }

    public enum GstSeekFlags : uint
    {
        None = 0,
        Flush = 1 << 0,
        KeyUnit = 1 << 2,
        Accurate = 1 << 1,
    }

    public enum GstFormat
    {
        Undefined = 0,
        Default = 1,
        Bytes = 2,
        Time = 3,
        Buffers = 4,
        Percent = 5,
    }

    // 1 second in GST_NSECONDS (GStreamer expresses time in nanoseconds).
    public const long GstSecond = 1_000_000_000L;

    #endregion

    #region Core (libgstreamer-1.0)

    [LibraryImport(LibGst, EntryPoint = "gst_init")]
    public static partial void gst_init(IntPtr argc, IntPtr argv);

    [LibraryImport(LibGst, EntryPoint = "gst_is_initialized")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_is_initialized();

    [LibraryImport(LibGst, EntryPoint = "gst_element_factory_make", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr gst_element_factory_make(string factoryName, string? name);

    // Plugin/feature registry — used for hardware-acceleration tuning. Bumping
    // the rank of a HW decoder factory above the SW alternatives forces playbin
    // (via decodebin) to pick it when negotiating decoders for the stream caps.
    [LibraryImport(LibGst, EntryPoint = "gst_registry_get")]
    public static partial IntPtr gst_registry_get();

    [LibraryImport(LibGst, EntryPoint = "gst_registry_lookup_feature", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr gst_registry_lookup_feature(IntPtr registry, string name);

    [LibraryImport(LibGst, EntryPoint = "gst_plugin_feature_set_rank")]
    public static partial void gst_plugin_feature_set_rank(IntPtr feature, uint rank);

    // Ranks GStreamer recognises. Anything ≥ Primary outranks the standard
    // SW decoders; Marginal keeps the factory available but ranked behind
    // anything else.
    public const uint GST_RANK_NONE = 0;
    public const uint GST_RANK_MARGINAL = 64;
    public const uint GST_RANK_SECONDARY = 128;
    public const uint GST_RANK_PRIMARY = 256;

    [LibraryImport(LibGst, EntryPoint = "gst_element_set_state")]
    public static partial GstStateChangeReturn gst_element_set_state(IntPtr element, GstState state);

    [LibraryImport(LibGst, EntryPoint = "gst_element_get_state")]
    public static partial GstStateChangeReturn gst_element_get_state(IntPtr element, out GstState state, out GstState pending, ulong timeout);

    [LibraryImport(LibGst, EntryPoint = "gst_element_get_bus")]
    public static partial IntPtr gst_element_get_bus(IntPtr element);

    [LibraryImport(LibGst, EntryPoint = "gst_element_query_position")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_element_query_position(IntPtr element, GstFormat format, out long position);

    [LibraryImport(LibGst, EntryPoint = "gst_element_query_duration")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_element_query_duration(IntPtr element, GstFormat format, out long duration);

    [LibraryImport(LibGst, EntryPoint = "gst_element_seek_simple")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_element_seek_simple(IntPtr element, GstFormat format, GstSeekFlags flags, long position);

    [LibraryImport(LibGst, EntryPoint = "gst_object_unref")]
    public static partial void gst_object_unref(IntPtr obj);

    [LibraryImport(LibGst, EntryPoint = "gst_object_ref")]
    public static partial IntPtr gst_object_ref(IntPtr obj);

    [LibraryImport(LibGst, EntryPoint = "gst_caps_from_string", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr gst_caps_from_string(string capsDescription);

    [LibraryImport(LibGst, EntryPoint = "gst_caps_unref")]
    public static partial void gst_caps_unref(IntPtr caps);

    [LibraryImport(LibGst, EntryPoint = "gst_caps_get_structure")]
    public static partial IntPtr gst_caps_get_structure(IntPtr caps, uint index);

    [LibraryImport(LibGst, EntryPoint = "gst_structure_get_int", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_structure_get_int(IntPtr structure, string fieldName, out int value);

    #endregion

    #region Bus messages

    [LibraryImport(LibGst, EntryPoint = "gst_bus_pop_filtered")]
    public static partial IntPtr gst_bus_pop_filtered(IntPtr bus, GstMessageType types);

    [LibraryImport(LibGst, EntryPoint = "gst_bus_timed_pop_filtered")]
    public static partial IntPtr gst_bus_timed_pop_filtered(IntPtr bus, ulong timeout, GstMessageType types);

    [LibraryImport(LibGst, EntryPoint = "gst_message_unref")]
    public static partial void gst_message_unref(IntPtr message);

    /// <summary>Read the GstMessageType field of a GstMessage. On x86_64,
    /// GstMiniObject is 64 bytes (type:8 + refcount:4 + lockstate:4 + flags:4 +
    /// priv_uint:4 + copy:8 + dispose:8 + free:8 + n_qdata:4 + pad:4 + qdata:8),
    /// so the inline GstMessageType field that follows lives at byte offset 64.
    /// An earlier version guessed offset 32 — that lands inside the dispose
    /// function pointer and returns garbage like 0xD982_4070 which the enum
    /// switch can't match.</summary>
    public static GstMessageType GstMessageGetType(IntPtr message)
    {
        const int OFFSET_TYPE = 64;
        return (GstMessageType)(uint)Marshal.ReadInt32(message, OFFSET_TYPE);
    }

    [LibraryImport(LibGst, EntryPoint = "gst_message_parse_error")]
    public static partial void gst_message_parse_error(IntPtr message, out IntPtr error, out IntPtr debugInfo);

    [LibraryImport(LibGst, EntryPoint = "g_error_free")]
    public static partial void g_error_free(IntPtr error);

    [LibraryImport(LibGst, EntryPoint = "g_free")]
    public static partial void g_free(IntPtr ptr);

    /// <summary>Read the message of a GError. GError layout: { GQuark domain; gint code; gchar *message; }.</summary>
    public static string? GErrorGetMessage(IntPtr error)
    {
        if (error == IntPtr.Zero) return null;
        // domain (4 bytes) + padding to 8 (alignment) + code (4 bytes) + padding to 8 + message (8 bytes ptr)
        // On 64-bit: domain at 0, code at 4 (no padding before next field due to int size match), message ptr at 8.
        var msgPtr = Marshal.ReadIntPtr(error, 8);
        return Marshal.PtrToStringUTF8(msgPtr);
    }

    #endregion

    #region GObject properties (g_object_set)

    [DllImport(LibGObject, EntryPoint = "g_object_set", CharSet = CharSet.Ansi)]
    public static extern void g_object_set(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string firstProperty, IntPtr value, IntPtr terminator);

    [DllImport(LibGObject, EntryPoint = "g_object_set", CharSet = CharSet.Ansi)]
    public static extern void g_object_set_string(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string firstProperty, [MarshalAs(UnmanagedType.LPStr)] string value, IntPtr terminator);

    [DllImport(LibGObject, EntryPoint = "g_object_set", CharSet = CharSet.Ansi)]
    public static extern void g_object_set_double(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string firstProperty, double value, IntPtr terminator);

    [DllImport(LibGObject, EntryPoint = "g_object_set", CharSet = CharSet.Ansi)]
    public static extern void g_object_set_bool(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string firstProperty, [MarshalAs(UnmanagedType.Bool)] bool value, IntPtr terminator);

    [DllImport(LibGObject, EntryPoint = "g_object_set", CharSet = CharSet.Ansi)]
    public static extern void g_object_set_int(IntPtr obj, [MarshalAs(UnmanagedType.LPStr)] string firstProperty, int value, IntPtr terminator);

    #endregion

    #region appsink (libgstapp-1.0)

    [StructLayout(LayoutKind.Sequential)]
    public struct GstAppSinkCallbacks
    {
        public IntPtr Eos;            // void (*eos)(GstAppSink *appsink, gpointer user_data);
        public IntPtr NewPreroll;     // GstFlowReturn (*new_preroll)(GstAppSink *appsink, gpointer user_data);
        public IntPtr NewSample;      // GstFlowReturn (*new_sample)(GstAppSink *appsink, gpointer user_data);
        public IntPtr NewEvent;       // GstFlowReturn (*new_event)(GstAppSink *appsink, gpointer user_data);  (since 1.20)
        public IntPtr PropertyNotify; // void (*property_notify)(GstAppSink *appsink, const gchar *property_name, gpointer user_data);  (since 1.24)
        // Padding for ABI stability (4 * sizeof(void*)).
        public IntPtr Reserved0;
        public IntPtr Reserved1;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
    }

    public delegate GstFlowReturn GstAppSinkNewSampleDelegate(IntPtr appsink, IntPtr userData);
    public delegate void GstAppSinkEosDelegate(IntPtr appsink, IntPtr userData);

    [LibraryImport(LibGstApp, EntryPoint = "gst_app_sink_set_callbacks")]
    public static partial void gst_app_sink_set_callbacks(IntPtr appsink, ref GstAppSinkCallbacks callbacks, IntPtr userData, IntPtr notify);

    [LibraryImport(LibGstApp, EntryPoint = "gst_app_sink_set_caps")]
    public static partial void gst_app_sink_set_caps(IntPtr appsink, IntPtr caps);

    [LibraryImport(LibGstApp, EntryPoint = "gst_app_sink_pull_sample")]
    public static partial IntPtr gst_app_sink_pull_sample(IntPtr appsink);

    [LibraryImport(LibGstApp, EntryPoint = "gst_app_sink_try_pull_sample")]
    public static partial IntPtr gst_app_sink_try_pull_sample(IntPtr appsink, ulong timeoutNs);

    #endregion

    #region GstSample / GstBuffer access

    [LibraryImport(LibGst, EntryPoint = "gst_sample_get_buffer")]
    public static partial IntPtr gst_sample_get_buffer(IntPtr sample);

    [LibraryImport(LibGst, EntryPoint = "gst_sample_get_caps")]
    public static partial IntPtr gst_sample_get_caps(IntPtr sample);

    [LibraryImport(LibGst, EntryPoint = "gst_sample_unref")]
    public static partial void gst_sample_unref(IntPtr sample);

    [StructLayout(LayoutKind.Sequential)]
    public struct GstMapInfo
    {
        public IntPtr Memory;           // GstMemory*
        public uint Flags;
        public IntPtr Data;             // pointer to the actual bytes
        public nuint Size;
        public nuint MaxSize;
        public IntPtr UserData0;
        public IntPtr UserData1;
        public IntPtr UserData2;
        public IntPtr UserData3;
    }

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_map")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_buffer_map(IntPtr buffer, out GstMapInfo info, GstMapFlags flags);

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_unmap")]
    public static partial void gst_buffer_unmap(IntPtr buffer, ref GstMapInfo info);

    #endregion

    #region One-time init

    private static readonly object s_initLock = new();
    private static bool s_initialized;

    /// <summary>
    /// Idempotent GStreamer initializer. Safe to call from any thread, but only
    /// runs gst_init once per process (gst_init asserts on re-entry).
    /// </summary>
    public static void EnsureInitialized()
    {
        if (s_initialized) return;
        lock (s_initLock)
        {
            if (s_initialized) return;
            try
            {
                if (!gst_is_initialized())
                    gst_init(IntPtr.Zero, IntPtr.Zero);
            }
            catch (DllNotFoundException)
            {
                throw new InvalidOperationException(
                    "GStreamer is not installed. Install gstreamer1, gstreamer1-plugins-good, " +
                    "gstreamer1-plugins-bad-free, gstreamer1-plugins-ugly-free, gstreamer1-libav " +
                    "(and gstreamer1-vaapi for HW decode) to use MediaElement on Linux.");
            }
            s_initialized = true;
        }
    }

    #endregion
}
