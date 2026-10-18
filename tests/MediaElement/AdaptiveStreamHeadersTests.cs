// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.MediaElement.Native;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.MediaElement;

/// <summary>
/// UriMediaSource.HttpHeaders on the HTTP sources an adaptive demuxer adds inside playbin
/// (deep-element-added): which elements take them, and what they end up sending. Uses real
/// GStreamer elements, no network; returns early where GStreamer or souphttpsrc is missing.
/// </summary>
public class AdaptiveStreamHeadersTests
{
    private const string LibGObject = "libgobject-2.0.so.0";
    private const string LibGst = "libgstreamer-1.0.so.0";

    [DllImport(LibGObject, EntryPoint = "g_object_get")]
    private static extern void g_object_get_ptr(IntPtr obj, [MarshalAs(UnmanagedType.LPUTF8Str)] string property, out IntPtr value, IntPtr terminator);

    [DllImport(LibGObject)]
    private static extern void g_free(IntPtr mem);

    [DllImport(LibGst)]
    private static extern IntPtr gst_object_ref_sink(IntPtr obj);

    [DllImport(LibGst)]
    private static extern IntPtr gst_structure_get_string(IntPtr structure, [MarshalAs(UnmanagedType.LPUTF8Str)] string field);

    [DllImport(LibGst)]
    private static extern int gst_structure_n_fields(IntPtr structure);

    private static IntPtr MakeElement(string factory)
    {
        try
        {
            GStreamerInterop.EnsureInitialized();
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }
        var element = GStreamerInterop.gst_element_factory_make(factory, null);
        return element == IntPtr.Zero ? IntPtr.Zero : gst_object_ref_sink(element);
    }

    private static readonly KeyValuePair<string, string>[] Headers =
    {
        new("Authorization", "Bearer t0k"),
        new("X-Custom", "42"),
        new("User-Agent", "Player/2"),
    };

    [Fact]
    public void A_segment_source_takes_the_headers_and_other_elements_do_not()
    {
        var soup = MakeElement("souphttpsrc");
        var queue = MakeElement("queue");
        try
        {
            if (soup == IntPtr.Zero || queue == IntPtr.Zero)
                return; // GStreamer or gstreamer1-plugins-good not installed

            SkiaMediaElement.IsHttpHeaderTarget(soup, Headers).Should().BeTrue("souphttpsrc is what hlsdemux and dashdemux fetch segments with");
            SkiaMediaElement.IsHttpHeaderTarget(queue, Headers).Should().BeFalse("only elements with extra-headers take request headers");
            SkiaMediaElement.IsHttpHeaderTarget(soup, Array.Empty<KeyValuePair<string, string>>()).Should().BeFalse("no headers, nothing to set");
            SkiaMediaElement.IsHttpHeaderTarget(IntPtr.Zero, Headers).Should().BeFalse();
        }
        finally
        {
            if (soup != IntPtr.Zero) GStreamerInterop.gst_object_unref(soup);
            if (queue != IntPtr.Zero) GStreamerInterop.gst_object_unref(queue);
        }
    }

    [Fact]
    public void A_segment_source_sends_the_headers_with_User_Agent_through_its_property()
    {
        var soup = MakeElement("souphttpsrc");
        if (soup == IntPtr.Zero)
            return;
        try
        {
            SkiaMediaElement.ApplyHttpHeaders(soup, Headers);

            g_object_get_ptr(soup, "extra-headers", out var structure, IntPtr.Zero);
            structure.Should().NotBe(IntPtr.Zero);
            try
            {
                Marshal.PtrToStringUTF8(gst_structure_get_string(structure, "Authorization")).Should().Be("Bearer t0k");
                Marshal.PtrToStringUTF8(gst_structure_get_string(structure, "X-Custom")).Should().Be("42");
                gst_structure_n_fields(structure).Should().Be(2, "User-Agent goes through the user-agent property, not as an extra header");
            }
            finally
            {
                GStreamerInterop.gst_structure_free(structure);
            }

            g_object_get_ptr(soup, "user-agent", out var agent, IntPtr.Zero);
            try
            {
                Marshal.PtrToStringUTF8(agent).Should().Be("Player/2");
            }
            finally
            {
                g_free(agent);
            }
        }
        finally
        {
            GStreamerInterop.gst_object_unref(soup);
        }
    }

    [Theory]
    [InlineData("https://media.example.com/live/master.m3u8", "https://media.example.com/live/seg1.ts", true)]
    [InlineData("https://media.example.com/live/master.m3u8", "https://MEDIA.example.com/other/seg1.ts", true)]
    [InlineData("https://media.example.com/live/master.m3u8", "https://cdn.example.net/seg1.ts", false)]
    [InlineData("https://media.example.com/live/master.m3u8", "https://media.example.com:8443/seg1.ts", false)]
    [InlineData("https://media.example.com/live/master.m3u8", "http://media.example.com/seg1.ts", false)]
    [InlineData("https://media.example.com/live/master.m3u8", null, false)]
    [InlineData(null, "https://media.example.com/seg1.ts", false)]
    public void Headers_go_only_to_the_media_sources_host_and_port(string? source, string? location, bool shares)
    {
        SkiaMediaElement.SharesOrigin(source, location).Should().Be(shares);
    }

    [Fact]
    public void A_segment_source_pointed_at_another_host_drops_the_headers_and_gets_its_user_agent_back()
    {
        var soup = MakeElement("souphttpsrc");
        if (soup == IntPtr.Zero)
            return;
        try
        {
            const string Source = "https://media.example.com/live/master.m3u8";
            var original = SkiaMediaElement.ReadString(soup, "user-agent");

            GStreamerInterop.g_object_set_string(soup, "location", "https://media.example.com/live/seg1.ts", IntPtr.Zero);
            SkiaMediaElement.UpdateHeadersForLocation(soup, Source, Headers, original);
            g_object_get_ptr(soup, "extra-headers", out var structure, IntPtr.Zero);
            structure.Should().NotBe(IntPtr.Zero, "a segment on the source's host gets the headers");
            GStreamerInterop.gst_structure_free(structure);
            SkiaMediaElement.ReadString(soup, "user-agent").Should().Be("Player/2");

            GStreamerInterop.g_object_set_string(soup, "location", "https://cdn.example.net/seg2.ts", IntPtr.Zero);
            SkiaMediaElement.UpdateHeadersForLocation(soup, Source, Headers, original);
            g_object_get_ptr(soup, "extra-headers", out structure, IntPtr.Zero);
            structure.Should().Be(IntPtr.Zero, "a segment on another host gets no Authorization or other app header");
            SkiaMediaElement.ReadString(soup, "user-agent").Should().Be(original);
        }
        finally
        {
            GStreamerInterop.gst_object_unref(soup);
        }
    }
}
