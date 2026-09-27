// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Maui.Platform.Linux.Rendering;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Rendering;

/// <summary>
/// The pure parts of the MediaElement's zero-copy video path: appsink caps
/// negotiation strings, GStreamer format to DRM fourcc mapping, colorimetry
/// to EGL YUV hints, and the path decision. No native calls here, so all of it
/// is unit-testable.
/// </summary>
internal static class VideoFrameFormats
{
    /// <summary>The system-memory caps the CPU path always accepts (Skia's Bgra8888).</summary>
    public const string SystemMemoryCaps = "video/x-raw,format=BGRA";

    /// <summary>The caps feature decoders use for DMA-BUF output.</summary>
    public const string DmaBufFeature = "memory:DMABuf";

    /// <summary>OPENMAUI_VIDEO_ZEROCOPY: <c>0</c> disables the GPU import, <c>force</c> offers DMA-BUF even when the render target is not known to be GPU.</summary>
    public const string EnvironmentVariable = "OPENMAUI_VIDEO_ZEROCOPY";

    /// <summary>
    /// Legacy DMA-BUF formats (GStreamer before 1.24 put the pixel format in
    /// <c>format</c> and had no modifier) the importer can express.
    /// </summary>
    private static readonly string[] s_legacyDmaBufFormats = { "NV12", "P010_10LE", "BGRx", "BGRA", "RGBx", "RGBA" };

    /// <summary>
    /// True when GStreamer negotiates DMA-BUF with explicit DRM formats and
    /// modifiers (<c>format=DMA_DRM, drm-format=FOURCC:0xMODIFIER</c>), i.e. 1.24+.
    /// </summary>
    public static bool SupportsDrmFormatCaps(uint major, uint minor) => major > 1 || (major == 1 && minor >= 24);

