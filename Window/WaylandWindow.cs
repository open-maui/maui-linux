using System;
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Input;

namespace Microsoft.Maui.Platform.Linux.Window;

public class WaylandWindow : IDisposable
{
	private struct WlInterface
	{
		public IntPtr Name;

		public int Version;

		public int MethodCount;

		public IntPtr Methods;

		public int EventCount;

		public IntPtr Events;
	}

	private struct WlRegistryListener
	{
		public IntPtr Global;

		public IntPtr GlobalRemove;
	}

	private struct WlSurfaceListener
	{
		public IntPtr Enter;

		public IntPtr Leave;
	}

	private struct WlBufferListener
	{
		public IntPtr Release;
	}

	private struct WlSeatListener
	{
		public IntPtr Capabilities;

		public IntPtr Name;
	}

	private struct WlPointerListener
	{
		public IntPtr Enter;

		public IntPtr Leave;

		public IntPtr Motion;

		public IntPtr Button;

		public IntPtr Axis;

		public IntPtr Frame;

		public IntPtr AxisSource;

		public IntPtr AxisStop;

		public IntPtr AxisDiscrete;
	}

	private struct WlKeyboardListener
	{
		public IntPtr Keymap;

		public IntPtr Enter;

		public IntPtr Leave;

		public IntPtr Key;

		public IntPtr Modifiers;

		public IntPtr RepeatInfo;
	}

	private struct XdgWmBaseListener
	{
		public IntPtr Ping;
	}

	private struct XdgSurfaceListener
	{
		public IntPtr Configure;
	}

	private struct XdgToplevelListener
	{
		public IntPtr Configure;

		public IntPtr Close;
	}

	private delegate void RegistryGlobalDelegate(IntPtr data, IntPtr registry, uint name, IntPtr iface, uint version);

	private delegate void RegistryGlobalRemoveDelegate(IntPtr data, IntPtr registry, uint name);

	private delegate void SeatCapabilitiesDelegate(IntPtr data, IntPtr seat, uint capabilities);

	private delegate void SeatNameDelegate(IntPtr data, IntPtr seat, IntPtr name);

	private delegate void PointerEnterDelegate(IntPtr data, IntPtr pointer, uint serial, IntPtr surface, int x, int y);

	private delegate void PointerLeaveDelegate(IntPtr data, IntPtr pointer, uint serial, IntPtr surface);

	private delegate void PointerMotionDelegate(IntPtr data, IntPtr pointer, uint time, int x, int y);

	private delegate void PointerButtonDelegate(IntPtr data, IntPtr pointer, uint serial, uint time, uint button, uint state);

	private delegate void PointerAxisDelegate(IntPtr data, IntPtr pointer, uint time, uint axis, int value);

	private delegate void PointerFrameDelegate(IntPtr data, IntPtr pointer);

	private delegate void KeyboardKeymapDelegate(IntPtr data, IntPtr keyboard, uint format, int fd, uint size);

	private delegate void KeyboardEnterDelegate(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface, IntPtr keys);

	private delegate void KeyboardLeaveDelegate(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface);

	private delegate void KeyboardKeyDelegate(IntPtr data, IntPtr keyboard, uint serial, uint time, uint key, uint state);

	private delegate void KeyboardModifiersDelegate(IntPtr data, IntPtr keyboard, uint serial, uint modsDepressed, uint modsLatched, uint modsLocked, uint group);

	private delegate void XdgWmBasePingDelegate(IntPtr data, IntPtr wmBase, uint serial);

	private delegate void XdgSurfaceConfigureDelegate(IntPtr data, IntPtr xdgSurface, uint serial);

	private delegate void XdgToplevelConfigureDelegate(IntPtr data, IntPtr toplevel, int width, int height, IntPtr states);

	private delegate void XdgToplevelCloseDelegate(IntPtr data, IntPtr toplevel);

	private delegate void BufferReleaseDelegate(IntPtr data, IntPtr buffer);

	private const string LibWaylandClient = "libwayland-client.so.0";

	private static IntPtr _wl_registry_interface;

	private static IntPtr _wl_compositor_interface;

	private static IntPtr _wl_shm_interface;

	private static IntPtr _wl_shm_pool_interface;

	private static IntPtr _wl_buffer_interface;

	private static IntPtr _wl_surface_interface;

	private static IntPtr _wl_seat_interface;

	private static IntPtr _wl_pointer_interface;

	private static IntPtr _wl_keyboard_interface;

	private const int RTLD_NOW = 2;

	private const int RTLD_GLOBAL = 256;

	private const uint WL_DISPLAY_GET_REGISTRY = 1u;

	private const uint WL_REGISTRY_BIND = 0u;

	private const uint WL_COMPOSITOR_CREATE_SURFACE = 0u;

	private const uint WL_SURFACE_DESTROY = 0u;

	private const uint WL_SURFACE_ATTACH = 1u;

	private const uint WL_SURFACE_DAMAGE = 2u;

	private const uint WL_SURFACE_COMMIT = 6u;

	private const uint WL_SURFACE_DAMAGE_BUFFER = 9u;

	private const uint WL_SHM_CREATE_POOL = 0u;

	private const uint WL_SHM_POOL_CREATE_BUFFER = 0u;

	private const uint WL_SHM_POOL_DESTROY = 1u;

	private const uint WL_BUFFER_DESTROY = 0u;

