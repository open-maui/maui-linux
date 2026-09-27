// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.MediaElement.Rendering;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;
using Microsoft.Maui.Platform.Linux.Rendering;
using Xunit;
using static Microsoft.Maui.Platform.Linux.MediaElement.Native.GStreamerInterop;

namespace Microsoft.Maui.Platform.Tests.MediaElement;

public class VideoFrameFormatsTests
{
    [Fact]
    public void AppSinkCaps_WithoutDmaBuf_AreSystemMemoryBgraOnly()
    {
        VideoFrameFormats.BuildAppSinkCaps(false, true, null).Should().Be("video/x-raw,format=BGRA");
    }

    [Fact]
    public void AppSinkCaps_DrmFormat_PrefersDmaBufThenFallsBackToBgra()
    {
        VideoFrameFormats.BuildAppSinkCaps(true, true, null)
            .Should().Be("video/x-raw(memory:DMABuf),format=DMA_DRM; video/x-raw,format=BGRA");
    }

    [Fact]
    public void AppSinkCaps_DrmFormat_ListsImportableFormatsAndModifiers()
    {
        var caps = VideoFrameFormats.BuildAppSinkCaps(true, true, new[]
        {
            (DrmFourcc.NV12, DrmFourcc.ModLinear),
            (DrmFourcc.NV12, 0x0100000000000002UL),
        });
        caps.Should().Be("video/x-raw(memory:DMABuf),format=DMA_DRM,drm-format=(string){ \"NV12\", \"NV12:0x0100000000000002\" }; video/x-raw,format=BGRA");
    }

    [Fact]
    public void AppSinkCaps_Legacy_ListsPixelFormatsWithoutModifiers()
    {
        var caps = VideoFrameFormats.BuildAppSinkCaps(true, false, new[] { (DrmFourcc.NV12, 2UL) });
        caps.Should().StartWith("video/x-raw(memory:DMABuf),format=(string){ NV12, P010_10LE, BGRx, BGRA, RGBx, RGBA }");
        caps.Should().EndWith("; video/x-raw,format=BGRA");
        caps.Should().NotContain("drm-format");
    }

    [Theory]
    [InlineData(1u, 22u, false)]
    [InlineData(1u, 24u, true)]
    [InlineData(1u, 28u, true)]
    [InlineData(2u, 0u, true)]
    public void DrmFormatCaps_NeedGStreamer124(uint major, uint minor, bool expected)
    {
        VideoFrameFormats.SupportsDrmFormatCaps(major, minor).Should().Be(expected);
    }

    [Fact]
    public void DrmFormat_DmaDrm_ParsesFourccAndModifier()
    {
        VideoFrameFormats.TryGetDrmFormat("DMA_DRM", "NV12:0x0100000000000002", out var fourcc, out var modifier).Should().BeTrue();
        fourcc.Should().Be(DrmFourcc.NV12);
        modifier.Should().Be(0x0100000000000002UL);
    }

    [Fact]
    public void DrmFormat_DmaDrmWithoutModifier_IsLinear()
    {
        VideoFrameFormats.TryGetDrmFormat("DMA_DRM", "P010", out var fourcc, out var modifier).Should().BeTrue();
        fourcc.Should().Be(DrmFourcc.P010);
        modifier.Should().Be(DrmFourcc.ModLinear);
    }

    [Theory]
    [InlineData("NV12", DrmFourcc.NV12)]
    [InlineData("P010_10LE", DrmFourcc.P010)]
    [InlineData("BGRA", DrmFourcc.ARGB8888)]
    [InlineData("BGRx", DrmFourcc.XRGB8888)]
    [InlineData("RGBA", DrmFourcc.ABGR8888)]
    [InlineData("RGBx", DrmFourcc.XBGR8888)]
    public void DrmFormat_LegacyNames_MapToDrmFourccWithImplicitModifier(string gstFormat, uint expected)
    {
        VideoFrameFormats.TryGetDrmFormat(gstFormat, null, out var fourcc, out var modifier).Should().BeTrue();
        fourcc.Should().Be(expected);
        modifier.Should().Be(DrmFourcc.ModInvalid);
    }

