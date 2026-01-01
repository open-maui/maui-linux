using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

public class HardwareVideoService : IDisposable
{
	private struct VaImage
	{
		public uint ImageId;

		public uint Format;

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

	private const string LibVa = "libva.so.2";

	private const string LibVaDrm = "libva-drm.so.2";

	private const string LibVaX11 = "libva-x11.so.2";

	private const int VA_STATUS_SUCCESS = 0;

	private const int VAProfileH264Baseline = 5;

	private const int VAProfileH264Main = 6;

	private const int VAProfileH264High = 7;

	private const int VAProfileHEVCMain = 12;

	private const int VAProfileHEVCMain10 = 13;

	private const int VAProfileVP8Version0_3 = 14;

	private const int VAProfileVP9Profile0 = 15;

	private const int VAProfileVP9Profile2 = 17;

	private const int VAProfileAV1Profile0 = 20;

	private const int VAEntrypointVLD = 1;

	private const uint VA_RT_FORMAT_YUV420 = 1u;

	private const uint VA_RT_FORMAT_YUV420_10 = 256u;

	private const string LibVdpau = "libvdpau.so.1";

	private const int O_RDWR = 2;

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

	private readonly HashSet<VideoProfile> _supportedProfiles = new HashSet<VideoProfile>();

	private readonly object _lock = new object();

	public VideoAccelerationApi CurrentApi => _currentApi;

	public bool IsHardwareAccelerated
	{
		get
		{
			if (_currentApi != VideoAccelerationApi.Software)
			{
				return _initialized;
			}
			return false;
		}
	}

	public IReadOnlySet<VideoProfile> SupportedProfiles => _supportedProfiles;

	[DllImport("libva.so.2")]
	private static extern IntPtr vaGetDisplayDRM(int fd);

	[DllImport("libva-x11.so.2")]
	private static extern IntPtr vaGetDisplay(IntPtr x11Display);

	[DllImport("libva.so.2")]
	private static extern int vaInitialize(IntPtr display, out int majorVersion, out int minorVersion);

	[DllImport("libva.so.2")]
	private static extern int vaTerminate(IntPtr display);

	[DllImport("libva.so.2")]
	private static extern IntPtr vaErrorStr(int errorCode);

	[DllImport("libva.so.2")]
	private static extern int vaQueryConfigProfiles(IntPtr display, [Out] int[] profileList, out int numProfiles);

	[DllImport("libva.so.2")]
	private static extern int vaQueryConfigEntrypoints(IntPtr display, int profile, [Out] int[] entrypoints, out int numEntrypoints);

	[DllImport("libva.so.2")]
	private static extern int vaCreateConfig(IntPtr display, int profile, int entrypoint, IntPtr attribList, int numAttribs, out uint configId);

	[DllImport("libva.so.2")]
	private static extern int vaDestroyConfig(IntPtr display, uint configId);

	[DllImport("libva.so.2")]
	private static extern int vaCreateContext(IntPtr display, uint configId, int pictureWidth, int pictureHeight, int flag, IntPtr renderTargets, int numRenderTargets, out uint contextId);

	[DllImport("libva.so.2")]
	private static extern int vaDestroyContext(IntPtr display, uint contextId);

	[DllImport("libva.so.2")]
	private static extern int vaCreateSurfaces(IntPtr display, uint format, uint width, uint height, [Out] uint[] surfaces, uint numSurfaces, IntPtr attribList, uint numAttribs);

	[DllImport("libva.so.2")]
	private static extern int vaDestroySurfaces(IntPtr display, [In] uint[] surfaces, int numSurfaces);

	[DllImport("libva.so.2")]
	private static extern int vaSyncSurface(IntPtr display, uint surfaceId);

	[DllImport("libva.so.2")]
	private static extern int vaMapBuffer(IntPtr display, uint bufferId, out IntPtr data);

	[DllImport("libva.so.2")]
	private static extern int vaUnmapBuffer(IntPtr display, uint bufferId);

	[DllImport("libva.so.2")]
	private static extern int vaDeriveImage(IntPtr display, uint surfaceId, out VaImage image);

	[DllImport("libva.so.2")]
	private static extern int vaDestroyImage(IntPtr display, uint imageId);

	[DllImport("libvdpau.so.1")]
	private static extern int vdp_device_create_x11(IntPtr display, int screen, out IntPtr device, out IntPtr getProcAddress);

	[DllImport("libc")]
	private static extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

	[DllImport("libc")]
	private static extern int close(int fd);

