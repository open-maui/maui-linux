// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.MediaElement.Native;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.MediaElement;

/// <summary>
/// MediaElement.Volume reaches playbin's "volume" property unchanged. A double passed
/// through the variadic g_object_set arrived as ~1e-322, which silenced the audio.
/// Uses a real playbin; returns early where GStreamer is missing.
/// </summary>
public class PlaybinVolumeTests
{
    [DllImport("libgstreamer-1.0.so.0")]
    private static extern IntPtr gst_object_ref_sink(IntPtr obj);

    private static IntPtr MakePlaybin()
    {
        try
        {
            GStreamerInterop.EnsureInitialized();
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }
        var element = GStreamerInterop.gst_element_factory_make("playbin", null);
        return element == IntPtr.Zero ? IntPtr.Zero : gst_object_ref_sink(element);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.6)]
    [InlineData(1.0)]
    public void Volume_is_set_on_playbin_unchanged(double volume)
    {
        var playbin = MakePlaybin();
        if (playbin == IntPtr.Zero)
            return; // GStreamer or gstreamer1-plugins-base not installed
        try
        {
            GStreamerInterop.SetDoubleProperty(playbin, "volume", volume);

            GStreamerInterop.GetDoubleProperty(playbin, "volume").Should().BeApproximately(volume, 1e-9);
        }
        finally
        {
            GStreamerInterop.gst_object_unref(playbin);
        }
    }
}
