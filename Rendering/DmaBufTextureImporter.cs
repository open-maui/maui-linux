// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Interop;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>DRM fourcc codes and modifiers (drm_fourcc.h) the DMA-BUF importers understand.</summary>
internal static class DrmFourcc
{
    public const uint ARGB8888 = 0x34325241; // AR24
    public const uint XRGB8888 = 0x34325258; // XR24
    public const uint ABGR8888 = 0x34324241; // AB24
    public const uint XBGR8888 = 0x34324258; // XB24
    public const uint NV12 = 0x3231564E;     // NV12: Y plane + interleaved CbCr plane, 4:2:0
    public const uint P010 = 0x30313050;     // P010: 10-bit NV12 layout in 16-bit words

    /// <summary>Linear (untiled) layout.</summary>
    public const ulong ModLinear = 0;

    /// <summary>No explicit modifier: the layout is implied by the producer (legacy import).</summary>
    public const ulong ModInvalid = 0x00ffffffffffffffUL;

    /// <summary>
    /// The fourcc for a four-character code such as <c>"NV12"</c> or
    /// <c>"XR24"</c>; null when <paramref name="code"/> is not four ASCII
    /// characters. A code shorter than four characters is space-padded, as
    /// drm_fourcc.h does (e.g. <c>"R8"</c>).
    /// </summary>
    public static uint? FromString(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 4) return null;
        uint value = 0;
        for (int i = 0; i < 4; i++)
        {
            char c = i < code.Length ? code[i] : ' ';
            if (c < 0x20 || c > 0x7e) return null;
            value |= (uint)c << (8 * i);
        }
        return value;
    }

    /// <summary>The four-character code of <paramref name="fourcc"/> (trailing spaces trimmed).</summary>
    public static string ToCode(uint fourcc)
    {
        Span<char> chars = stackalloc char[4];
        for (int i = 0; i < 4; i++)
        {
            char c = (char)((fourcc >> (8 * i)) & 0xff);
            chars[i] = c is >= ' ' and <= '~' ? c : '?';
        }
        return new string(chars).TrimEnd(' ');
    }

    /// <summary>True for the YUV formats (sampled through GL_TEXTURE_EXTERNAL_OES).</summary>
    public static bool IsYuv(uint fourcc) => fourcc is NV12 or P010;

    /// <summary>Planes a buffer of this format has; 0 for formats the importers do not handle.</summary>
    public static int PlaneCount(uint fourcc) => fourcc switch
    {
        ARGB8888 or XRGB8888 or ABGR8888 or XBGR8888 => 1,
        NV12 or P010 => 2,
        _ => 0,
    };

    /// <summary>How Skia should treat alpha for a frame of this format; null when unsupported.</summary>
    public static SKAlphaType? AlphaType(uint fourcc) => fourcc switch
    {
        ARGB8888 or ABGR8888 => SKAlphaType.Premul,
        XRGB8888 or XBGR8888 => SKAlphaType.Opaque,
        NV12 or P010 => SKAlphaType.Opaque, // YUV has no alpha; the external sampler returns RGB with a = 1
        _ => null,
    };

    /// <summary>
    /// Parses a GStreamer DRM format string (<c>"NV12:0x0100000000000002"</c>,
    /// or <c>"NV12"</c> for linear), as carried by the <c>drm-format</c> field
    /// of <c>video/x-raw(memory:DMABuf),format=DMA_DRM</c> caps (GStreamer 1.24+).
    /// </summary>
    public static bool TryParseDrmFormat(string? value, out uint fourcc, out ulong modifier)
    {
        fourcc = 0;
        modifier = ModLinear;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var s = value.Trim();
        int colon = s.IndexOf(':');
        var code = colon >= 0 ? s[..colon] : s;
        if (FromString(code) is not uint parsed) return false;
        if (colon >= 0)
        {
            var mod = s[(colon + 1)..];
            if (mod.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) mod = mod[2..];
            if (!ulong.TryParse(mod, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out modifier))
                return false;
        }
        fourcc = parsed;
        return true;
    }

    /// <summary>Formats a fourcc + modifier the way GStreamer's <c>drm-format</c> field spells it.</summary>
    public static string ToDrmFormatString(uint fourcc, ulong modifier)
        => modifier == ModLinear
            ? ToCode(fourcc)
            : $"{ToCode(fourcc)}:0x{modifier.ToString("x16", CultureInfo.InvariantCulture)}";
}

