// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Native;

/// <summary>
/// GStreamer bindings for the zero-copy video path: caps features, buffer
/// memory/DMA-BUF access (libgstallocators), GstVideoMeta (libgstvideo), the
/// appsink allocation-query probe.
/// </summary>
public static partial class GStreamerInterop
{
    private const string LibGstVideo = "libgstvideo-1.0.so.0";
    private const string LibGstAllocators = "libgstallocators-1.0.so.0";

    [LibraryImport(LibGst, EntryPoint = "gst_version")]
    public static partial void gst_version(out uint major, out uint minor, out uint micro, out uint nano);

    [LibraryImport(LibGst, EntryPoint = "gst_caps_get_features")]
    public static partial IntPtr gst_caps_get_features(IntPtr caps, uint index);

    [LibraryImport(LibGst, EntryPoint = "gst_caps_features_contains", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_caps_features_contains(IntPtr features, string feature);

    [LibraryImport(LibGst, EntryPoint = "gst_structure_get_string", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr gst_structure_get_string(IntPtr structure, string fieldName);

    /// <summary>A string field of a caps structure (transfer none), or null.</summary>
    public static string? StructureGetString(IntPtr structure, string fieldName)
    {
        var p = gst_structure_get_string(structure, fieldName);
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    }

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_n_memory")]
    public static partial uint gst_buffer_n_memory(IntPtr buffer);

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_peek_memory")]
    public static partial IntPtr gst_buffer_peek_memory(IntPtr buffer, uint index);

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_find_memory")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_buffer_find_memory(IntPtr buffer, nuint offset, nuint size, out uint index, out uint length, out nuint skip);

    [LibraryImport(LibGst, EntryPoint = "gst_memory_get_sizes")]
    public static partial nuint gst_memory_get_sizes(IntPtr memory, out nuint offset, out nuint maxSize);

    [LibraryImport(LibGst, EntryPoint = "gst_buffer_get_meta")]
    public static partial IntPtr gst_buffer_get_meta(IntPtr buffer, nuint api);

    [LibraryImport(LibGstVideo, EntryPoint = "gst_video_meta_api_get_type")]
    public static partial nuint gst_video_meta_api_get_type();

    [LibraryImport(LibGstAllocators, EntryPoint = "gst_is_dmabuf_memory")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_is_dmabuf_memory(IntPtr memory);

    [LibraryImport(LibGstAllocators, EntryPoint = "gst_dmabuf_memory_get_fd")]
    public static partial int gst_dmabuf_memory_get_fd(IntPtr memory);

    /// <summary>
    /// The fields of a GstVideoMeta this code reads. Layout on 64-bit:
    /// GstMeta { flags (4) + pad (4), info* (8) } = 16; buffer* @16; flags @24;
    /// format @28; id @32; width @36; height @40; n_planes @44;
    /// gsize offset[4] @48; gint stride[4] @80.
    /// </summary>
    public readonly record struct VideoMetaLayout(int Width, int Height, int Planes, ulong[] Offsets, int[] Strides);

    public static VideoMetaLayout ReadVideoMeta(IntPtr meta)
    {
        const int OffsetWidth = 36, OffsetHeight = 40, OffsetPlanes = 44, OffsetOffsets = 48, OffsetStrides = 80;
        var offsets = new ulong[4];
        var strides = new int[4];
        for (int i = 0; i < 4; i++)
        {
            offsets[i] = (ulong)Marshal.ReadInt64(meta, OffsetOffsets + i * 8);
            strides[i] = Marshal.ReadInt32(meta, OffsetStrides + i * 4);
        }
        return new VideoMetaLayout(
            Marshal.ReadInt32(meta, OffsetWidth),
            Marshal.ReadInt32(meta, OffsetHeight),
            Marshal.ReadInt32(meta, OffsetPlanes),
            offsets,
            strides);
    }

    #region Pads, probes, queries, events

    [Flags]
    public enum GstPadProbeType : uint
    {
        QueryDownstream = 1 << 9,
    }

    public enum GstPadProbeReturn
    {
        Drop = 0,
        Ok = 1,
        Remove = 2,
        Pass = 3,
        Handled = 4,
    }

    /// <summary>GST_QUERY_ALLOCATION = GST_QUERY_MAKE_TYPE (140, DOWNSTREAM | SERIALIZED) = 0x8C06.</summary>
    public const int GST_QUERY_ALLOCATION = (140 << 8) | (1 << 1) | (1 << 2);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate GstPadProbeReturn GstPadProbeCallback(IntPtr pad, IntPtr info, IntPtr userData);

    [LibraryImport(LibGst, EntryPoint = "gst_element_get_static_pad", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr gst_element_get_static_pad(IntPtr element, string name);

    [LibraryImport(LibGst, EntryPoint = "gst_pad_add_probe")]
    public static partial nuint gst_pad_add_probe(IntPtr pad, GstPadProbeType mask, IntPtr callback, IntPtr userData, IntPtr destroyData);

    [LibraryImport(LibGst, EntryPoint = "gst_pad_remove_probe")]
    public static partial void gst_pad_remove_probe(IntPtr pad, nuint id);

    [LibraryImport(LibGst, EntryPoint = "gst_pad_probe_info_get_query")]
    public static partial IntPtr gst_pad_probe_info_get_query(IntPtr info);

    [LibraryImport(LibGst, EntryPoint = "gst_query_add_allocation_meta")]
    public static partial void gst_query_add_allocation_meta(IntPtr query, nuint api, IntPtr parameters);

    [LibraryImport(LibGst, EntryPoint = "gst_query_find_allocation_meta")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool gst_query_find_allocation_meta(IntPtr query, nuint api, out uint index);

    /// <summary>GST_QUERY_TYPE: the GstQueryType follows the 64-byte GstMiniObject (see <see cref="GstMessageGetType"/>).</summary>
    public static int GstQueryGetType(IntPtr query) => Marshal.ReadInt32(query, 64);

    #endregion
}
