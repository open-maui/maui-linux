// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Interop;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.Rendering;

public class DmaBufTextureImporterTests
{
    [Theory]
    [InlineData("NV12", 0x3231564Eu)]
    [InlineData("P010", 0x30313050u)]
    [InlineData("XR24", 0x34325258u)]
    [InlineData("AR24", 0x34325241u)]
    [InlineData("AB24", 0x34324241u)]
    [InlineData("XB24", 0x34324258u)]
    public void Fourcc_RoundTripsFourCharacterCodes(string code, uint fourcc)
    {
        DrmFourcc.FromString(code).Should().Be(fourcc);
        DrmFourcc.ToCode(fourcc).Should().Be(code);
    }

    [Fact]
    public void Fourcc_RejectsLongOrEmptyCodes()
    {
        DrmFourcc.FromString("NV12X").Should().BeNull();
        DrmFourcc.FromString("").Should().BeNull();
        DrmFourcc.FromString(null).Should().BeNull();
    }

    [Theory]
    [InlineData("NV12:0x0100000000000002", DrmFourcc.NV12, 0x0100000000000002UL)]
    [InlineData("NV12", DrmFourcc.NV12, DrmFourcc.ModLinear)]
    [InlineData(" XR24:0x00ffffffffffffff ", DrmFourcc.XRGB8888, DrmFourcc.ModInvalid)]
    public void DrmFormatString_Parses(string value, uint fourcc, ulong modifier)
    {
        DrmFourcc.TryParseDrmFormat(value, out var f, out var m).Should().BeTrue();
        f.Should().Be(fourcc);
        m.Should().Be(modifier);
    }

    [Fact]
    public void DrmFormatString_FormatsLikeGStreamer()
    {
        DrmFourcc.ToDrmFormatString(DrmFourcc.NV12, 0x0100000000000002UL).Should().Be("NV12:0x0100000000000002");
        DrmFourcc.ToDrmFormatString(DrmFourcc.NV12, DrmFourcc.ModLinear).Should().Be("NV12");
    }

    [Theory]
    [InlineData(DrmFourcc.NV12, 2, true, SKAlphaType.Opaque)]
    [InlineData(DrmFourcc.P010, 2, true, SKAlphaType.Opaque)]
    [InlineData(DrmFourcc.ARGB8888, 1, false, SKAlphaType.Premul)]
    [InlineData(DrmFourcc.XRGB8888, 1, false, SKAlphaType.Opaque)]
    public void Formats_PlaneCountYuvAndAlpha(uint fourcc, int planes, bool yuv, SKAlphaType alpha)
    {
        DrmFourcc.PlaneCount(fourcc).Should().Be(planes);
        DrmFourcc.IsYuv(fourcc).Should().Be(yuv);
        DrmFourcc.AlphaType(fourcc).Should().Be(alpha);
    }

    [Fact]
    public void Attributes_Rgb_MatchTheWebViewImportList()
    {
        var d = new DmaBufDescriptor
        {
            Width = 800,
            Height = 600,
            Fourcc = DrmFourcc.ARGB8888,
            Modifier = 0x0100000000000001UL,
            Planes = new[] { new DmaBufPlane(42, 0, 3200) },
        };
        DmaBufTextureImporter.BuildImageAttributes(d, useModifiers: true).Should().Equal(
            Egl.EGL_WIDTH, 800,
            Egl.EGL_HEIGHT, 600,
            Egl.EGL_LINUX_DRM_FOURCC_EXT, (nint)DrmFourcc.ARGB8888,
            Egl.EGL_DMA_BUF_PLANE_FD_EXT[0], 42,
            Egl.EGL_DMA_BUF_PLANE_OFFSET_EXT[0], 0,
            Egl.EGL_DMA_BUF_PLANE_PITCH_EXT[0], 3200,
            Egl.EGL_DMA_BUF_PLANE_MODIFIER_LO_EXT[0], 1,
            Egl.EGL_DMA_BUF_PLANE_MODIFIER_HI_EXT[0], 0x01000000,
            Egl.EGL_IMAGE_PRESERVED_KHR, Egl.EGL_TRUE,
            Egl.EGL_NONE);
    }

    [Fact]
    public void Attributes_Nv12_TwoPlanesWithModifierAndColourHints()
    {
        var d = new DmaBufDescriptor
        {
            Width = 1920,
            Height = 1080,
            Fourcc = DrmFourcc.NV12,
            Modifier = 0x0100000000000002UL,
            Planes = new[] { new DmaBufPlane(7, 0, 2048), new DmaBufPlane(7, 2048 * 1088, 2048) },
            ColorSpace = DmaBufYuvColorSpace.Rec601,
            Range = DmaBufYuvRange.Full,
        };
        var a = DmaBufTextureImporter.BuildImageAttributes(d, useModifiers: true);
        Value(a, Egl.EGL_DMA_BUF_PLANE_FD_EXT[1]).Should().Be(7);
        Value(a, Egl.EGL_DMA_BUF_PLANE_OFFSET_EXT[1]).Should().Be(2048 * 1088);
        Value(a, Egl.EGL_DMA_BUF_PLANE_PITCH_EXT[1]).Should().Be(2048);
        Value(a, Egl.EGL_DMA_BUF_PLANE_MODIFIER_LO_EXT[1]).Should().Be(2);
        Value(a, Egl.EGL_DMA_BUF_PLANE_MODIFIER_HI_EXT[1]).Should().Be(0x01000000);
        Value(a, Egl.EGL_YUV_COLOR_SPACE_HINT_EXT).Should().Be(Egl.EGL_ITU_REC601_EXT);
        Value(a, Egl.EGL_SAMPLE_RANGE_HINT_EXT).Should().Be(Egl.EGL_YUV_FULL_RANGE_EXT);
        a[^1].Should().Be(Egl.EGL_NONE);
    }

    [Fact]
    public void Attributes_ImplicitModifier_OmitsModifierAttributes()
    {
        var d = new DmaBufDescriptor
        {
            Width = 64,
            Height = 64,
            Fourcc = DrmFourcc.NV12,
            Modifier = DrmFourcc.ModInvalid,
            Planes = new[] { new DmaBufPlane(3, 0, 64), new DmaBufPlane(3, 4096, 64) },
        };
        var a = DmaBufTextureImporter.BuildImageAttributes(d, useModifiers: true);
        a.Should().NotContain(Egl.EGL_DMA_BUF_PLANE_MODIFIER_LO_EXT[0]);
        Value(a, Egl.EGL_YUV_COLOR_SPACE_HINT_EXT).Should().Be(Egl.EGL_ITU_REC709_EXT);
        Value(a, Egl.EGL_SAMPLE_RANGE_HINT_EXT).Should().Be(Egl.EGL_YUV_NARROW_RANGE_EXT);
    }

    private static nint Value(nint[] attribs, int key)
    {
        for (int i = 0; i + 1 < attribs.Length; i += 2)
            if (attribs[i] == key) return attribs[i + 1];
        throw new KeyNotFoundException($"0x{key:X} not in the attribute list");
    }
}