/// <summary>One plane of a DMA-BUF: file descriptor, byte offset and row pitch.</summary>
internal readonly record struct DmaBufPlane(int Fd, uint Offset, uint Pitch);

/// <summary>YUV-to-RGB matrix hint for an imported YUV buffer.</summary>
internal enum DmaBufYuvColorSpace
{
    Rec601,
    Rec709,
    Rec2020,
}

/// <summary>Quantisation range hint for an imported YUV buffer.</summary>
internal enum DmaBufYuvRange
{
    /// <summary>Limited/studio range (Y 16-235, C 16-240), the video default.</summary>
    Narrow,
    Full,
}

/// <summary>Layout of one DMA-BUF frame, as EGL_EXT_image_dma_buf_import wants it.</summary>
internal sealed class DmaBufDescriptor
{
    public int Width { get; init; }
    public int Height { get; init; }
    public uint Fourcc { get; init; }
    public ulong Modifier { get; init; } = DrmFourcc.ModInvalid;
    public DmaBufPlane[] Planes { get; init; } = Array.Empty<DmaBufPlane>();
    public DmaBufYuvColorSpace ColorSpace { get; init; } = DmaBufYuvColorSpace.Rec709;
    public DmaBufYuvRange Range { get; init; } = DmaBufYuvRange.Narrow;

    public override string ToString()
        => $"{Width}x{Height} {DrmFourcc.ToCode(Fourcc)} modifier 0x{Modifier:X} planes {Planes.Length}";
}

/// <summary>What a producer needs to know when one of its frames leaves the screen.</summary>
internal readonly struct DmaBufReleaseInfo
{
    public DmaBufReleaseInfo(IntPtr display, bool sampled, bool nativeFence)
    {
        Display = display;
        Sampled = sampled;
        NativeFence = nativeFence;
    }

    /// <summary>The EGL display the frame was imported on (current when <see cref="Sampled"/>).</summary>
    public IntPtr Display { get; }

    /// <summary>
    /// The GPU may still be reading the buffer: the producer must synchronise
    /// (fence or <c>glFinish</c>) before it writes into it again. When set,
    /// the importing context is current during <see cref="DmaBufFrame.Release"/>.
    /// </summary>
    public bool Sampled { get; }

    /// <summary>EGL_ANDROID_native_fence_sync + EGL_KHR_wait_sync are available.</summary>
    public bool NativeFence { get; }
}

/// <summary>
/// A producer-owned DMA-BUF frame (a WPEBuffer, a GStreamer sample, ...) that
/// a <see cref="DmaBufTextureImporter"/> holds while it is on screen.
/// </summary>
internal abstract class DmaBufFrame
{
    /// <summary>The frame's layout; null (with the reason) when it cannot be described.</summary>
    public abstract DmaBufDescriptor? Describe(out string? error);

    /// <summary>
    /// Called once after the EGLImage is created and before the texture is
    /// first sampled: wait (GPU-side where possible) for the producer's
    /// rendering to finish. Default: nothing (implicit sync).
    /// </summary>
    public virtual void WaitForRendering(IntPtr display, bool nativeFence) { }

    /// <summary>Hands the frame back to its producer (see <see cref="DmaBufReleaseInfo.Sampled"/>).</summary>
    public abstract void Release(in DmaBufReleaseInfo info);
}

/// <summary>
/// Imports producer DMA-BUF frames as EGLImage textures on the platform's own
/// EGL display (the one current while the render target draws) and hands them
/// to Skia as borrowed <see cref="GRBackendTexture"/>s. No pixels are read back
/// or copied. Shared by the WebView (<see cref="DmaBufFrameImporter"/>, WPE
/// buffers) and the MediaElement (GStreamer decoder buffers).
/// </summary>
/// <remarks>
/// <para>RGB formats bind as <c>GL_TEXTURE_2D</c> (external when the layout is
/// external-only). YUV formats (NV12, P010) are imported as a single
/// multi-plane EGLImage with the colour-space and range hints and bound to
/// <c>GL_TEXTURE_EXTERNAL_OES</c>: the driver converts to RGB in the sampler,
/// so Skia draws them like any other read-only texture.</para>
/// <para>Lifetime: exactly one frame is held (the one on screen) with its
/// EGLImage and texture, until a newer frame replaces it; the previous frame is
/// then released with <see cref="DmaBufReleaseInfo.Sampled"/> set so the
/// producer can fence it.</para>
/// <para>Threading/contexts: UI thread only. GL objects belong to the context
/// current at import; they are deleted in that context (made current
/// surfaceless if needed), then the previous binding is restored.</para>
/// </remarks>
internal sealed class DmaBufTextureImporter : IDisposable
{
    private static readonly Dictionary<IntPtr, DisplaySupport> s_displays = new();