    [Theory]
    [InlineData("DMA_DRM", null)]
    [InlineData("DMA_DRM", "YUYV")]      // parsed but not a format the importer handles
    [InlineData("DMA_DRM", "NV12:0xZZ")] // bad modifier
    [InlineData("I420", null)]           // three planes: not importable as one image here
    [InlineData(null, null)]
    public void DrmFormat_Unsupported_IsRejected(string? gstFormat, string? drmFormat)
    {
        VideoFrameFormats.TryGetDrmFormat(gstFormat, drmFormat, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("bt709", 1080, (int)DmaBufYuvColorSpace.Rec709, (int)DmaBufYuvRange.Narrow)]
    [InlineData("bt601", 1080, (int)DmaBufYuvColorSpace.Rec601, (int)DmaBufYuvRange.Narrow)]
    [InlineData("bt2020", 2160, (int)DmaBufYuvColorSpace.Rec2020, (int)DmaBufYuvRange.Narrow)]
    [InlineData("bt2100-pq", 2160, (int)DmaBufYuvColorSpace.Rec2020, (int)DmaBufYuvRange.Narrow)]
    [InlineData("1:4:0:0", 480, (int)DmaBufYuvColorSpace.Rec601, (int)DmaBufYuvRange.Full)]   // JPEG-style full range
    [InlineData("2:3:5:1", 720, (int)DmaBufYuvColorSpace.Rec709, (int)DmaBufYuvRange.Narrow)]
    [InlineData(null, 720, (int)DmaBufYuvColorSpace.Rec709, (int)DmaBufYuvRange.Narrow)]      // GStreamer default: HD
    [InlineData(null, 576, (int)DmaBufYuvColorSpace.Rec601, (int)DmaBufYuvRange.Narrow)]      // GStreamer default: SD
    [InlineData("garbage", 480, (int)DmaBufYuvColorSpace.Rec601, (int)DmaBufYuvRange.Narrow)]
    public void Colorimetry_MapsToEglYuvHints(string? colorimetry, int height, int space, int range)
    {
        // (int: the hint enums are internal; xUnit theory methods are public.)
        VideoFrameFormats.ParseColorimetry(colorimetry, height).Should().Be(((DmaBufYuvColorSpace)space, (DmaBufYuvRange)range));
    }

    [Theory]
    [InlineData(null, true, null)]
    [InlineData(null, null, null)]                       // target unknown yet: offer, the first draw decides
    [InlineData(null, false, "raster render target")]
    [InlineData("0", true, "OPENMAUI_VIDEO_ZEROCOPY=0")]
    [InlineData("off", true, "OPENMAUI_VIDEO_ZEROCOPY=off")]
    [InlineData("force", false, null)]
    [InlineData("1", true, null)]
    public void PathSelection_OffersDmaBufOnlyWhenUseful(string? env, bool? gpuTarget, string? expectedReason)
    {
        VideoFrameFormats.ZeroCopyUnavailableReason(env, gpuTarget).Should().Be(expectedReason);
    }

    [Fact]
    public void Seeks_AreAccurateWithoutKeyUnit()
    {
        // KEY_UNIT combined with ACCURATE snaps to the previous keyframe (seconds off).
        SkiaMediaElement.AccurateSeekFlags.Should().HaveFlag(GstSeekFlags.Accurate);
        SkiaMediaElement.AccurateSeekFlags.Should().HaveFlag(GstSeekFlags.Flush);
        SkiaMediaElement.AccurateSeekFlags.HasFlag(GstSeekFlags.KeyUnit).Should().BeFalse();
    }

    [Fact]
    public void AllocationQueryType_MatchesGstQueryAllocation()
    {
        // GST_QUERY_MAKE_TYPE (140, DOWNSTREAM | SERIALIZED), as GStreamer defines it.
        GST_QUERY_ALLOCATION.Should().Be(35846);
    }
}
