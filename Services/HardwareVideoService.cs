// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Supported hardware video acceleration APIs.
/// </summary>
public enum VideoAccelerationApi
{
    /// <summary>
    /// Automatically select the best available API.
    /// </summary>
    Auto,

    /// <summary>
    /// VA-API (Video Acceleration API) - Intel, AMD, and some NVIDIA.
    /// </summary>
    VaApi,

    /// <summary>
    /// VDPAU (Video Decode and Presentation API for Unix) - NVIDIA.
    /// </summary>
    Vdpau,

    /// <summary>
    /// Software decoding fallback.
    /// </summary>
    Software
}

/// <summary>
/// Video codec profiles supported by hardware acceleration.
/// </summary>
public enum VideoProfile
{
    H264Baseline,
    H264Main,
    H264High,
    H265Main,
    H265Main10,
    Vp8,
    Vp9Profile0,
    Vp9Profile2,
    Av1Main
}

/// <summary>
/// Information about a decoded video frame.
/// </summary>
public class VideoFrame : IDisposable
{
    public int Width { get; init; }
    public int Height { get; init; }
    public IntPtr DataY { get; init; }
    public IntPtr DataU { get; init; }
    public IntPtr DataV { get; init; }
    public int StrideY { get; init; }
    public int StrideU { get; init; }
    public int StrideV { get; init; }
    public long Timestamp { get; init; }
    public bool IsKeyFrame { get; init; }

    private bool _disposed;
    private Action? _releaseCallback;

    internal void SetReleaseCallback(Action callback) => _releaseCallback = callback;

    public void Dispose()
    {
        if (!_disposed)
        {
            _releaseCallback?.Invoke();
            _disposed = true;
        }
    }
}

/// <summary>
/// Hardware-accelerated video decoding service using VA-API or VDPAU.
/// Provides efficient video decode for media playback on Linux.
/// </summary>
public class HardwareVideoService : IDisposable
{
    #region VA-API Native Interop

    private const string LibVa = "libva.so.2";
    private const string LibVaDrm = "libva-drm.so.2";
    private const string LibVaX11 = "libva-x11.so.2";

    // VA-API error codes
    private const int VA_STATUS_SUCCESS = 0;

    // VA-API profile constants
    private const int VAProfileH264Baseline = 5;
    private const int VAProfileH264Main = 6;
    private const int VAProfileH264High = 7;
    private const int VAProfileHEVCMain = 12;
    private const int VAProfileHEVCMain10 = 13;
    private const int VAProfileVP8Version0_3 = 14;
    private const int VAProfileVP9Profile0 = 15;
    private const int VAProfileVP9Profile2 = 17;
    private const int VAProfileAV1Profile0 = 20;

    // VA-API entrypoint
    private const int VAEntrypointVLD = 1; // Video Decode

    // Surface formats
    private const uint VA_RT_FORMAT_YUV420 = 0x00000001;
    private const uint VA_RT_FORMAT_YUV420_10 = 0x00000100;

    [DllImport(LibVa)]
    private static extern IntPtr vaGetDisplayDRM(int fd);

    [DllImport(LibVaX11)]
    private static extern IntPtr vaGetDisplay(IntPtr x11Display);

    [DllImport(LibVa)]
    private static extern int vaInitialize(IntPtr display, out int majorVersion, out int minorVersion);

    [DllImport(LibVa)]
    private static extern int vaTerminate(IntPtr display);

    [DllImport(LibVa)]
    private static extern IntPtr vaErrorStr(int errorCode);

    [DllImport(LibVa)]
    private static extern int vaQueryConfigProfiles(IntPtr display, [Out] int[] profileList, out int numProfiles);

    [DllImport(LibVa)]
    private static extern int vaQueryConfigEntrypoints(IntPtr display, int profile, [Out] int[] entrypoints, out int numEntrypoints);

    [DllImport(LibVa)]
    private static extern int vaCreateConfig(IntPtr display, int profile, int entrypoint, IntPtr attribList, int numAttribs, out uint configId);

    [DllImport(LibVa)]
    private static extern int vaDestroyConfig(IntPtr display, uint configId);

    [DllImport(LibVa)]
    private static extern int vaCreateContext(IntPtr display, uint configId, int pictureWidth, int pictureHeight, int flag, IntPtr renderTargets, int numRenderTargets, out uint contextId);