	private const uint WL_SEAT_GET_POINTER = 0u;

	private const uint WL_SEAT_GET_KEYBOARD = 1u;

	private const uint XDG_WM_BASE_GET_XDG_SURFACE = 2u;

	private const uint XDG_WM_BASE_PONG = 3u;

	private const uint XDG_SURFACE_DESTROY = 0u;

	private const uint XDG_SURFACE_GET_TOPLEVEL = 1u;

	private const uint XDG_SURFACE_ACK_CONFIGURE = 4u;

	private const uint XDG_TOPLEVEL_DESTROY = 0u;

	private const uint XDG_TOPLEVEL_SET_TITLE = 2u;

	private const uint XDG_TOPLEVEL_SET_APP_ID = 3u;

	private static IntPtr _xdg_wm_base_interface;

	private static IntPtr _xdg_surface_interface;

	private static IntPtr _xdg_toplevel_interface;

	private static GCHandle _xdgWmBaseInterfaceHandle;

	private static GCHandle _xdgSurfaceInterfaceHandle;

	private static GCHandle _xdgToplevelInterfaceHandle;

	private static IntPtr _xdgWmBaseName;

	private static IntPtr _xdgSurfaceName;

	private static IntPtr _xdgToplevelName;

	private const int O_RDWR = 2;

	private const int O_CREAT = 64;

	private const int O_EXCL = 128;

	private const int PROT_READ = 1;

	private const int PROT_WRITE = 2;

	private const int MAP_SHARED = 1;

	private const uint MFD_CLOEXEC = 1u;

	private const uint WL_SHM_FORMAT_ARGB8888 = 0u;

	private const uint WL_SHM_FORMAT_XRGB8888 = 1u;

	private const uint WL_SEAT_CAPABILITY_POINTER = 1u;

	private const uint WL_SEAT_CAPABILITY_KEYBOARD = 2u;

	private const uint WL_POINTER_BUTTON_STATE_RELEASED = 0u;

	private const uint WL_POINTER_BUTTON_STATE_PRESSED = 1u;

	private const uint BTN_LEFT = 272u;

	private const uint BTN_RIGHT = 273u;

	private const uint BTN_MIDDLE = 274u;

	private const uint WL_KEYBOARD_KEY_STATE_RELEASED = 0u;

	private const uint WL_KEYBOARD_KEY_STATE_PRESSED = 1u;

	private IntPtr _display;

	private IntPtr _registry;

	private IntPtr _compositor;

	private IntPtr _shm;

	private IntPtr _seat;

	private IntPtr _xdgWmBase;

	private IntPtr _surface;

	private IntPtr _xdgSurface;

	private IntPtr _xdgToplevel;

	private IntPtr _pointer;

	private IntPtr _keyboard;

	private IntPtr _shmPool;

	private IntPtr _buffer;

	private IntPtr _pixelData;

	private int _shmFd = -1;

	private int _bufferSize;

	private int _stride;

	private int _width;

	private int _height;

	private int _pendingWidth;

	private int _pendingHeight;

	private string _title;

	private bool _isRunning;

	private bool _disposed;

	private bool _configured;

	private uint _lastConfigureSerial;

	private float _pointerX;

	private float _pointerY;

	private uint _pointerSerial;

	private uint _modifiers;

	private WlRegistryListener _registryListener;

	private WlSeatListener _seatListener;

	private WlPointerListener _pointerListener;

	private WlKeyboardListener _keyboardListener;

	private XdgWmBaseListener _wmBaseListener;

	private XdgSurfaceListener _xdgSurfaceListener;

	private XdgToplevelListener _toplevelListener;

	private WlBufferListener _bufferListener;

	private GCHandle _registryListenerHandle;

	private GCHandle _seatListenerHandle;

	private GCHandle _pointerListenerHandle;

	private GCHandle _keyboardListenerHandle;

	private GCHandle _wmBaseListenerHandle;

	private GCHandle _xdgSurfaceListenerHandle;

	private GCHandle _toplevelListenerHandle;

	private GCHandle _bufferListenerHandle;

	private static bool _interfacesInitialized;

	private GCHandle _thisHandle;

	public IntPtr Display => _display;

	public IntPtr Surface => _surface;

	public int Width => _width;

	public int Height => _height;

	public bool IsRunning => _isRunning;

	public IntPtr PixelData => _pixelData;

	public int Stride => _stride;

	public event EventHandler<KeyEventArgs>? KeyDown;

	public event EventHandler<KeyEventArgs>? KeyUp;

	public event EventHandler<TextInputEventArgs>? TextInput;

	public event EventHandler<PointerEventArgs>? PointerMoved;

	public event EventHandler<PointerEventArgs>? PointerPressed;

	public event EventHandler<PointerEventArgs>? PointerReleased;

	public event EventHandler<ScrollEventArgs>? Scroll;

	public event EventHandler? Exposed;

	public event EventHandler<(int Width, int Height)>? Resized;

	public event EventHandler? CloseRequested;

	public event EventHandler? FocusGained;

	public event EventHandler? FocusLost;

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_display_connect(string? name);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_display_disconnect(IntPtr display);

	[DllImport("libwayland-client.so.0")]
	private static extern int wl_display_dispatch(IntPtr display);

	[DllImport("libwayland-client.so.0")]
	private static extern int wl_display_dispatch_pending(IntPtr display);