    private IntPtr _display;
    private IntPtr _context;
    private GRContext? _gr;
    private IntPtr _image;
    private uint _texture;
    private uint _target;
    private int _width;
    private int _height;
    private SKAlphaType _alphaType;
    private DmaBufFrame? _frame;

    /// <summary>What a display offers, probed once per EGL display.</summary>
    internal sealed record DisplaySupport(string? Unsupported, bool Modifiers, bool NativeFence, bool ExternalTextures, string Device);

    /// <summary>True while a frame texture is held and can be drawn.</summary>
    public bool HasFrame => _texture != 0 && _frame != null;

    /// <summary>The frame currently held (on screen), or null.</summary>
    public DmaBufFrame? Frame => _frame;

    /// <summary>True when the held texture is sampled through GL_TEXTURE_EXTERNAL_OES.</summary>
    public bool IsExternalTexture => _target == Gles.GL_TEXTURE_EXTERNAL_OES;

    /// <summary>The Skia context the held frame was imported with (drawn with), or null.</summary>
    public GRContext? Context => _gr;

    /// <summary>True when the frame's GL objects live in the context current now.</summary>
    public bool IsCurrentContext => _context != IntPtr.Zero && Egl.eglGetCurrentContext() == _context;

    /// <summary>
    /// Checks whether zero-copy import can work on the EGL display current on
    /// this thread: DMA-BUF import extensions, GL_OES_EGL_image, and the same
    /// GPU as the producer (<paramref name="producerDevice"/>, a DRM node, when
    /// known). Returns null when supported, else the reason. Cached per display.
    /// </summary>
    public static string? CheckCurrentDisplay(string? producerDevice, out string deviceDescription)
    {
        deviceDescription = "unknown device";
        var display = Egl.eglGetCurrentDisplay();
        if (display == Egl.EGL_NO_DISPLAY || Egl.eglGetCurrentContext() == Egl.EGL_NO_CONTEXT)
            return "no current EGL context";

        var support = GetSupport(display);
        deviceDescription = support.Device;
        if (support.Unsupported != null)
            return support.Unsupported;

        if (string.IsNullOrEmpty(producerDevice))
            return null;

        lock (s_deviceChecks)
        {
            if (!s_deviceChecks.TryGetValue((display, producerDevice), out var mismatch))
            {
                var nodes = Egl.GetDisplayDrmNodes(display);
                mismatch = nodes.Count > 0 && !nodes.Any(n => SameGpu(n, producerDevice))
                    ? $"frames are rendered on {producerDevice} but the window's EGL display is on {string.Join("/", nodes)}"
                    : null;
                s_deviceChecks[(display, producerDevice)] = mismatch;
            }
            return mismatch;
        }
    }

    private static readonly Dictionary<(IntPtr Display, string Device), string?> s_deviceChecks = new();

    /// <summary>
    /// The display capabilities of <paramref name="display"/> (probed once;
    /// needs a context of that display current for the GL extension string).
    /// </summary>
    internal static DisplaySupport GetSupport(IntPtr display)
    {
        lock (s_displays)
        {
            if (!s_displays.TryGetValue(display, out var support))
            {
                support = Probe(display);
                s_displays[display] = support;
            }
            return support;
        }
    }

    private static DisplaySupport Probe(IntPtr display)
    {
        var nodes = Egl.GetDisplayDrmNodes(display);
        string device = nodes.Count > 0 ? nodes[0] : "unknown device";
        bool modifiers = Egl.HasDisplayExtension(display, "EGL_EXT_image_dma_buf_import_modifiers");
        // OPENMAUI_WEBVIEW_RELEASE_SYNC=finish forces a full glFinish before each
        // buffer goes back to its producer instead of a release fence (for A/B
        // checks, or a driver/producer combination that ignores the fence).
        bool forceFinish = string.Equals(Environment.GetEnvironmentVariable("OPENMAUI_WEBVIEW_RELEASE_SYNC"), "finish", StringComparison.OrdinalIgnoreCase);
        bool nativeFence = !forceFinish
            && Egl.HasDisplayExtension(display, "EGL_ANDROID_native_fence_sync")
            && Egl.HasDisplayExtension(display, "EGL_KHR_wait_sync");
        var glExtensions = Gles.Extensions();
        bool external = Egl.HasExtension(glExtensions, "GL_OES_EGL_image_external");

        string? unsupported = null;
        if (!Egl.HasDisplayExtension(display, "EGL_EXT_image_dma_buf_import"))
            unsupported = "EGL_EXT_image_dma_buf_import missing";
        else if (!Gles.HasEglImageTarget || !Egl.HasExtension(glExtensions, "GL_OES_EGL_image"))
            unsupported = "GL_OES_EGL_image missing";

        return new DisplaySupport(unsupported, modifiers, nativeFence, external, device);
    }