    [DllImport(LibVa)]
    private static extern int vaDestroyContext(IntPtr display, uint contextId);

    [DllImport(LibVa)]
    private static extern int vaCreateSurfaces(IntPtr display, uint format, uint width, uint height, [Out] uint[] surfaces, uint numSurfaces, IntPtr attribList, uint numAttribs);

    [DllImport(LibVa)]
    private static extern int vaDestroySurfaces(IntPtr display, [In] uint[] surfaces, int numSurfaces);

    [DllImport(LibVa)]
    private static extern int vaSyncSurface(IntPtr display, uint surfaceId);

    [DllImport(LibVa)]
    private static extern int vaMapBuffer(IntPtr display, uint bufferId, out IntPtr data);

    [DllImport(LibVa)]
    private static extern int vaUnmapBuffer(IntPtr display, uint bufferId);

    [DllImport(LibVa)]
    private static extern int vaDeriveImage(IntPtr display, uint surfaceId, out VaImage image);

    [DllImport(LibVa)]
    private static extern int vaDestroyImage(IntPtr display, uint imageId);

    [StructLayout(LayoutKind.Sequential)]
    private struct VaImage
    {
        public uint ImageId;
        public uint Format;        // VAImageFormat (simplified)
        public uint FormatFourCC;
        public int Width;
        public int Height;
        public uint DataSize;
        public uint NumPlanes;
        public uint PitchesPlane0;
        public uint PitchesPlane1;
        public uint PitchesPlane2;
        public uint PitchesPlane3;
        public uint OffsetsPlane0;
        public uint OffsetsPlane1;
        public uint OffsetsPlane2;
        public uint OffsetsPlane3;
        public uint BufferId;
    }

    #endregion

    #region VDPAU Native Interop

    private const string LibVdpau = "libvdpau.so.1";

    [DllImport(LibVdpau)]
    private static extern int vdp_device_create_x11(IntPtr display, int screen, out IntPtr device, out IntPtr getProcAddress);

    #endregion

    #region DRM Interop

    [DllImport("libc", EntryPoint = "open")]
    private static extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int close(int fd);

    private const int O_RDWR = 2;

    #endregion

    #region Fields

    private IntPtr _vaDisplay;
    private uint _vaConfigId;
    private uint _vaContextId;
    private uint[] _vaSurfaces = Array.Empty<uint>();
    private int _drmFd = -1;
    private bool _initialized;
    private bool _disposed;

    private VideoAccelerationApi _currentApi = VideoAccelerationApi.Software;
    private int _width;
    private int _height;
    private VideoProfile _profile;

    private readonly HashSet<VideoProfile> _supportedProfiles = new();
    private readonly object _lock = new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets the currently active video acceleration API.
    /// </summary>
    public VideoAccelerationApi CurrentApi => _currentApi;

    /// <summary>
    /// Gets whether hardware acceleration is available and initialized.
    /// </summary>
    public bool IsHardwareAccelerated => _currentApi != VideoAccelerationApi.Software && _initialized;

    /// <summary>
    /// Gets the supported video profiles.
    /// </summary>
    public IReadOnlySet<VideoProfile> SupportedProfiles => _supportedProfiles;

    #endregion

    #region Initialization

    /// <summary>
    /// Creates a new hardware video service.
    /// </summary>
    public HardwareVideoService()
    {
    }

    /// <summary>
    /// Initializes the hardware video acceleration.
    /// </summary>
    /// <param name="api">The preferred API to use.</param>
    /// <param name="x11Display">Optional X11 display for VA-API X11 backend.</param>
    /// <returns>True if initialization succeeded.</returns>
    public bool Initialize(VideoAccelerationApi api = VideoAccelerationApi.Auto, IntPtr x11Display = default)
    {
        if (_initialized)
            return true;

        lock (_lock)
        {
            if (_initialized)
                return true;

            // Try VA-API first (works with Intel, AMD, and some NVIDIA)
            if (api == VideoAccelerationApi.Auto || api == VideoAccelerationApi.VaApi)
            {
                if (TryInitializeVaApi(x11Display))
                {
                    _currentApi = VideoAccelerationApi.VaApi;
                    _initialized = true;
                    Console.WriteLine($"[HardwareVideo] Initialized VA-API with {_supportedProfiles.Count} supported profiles");
                    return true;
                }
            }

            // Try VDPAU (NVIDIA proprietary)
            if (api == VideoAccelerationApi.Auto || api == VideoAccelerationApi.Vdpau)
            {
                if (TryInitializeVdpau(x11Display))
                {
                    _currentApi = VideoAccelerationApi.Vdpau;
                    _initialized = true;
                    Console.WriteLine("[HardwareVideo] Initialized VDPAU");
                    return true;
                }
            }

            Console.WriteLine("[HardwareVideo] No hardware acceleration available, using software");
            _currentApi = VideoAccelerationApi.Software;
            return false;
        }
    }

