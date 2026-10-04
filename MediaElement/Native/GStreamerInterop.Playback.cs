// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Native;

/// <summary>
/// Bus monitoring, rate seeks, video size and HTTP header bindings used by the
/// playback state machine (MediaOpened / MediaFailed / StateChanged, Speed,
/// MediaWidth / MediaHeight, UriMediaSource.HttpHeaders). Internal so the
/// package's public surface does not grow.
/// </summary>
public static partial class GStreamerInterop
{
    /// <summary>
    /// GstMessageType bits (gstmessage.h). The public <see cref="GstMessageType"/>
    /// enum predates these and only its Eos/Error/Warning/Info members are
    /// correct; these are the values GStreamer uses.
    /// </summary>
    internal static class MessageBits
    {
        public const uint Eos = 1u << 0;
        public const uint Error = 1u << 1;
        public const uint Warning = 1u << 2;
        public const uint Buffering = 1u << 5;
        public const uint StateChanged = 1u << 6;
        public const uint DurationChanged = 1u << 18;
        public const uint AsyncDone = 1u << 21;
    }

    internal enum GstSeekType
    {
        None = 0,
        Set = 1,
        End = 2,
    }

    [LibraryImport(LibGst, EntryPoint = "gst_bus_timed_pop_filtered")]
    internal static partial IntPtr gst_bus_timed_pop_filtered_bits(IntPtr bus, ulong timeout, uint types);

    /// <summary>GST_MESSAGE_TYPE as raw bits (GstMessage.type, after the 64-byte GstMiniObject).</summary>
    internal static uint MessageTypeBits(IntPtr message) => (uint)Marshal.ReadInt32(message, 64);

    /// <summary>GST_MESSAGE_SRC (GstMessage.src: type at 64, timestamp at 72, src at 80).</summary>
    internal static IntPtr MessageSource(IntPtr message) => Marshal.ReadIntPtr(message, 80);

    [LibraryImport(LibGst, EntryPoint = "gst_message_parse_state_changed")]
    internal static partial void gst_message_parse_state_changed(IntPtr message, out GstState oldState, out GstState newState, out GstState pending);

    [LibraryImport(LibGst, EntryPoint = "gst_message_parse_buffering")]
    internal static partial void gst_message_parse_buffering(IntPtr message, out int percent);

    [LibraryImport(LibGst, EntryPoint = "gst_message_parse_warning")]
    internal static partial void gst_message_parse_warning(IntPtr message, out IntPtr error, out IntPtr debugInfo);

    [LibraryImport(LibGst, EntryPoint = "gst_element_seek")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool gst_element_seek(IntPtr element, double rate, GstFormat format, GstSeekFlags flags,
        GstSeekType startType, long start, GstSeekType stopType, long stop);

    [LibraryImport(LibGst, EntryPoint = "gst_pad_get_current_caps")]
    internal static partial IntPtr gst_pad_get_current_caps(IntPtr pad);

    /// <summary>Reads width/height from a caps' first structure.</summary>
    internal static bool TryGetCapsSize(IntPtr caps, out int width, out int height)
    {
        width = height = 0;
        if (caps == IntPtr.Zero) return false;
        var structure = gst_caps_get_structure(caps, 0);
        if (structure == IntPtr.Zero) return false;
        return gst_structure_get_int(structure, "width", out width)
            && gst_structure_get_int(structure, "height", out height)
            && width > 0 && height > 0;
    }

    // ---- signals / GObject properties ------------------------------------

    /// <summary>playbin "source-setup" and GstBin "deep-element-added" both pass (self, element, user_data) except deep-element-added which passes (bin, sub_bin, element, user_data).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SourceSetupCallback(IntPtr playbin, IntPtr source, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DeepElementAddedCallback(IntPtr bin, IntPtr subBin, IntPtr element, IntPtr userData);

    [LibraryImport(LibGObject, EntryPoint = "g_signal_connect_data", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nuint g_signal_connect_data(IntPtr instance, string detailedSignal, IntPtr handler, IntPtr data, IntPtr destroyData, int connectFlags);

    [LibraryImport(LibGObject, EntryPoint = "g_signal_handler_disconnect")]
    internal static partial void g_signal_handler_disconnect(IntPtr instance, nuint handlerId);

    [LibraryImport(LibGObject, EntryPoint = "g_object_class_find_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr g_object_class_find_property(IntPtr objectClass, string propertyName);

    /// <summary>True when the GObject instance has a property of this name (G_OBJECT_GET_CLASS is the instance's first field).</summary>
    internal static bool HasProperty(IntPtr instance, string name)
    {
        if (instance == IntPtr.Zero) return false;
        var klass = Marshal.ReadIntPtr(instance);
        return klass != IntPtr.Zero && g_object_class_find_property(klass, name) != IntPtr.Zero;
    }

    [LibraryImport(LibGst, EntryPoint = "gst_structure_new_empty", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gst_structure_new_empty(string name);

    [LibraryImport(LibGst, EntryPoint = "gst_structure_set_value", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void gst_structure_set_value(IntPtr structure, string fieldName, IntPtr value);

    [LibraryImport(LibGst, EntryPoint = "gst_structure_free")]
    internal static partial void gst_structure_free(IntPtr structure);

    [LibraryImport(LibGObject, EntryPoint = "g_value_init")]
    internal static partial IntPtr g_value_init(IntPtr value, nuint gtype);

    [LibraryImport(LibGObject, EntryPoint = "g_value_set_string", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void g_value_set_string(IntPtr value, string text);

    [LibraryImport(LibGObject, EntryPoint = "g_value_unset")]
    internal static partial void g_value_unset(IntPtr value);

    /// <summary>G_TYPE_STRING (fundamental 16 &lt;&lt; G_TYPE_FUNDAMENTAL_SHIFT).</summary>
    internal const nuint GTypeString = 16 << 2;

    /// <summary>sizeof(GValue) on 64-bit: GType + two 8-byte data slots.</summary>
    internal const int GValueSize = 24;

    /// <summary>
    /// Builds an "extra-headers" GstStructure (souphttpsrc's property type) from
    /// header pairs; the caller frees it with gst_structure_free.
    /// </summary>
    internal static IntPtr BuildHeaderStructure(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var structure = gst_structure_new_empty("extra-headers");
        var value = Marshal.AllocHGlobal(GValueSize);
        try
        {
            foreach (var (name, text) in headers)
            {
                unsafe { new Span<byte>((void*)value, GValueSize).Clear(); }
                g_value_init(value, GTypeString);
                g_value_set_string(value, text);
                gst_structure_set_value(structure, name, value);
                g_value_unset(value);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(value);
        }
        return structure;
    }
}