	[DllImport("libwayland-client.so.0")]
	private static extern int wl_display_roundtrip(IntPtr display);

	[DllImport("libwayland-client.so.0")]
	private static extern int wl_display_flush(IntPtr display);

	[DllImport("libwayland-client.so.0")]
	private static extern int wl_display_get_fd(IntPtr display);

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_proxy_marshal_constructor(IntPtr proxy, uint opcode, IntPtr iface, IntPtr arg);

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_proxy_marshal_constructor_versioned(IntPtr proxy, uint opcode, IntPtr iface, uint version, IntPtr arg);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, IntPtr arg1);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, int arg1, int arg2);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, IntPtr arg1, int arg2, int arg3);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, int arg1, int arg2, int arg3, int arg4);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, uint arg1);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, [MarshalAs(UnmanagedType.LPStr)] string arg1);

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_proxy_marshal_array_constructor(IntPtr proxy, uint opcode, IntPtr args, IntPtr iface);

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_proxy_marshal_array_constructor_versioned(IntPtr proxy, uint opcode, IntPtr args, IntPtr iface, uint version);

	[DllImport("libwayland-client.so.0")]
	private static extern int wl_proxy_add_listener(IntPtr proxy, IntPtr impl, IntPtr data);

	[DllImport("libwayland-client.so.0")]
	private static extern void wl_proxy_destroy(IntPtr proxy);

	[DllImport("libwayland-client.so.0")]
	private static extern uint wl_proxy_get_version(IntPtr proxy);

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_registry_interface_ptr();

	[DllImport("libdl.so.2")]
	private static extern IntPtr dlopen(string? filename, int flags);

	[DllImport("libdl.so.2")]
	private static extern IntPtr dlsym(IntPtr handle, string symbol);

	[DllImport("libdl.so.2")]
	private static extern int dlclose(IntPtr handle);

	private static void LoadInterfaceSymbols()
	{
		if (_wl_registry_interface == IntPtr.Zero)
		{
			IntPtr intPtr = dlopen("libwayland-client.so.0", 258);
			if (intPtr == IntPtr.Zero)
			{
				throw new InvalidOperationException("Failed to load libwayland-client.so.0");
			}
			_wl_registry_interface = dlsym(intPtr, "wl_registry_interface");
			_wl_compositor_interface = dlsym(intPtr, "wl_compositor_interface");
			_wl_shm_interface = dlsym(intPtr, "wl_shm_interface");
			_wl_shm_pool_interface = dlsym(intPtr, "wl_shm_pool_interface");
			_wl_buffer_interface = dlsym(intPtr, "wl_buffer_interface");
			_wl_surface_interface = dlsym(intPtr, "wl_surface_interface");
			_wl_seat_interface = dlsym(intPtr, "wl_seat_interface");
			_wl_pointer_interface = dlsym(intPtr, "wl_pointer_interface");
			_wl_keyboard_interface = dlsym(intPtr, "wl_keyboard_interface");
		}
	}

	private static IntPtr wl_display_get_registry(IntPtr display)
	{
		return wl_proxy_marshal_constructor(display, 1u, _wl_registry_interface, IntPtr.Zero);
	}

	private static int wl_registry_add_listener(IntPtr registry, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(registry, listener, data);
	}

	[DllImport("libwayland-client.so.0")]
	private static extern IntPtr wl_proxy_marshal_flags(IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags, uint name, IntPtr ifaceName, uint ifaceVersion);

	private static IntPtr wl_registry_bind(IntPtr registry, uint name, IntPtr iface, uint version)
	{
		return wl_proxy_marshal_flags(registry, 0u, iface, version, 0u, name, iface, version);
	}

	private static IntPtr wl_compositor_create_surface(IntPtr compositor)
	{
		return wl_proxy_marshal_constructor(compositor, 0u, _wl_surface_interface, IntPtr.Zero);
	}

	private static void wl_surface_attach(IntPtr surface, IntPtr buffer, int x, int y)
	{
		wl_proxy_marshal(surface, 1u, buffer, x, y);
	}

	private static void wl_surface_damage(IntPtr surface, int x, int y, int width, int height)
	{
		wl_proxy_marshal(surface, 2u, x, y, width, height);
	}

	private static void wl_surface_damage_buffer(IntPtr surface, int x, int y, int width, int height)
	{
		wl_proxy_marshal(surface, 9u, x, y, width, height);
	}

	private static void wl_surface_commit(IntPtr surface)
	{
		wl_proxy_marshal(surface, 6u);
	}

	private static void wl_surface_destroy(IntPtr surface)
	{
		wl_proxy_marshal(surface, 0u);
		wl_proxy_destroy(surface);
	}

	[DllImport("libwayland-client.so.0", EntryPoint = "wl_proxy_marshal_flags")]
	private static extern IntPtr wl_proxy_marshal_flags_fd(IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags, IntPtr newId, int fd, int size);

	private static IntPtr wl_shm_create_pool(IntPtr shm, int fd, int size)
	{
		return wl_proxy_marshal_flags_fd(shm, 0u, _wl_shm_pool_interface, wl_proxy_get_version(shm), 0u, IntPtr.Zero, fd, size);
	}

	[DllImport("libwayland-client.so.0", EntryPoint = "wl_proxy_marshal_flags")]
	private static extern IntPtr wl_proxy_marshal_flags_buffer(IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags, IntPtr newId, int offset, int width, int height, int stride, uint format);

	private static IntPtr wl_shm_pool_create_buffer(IntPtr pool, int offset, int width, int height, int stride, uint format)
	{
		return wl_proxy_marshal_flags_buffer(pool, 0u, _wl_buffer_interface, wl_proxy_get_version(pool), 0u, IntPtr.Zero, offset, width, height, stride, format);
	}

	private static void wl_shm_pool_destroy(IntPtr pool)
	{
		wl_proxy_marshal(pool, 1u);
		wl_proxy_destroy(pool);
	}

	private static void wl_buffer_destroy(IntPtr buffer)
	{
		wl_proxy_marshal(buffer, 0u);
		wl_proxy_destroy(buffer);
	}

	private static int wl_buffer_add_listener(IntPtr buffer, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(buffer, listener, data);
	}

	private static int wl_seat_add_listener(IntPtr seat, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(seat, listener, data);
	}

	private static IntPtr wl_seat_get_pointer(IntPtr seat)
	{
		return wl_proxy_marshal_constructor(seat, 0u, _wl_pointer_interface, IntPtr.Zero);
	}

	private static IntPtr wl_seat_get_keyboard(IntPtr seat)
	{
		return wl_proxy_marshal_constructor(seat, 1u, _wl_keyboard_interface, IntPtr.Zero);
	}

	private static int wl_pointer_add_listener(IntPtr pointer, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(pointer, listener, data);
	}

	private static int wl_keyboard_add_listener(IntPtr keyboard, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(keyboard, listener, data);
	}

	private static void LoadXdgShellInterfaces()
	{
		if (_xdg_wm_base_interface == IntPtr.Zero)
		{
			_xdgWmBaseName = Marshal.StringToHGlobalAnsi("xdg_wm_base");
			_xdgSurfaceName = Marshal.StringToHGlobalAnsi("xdg_surface");
			_xdgToplevelName = Marshal.StringToHGlobalAnsi("xdg_toplevel");
			_xdgWmBaseInterfaceHandle = GCHandle.Alloc(new WlInterface
			{
				Name = _xdgWmBaseName,
				Version = 6,
				MethodCount = 4,
				Methods = IntPtr.Zero,
				EventCount = 1,
				Events = IntPtr.Zero
			}, GCHandleType.Pinned);
			_xdg_wm_base_interface = _xdgWmBaseInterfaceHandle.AddrOfPinnedObject();
			_xdgSurfaceInterfaceHandle = GCHandle.Alloc(new WlInterface
			{
				Name = _xdgSurfaceName,
				Version = 6,
				MethodCount = 5,
				Methods = IntPtr.Zero,
				EventCount = 1,
				Events = IntPtr.Zero
			}, GCHandleType.Pinned);
			_xdg_surface_interface = _xdgSurfaceInterfaceHandle.AddrOfPinnedObject();
			_xdgToplevelInterfaceHandle = GCHandle.Alloc(new WlInterface
			{
				Name = _xdgToplevelName,
				Version = 6,
				MethodCount = 14,
				Methods = IntPtr.Zero,
				EventCount = 4,
				Events = IntPtr.Zero
			}, GCHandleType.Pinned);
			_xdg_toplevel_interface = _xdgToplevelInterfaceHandle.AddrOfPinnedObject();
		}
	}

	private static IntPtr xdg_wm_base_get_xdg_surface(IntPtr wmBase, IntPtr surface)
	{
		return wl_proxy_marshal_constructor(wmBase, 2u, _xdg_surface_interface, surface);
	}

	private static void xdg_wm_base_pong(IntPtr wmBase, uint serial)
	{
		wl_proxy_marshal(wmBase, 3u, serial);
	}

	private static int xdg_wm_base_add_listener(IntPtr wmBase, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(wmBase, listener, data);
	}

	private static IntPtr xdg_surface_get_toplevel(IntPtr xdgSurface)
	{
		return wl_proxy_marshal_constructor(xdgSurface, 1u, _xdg_toplevel_interface, IntPtr.Zero);
	}

	private static void xdg_surface_ack_configure(IntPtr xdgSurface, uint serial)
	{
		wl_proxy_marshal(xdgSurface, 4u, serial);
	}

	private static int xdg_surface_add_listener(IntPtr xdgSurface, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(xdgSurface, listener, data);
	}

	private static void xdg_surface_destroy(IntPtr xdgSurface)
	{
		wl_proxy_marshal(xdgSurface, 0u);
		wl_proxy_destroy(xdgSurface);
	}

	private static void xdg_toplevel_set_title(IntPtr toplevel, string title)
	{
		wl_proxy_marshal(toplevel, 2u, title);
	}

	private static void xdg_toplevel_set_app_id(IntPtr toplevel, string appId)
	{
		wl_proxy_marshal(toplevel, 3u, appId);
	}

	private static int xdg_toplevel_add_listener(IntPtr toplevel, IntPtr listener, IntPtr data)
	{
		return wl_proxy_add_listener(toplevel, listener, data);
	}

	private static void xdg_toplevel_destroy(IntPtr toplevel)
	{
		wl_proxy_marshal(toplevel, 0u);
		wl_proxy_destroy(toplevel);
	}

	[DllImport("libc")]
	private static extern int shm_open([MarshalAs(UnmanagedType.LPStr)] string name, int oflag, int mode);

	[DllImport("libc")]
	private static extern int shm_unlink([MarshalAs(UnmanagedType.LPStr)] string name);

	[DllImport("libc")]
	private static extern int ftruncate(int fd, long length);

	[DllImport("libc")]
	private static extern IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, long offset);

	[DllImport("libc")]
	private static extern int munmap(IntPtr addr, UIntPtr length);

	[DllImport("libc")]
	private static extern int close(int fd);

	[DllImport("libc")]
	private static extern int memfd_create([MarshalAs(UnmanagedType.LPStr)] string name, uint flags);

	public WaylandWindow(string title, int width, int height)
	{
		_title = title;
		_width = width;
		_height = height;
		_pendingWidth = width;
		_pendingHeight = height;
		InitializeInterfaces();
		Initialize();
	}

	private static void InitializeInterfaces()
	{
		if (!_interfacesInitialized)
		{
			LoadInterfaceSymbols();
			LoadXdgShellInterfaces();
			_interfacesInitialized = true;
		}
	}

	private void Initialize()
	{
		_thisHandle = GCHandle.Alloc(this);
		_display = wl_display_connect(null);
		if (_display == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to connect to Wayland display. Ensure WAYLAND_DISPLAY is set and a compositor is running.");
		}
		_registry = wl_display_get_registry(_display);
		if (_registry == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to get Wayland registry");
		}
		_registryListener = new WlRegistryListener
		{
			Global = Marshal.GetFunctionPointerForDelegate<RegistryGlobalDelegate>(RegistryGlobal),
			GlobalRemove = Marshal.GetFunctionPointerForDelegate<RegistryGlobalRemoveDelegate>(RegistryGlobalRemove)
		};
		_registryListenerHandle = GCHandle.Alloc(_registryListener, GCHandleType.Pinned);
		wl_registry_add_listener(_registry, _registryListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		wl_display_roundtrip(_display);
		if (_compositor == IntPtr.Zero)
		{
			throw new InvalidOperationException("Wayland compositor not found");
		}
		if (_shm == IntPtr.Zero)
		{
			throw new InvalidOperationException("Wayland shm not found");
		}
		if (_xdgWmBase == IntPtr.Zero)
		{
			throw new InvalidOperationException("xdg_wm_base not found - compositor doesn't support xdg-shell");
		}
		_surface = wl_compositor_create_surface(_compositor);
		if (_surface == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to create Wayland surface");
		}
		_xdgSurface = xdg_wm_base_get_xdg_surface(_xdgWmBase, _surface);
		if (_xdgSurface == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to create xdg_surface");
		}
		_xdgSurfaceListener = new XdgSurfaceListener
		{
			Configure = Marshal.GetFunctionPointerForDelegate<XdgSurfaceConfigureDelegate>(XdgSurfaceConfigure)
		};
		_xdgSurfaceListenerHandle = GCHandle.Alloc(_xdgSurfaceListener, GCHandleType.Pinned);
		xdg_surface_add_listener(_xdgSurface, _xdgSurfaceListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		_xdgToplevel = xdg_surface_get_toplevel(_xdgSurface);
		if (_xdgToplevel == IntPtr.Zero)
		{
			throw new InvalidOperationException("Failed to create xdg_toplevel");
		}
		_toplevelListener = new XdgToplevelListener
		{
			Configure = Marshal.GetFunctionPointerForDelegate<XdgToplevelConfigureDelegate>(XdgToplevelConfigure),
			Close = Marshal.GetFunctionPointerForDelegate<XdgToplevelCloseDelegate>(XdgToplevelClose)
		};
		_toplevelListenerHandle = GCHandle.Alloc(_toplevelListener, GCHandleType.Pinned);
		xdg_toplevel_add_listener(_xdgToplevel, _toplevelListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		xdg_toplevel_set_title(_xdgToplevel, _title);
		xdg_toplevel_set_app_id(_xdgToplevel, "com.openmaui.app");
		wl_surface_commit(_surface);
		wl_display_roundtrip(_display);
		CreateShmBuffer();
		Console.WriteLine($"[Wayland] Window created: {_width}x{_height}");
	}

	private void CreateShmBuffer()
	{
		_stride = _width * 4;
		_bufferSize = _stride * _height;
		_shmFd = memfd_create("maui-buffer", 1u);
		if (_shmFd < 0)
		{
			string name = $"/maui-{Environment.ProcessId}-{DateTime.Now.Ticks}";
			_shmFd = shm_open(name, 194, 384);
			if (_shmFd >= 0)
			{
				shm_unlink(name);
			}
		}
		if (_shmFd < 0)
		{
			throw new InvalidOperationException("Failed to create shared memory");
		}
		if (ftruncate(_shmFd, _bufferSize) < 0)
		{
			close(_shmFd);
			throw new InvalidOperationException("Failed to resize shared memory");
		}
		_pixelData = mmap(IntPtr.Zero, (nuint)_bufferSize, 3, 1, _shmFd, 0L);
		if (_pixelData == IntPtr.Zero || _pixelData == new IntPtr(-1))
		{
			close(_shmFd);
			throw new InvalidOperationException("Failed to mmap shared memory");
		}
		_shmPool = wl_shm_create_pool(_shm, _shmFd, _bufferSize);
		if (_shmPool == IntPtr.Zero)
		{
			munmap(_pixelData, (nuint)_bufferSize);
			close(_shmFd);
			throw new InvalidOperationException("Failed to create wl_shm_pool");
		}
		_buffer = wl_shm_pool_create_buffer(_shmPool, 0, _width, _height, _stride, 0u);
		if (_buffer == IntPtr.Zero)
		{
			wl_shm_pool_destroy(_shmPool);
			munmap(_pixelData, (nuint)_bufferSize);
			close(_shmFd);
			throw new InvalidOperationException("Failed to create wl_buffer");
		}
		_bufferListener = new WlBufferListener
		{
			Release = Marshal.GetFunctionPointerForDelegate<BufferReleaseDelegate>(BufferRelease)
		};
		if (_bufferListenerHandle.IsAllocated)
		{
			_bufferListenerHandle.Free();
		}
		_bufferListenerHandle = GCHandle.Alloc(_bufferListener, GCHandleType.Pinned);
		wl_buffer_add_listener(_buffer, _bufferListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
	}

	private void ResizeBuffer(int newWidth, int newHeight)
	{
		if ((newWidth != _width || newHeight != _height) && newWidth > 0 && newHeight > 0)
		{
			if (_buffer != IntPtr.Zero)
			{
				wl_buffer_destroy(_buffer);
			}
			if (_shmPool != IntPtr.Zero)
			{
				wl_shm_pool_destroy(_shmPool);
			}
			if (_pixelData != IntPtr.Zero && _pixelData != new IntPtr(-1))
			{
				munmap(_pixelData, (nuint)_bufferSize);
			}
			if (_shmFd >= 0)
			{
				close(_shmFd);
			}
			_width = newWidth;
			_height = newHeight;
			CreateShmBuffer();
			this.Resized?.Invoke(this, (_width, _height));
		}
	}

	private static void RegistryGlobal(IntPtr data, IntPtr registry, uint name, IntPtr iface, uint version)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			string text = Marshal.PtrToStringAnsi(iface);
			Console.WriteLine($"[Wayland] Global: {text} v{version}");
			switch (text)
			{
			case "wl_compositor":
				waylandWindow._compositor = wl_registry_bind(registry, name, _wl_compositor_interface, Math.Min(version, 4u));
				break;
			case "wl_shm":
				waylandWindow._shm = wl_registry_bind(registry, name, _wl_shm_interface, 1u);
				break;
			case "wl_seat":
				waylandWindow._seat = wl_registry_bind(registry, name, _wl_seat_interface, Math.Min(version, 5u));
				waylandWindow.SetupSeat();
				break;
			case "xdg_wm_base":
				waylandWindow._xdgWmBase = wl_registry_bind(registry, name, _xdg_wm_base_interface, Math.Min(version, 2u));
				waylandWindow.SetupXdgWmBase();
				break;
			}
		}
	}

	private static void RegistryGlobalRemove(IntPtr data, IntPtr registry, uint name)
	{
	}

	private void SetupSeat()
	{
		if (_seat != IntPtr.Zero)
		{
			_seatListener = new WlSeatListener
			{
				Capabilities = Marshal.GetFunctionPointerForDelegate<SeatCapabilitiesDelegate>(SeatCapabilities),
				Name = Marshal.GetFunctionPointerForDelegate<SeatNameDelegate>(SeatName)
			};
			_seatListenerHandle = GCHandle.Alloc(_seatListener, GCHandleType.Pinned);
			wl_seat_add_listener(_seat, _seatListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		}
	}

	private static void SeatCapabilities(IntPtr data, IntPtr seat, uint capabilities)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			if ((capabilities & 1) != 0 && waylandWindow._pointer == IntPtr.Zero)
			{
				waylandWindow._pointer = wl_seat_get_pointer(seat);
				waylandWindow.SetupPointer();
			}
			if ((capabilities & 2) != 0 && waylandWindow._keyboard == IntPtr.Zero)
			{
				waylandWindow._keyboard = wl_seat_get_keyboard(seat);
				waylandWindow.SetupKeyboard();
			}
		}
	}

	private static void SeatName(IntPtr data, IntPtr seat, IntPtr name)
	{
	}

	private void SetupPointer()
	{
		if (_pointer != IntPtr.Zero)
		{
			_pointerListener = new WlPointerListener
			{
				Enter = Marshal.GetFunctionPointerForDelegate<PointerEnterDelegate>(PointerEnter),
				Leave = Marshal.GetFunctionPointerForDelegate<PointerLeaveDelegate>(PointerLeave),
				Motion = Marshal.GetFunctionPointerForDelegate<PointerMotionDelegate>(PointerMotion),
				Button = Marshal.GetFunctionPointerForDelegate<PointerButtonDelegate>(OnPointerButton),
				Axis = Marshal.GetFunctionPointerForDelegate<PointerAxisDelegate>(PointerAxis),
				Frame = Marshal.GetFunctionPointerForDelegate<PointerFrameDelegate>(PointerFrame)
			};
			_pointerListenerHandle = GCHandle.Alloc(_pointerListener, GCHandleType.Pinned);
			wl_pointer_add_listener(_pointer, _pointerListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		}
	}

	private static void PointerEnter(IntPtr data, IntPtr pointer, uint serial, IntPtr surface, int x, int y)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow obj = (WaylandWindow)gCHandle.Target;
			obj._pointerSerial = serial;
			obj._pointerX = (float)x / 256f;
			obj._pointerY = (float)y / 256f;
		}
	}

	private static void PointerLeave(IntPtr data, IntPtr pointer, uint serial, IntPtr surface)
	{
	}

	private static void PointerMotion(IntPtr data, IntPtr pointer, uint time, int x, int y)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			waylandWindow._pointerX = (float)x / 256f;
			waylandWindow._pointerY = (float)y / 256f;
			waylandWindow.PointerMoved?.Invoke(waylandWindow, new PointerEventArgs((int)waylandWindow._pointerX, (int)waylandWindow._pointerY));
		}
	}

	private static void OnPointerButton(IntPtr data, IntPtr pointer, uint serial, uint time, uint button, uint state)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			PointerEventArgs e = new PointerEventArgs((int)waylandWindow._pointerX, (int)waylandWindow._pointerY, button switch
			{
				272u => PointerButton.Left, 
				273u => PointerButton.Right, 
				274u => PointerButton.Middle, 
				_ => PointerButton.None, 
			});
			if (state == 1)
			{
				waylandWindow.PointerPressed?.Invoke(waylandWindow, e);
			}
			else
			{
				waylandWindow.PointerReleased?.Invoke(waylandWindow, e);
			}
		}
	}

	private static void PointerAxis(IntPtr data, IntPtr pointer, uint time, uint axis, int value)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			float num = (float)value / 256f / 10f;
			if (axis == 0)
			{
				waylandWindow.Scroll?.Invoke(waylandWindow, new ScrollEventArgs((int)waylandWindow._pointerX, (int)waylandWindow._pointerY, 0f, num));
			}
			else
			{
				waylandWindow.Scroll?.Invoke(waylandWindow, new ScrollEventArgs((int)waylandWindow._pointerX, (int)waylandWindow._pointerY, num, 0f));
			}
		}
	}

	private static void PointerFrame(IntPtr data, IntPtr pointer)
	{
	}

	private void SetupKeyboard()
	{
		if (_keyboard != IntPtr.Zero)
		{
			_keyboardListener = new WlKeyboardListener
			{
				Keymap = Marshal.GetFunctionPointerForDelegate<KeyboardKeymapDelegate>(KeyboardKeymap),
				Enter = Marshal.GetFunctionPointerForDelegate<KeyboardEnterDelegate>(KeyboardEnter),
				Leave = Marshal.GetFunctionPointerForDelegate<KeyboardLeaveDelegate>(KeyboardLeave),
				Key = Marshal.GetFunctionPointerForDelegate<KeyboardKeyDelegate>(KeyboardKey),
				Modifiers = Marshal.GetFunctionPointerForDelegate<KeyboardModifiersDelegate>(KeyboardModifiers)
			};
			_keyboardListenerHandle = GCHandle.Alloc(_keyboardListener, GCHandleType.Pinned);
			wl_keyboard_add_listener(_keyboard, _keyboardListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		}
	}

	private static void KeyboardKeymap(IntPtr data, IntPtr keyboard, uint format, int fd, uint size)
	{
		close(fd);
	}

	private static void KeyboardEnter(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface, IntPtr keys)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			waylandWindow.FocusGained?.Invoke(waylandWindow, EventArgs.Empty);
		}
	}

	private static void KeyboardLeave(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			waylandWindow.FocusLost?.Invoke(waylandWindow, EventArgs.Empty);
		}
	}

	private static void KeyboardKey(IntPtr data, IntPtr keyboard, uint serial, uint time, uint keycode, uint state)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (!gCHandle.IsAllocated)
		{
			return;
		}
		WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
		Key key = KeyMapping.FromLinuxKeycode(keycode + 8);
		KeyModifiers modifiers = (KeyModifiers)waylandWindow._modifiers;
		KeyEventArgs e = new KeyEventArgs(key, modifiers);
		if (state == 1)
		{
			waylandWindow.KeyDown?.Invoke(waylandWindow, e);
			char? c = KeyMapping.ToChar(key, modifiers);
			if (c.HasValue)
			{
				waylandWindow.TextInput?.Invoke(waylandWindow, new TextInputEventArgs(c.Value.ToString()));
			}
		}
		else
		{
			waylandWindow.KeyUp?.Invoke(waylandWindow, e);
		}
	}

	private static void KeyboardModifiers(IntPtr data, IntPtr keyboard, uint serial, uint modsDepressed, uint modsLatched, uint modsLocked, uint group)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			((WaylandWindow)gCHandle.Target)._modifiers = modsDepressed | modsLatched;
		}
	}

	private void SetupXdgWmBase()
	{
		if (_xdgWmBase != IntPtr.Zero)
		{
			_wmBaseListener = new XdgWmBaseListener
			{
				Ping = Marshal.GetFunctionPointerForDelegate<XdgWmBasePingDelegate>(XdgWmBasePing)
			};
			_wmBaseListenerHandle = GCHandle.Alloc(_wmBaseListener, GCHandleType.Pinned);
			xdg_wm_base_add_listener(_xdgWmBase, _wmBaseListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
		}
	}

	private static void XdgWmBasePing(IntPtr data, IntPtr wmBase, uint serial)
	{
		xdg_wm_base_pong(wmBase, serial);
	}

	private static void XdgSurfaceConfigure(IntPtr data, IntPtr xdgSurface, uint serial)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (!gCHandle.IsAllocated)
		{
			return;
		}
		WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
		xdg_surface_ack_configure(xdgSurface, serial);
		waylandWindow._lastConfigureSerial = serial;
		if (!waylandWindow._configured)
		{
			waylandWindow._configured = true;
			if (waylandWindow._pendingWidth > 0 && waylandWindow._pendingHeight > 0)
			{
				waylandWindow.ResizeBuffer(waylandWindow._pendingWidth, waylandWindow._pendingHeight);
			}
			waylandWindow.Exposed?.Invoke(waylandWindow, EventArgs.Empty);
		}
	}

	private static void XdgToplevelConfigure(IntPtr data, IntPtr toplevel, int width, int height, IntPtr states)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (!gCHandle.IsAllocated)
		{
			return;
		}
		WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
		if (width > 0 && height > 0)
		{
			waylandWindow._pendingWidth = width;
			waylandWindow._pendingHeight = height;
			if (waylandWindow._configured)
			{
				waylandWindow.ResizeBuffer(width, height);
			}
		}
	}

	private static void XdgToplevelClose(IntPtr data, IntPtr toplevel)
	{
		GCHandle gCHandle = GCHandle.FromIntPtr(data);
		if (gCHandle.IsAllocated)
		{
			WaylandWindow waylandWindow = (WaylandWindow)gCHandle.Target;
			waylandWindow.CloseRequested?.Invoke(waylandWindow, EventArgs.Empty);
			waylandWindow._isRunning = false;
		}
	}

	private static void BufferRelease(IntPtr data, IntPtr buffer)
	{
	}

	public void Show()
	{
		_isRunning = true;
		wl_surface_attach(_surface, _buffer, 0, 0);
		wl_surface_damage_buffer(_surface, 0, 0, _width, _height);
		wl_surface_commit(_surface);
		wl_display_flush(_display);
	}

	public void Hide()
	{
		wl_surface_attach(_surface, IntPtr.Zero, 0, 0);
		wl_surface_commit(_surface);
		wl_display_flush(_display);
	}

	public void SetTitle(string title)
	{
		_title = title;
		if (_xdgToplevel != IntPtr.Zero)
		{
			xdg_toplevel_set_title(_xdgToplevel, title);
		}
	}

	public void Resize(int width, int height)
	{
		ResizeBuffer(width, height);
	}

	public void ProcessEvents()
	{
		if (_isRunning && _display != IntPtr.Zero)
		{
			wl_display_dispatch_pending(_display);
			wl_display_flush(_display);
		}
	}

	public void Stop()
	{
		_isRunning = false;
	}

	public void CommitFrame()
	{
		if (_surface != IntPtr.Zero && _buffer != IntPtr.Zero)
		{
			wl_surface_attach(_surface, _buffer, 0, 0);
			wl_surface_damage_buffer(_surface, 0, 0, _width, _height);
			wl_surface_commit(_surface);
			wl_display_flush(_display);
		}
	}

	public int GetFileDescriptor()
	{
		return wl_display_get_fd(_display);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_isRunning = false;
			if (_buffer != IntPtr.Zero)
			{
				wl_buffer_destroy(_buffer);
				_buffer = IntPtr.Zero;
			}
			if (_shmPool != IntPtr.Zero)
			{
				wl_shm_pool_destroy(_shmPool);
				_shmPool = IntPtr.Zero;
			}
			if (_pixelData != IntPtr.Zero && _pixelData != new IntPtr(-1))
			{
				munmap(_pixelData, (nuint)_bufferSize);
				_pixelData = IntPtr.Zero;
			}
			if (_shmFd >= 0)
			{
				close(_shmFd);
				_shmFd = -1;
			}
			if (_xdgToplevel != IntPtr.Zero)
			{
				xdg_toplevel_destroy(_xdgToplevel);
				_xdgToplevel = IntPtr.Zero;
			}
			if (_xdgSurface != IntPtr.Zero)
			{
				xdg_surface_destroy(_xdgSurface);
				_xdgSurface = IntPtr.Zero;
			}
			if (_surface != IntPtr.Zero)
			{
				wl_surface_destroy(_surface);
				_surface = IntPtr.Zero;
			}
			if (_display != IntPtr.Zero)
			{
				wl_display_disconnect(_display);
				_display = IntPtr.Zero;
			}
			if (_registryListenerHandle.IsAllocated)
			{
				_registryListenerHandle.Free();
			}
			if (_seatListenerHandle.IsAllocated)
			{
				_seatListenerHandle.Free();
			}
			if (_pointerListenerHandle.IsAllocated)
			{
				_pointerListenerHandle.Free();
			}
			if (_keyboardListenerHandle.IsAllocated)
			{
				_keyboardListenerHandle.Free();
			}
			if (_wmBaseListenerHandle.IsAllocated)
			{
				_wmBaseListenerHandle.Free();
			}
			if (_xdgSurfaceListenerHandle.IsAllocated)
			{
				_xdgSurfaceListenerHandle.Free();
			}
			if (_toplevelListenerHandle.IsAllocated)
			{
				_toplevelListenerHandle.Free();
			}
			if (_bufferListenerHandle.IsAllocated)
			{
				_bufferListenerHandle.Free();
			}
			if (_thisHandle.IsAllocated)
			{
				_thisHandle.Free();
			}
			GC.SuppressFinalize(this);
		}
	}

	~WaylandWindow()
	{
		Dispose();
	}
}