    private bool TryInitializeVaApi(IntPtr x11Display)
    {
        try
        {
            // Try DRM backend first (works in Wayland and headless)
            string[] drmDevices = { "/dev/dri/renderD128", "/dev/dri/renderD129", "/dev/dri/card0" };
            foreach (var device in drmDevices)
            {
                _drmFd = open(device, O_RDWR);
                if (_drmFd >= 0)
                {
                    _vaDisplay = vaGetDisplayDRM(_drmFd);
                    if (_vaDisplay != IntPtr.Zero)
                    {
                        if (InitializeVaDisplay())
                            return true;
                    }
                    close(_drmFd);
                    _drmFd = -1;
                }
            }

            // Fall back to X11 backend if display provided
            if (x11Display != IntPtr.Zero)
            {
                _vaDisplay = vaGetDisplay(x11Display);
                if (_vaDisplay != IntPtr.Zero && InitializeVaDisplay())
                    return true;
            }

            return false;
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("[HardwareVideo] VA-API libraries not found");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HardwareVideo] VA-API initialization failed: {ex.Message}");
            return false;
        }
    }

    private bool InitializeVaDisplay()
    {
        int status = vaInitialize(_vaDisplay, out int major, out int minor);
        if (status != VA_STATUS_SUCCESS)
        {
            Console.WriteLine($"[HardwareVideo] vaInitialize failed: {GetVaError(status)}");
            return false;
        }

        Console.WriteLine($"[HardwareVideo] VA-API {major}.{minor} initialized");

        // Query supported profiles
        int[] profiles = new int[32];
        status = vaQueryConfigProfiles(_vaDisplay, profiles, out int numProfiles);
        if (status == VA_STATUS_SUCCESS)
        {
            for (int i = 0; i < numProfiles; i++)
            {
                if (TryMapVaProfile(profiles[i], out var videoProfile))
                {
                    // Check if VLD (decode) entrypoint is supported
                    int[] entrypoints = new int[8];
                    if (vaQueryConfigEntrypoints(_vaDisplay, profiles[i], entrypoints, out int numEntrypoints) == VA_STATUS_SUCCESS)
                    {
                        for (int j = 0; j < numEntrypoints; j++)
                        {
                            if (entrypoints[j] == VAEntrypointVLD)
                            {
                                _supportedProfiles.Add(videoProfile);
                                break;
                            }
                        }
                    }
                }
            }
        }

        return true;
    }

    private bool TryInitializeVdpau(IntPtr x11Display)
    {
        if (x11Display == IntPtr.Zero)
            return false;

        try
        {
            int result = vdp_device_create_x11(x11Display, 0, out IntPtr device, out IntPtr getProcAddress);
            if (result == 0 && device != IntPtr.Zero)
            {
                // VDPAU initialized - would need additional setup for actual use
                // For now, just mark as available
                _supportedProfiles.Add(VideoProfile.H264Baseline);
                _supportedProfiles.Add(VideoProfile.H264Main);
                _supportedProfiles.Add(VideoProfile.H264High);
                return true;
            }
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("[HardwareVideo] VDPAU libraries not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HardwareVideo] VDPAU initialization failed: {ex.Message}");
        }

        return false;
    }

    #endregion

    #region Decoder Creation

    /// <summary>
    /// Creates a decoder context for the specified profile and dimensions.
    /// </summary>
    public bool CreateDecoder(VideoProfile profile, int width, int height)
    {
        if (!_initialized || _currentApi == VideoAccelerationApi.Software)
            return false;

        if (!_supportedProfiles.Contains(profile))
        {
            Console.WriteLine($"[HardwareVideo] Profile {profile} not supported");
            return false;
        }

        lock (_lock)
        {
            // Destroy existing context
            DestroyDecoder();

            _width = width;
            _height = height;
            _profile = profile;

            if (_currentApi == VideoAccelerationApi.VaApi)
                return CreateVaApiDecoder(profile, width, height);

            return false;
        }
    }

    private bool CreateVaApiDecoder(VideoProfile profile, int width, int height)
    {
        int vaProfile = MapToVaProfile(profile);

        // Create config
        int status = vaCreateConfig(_vaDisplay, vaProfile, VAEntrypointVLD, IntPtr.Zero, 0, out _vaConfigId);
        if (status != VA_STATUS_SUCCESS)
        {
            Console.WriteLine($"[HardwareVideo] vaCreateConfig failed: {GetVaError(status)}");
            return false;
        }

        // Create surfaces for decoded frames (use a pool of 8)
        uint format = profile == VideoProfile.H265Main10 || profile == VideoProfile.Vp9Profile2
            ? VA_RT_FORMAT_YUV420_10
            : VA_RT_FORMAT_YUV420;

        _vaSurfaces = new uint[8];
        status = vaCreateSurfaces(_vaDisplay, format, (uint)width, (uint)height, _vaSurfaces, 8, IntPtr.Zero, 0);
        if (status != VA_STATUS_SUCCESS)
        {
            Console.WriteLine($"[HardwareVideo] vaCreateSurfaces failed: {GetVaError(status)}");
            vaDestroyConfig(_vaDisplay, _vaConfigId);
            return false;
        }

        // Create context
        status = vaCreateContext(_vaDisplay, _vaConfigId, width, height, 0, IntPtr.Zero, 0, out _vaContextId);
        if (status != VA_STATUS_SUCCESS)
        {
            Console.WriteLine($"[HardwareVideo] vaCreateContext failed: {GetVaError(status)}");
            vaDestroySurfaces(_vaDisplay, _vaSurfaces, _vaSurfaces.Length);
            vaDestroyConfig(_vaDisplay, _vaConfigId);
            return false;
        }

        Console.WriteLine($"[HardwareVideo] Created decoder: {profile} {width}x{height}");
        return true;
    }

    /// <summary>
    /// Destroys the current decoder context.
    /// </summary>
    public void DestroyDecoder()
    {
        lock (_lock)
        {
            if (_currentApi == VideoAccelerationApi.VaApi && _vaDisplay != IntPtr.Zero)
            {
                if (_vaContextId != 0)
                {
                    vaDestroyContext(_vaDisplay, _vaContextId);
                    _vaContextId = 0;
                }

                if (_vaSurfaces.Length > 0)
                {
                    vaDestroySurfaces(_vaDisplay, _vaSurfaces, _vaSurfaces.Length);
                    _vaSurfaces = Array.Empty<uint>();
                }

                if (_vaConfigId != 0)
                {
                    vaDestroyConfig(_vaDisplay, _vaConfigId);
                    _vaConfigId = 0;
                }
            }
        }
    }

    #endregion

    #region Frame Retrieval

    /// <summary>
    /// Retrieves a decoded frame from the specified surface.
    /// </summary>
    public VideoFrame? GetDecodedFrame(int surfaceIndex, long timestamp, bool isKeyFrame)
    {
        if (!_initialized || _currentApi != VideoAccelerationApi.VaApi)
            return null;

        if (surfaceIndex < 0 || surfaceIndex >= _vaSurfaces.Length)
            return null;

        uint surfaceId = _vaSurfaces[surfaceIndex];

        // Wait for decode to complete
        int status = vaSyncSurface(_vaDisplay, surfaceId);
        if (status != VA_STATUS_SUCCESS)
            return null;

        // Derive image from surface
        status = vaDeriveImage(_vaDisplay, surfaceId, out VaImage image);
        if (status != VA_STATUS_SUCCESS)
            return null;

        // Map the buffer
        status = vaMapBuffer(_vaDisplay, image.BufferId, out IntPtr data);
        if (status != VA_STATUS_SUCCESS)
        {
            vaDestroyImage(_vaDisplay, image.ImageId);
            return null;
        }

        var frame = new VideoFrame
        {
            Width = image.Width,
            Height = image.Height,
            DataY = data + (int)image.OffsetsPlane0,
            DataU = data + (int)image.OffsetsPlane1,
            DataV = data + (int)image.OffsetsPlane2,
            StrideY = (int)image.PitchesPlane0,
            StrideU = (int)image.PitchesPlane1,
            StrideV = (int)image.PitchesPlane2,
            Timestamp = timestamp,
            IsKeyFrame = isKeyFrame
        };

        // Set cleanup callback
        frame.SetReleaseCallback(() =>
        {
            vaUnmapBuffer(_vaDisplay, image.BufferId);
            vaDestroyImage(_vaDisplay, image.ImageId);
        });

        return frame;
    }

    /// <summary>
    /// Converts a decoded frame to an SKBitmap for display.
    /// </summary>
    public SKBitmap? ConvertFrameToSkia(VideoFrame frame)
    {
        if (frame == null)
            return null;

        // Create BGRA bitmap
        var bitmap = new SKBitmap(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);

        // Convert YUV to BGRA
        unsafe
        {
            byte* yPtr = (byte*)frame.DataY;
            byte* uPtr = (byte*)frame.DataU;
            byte* vPtr = (byte*)frame.DataV;
            byte* dst = (byte*)bitmap.GetPixels();

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    int yIndex = y * frame.StrideY + x;
                    int uvIndex = (y / 2) * frame.StrideU + (x / 2);

                    int yVal = yPtr[yIndex];
                    int uVal = uPtr[uvIndex] - 128;
                    int vVal = vPtr[uvIndex] - 128;

                    // YUV to RGB conversion
                    int r = (int)(yVal + 1.402 * vVal);
                    int g = (int)(yVal - 0.344 * uVal - 0.714 * vVal);
                    int b = (int)(yVal + 1.772 * uVal);

                    r = Math.Clamp(r, 0, 255);
                    g = Math.Clamp(g, 0, 255);
                    b = Math.Clamp(b, 0, 255);

                    int dstIndex = (y * frame.Width + x) * 4;
                    dst[dstIndex] = (byte)b;
                    dst[dstIndex + 1] = (byte)g;
                    dst[dstIndex + 2] = (byte)r;
                    dst[dstIndex + 3] = 255;
                }
            }
        }

        return bitmap;
    }

    #endregion

    #region Helpers

    private static bool TryMapVaProfile(int vaProfile, out VideoProfile profile)
    {
        profile = vaProfile switch
        {
            VAProfileH264Baseline => VideoProfile.H264Baseline,
            VAProfileH264Main => VideoProfile.H264Main,
            VAProfileH264High => VideoProfile.H264High,
            VAProfileHEVCMain => VideoProfile.H265Main,
            VAProfileHEVCMain10 => VideoProfile.H265Main10,
            VAProfileVP8Version0_3 => VideoProfile.Vp8,
            VAProfileVP9Profile0 => VideoProfile.Vp9Profile0,
            VAProfileVP9Profile2 => VideoProfile.Vp9Profile2,
            VAProfileAV1Profile0 => VideoProfile.Av1Main,
            _ => VideoProfile.H264Main
        };

        return vaProfile >= VAProfileH264Baseline && vaProfile <= VAProfileAV1Profile0;
    }

    private static int MapToVaProfile(VideoProfile profile)
    {
        return profile switch
        {
            VideoProfile.H264Baseline => VAProfileH264Baseline,
            VideoProfile.H264Main => VAProfileH264Main,
            VideoProfile.H264High => VAProfileH264High,
            VideoProfile.H265Main => VAProfileHEVCMain,
            VideoProfile.H265Main10 => VAProfileHEVCMain10,
            VideoProfile.Vp8 => VAProfileVP8Version0_3,
            VideoProfile.Vp9Profile0 => VAProfileVP9Profile0,
            VideoProfile.Vp9Profile2 => VAProfileVP9Profile2,
            VideoProfile.Av1Main => VAProfileAV1Profile0,
            _ => VAProfileH264Main
        };
    }

    private static string GetVaError(int status)
    {
        try
        {
            IntPtr errPtr = vaErrorStr(status);
            return Marshal.PtrToStringAnsi(errPtr) ?? $"Unknown error {status}";
        }
        catch
        {
            return $"Error code {status}";
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        DestroyDecoder();

        if (_currentApi == VideoAccelerationApi.VaApi && _vaDisplay != IntPtr.Zero)
        {
            vaTerminate(_vaDisplay);
            _vaDisplay = IntPtr.Zero;
        }

        if (_drmFd >= 0)
        {
            close(_drmFd);
            _drmFd = -1;
        }

        GC.SuppressFinalize(this);
    }

    ~HardwareVideoService()
    {
        Dispose();
    }

    #endregion
}