	public bool Initialize(VideoAccelerationApi api = VideoAccelerationApi.Auto, IntPtr x11Display = 0)
	{
		if (_initialized)
		{
			return true;
		}
		lock (_lock)
		{
			if (_initialized)
			{
				return true;
			}
			if ((api == VideoAccelerationApi.Auto || api == VideoAccelerationApi.VaApi) && TryInitializeVaApi(x11Display))
			{
				_currentApi = VideoAccelerationApi.VaApi;
				_initialized = true;
				Console.WriteLine($"[HardwareVideo] Initialized VA-API with {_supportedProfiles.Count} supported profiles");
				return true;
			}
			if ((api == VideoAccelerationApi.Auto || api == VideoAccelerationApi.Vdpau) && TryInitializeVdpau(x11Display))
			{
				_currentApi = VideoAccelerationApi.Vdpau;
				_initialized = true;
				Console.WriteLine("[HardwareVideo] Initialized VDPAU");
				return true;
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
			string[] array = new string[3] { "/dev/dri/renderD128", "/dev/dri/renderD129", "/dev/dri/card0" };
			foreach (string path in array)
			{
				_drmFd = open(path, 2);
				if (_drmFd >= 0)
				{
					_vaDisplay = vaGetDisplayDRM(_drmFd);
					if (_vaDisplay != IntPtr.Zero && InitializeVaDisplay())
					{
						return true;
					}
					close(_drmFd);
					_drmFd = -1;
				}
			}
			if (x11Display != IntPtr.Zero)
			{
				_vaDisplay = vaGetDisplay(x11Display);
				if (_vaDisplay != IntPtr.Zero && InitializeVaDisplay())
				{
					return true;
				}
			}
			return false;
		}
		catch (DllNotFoundException)
		{
			Console.WriteLine("[HardwareVideo] VA-API libraries not found");
			return false;
		}
		catch (Exception ex2)
		{
			Console.WriteLine("[HardwareVideo] VA-API initialization failed: " + ex2.Message);
			return false;
		}
	}

	private bool InitializeVaDisplay()
	{
		int majorVersion;
		int minorVersion;
		int num = vaInitialize(_vaDisplay, out majorVersion, out minorVersion);
		if (num != 0)
		{
			Console.WriteLine("[HardwareVideo] vaInitialize failed: " + GetVaError(num));
			return false;
		}
		Console.WriteLine($"[HardwareVideo] VA-API {majorVersion}.{minorVersion} initialized");
		int[] array = new int[32];
		if (vaQueryConfigProfiles(_vaDisplay, array, out var numProfiles) == 0)
		{
			for (int i = 0; i < numProfiles; i++)
			{
				if (!TryMapVaProfile(array[i], out var profile))
				{
					continue;
				}
				int[] array2 = new int[8];
				if (vaQueryConfigEntrypoints(_vaDisplay, array[i], array2, out var numEntrypoints) != 0)
				{
					continue;
				}
				for (int j = 0; j < numEntrypoints; j++)
				{
					if (array2[j] == 1)
					{
						_supportedProfiles.Add(profile);
						break;
					}
				}
			}
		}
		return true;
	}

	private bool TryInitializeVdpau(IntPtr x11Display)
	{
		if (x11Display == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			if (vdp_device_create_x11(x11Display, 0, out var device, out var _) == 0 && device != IntPtr.Zero)
			{
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
		catch (Exception ex2)
		{
			Console.WriteLine("[HardwareVideo] VDPAU initialization failed: " + ex2.Message);
		}
		return false;
	}

	public bool CreateDecoder(VideoProfile profile, int width, int height)
	{
		if (!_initialized || _currentApi == VideoAccelerationApi.Software)
		{
			return false;
		}
		if (!_supportedProfiles.Contains(profile))
		{
			Console.WriteLine($"[HardwareVideo] Profile {profile} not supported");
			return false;
		}
		lock (_lock)
		{
			DestroyDecoder();
			_width = width;
			_height = height;
			_profile = profile;
			if (_currentApi == VideoAccelerationApi.VaApi)
			{
				return CreateVaApiDecoder(profile, width, height);
			}
			return false;
		}
	}

	private bool CreateVaApiDecoder(VideoProfile profile, int width, int height)
	{
		int profile2 = MapToVaProfile(profile);
		int num = vaCreateConfig(_vaDisplay, profile2, 1, IntPtr.Zero, 0, out _vaConfigId);
		if (num != 0)
		{
			Console.WriteLine("[HardwareVideo] vaCreateConfig failed: " + GetVaError(num));
			return false;
		}
		uint format = ((profile != VideoProfile.H265Main10 && profile != VideoProfile.Vp9Profile2) ? 1u : 256u);
		_vaSurfaces = new uint[8];
		num = vaCreateSurfaces(_vaDisplay, format, (uint)width, (uint)height, _vaSurfaces, 8u, IntPtr.Zero, 0u);
		if (num != 0)
		{
			Console.WriteLine("[HardwareVideo] vaCreateSurfaces failed: " + GetVaError(num));
			vaDestroyConfig(_vaDisplay, _vaConfigId);
			return false;
		}
		num = vaCreateContext(_vaDisplay, _vaConfigId, width, height, 0, IntPtr.Zero, 0, out _vaContextId);
		if (num != 0)
		{
			Console.WriteLine("[HardwareVideo] vaCreateContext failed: " + GetVaError(num));
			vaDestroySurfaces(_vaDisplay, _vaSurfaces, _vaSurfaces.Length);
			vaDestroyConfig(_vaDisplay, _vaConfigId);
			return false;
		}
		Console.WriteLine($"[HardwareVideo] Created decoder: {profile} {width}x{height}");
		return true;
	}

	public void DestroyDecoder()
	{
		lock (_lock)
		{
			if (_currentApi == VideoAccelerationApi.VaApi && _vaDisplay != IntPtr.Zero)
			{
				if (_vaContextId != 0)
				{
					vaDestroyContext(_vaDisplay, _vaContextId);
					_vaContextId = 0u;
				}
				if (_vaSurfaces.Length != 0)
				{
					vaDestroySurfaces(_vaDisplay, _vaSurfaces, _vaSurfaces.Length);
					_vaSurfaces = Array.Empty<uint>();
				}
				if (_vaConfigId != 0)
				{
					vaDestroyConfig(_vaDisplay, _vaConfigId);
					_vaConfigId = 0u;
				}
			}
		}
	}

	public VideoFrame? GetDecodedFrame(int surfaceIndex, long timestamp, bool isKeyFrame)
	{
		if (!_initialized || _currentApi != VideoAccelerationApi.VaApi)
		{
			return null;
		}
		if (surfaceIndex < 0 || surfaceIndex >= _vaSurfaces.Length)
		{
			return null;
		}
		uint surfaceId = _vaSurfaces[surfaceIndex];
		if (vaSyncSurface(_vaDisplay, surfaceId) != 0)
		{
			return null;
		}
		if (vaDeriveImage(_vaDisplay, surfaceId, out var image) != 0)
		{
			return null;
		}
		if (vaMapBuffer(_vaDisplay, image.BufferId, out nint data) != 0)
		{
			vaDestroyImage(_vaDisplay, image.ImageId);
			return null;
		}
		VideoFrame obj = new VideoFrame
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
		obj.SetReleaseCallback(delegate
		{
			vaUnmapBuffer(_vaDisplay, image.BufferId);
			vaDestroyImage(_vaDisplay, image.ImageId);
		});
		return obj;
	}

	public unsafe SKBitmap? ConvertFrameToSkia(VideoFrame frame)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		if (frame == null)
		{
			return null;
		}
		SKBitmap val = new SKBitmap(frame.Width, frame.Height, (SKColorType)6, (SKAlphaType)1);
		byte* dataY = (byte*)frame.DataY;
		byte* dataU = (byte*)frame.DataU;
		byte* dataV = (byte*)frame.DataV;
		byte* pixels = (byte*)val.GetPixels();
		for (int i = 0; i < frame.Height; i++)
		{
			for (int j = 0; j < frame.Width; j++)
			{
				int num = i * frame.StrideY + j;
				int num2 = i / 2 * frame.StrideU + j / 2;
				byte num3 = dataY[num];
				int num4 = dataU[num2] - 128;
				int num5 = dataV[num2] - 128;
				int value = (int)((double)(int)num3 + 1.402 * (double)num5);
				int value2 = (int)((double)(int)num3 - 0.344 * (double)num4 - 0.714 * (double)num5);
				int value3 = (int)((double)(int)num3 + 1.772 * (double)num4);
				value = Math.Clamp(value, 0, 255);
				value2 = Math.Clamp(value2, 0, 255);
				value3 = Math.Clamp(value3, 0, 255);
				int num6 = (i * frame.Width + j) * 4;
				pixels[num6] = (byte)value3;
				pixels[num6 + 1] = (byte)value2;
				pixels[num6 + 2] = (byte)value;
				pixels[num6 + 3] = byte.MaxValue;
			}
		}
		return val;
	}

	private static bool TryMapVaProfile(int vaProfile, out VideoProfile profile)
	{
		profile = vaProfile switch
		{
			5 => VideoProfile.H264Baseline, 
			6 => VideoProfile.H264Main, 
			7 => VideoProfile.H264High, 
			12 => VideoProfile.H265Main, 
			13 => VideoProfile.H265Main10, 
			14 => VideoProfile.Vp8, 
			15 => VideoProfile.Vp9Profile0, 
			17 => VideoProfile.Vp9Profile2, 
			20 => VideoProfile.Av1Main, 
			_ => VideoProfile.H264Main, 
		};
		if (vaProfile >= 5)
		{
			return vaProfile <= 20;
		}
		return false;
	}

	private static int MapToVaProfile(VideoProfile profile)
	{
		return profile switch
		{
			VideoProfile.H264Baseline => 5, 
			VideoProfile.H264Main => 6, 
			VideoProfile.H264High => 7, 
			VideoProfile.H265Main => 12, 
			VideoProfile.H265Main10 => 13, 
			VideoProfile.Vp8 => 14, 
			VideoProfile.Vp9Profile0 => 15, 
			VideoProfile.Vp9Profile2 => 17, 
			VideoProfile.Av1Main => 20, 
			_ => 6, 
		};
	}

	private static string GetVaError(int status)
	{
		try
		{
			return Marshal.PtrToStringAnsi(vaErrorStr(status)) ?? $"Unknown error {status}";
		}
		catch
		{
			return $"Error code {status}";
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
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
	}

	~HardwareVideoService()
	{
		Dispose();
	}
}