    /// <summary>
    /// The appsink caps. With <paramref name="offerDmaBuf"/>, DMA-BUF caps
    /// come first (preferred) and system-memory BGRA follows, so decoders that
    /// cannot export DMA-BUF (software, NVDEC/CUDA) still negotiate the CPU
    /// path through playsink's converter. <paramref name="importable"/> (the
    /// fourcc/modifier pairs the EGL display can import) restricts the
    /// <c>drm-format</c> list so a decoder only picks a layout EGL accepts;
    /// null or empty leaves it open (the import then validates it).
    /// </summary>
    public static string BuildAppSinkCaps(bool offerDmaBuf, bool drmFormatCaps, IReadOnlyCollection<(uint Fourcc, ulong Modifier)>? importable)
    {
        if (!offerDmaBuf)
            return SystemMemoryCaps;

        var sb = new StringBuilder();
        sb.Append("video/x-raw(").Append(DmaBufFeature).Append(')');
        if (drmFormatCaps)
        {
            sb.Append(",format=DMA_DRM");
            if (importable is { Count: > 0 })
            {
                sb.Append(",drm-format=(string){ ");
                bool first = true;
                foreach (var (fourcc, modifier) in importable)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append('"').Append(DrmFourcc.ToDrmFormatString(fourcc, modifier)).Append('"');
                }
                sb.Append(" }");
            }
        }
        else
        {
            sb.Append(",format=(string){ ").Append(string.Join(", ", s_legacyDmaBufFormats)).Append(" }");
        }
        sb.Append("; ").Append(SystemMemoryCaps);
        return sb.ToString();
    }

    /// <summary>
    /// The DRM fourcc and modifier of a negotiated DMA-BUF frame: from
    /// <paramref name="drmFormat"/> when <paramref name="gstFormat"/> is
    /// <c>DMA_DRM</c> (1.24+), otherwise from the legacy format name with an
    /// implicit modifier (<see cref="DrmFourcc.ModInvalid"/>).
    /// </summary>
    public static bool TryGetDrmFormat(string? gstFormat, string? drmFormat, out uint fourcc, out ulong modifier)
    {
        fourcc = 0;
        modifier = DrmFourcc.ModInvalid;
        if (string.Equals(gstFormat, "DMA_DRM", StringComparison.Ordinal))
            return DrmFourcc.TryParseDrmFormat(drmFormat, out fourcc, out modifier) && DrmFourcc.PlaneCount(fourcc) > 0;

        uint? mapped = gstFormat switch
        {
            // GStreamer names byte order in memory; DRM names a little-endian word.
            "NV12" => DrmFourcc.NV12,
            "P010_10LE" => DrmFourcc.P010,
            "BGRA" => DrmFourcc.ARGB8888,
            "BGRx" => DrmFourcc.XRGB8888,
            "RGBA" => DrmFourcc.ABGR8888,
            "RGBx" => DrmFourcc.XBGR8888,
            _ => null,
        };
        if (mapped is not uint f) return false;
        fourcc = f;
        return true;
    }

    /// <summary>
    /// YUV matrix and range hints from a GStreamer colorimetry string
    /// (<c>"bt709"</c>, <c>"bt601"</c>, <c>"bt2020"</c>, <c>"bt2100-pq"</c>,
    /// or the numeric <c>range:matrix:transfer:primaries</c> form such as
    /// <c>"1:4:0:0"</c>). Unknown or missing values fall back to GStreamer's
    /// own default: BT.709 from 720 lines up, BT.601 below, limited range.
    /// </summary>
    public static (DmaBufYuvColorSpace ColorSpace, DmaBufYuvRange Range) ParseColorimetry(string? colorimetry, int height)
    {
        var defaultSpace = height >= 720 ? DmaBufYuvColorSpace.Rec709 : DmaBufYuvColorSpace.Rec601;
        if (string.IsNullOrWhiteSpace(colorimetry))
            return (defaultSpace, DmaBufYuvRange.Narrow);

        var c = colorimetry.Trim().ToLowerInvariant();
        switch (c)
        {
            case "bt601":
                return (DmaBufYuvColorSpace.Rec601, DmaBufYuvRange.Narrow);
            case "bt709":
            case "smpte240m":
                return (DmaBufYuvColorSpace.Rec709, DmaBufYuvRange.Narrow);
            case "bt2020":
            case "bt2020-10":
            case "bt2100-pq":
            case "bt2100-hlg":
                return (DmaBufYuvColorSpace.Rec2020, DmaBufYuvRange.Narrow);
        }

        var parts = c.Split(':');
        if (parts.Length == 4 && int.TryParse(parts[0], out int range) && int.TryParse(parts[1], out int matrix))
        {
            // GstVideoColorRange: 1 = 0-255, 2 = 16-235. GstVideoColorMatrix:
            // 3 = BT.709, 4 = BT.601, 5 = SMPTE 240M, 6 = BT.2020.
            var r = range == 1 ? DmaBufYuvRange.Full : DmaBufYuvRange.Narrow;
            var m = matrix switch
            {
                3 or 5 => DmaBufYuvColorSpace.Rec709,
                4 => DmaBufYuvColorSpace.Rec601,
                6 => DmaBufYuvColorSpace.Rec2020,
                _ => defaultSpace,
            };
            return (m, r);
        }

        return (defaultSpace, DmaBufYuvRange.Narrow);
    }

    /// <summary>
    /// Why the pipeline should not offer DMA-BUF caps, or null when it should.
    /// <paramref name="gpuTarget"/> is the primary window's render target kind
    /// (null when unknown, e.g. before the first window exists).
    /// </summary>
    public static string? ZeroCopyUnavailableReason(string? environmentValue, bool? gpuTarget)
    {
        var env = environmentValue?.Trim();
        if (env == "0" || string.Equals(env, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(env, "off", StringComparison.OrdinalIgnoreCase))
            return $"{EnvironmentVariable}={env}";
        if (string.Equals(env, "force", StringComparison.OrdinalIgnoreCase))
            return null;
        if (gpuTarget == false)
            return "raster render target";
        return null;
    }
}