    /// <summary>
    /// True when two DRM nodes (render or primary, e.g. /dev/dri/renderD128
    /// and /dev/dri/card1) belong to the same GPU: their sysfs device links
    /// resolve to the same bus device.
    /// </summary>
    internal static bool SameGpu(string nodeA, string nodeB)
    {
        if (string.Equals(nodeA, nodeB, StringComparison.Ordinal))
            return true;
        var a = SysfsDevice(nodeA);
        var b = SysfsDevice(nodeB);
        return a != null && b != null && string.Equals(a, b, StringComparison.Ordinal);
    }

    private static string? SysfsDevice(string node)
    {
        var name = Path.GetFileName(node);
        if (string.IsNullOrEmpty(name)) return null;
        return RealPath($"/sys/class/drm/{name}/device");
    }

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr realpath_native(string path, IntPtr resolved);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void free_native(IntPtr p);

    private static string? RealPath(string path)
    {
        try
        {
            var p = realpath_native(path, IntPtr.Zero);
            if (p == IntPtr.Zero) return null;
            var s = Marshal.PtrToStringUTF8(p);
            free_native(p);
            return s;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// The EGLImage attribute list for <paramref name="d"/>. RGB lists are
    /// exactly what the WebView importer always passed; YUV lists add the
    /// colour-space and range hints. Modifiers are passed per plane only when
    /// explicit and <paramref name="useModifiers"/> (the display supports
    /// EGL_EXT_image_dma_buf_import_modifiers).
    /// </summary>
    internal static nint[] BuildImageAttributes(DmaBufDescriptor d, bool useModifiers)
    {
        bool explicitModifier = d.Modifier != DrmFourcc.ModInvalid;
        var attribs = new List<nint>(8 + d.Planes.Length * 10)
        {
            Egl.EGL_WIDTH, d.Width,
            Egl.EGL_HEIGHT, d.Height,
            Egl.EGL_LINUX_DRM_FOURCC_EXT, (nint)d.Fourcc,
        };
        int planes = Math.Min(d.Planes.Length, 4);
        for (int p = 0; p < planes; p++)
        {
            var plane = d.Planes[p];
            attribs.Add(Egl.EGL_DMA_BUF_PLANE_FD_EXT[p]); attribs.Add(plane.Fd);
            attribs.Add(Egl.EGL_DMA_BUF_PLANE_OFFSET_EXT[p]); attribs.Add((nint)plane.Offset);
            attribs.Add(Egl.EGL_DMA_BUF_PLANE_PITCH_EXT[p]); attribs.Add((nint)plane.Pitch);
            if (explicitModifier && useModifiers)
            {
                attribs.Add(Egl.EGL_DMA_BUF_PLANE_MODIFIER_LO_EXT[p]); attribs.Add((nint)(d.Modifier & 0xffffffffUL));
                attribs.Add(Egl.EGL_DMA_BUF_PLANE_MODIFIER_HI_EXT[p]); attribs.Add((nint)(d.Modifier >> 32));
            }
        }
        if (DrmFourcc.IsYuv(d.Fourcc))
        {
            attribs.Add(Egl.EGL_YUV_COLOR_SPACE_HINT_EXT);
            attribs.Add(d.ColorSpace switch
            {
                DmaBufYuvColorSpace.Rec601 => Egl.EGL_ITU_REC601_EXT,
                DmaBufYuvColorSpace.Rec2020 => Egl.EGL_ITU_REC2020_EXT,
                _ => Egl.EGL_ITU_REC709_EXT,
            });
            attribs.Add(Egl.EGL_SAMPLE_RANGE_HINT_EXT);
            attribs.Add(d.Range == DmaBufYuvRange.Full ? Egl.EGL_YUV_FULL_RANGE_EXT : Egl.EGL_YUV_NARROW_RANGE_EXT);
        }
        attribs.Add(Egl.EGL_IMAGE_PRESERVED_KHR); attribs.Add(Egl.EGL_TRUE);
        attribs.Add(Egl.EGL_NONE);
        return attribs.ToArray();
    }

    /// <summary>
    /// Imports <paramref name="frame"/> as the new on-screen texture in the
    /// current context. On success the importer owns the frame and releases
    /// the previous one (sampled); on failure nothing changes, the caller
    /// still owns <paramref name="frame"/>, and the reason is returned.
    /// </summary>
    public string? Import(DmaBufFrame frame, GRContext gr)
    {
        var display = Egl.eglGetCurrentDisplay();
        var context = Egl.eglGetCurrentContext();
        if (display == Egl.EGL_NO_DISPLAY || context == Egl.EGL_NO_CONTEXT)
            return "no current EGL context";

        DisplaySupport support;
        lock (s_displays)
        {
            if (!s_displays.TryGetValue(display, out support!))
                return "display not probed";
        }

        var d = frame.Describe(out var describeError);
        if (d == null)
            return describeError ?? "frame has no DMA-BUF layout";
        if (DrmFourcc.AlphaType(d.Fourcc) is not SKAlphaType alpha)
            return $"unsupported DRM format {DrmFourcc.ToCode(d.Fourcc)} (0x{d.Fourcc:X8})";
        if (d.Planes.Length == 0 || d.Width <= 0 || d.Height <= 0)
            return $"invalid DMA-BUF layout {d}";
        bool yuv = DrmFourcc.IsYuv(d.Fourcc);
        if (yuv && !support.ExternalTextures)
            return "GL_OES_EGL_image_external missing (needed to sample YUV)";
        if (d.Modifier != DrmFourcc.ModInvalid && d.Modifier != DrmFourcc.ModLinear && !support.Modifiers)
            return $"modifier 0x{d.Modifier:X} needs EGL_EXT_image_dma_buf_import_modifiers";

        var image = Egl.eglCreateImage(display, Egl.EGL_NO_CONTEXT, Egl.EGL_LINUX_DMA_BUF_EXT, IntPtr.Zero,
            BuildImageAttributes(d, support.Modifiers));
        if (image == IntPtr.Zero)
            return $"eglCreateImage(DMA-BUF {d}) failed: {Egl.ErrorName(Egl.eglGetError())}";

        Gles.ClearErrors();
        uint target;
        uint texture;
        if (yuv)
        {
            // YUV is external-only on Mesa (and in general): the sampler does the
            // YUV->RGB conversion using the colour hints above.
            target = Gles.GL_TEXTURE_EXTERNAL_OES;
            texture = BindImage(target, image);
        }
        else
        {
            target = Gles.GL_TEXTURE_2D;
            texture = BindImage(target, image);
            if (texture == 0 && support.ExternalTextures)
            {
                // Some tiled/compressed layouts are external-only (sampled through
                // GL_TEXTURE_EXTERNAL_OES); Skia draws those read-only, which is all we need.
                target = Gles.GL_TEXTURE_EXTERNAL_OES;
                texture = BindImage(target, image);
            }
        }
        // Skia caches GL state (texture bindings): tell it we touched it.
        gr.ResetContext();
        if (texture == 0)
        {
            Egl.eglDestroyImage(display, image);
            return "glEGLImageTargetTexture2DOES rejected the image";
        }

        frame.WaitForRendering(display, support.NativeFence);

        // Swap in the new frame, then retire the old one. Same context: no
        // switching needed; another context: the owner context is used.
        var oldFrame = _frame;
        bool oldSampled = true;
        if (_context != context || _display != display)
        {
            // The old frame was sampled in another window's context: finish
            // there before it goes back to its producer.
            bool hadFrame = oldFrame != null;
            if (!WithOwnerContext(() => { if (hadFrame) Gles.Finish(); DeleteGlObjectsInOwnerContext(); }))
                ForgetGlObjects();
            oldSampled = false;
        }
        else
        {
            if (_texture != 0) Gles.DeleteTexture(_texture);
            if (_image != IntPtr.Zero) Egl.eglDestroyImage(_display, _image);
            gr.ResetContext();
        }

        _display = display;
        _context = context;
        _gr = gr;
        _image = image;
        _texture = texture;
        _target = target;
        _width = d.Width;
        _height = d.Height;
        _alphaType = alpha;
        _frame = frame;

        oldFrame?.Release(new DmaBufReleaseInfo(display, oldSampled, support.NativeFence));
        return null;
    }

    private static uint BindImage(uint target, IntPtr image)
    {
        uint texture = Gles.GenTexture();
        Gles.BindTexture(target, texture);
        Gles.TexParameteri(target, Gles.GL_TEXTURE_MIN_FILTER, Gles.GL_LINEAR);
        Gles.TexParameteri(target, Gles.GL_TEXTURE_MAG_FILTER, Gles.GL_LINEAR);
        Gles.TexParameteri(target, Gles.GL_TEXTURE_WRAP_S, Gles.GL_CLAMP_TO_EDGE);
        Gles.TexParameteri(target, Gles.GL_TEXTURE_WRAP_T, Gles.GL_CLAMP_TO_EDGE);
        Gles.EglImageTargetTexture2D(target, image);
        uint error = Gles.GetError();
        Gles.BindTexture(target, 0);
        if (error != Gles.GL_NO_ERROR)
        {
            Gles.DeleteTexture(texture);
            Gles.ClearErrors();
            return 0;
        }
        return texture;
    }

    /// <summary>
    /// A borrowed-texture image of the current frame for drawing on
    /// <paramref name="gr"/>'s canvas, or null when there is none or the
    /// context differs from the one that imported it. Dispose after drawing.
    /// </summary>
    public SKImage? CreateImage(GRContext gr)
    {
        if (!HasFrame || !ReferenceEquals(gr, _gr) || Egl.eglGetCurrentContext() != _context)
            return null;
        var info = new GRGlTextureInfo(_target, _texture, Gles.GL_RGBA8);
        using var backend = new GRBackendTexture(_width, _height, false, info);
        return SKImage.FromTexture(gr, backend, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888, _alphaType);
    }

    /// <summary>
    /// Drops the held frame: deletes the GL objects in their own context and
    /// releases the frame to its producer (after <c>glFinish</c>, since it was
    /// sampled). With <paramref name="keepFrame"/> the frame is returned
    /// instead of released (ownership passes to the caller).
    /// </summary>
    public DmaBufFrame? Reset(bool keepFrame = false)
    {
        var frame = _frame;
        _frame = null;
        var display = _display;

        bool finished = WithOwnerContext(() =>
        {
            if (frame != null && !keepFrame)
                Gles.Finish();
            DeleteGlObjectsInOwnerContext();
        });
        if (!finished)
            ForgetGlObjects();

        _display = IntPtr.Zero;
        _context = IntPtr.Zero;
        _gr = null;

        if (frame == null) return null;
        if (keepFrame) return frame;
        // Finished above (or the context is gone with everything it sampled).
        frame.Release(new DmaBufReleaseInfo(display, sampled: false, nativeFence: false));
        return null;
    }

    private void DeleteGlObjectsInOwnerContext()
    {
        if (_texture != 0) Gles.DeleteTexture(_texture);
        if (_image != IntPtr.Zero) Egl.eglDestroyImage(_display, _image);
        if (_gr is { Handle: not 0 } gr) gr.ResetContext();
        ForgetGlObjects();
    }

    private void ForgetGlObjects()
    {
        _texture = 0;
        _image = IntPtr.Zero;
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the owning context current (made
    /// current surfaceless when another one is), restoring the previous
    /// binding afterwards. False when the owner is gone (its GRContext was
    /// disposed with its render target, which destroyed the GL objects too).
    /// </summary>
    private bool WithOwnerContext(Action action)
    {
        if (_context == IntPtr.Zero || _display == IntPtr.Zero)
            return false;
        if (_gr is not { Handle: not 0 })
            return false;

        var prevDisplay = Egl.eglGetCurrentDisplay();
        var prevContext = Egl.eglGetCurrentContext();
        if (prevContext == _context)
        {
            action();
            return true;
        }

        var prevDraw = Egl.eglGetCurrentSurface(Egl.EGL_DRAW);
        var prevRead = Egl.eglGetCurrentSurface(Egl.EGL_READ);
        if (Egl.eglMakeCurrent(_display, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, _context) == Egl.EGL_FALSE)
            return false;
        try
        {
            action();
        }
        finally
        {
            if (prevContext != Egl.EGL_NO_CONTEXT)
                Egl.eglMakeCurrent(prevDisplay, prevDraw, prevRead, prevContext);
            else
                Egl.eglMakeCurrent(_display, Egl.EGL_NO_SURFACE, Egl.EGL_NO_SURFACE, Egl.EGL_NO_CONTEXT);
        }
        return true;
    }

    public void Dispose() => Reset();
}
