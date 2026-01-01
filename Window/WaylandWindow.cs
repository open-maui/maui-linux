// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Input;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// Native Wayland window implementation using xdg-shell protocol.
/// Provides full Wayland support without XWayland dependency.
/// </summary>
public class WaylandWindow : IDisposable
{
    #region Native Interop - libwayland-client

    private const string LibWaylandClient = "libwayland-client.so.0";

    // Core display functions (actually exported)
    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_display_connect(string? name);

    [DllImport(LibWaylandClient)]
    private static extern void wl_display_disconnect(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_dispatch(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_dispatch_pending(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_roundtrip(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_flush(IntPtr display);

    [DllImport(LibWaylandClient)]
    private static extern int wl_display_get_fd(IntPtr display);

    // Low-level proxy API (actually exported - used to implement protocol wrappers)
    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_proxy_marshal_constructor(
        IntPtr proxy, uint opcode, IntPtr iface, IntPtr arg);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_proxy_marshal_constructor_versioned(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, IntPtr arg);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, IntPtr arg1);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, int arg1, int arg2);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, IntPtr arg1, int arg2, int arg3);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, int arg1, int arg2, int arg3, int arg4);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode, uint arg1);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode,
        [MarshalAs(UnmanagedType.LPStr)] string arg1);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_proxy_marshal_array_constructor(
        IntPtr proxy, uint opcode, IntPtr args, IntPtr iface);

    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_proxy_marshal_array_constructor_versioned(
        IntPtr proxy, uint opcode, IntPtr args, IntPtr iface, uint version);

    [DllImport(LibWaylandClient)]
    private static extern int wl_proxy_add_listener(IntPtr proxy, IntPtr impl, IntPtr data);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_destroy(IntPtr proxy);

    [DllImport(LibWaylandClient)]
    private static extern uint wl_proxy_get_version(IntPtr proxy);

    // Interface globals (exported as data symbols)
    [DllImport(LibWaylandClient)]
    private static extern IntPtr wl_registry_interface_ptr();

    // We need to load these at runtime since they're data symbols
    private static IntPtr _wl_registry_interface;
    private static IntPtr _wl_compositor_interface;
    private static IntPtr _wl_shm_interface;
    private static IntPtr _wl_shm_pool_interface;
    private static IntPtr _wl_buffer_interface;
    private static IntPtr _wl_surface_interface;
    private static IntPtr _wl_seat_interface;
    private static IntPtr _wl_pointer_interface;
    private static IntPtr _wl_keyboard_interface;

    // dlsym for loading interface symbols
    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen(string? filename, int flags);

    [DllImport("libdl.so.2", EntryPoint = "dlsym")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libdl.so.2", EntryPoint = "dlclose")]
    private static extern int dlclose(IntPtr handle);

    private const int RTLD_NOW = 2;
    private const int RTLD_GLOBAL = 0x100;

    #endregion

    #region Wayland Protocol Opcodes

    // wl_display opcodes
    private const uint WL_DISPLAY_GET_REGISTRY = 1;

    // wl_registry opcodes
    private const uint WL_REGISTRY_BIND = 0;

    // wl_compositor opcodes
    private const uint WL_COMPOSITOR_CREATE_SURFACE = 0;

    // wl_surface opcodes
    private const uint WL_SURFACE_DESTROY = 0;
    private const uint WL_SURFACE_ATTACH = 1;
    private const uint WL_SURFACE_DAMAGE = 2;
    private const uint WL_SURFACE_COMMIT = 6;
    private const uint WL_SURFACE_DAMAGE_BUFFER = 9;

    // wl_shm opcodes
    private const uint WL_SHM_CREATE_POOL = 0;

    // wl_shm_pool opcodes
    private const uint WL_SHM_POOL_CREATE_BUFFER = 0;
    private const uint WL_SHM_POOL_DESTROY = 1;

    // wl_buffer opcodes
    private const uint WL_BUFFER_DESTROY = 0;

    // wl_seat opcodes
    private const uint WL_SEAT_GET_POINTER = 0;
    private const uint WL_SEAT_GET_KEYBOARD = 1;

    // xdg_wm_base opcodes
    private const uint XDG_WM_BASE_GET_XDG_SURFACE = 2;
    private const uint XDG_WM_BASE_PONG = 3;

    // xdg_surface opcodes
    private const uint XDG_SURFACE_DESTROY = 0;
    private const uint XDG_SURFACE_GET_TOPLEVEL = 1;
    private const uint XDG_SURFACE_ACK_CONFIGURE = 4;

    // xdg_toplevel opcodes
    private const uint XDG_TOPLEVEL_DESTROY = 0;
    private const uint XDG_TOPLEVEL_SET_TITLE = 2;
    private const uint XDG_TOPLEVEL_SET_APP_ID = 3;

    #endregion

    #region Protocol Wrapper Methods

    private static void LoadInterfaceSymbols()
    {
        if (_wl_registry_interface != IntPtr.Zero) return;

        var handle = dlopen("libwayland-client.so.0", RTLD_NOW | RTLD_GLOBAL);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to load libwayland-client.so.0");

        _wl_registry_interface = dlsym(handle, "wl_registry_interface");
        _wl_compositor_interface = dlsym(handle, "wl_compositor_interface");
        _wl_shm_interface = dlsym(handle, "wl_shm_interface");
        _wl_shm_pool_interface = dlsym(handle, "wl_shm_pool_interface");
        _wl_buffer_interface = dlsym(handle, "wl_buffer_interface");
        _wl_surface_interface = dlsym(handle, "wl_surface_interface");
        _wl_seat_interface = dlsym(handle, "wl_seat_interface");
        _wl_pointer_interface = dlsym(handle, "wl_pointer_interface");
        _wl_keyboard_interface = dlsym(handle, "wl_keyboard_interface");

        // Don't close - we need the symbols to remain valid
    }

    // wl_display_get_registry wrapper
    private static IntPtr wl_display_get_registry(IntPtr display)
    {
        return wl_proxy_marshal_constructor(display, WL_DISPLAY_GET_REGISTRY,
            _wl_registry_interface, IntPtr.Zero);
    }

    // wl_registry_add_listener wrapper
    private static int wl_registry_add_listener(IntPtr registry, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(registry, listener, data);
    }

    // wl_registry_bind wrapper - uses special marshaling
    [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    private static extern IntPtr wl_proxy_marshal_flags(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags,
        uint name, IntPtr ifaceName, uint ifaceVersion);

    private static IntPtr wl_registry_bind(IntPtr registry, uint name, IntPtr iface, uint version)
    {
        // For registry bind, we need to use marshal_flags with the interface
        return wl_proxy_marshal_flags(registry, WL_REGISTRY_BIND, iface, version, 0,
            name, iface, version);
    }

    // wl_compositor_create_surface wrapper
    private static IntPtr wl_compositor_create_surface(IntPtr compositor)
    {
        return wl_proxy_marshal_constructor(compositor, WL_COMPOSITOR_CREATE_SURFACE,
            _wl_surface_interface, IntPtr.Zero);
    }

    // wl_surface methods
    private static void wl_surface_attach(IntPtr surface, IntPtr buffer, int x, int y)
    {
        wl_proxy_marshal(surface, WL_SURFACE_ATTACH, buffer, x, y);
    }

    private static void wl_surface_damage(IntPtr surface, int x, int y, int width, int height)
    {
        wl_proxy_marshal(surface, WL_SURFACE_DAMAGE, x, y, width, height);
    }

    private static void wl_surface_damage_buffer(IntPtr surface, int x, int y, int width, int height)
    {
        wl_proxy_marshal(surface, WL_SURFACE_DAMAGE_BUFFER, x, y, width, height);
    }

    private static void wl_surface_commit(IntPtr surface)
    {
        wl_proxy_marshal(surface, WL_SURFACE_COMMIT);
    }

    private static void wl_surface_destroy(IntPtr surface)
    {
        wl_proxy_marshal(surface, WL_SURFACE_DESTROY);
        wl_proxy_destroy(surface);
    }

    // wl_shm_create_pool wrapper
    [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    private static extern IntPtr wl_proxy_marshal_flags_fd(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags,
        IntPtr newId, int fd, int size);

    private static IntPtr wl_shm_create_pool(IntPtr shm, int fd, int size)
    {
        return wl_proxy_marshal_flags_fd(shm, WL_SHM_CREATE_POOL,
            _wl_shm_pool_interface, wl_proxy_get_version(shm), 0,
            IntPtr.Zero, fd, size);
    }

    // wl_shm_pool methods
    [DllImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    private static extern IntPtr wl_proxy_marshal_flags_buffer(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags,
        IntPtr newId, int offset, int width, int height, int stride, uint format);

    private static IntPtr wl_shm_pool_create_buffer(IntPtr pool, int offset, int width, int height, int stride, uint format)
    {
        return wl_proxy_marshal_flags_buffer(pool, WL_SHM_POOL_CREATE_BUFFER,
            _wl_buffer_interface, wl_proxy_get_version(pool), 0,
            IntPtr.Zero, offset, width, height, stride, format);
    }

    private static void wl_shm_pool_destroy(IntPtr pool)
    {
        wl_proxy_marshal(pool, WL_SHM_POOL_DESTROY);
        wl_proxy_destroy(pool);
    }

    // wl_buffer methods
    private static void wl_buffer_destroy(IntPtr buffer)
    {
        wl_proxy_marshal(buffer, WL_BUFFER_DESTROY);
        wl_proxy_destroy(buffer);
    }

    private static int wl_buffer_add_listener(IntPtr buffer, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(buffer, listener, data);
    }

    // wl_seat methods
    private static int wl_seat_add_listener(IntPtr seat, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(seat, listener, data);
    }

    private static IntPtr wl_seat_get_pointer(IntPtr seat)
    {
        return wl_proxy_marshal_constructor(seat, WL_SEAT_GET_POINTER,
            _wl_pointer_interface, IntPtr.Zero);
    }

    private static IntPtr wl_seat_get_keyboard(IntPtr seat)
    {
        return wl_proxy_marshal_constructor(seat, WL_SEAT_GET_KEYBOARD,
            _wl_keyboard_interface, IntPtr.Zero);
    }

    private static int wl_pointer_add_listener(IntPtr pointer, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(pointer, listener, data);
    }

    private static int wl_keyboard_add_listener(IntPtr keyboard, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(keyboard, listener, data);
    }

    #endregion

    #region xdg-shell Protocol Wrappers

    private static IntPtr _xdg_wm_base_interface;
    private static IntPtr _xdg_surface_interface;
    private static IntPtr _xdg_toplevel_interface;

    // We need to create and pin interface structures for xdg-shell
    private static GCHandle _xdgWmBaseInterfaceHandle;
    private static GCHandle _xdgSurfaceInterfaceHandle;
    private static GCHandle _xdgToplevelInterfaceHandle;
    private static IntPtr _xdgWmBaseName;
    private static IntPtr _xdgSurfaceName;
    private static IntPtr _xdgToplevelName;

    private static void LoadXdgShellInterfaces()
    {
        if (_xdg_wm_base_interface != IntPtr.Zero) return;

        // xdg-shell interfaces are NOT in libwayland-client
        // We need to create minimal interface structs ourselves
        // The key fields are: name (string ptr), version, method_count, methods, event_count, events

        // Allocate interface names
        _xdgWmBaseName = Marshal.StringToHGlobalAnsi("xdg_wm_base");
        _xdgSurfaceName = Marshal.StringToHGlobalAnsi("xdg_surface");
        _xdgToplevelName = Marshal.StringToHGlobalAnsi("xdg_toplevel");

        // Create interface structures
        var wmBaseInterface = new WlInterface
        {
            Name = _xdgWmBaseName,
            Version = 6,
            MethodCount = 4,  // destroy, create_positioner, get_xdg_surface, pong
            Methods = IntPtr.Zero,
            EventCount = 1,   // ping
            Events = IntPtr.Zero
        };
        _xdgWmBaseInterfaceHandle = GCHandle.Alloc(wmBaseInterface, GCHandleType.Pinned);
        _xdg_wm_base_interface = _xdgWmBaseInterfaceHandle.AddrOfPinnedObject();

        var surfaceInterface = new WlInterface
        {
            Name = _xdgSurfaceName,
            Version = 6,
            MethodCount = 5,  // destroy, get_toplevel, get_popup, set_window_geometry, ack_configure
            Methods = IntPtr.Zero,
            EventCount = 1,   // configure
            Events = IntPtr.Zero
        };
        _xdgSurfaceInterfaceHandle = GCHandle.Alloc(surfaceInterface, GCHandleType.Pinned);
        _xdg_surface_interface = _xdgSurfaceInterfaceHandle.AddrOfPinnedObject();

        var toplevelInterface = new WlInterface
        {
            Name = _xdgToplevelName,
            Version = 6,
            MethodCount = 14, // destroy, set_parent, set_title, set_app_id, etc.
            Methods = IntPtr.Zero,
            EventCount = 4,   // configure, close, configure_bounds, wm_capabilities
            Events = IntPtr.Zero
        };
        _xdgToplevelInterfaceHandle = GCHandle.Alloc(toplevelInterface, GCHandleType.Pinned);
        _xdg_toplevel_interface = _xdgToplevelInterfaceHandle.AddrOfPinnedObject();
    }

    private static IntPtr xdg_wm_base_get_xdg_surface(IntPtr wmBase, IntPtr surface)
    {
        return wl_proxy_marshal_constructor(wmBase, XDG_WM_BASE_GET_XDG_SURFACE,
            _xdg_surface_interface, surface);
    }

    private static void xdg_wm_base_pong(IntPtr wmBase, uint serial)
    {
        wl_proxy_marshal(wmBase, XDG_WM_BASE_PONG, serial);
    }

    private static int xdg_wm_base_add_listener(IntPtr wmBase, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(wmBase, listener, data);
    }

    private static IntPtr xdg_surface_get_toplevel(IntPtr xdgSurface)
    {
        return wl_proxy_marshal_constructor(xdgSurface, XDG_SURFACE_GET_TOPLEVEL,
            _xdg_toplevel_interface, IntPtr.Zero);
    }

    private static void xdg_surface_ack_configure(IntPtr xdgSurface, uint serial)
    {
        wl_proxy_marshal(xdgSurface, XDG_SURFACE_ACK_CONFIGURE, serial);
    }

    private static int xdg_surface_add_listener(IntPtr xdgSurface, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(xdgSurface, listener, data);
    }

    private static void xdg_surface_destroy(IntPtr xdgSurface)
    {
        wl_proxy_marshal(xdgSurface, XDG_SURFACE_DESTROY);
        wl_proxy_destroy(xdgSurface);
    }

    private static void xdg_toplevel_set_title(IntPtr toplevel, string title)
    {
        wl_proxy_marshal(toplevel, XDG_TOPLEVEL_SET_TITLE, title);
    }

    private static void xdg_toplevel_set_app_id(IntPtr toplevel, string appId)
    {
        wl_proxy_marshal(toplevel, XDG_TOPLEVEL_SET_APP_ID, appId);
    }

    private static int xdg_toplevel_add_listener(IntPtr toplevel, IntPtr listener, IntPtr data)
    {
        return wl_proxy_add_listener(toplevel, listener, data);
    }

    private static void xdg_toplevel_destroy(IntPtr toplevel)
    {
        wl_proxy_marshal(toplevel, XDG_TOPLEVEL_DESTROY);
        wl_proxy_destroy(toplevel);
    }

    #endregion

    #region Native Interop - libc

    [DllImport("libc", EntryPoint = "shm_open")]
    private static extern int shm_open([MarshalAs(UnmanagedType.LPStr)] string name, int oflag, int mode);

    [DllImport("libc", EntryPoint = "shm_unlink")]
    private static extern int shm_unlink([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("libc", EntryPoint = "ftruncate")]
    private static extern int ftruncate(int fd, long length);

    [DllImport("libc", EntryPoint = "mmap")]
    private static extern IntPtr mmap(IntPtr addr, nuint length, int prot, int flags, int fd, long offset);

    [DllImport("libc", EntryPoint = "munmap")]
    private static extern int munmap(IntPtr addr, nuint length);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int close(int fd);

    [DllImport("libc", EntryPoint = "memfd_create")]
    private static extern int memfd_create([MarshalAs(UnmanagedType.LPStr)] string name, uint flags);

    private const int O_RDWR = 2;
    private const int O_CREAT = 64;
    private const int O_EXCL = 128;
    private const int PROT_READ = 1;
    private const int PROT_WRITE = 2;
    private const int MAP_SHARED = 1;
    private const uint MFD_CLOEXEC = 1;

    #endregion

    #region Wayland Structures

    [StructLayout(LayoutKind.Sequential)]
    private struct WlInterface
    {
        public IntPtr Name;
        public int Version;
        public int MethodCount;
        public IntPtr Methods;
        public int EventCount;
        public IntPtr Events;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlRegistryListener
    {
        public IntPtr Global;
        public IntPtr GlobalRemove;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlSurfaceListener
    {
        public IntPtr Enter;
        public IntPtr Leave;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlBufferListener
    {
        public IntPtr Release;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlSeatListener
    {
        public IntPtr Capabilities;
        public IntPtr Name;
    }

    [StructLayout(LayoutKind.Sequential)]
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

    [StructLayout(LayoutKind.Sequential)]
    private struct WlKeyboardListener
    {
        public IntPtr Keymap;
        public IntPtr Enter;
        public IntPtr Leave;
        public IntPtr Key;
        public IntPtr Modifiers;
        public IntPtr RepeatInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XdgWmBaseListener
    {
        public IntPtr Ping;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XdgSurfaceListener
    {
        public IntPtr Configure;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XdgToplevelListener
    {
        public IntPtr Configure;
        public IntPtr Close;
    }

    private const uint WL_SHM_FORMAT_ARGB8888 = 0;
    private const uint WL_SHM_FORMAT_XRGB8888 = 1;

    // Seat capabilities
    private const uint WL_SEAT_CAPABILITY_POINTER = 1;
    private const uint WL_SEAT_CAPABILITY_KEYBOARD = 2;

    // Pointer button states
    private const uint WL_POINTER_BUTTON_STATE_RELEASED = 0;
    private const uint WL_POINTER_BUTTON_STATE_PRESSED = 1;

    // Linux input button codes
    private const uint BTN_LEFT = 0x110;
    private const uint BTN_RIGHT = 0x111;
    private const uint BTN_MIDDLE = 0x112;

    // Key states
    private const uint WL_KEYBOARD_KEY_STATE_RELEASED = 0;
    private const uint WL_KEYBOARD_KEY_STATE_PRESSED = 1;

    #endregion

    #region Fields

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

    // Input state
    private float _pointerX;
    private float _pointerY;
    private uint _pointerSerial;
    private uint _modifiers;

    // Delegates to prevent GC
    private WlRegistryListener _registryListener;
    private WlSeatListener _seatListener;
    private WlPointerListener _pointerListener;
    private WlKeyboardListener _keyboardListener;
    private XdgWmBaseListener _wmBaseListener;
    private XdgSurfaceListener _xdgSurfaceListener;
    private XdgToplevelListener _toplevelListener;
    private WlBufferListener _bufferListener;

    // GCHandles for listener structs to prevent GC
    private GCHandle _registryListenerHandle;
    private GCHandle _seatListenerHandle;
    private GCHandle _pointerListenerHandle;
    private GCHandle _keyboardListenerHandle;
    private GCHandle _wmBaseListenerHandle;
    private GCHandle _xdgSurfaceListenerHandle;
    private GCHandle _toplevelListenerHandle;
    private GCHandle _bufferListenerHandle;

    private static bool _interfacesInitialized;

    // GCHandles to prevent delegate collection
    private GCHandle _thisHandle;

    #endregion

    #region Properties

    public IntPtr Display => _display;
    public IntPtr Surface => _surface;
    public int Width => _width;
    public int Height => _height;
    public bool IsRunning => _isRunning;
    public IntPtr PixelData => _pixelData;
    public int Stride => _stride;

    #endregion

    #region Events

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

    #endregion

    #region Constructor

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

    #endregion

    #region Initialization

    private static void InitializeInterfaces()
    {
        if (_interfacesInitialized) return;

        // Load interface symbols from libwayland-client using dlsym
        LoadInterfaceSymbols();
        LoadXdgShellInterfaces();

        _interfacesInitialized = true;
    }

    private void Initialize()
    {
        // Keep this object alive for callbacks
        _thisHandle = GCHandle.Alloc(this);

        // Connect to Wayland display
        _display = wl_display_connect(null);
        if (_display == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Failed to connect to Wayland display. " +
                "Ensure WAYLAND_DISPLAY is set and a compositor is running.");
        }

        // Get registry
        _registry = wl_display_get_registry(_display);
        if (_registry == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get Wayland registry");
        }

        // Set up registry listener
        _registryListener = new WlRegistryListener
        {
            Global = Marshal.GetFunctionPointerForDelegate<RegistryGlobalDelegate>(RegistryGlobal),
            GlobalRemove = Marshal.GetFunctionPointerForDelegate<RegistryGlobalRemoveDelegate>(RegistryGlobalRemove)
        };
        _registryListenerHandle = GCHandle.Alloc(_registryListener, GCHandleType.Pinned);
        wl_registry_add_listener(_registry, _registryListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        // Do initial roundtrip to get globals
        wl_display_roundtrip(_display);

        // Verify we got required globals
        if (_compositor == IntPtr.Zero)
            throw new InvalidOperationException("Wayland compositor not found");
        if (_shm == IntPtr.Zero)
            throw new InvalidOperationException("Wayland shm not found");
        if (_xdgWmBase == IntPtr.Zero)
            throw new InvalidOperationException("xdg_wm_base not found - compositor doesn't support xdg-shell");

        // Create surface
        _surface = wl_compositor_create_surface(_compositor);
        if (_surface == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create Wayland surface");

        // Create xdg_surface
        _xdgSurface = xdg_wm_base_get_xdg_surface(_xdgWmBase, _surface);
        if (_xdgSurface == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create xdg_surface");

        _xdgSurfaceListener = new XdgSurfaceListener
        {
            Configure = Marshal.GetFunctionPointerForDelegate<XdgSurfaceConfigureDelegate>(XdgSurfaceConfigure)
        };
        _xdgSurfaceListenerHandle = GCHandle.Alloc(_xdgSurfaceListener, GCHandleType.Pinned);
        xdg_surface_add_listener(_xdgSurface, _xdgSurfaceListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        // Create toplevel
        _xdgToplevel = xdg_surface_get_toplevel(_xdgSurface);
        if (_xdgToplevel == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create xdg_toplevel");

        _toplevelListener = new XdgToplevelListener
        {
            Configure = Marshal.GetFunctionPointerForDelegate<XdgToplevelConfigureDelegate>(XdgToplevelConfigure),
            Close = Marshal.GetFunctionPointerForDelegate<XdgToplevelCloseDelegate>(XdgToplevelClose)
        };
        _toplevelListenerHandle = GCHandle.Alloc(_toplevelListener, GCHandleType.Pinned);
        xdg_toplevel_add_listener(_xdgToplevel, _toplevelListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));

        // Set title and app_id
        xdg_toplevel_set_title(_xdgToplevel, _title);
        xdg_toplevel_set_app_id(_xdgToplevel, "com.openmaui.app");

        // Commit empty surface to get initial configure
        wl_surface_commit(_surface);
        wl_display_roundtrip(_display);

        // Create shared memory buffer
        CreateShmBuffer();

        Console.WriteLine($"[Wayland] Window created: {_width}x{_height}");
    }

    private void CreateShmBuffer()
    {
        _stride = _width * 4;
        _bufferSize = _stride * _height;

        // Create anonymous file for shared memory
        _shmFd = memfd_create("maui-buffer", MFD_CLOEXEC);
        if (_shmFd < 0)
        {
            // Fall back to shm_open
            string shmName = $"/maui-{Environment.ProcessId}-{DateTime.Now.Ticks}";
            _shmFd = shm_open(shmName, O_RDWR | O_CREAT | O_EXCL, 0x180); // 0600
            if (_shmFd >= 0)
                shm_unlink(shmName);
        }

        if (_shmFd < 0)
            throw new InvalidOperationException("Failed to create shared memory");

        if (ftruncate(_shmFd, _bufferSize) < 0)
        {
            close(_shmFd);
            throw new InvalidOperationException("Failed to resize shared memory");
        }

        _pixelData = mmap(IntPtr.Zero, (nuint)_bufferSize, PROT_READ | PROT_WRITE, MAP_SHARED, _shmFd, 0);
        if (_pixelData == IntPtr.Zero || _pixelData == new IntPtr(-1))
        {
            close(_shmFd);
            throw new InvalidOperationException("Failed to mmap shared memory");
        }

        // Create pool and buffer
        _shmPool = wl_shm_create_pool(_shm, _shmFd, _bufferSize);
        if (_shmPool == IntPtr.Zero)
        {
            munmap(_pixelData, (nuint)_bufferSize);
            close(_shmFd);
            throw new InvalidOperationException("Failed to create wl_shm_pool");
        }

        _buffer = wl_shm_pool_create_buffer(_shmPool, 0, _width, _height, _stride, WL_SHM_FORMAT_ARGB8888);
        if (_buffer == IntPtr.Zero)
        {
            wl_shm_pool_destroy(_shmPool);
            munmap(_pixelData, (nuint)_bufferSize);
            close(_shmFd);
            throw new InvalidOperationException("Failed to create wl_buffer");
        }

        // Listen for buffer release
        _bufferListener = new WlBufferListener
        {
            Release = Marshal.GetFunctionPointerForDelegate<BufferReleaseDelegate>(BufferRelease)
        };
        if (_bufferListenerHandle.IsAllocated) _bufferListenerHandle.Free();
        _bufferListenerHandle = GCHandle.Alloc(_bufferListener, GCHandleType.Pinned);
        wl_buffer_add_listener(_buffer, _bufferListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private void ResizeBuffer(int newWidth, int newHeight)
    {
        if (newWidth == _width && newHeight == _height) return;
        if (newWidth <= 0 || newHeight <= 0) return;

        // Destroy old buffer
        if (_buffer != IntPtr.Zero)
            wl_buffer_destroy(_buffer);
        if (_shmPool != IntPtr.Zero)
            wl_shm_pool_destroy(_shmPool);
        if (_pixelData != IntPtr.Zero && _pixelData != new IntPtr(-1))
            munmap(_pixelData, (nuint)_bufferSize);
        if (_shmFd >= 0)
            close(_shmFd);

        _width = newWidth;
        _height = newHeight;

        CreateShmBuffer();
        Resized?.Invoke(this, (_width, _height));
    }

    #endregion

    #region Callback Delegates

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

    #endregion

    #region Callback Implementations

    private static void RegistryGlobal(IntPtr data, IntPtr registry, uint name, IntPtr iface, uint version)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        var interfaceName = Marshal.PtrToStringAnsi(iface);
        Console.WriteLine($"[Wayland] Global: {interfaceName} v{version}");

        switch (interfaceName)
        {
            case "wl_compositor":
                window._compositor = wl_registry_bind(registry, name, _wl_compositor_interface, Math.Min(version, 4u));
                break;
            case "wl_shm":
                window._shm = wl_registry_bind(registry, name, _wl_shm_interface, 1);
                break;
            case "wl_seat":
                window._seat = wl_registry_bind(registry, name, _wl_seat_interface, Math.Min(version, 5u));
                window.SetupSeat();
                break;
            case "xdg_wm_base":
                window._xdgWmBase = wl_registry_bind(registry, name, _xdg_wm_base_interface, Math.Min(version, 2u));
                window.SetupXdgWmBase();
                break;
        }
    }

    private static void RegistryGlobalRemove(IntPtr data, IntPtr registry, uint name)
    {
        // Handle global removal if needed
    }

    private void SetupSeat()
    {
        if (_seat == IntPtr.Zero) return;

        _seatListener = new WlSeatListener
        {
            Capabilities = Marshal.GetFunctionPointerForDelegate<SeatCapabilitiesDelegate>(SeatCapabilities),
            Name = Marshal.GetFunctionPointerForDelegate<SeatNameDelegate>(SeatName)
        };
        _seatListenerHandle = GCHandle.Alloc(_seatListener, GCHandleType.Pinned);
        wl_seat_add_listener(_seat, _seatListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private static void SeatCapabilities(IntPtr data, IntPtr seat, uint capabilities)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        if ((capabilities & WL_SEAT_CAPABILITY_POINTER) != 0 && window._pointer == IntPtr.Zero)
        {
            window._pointer = wl_seat_get_pointer(seat);
            window.SetupPointer();
        }

        if ((capabilities & WL_SEAT_CAPABILITY_KEYBOARD) != 0 && window._keyboard == IntPtr.Zero)
        {
            window._keyboard = wl_seat_get_keyboard(seat);
            window.SetupKeyboard();
        }
    }

    private static void SeatName(IntPtr data, IntPtr seat, IntPtr name) { }

    private void SetupPointer()
    {
        if (_pointer == IntPtr.Zero) return;

        _pointerListener = new WlPointerListener
        {
            Enter = Marshal.GetFunctionPointerForDelegate<PointerEnterDelegate>(PointerEnter),
            Leave = Marshal.GetFunctionPointerForDelegate<PointerLeaveDelegate>(PointerLeave),
            Motion = Marshal.GetFunctionPointerForDelegate<PointerMotionDelegate>(PointerMotion),
            Button = Marshal.GetFunctionPointerForDelegate<PointerButtonDelegate>(OnPointerButton),
            Axis = Marshal.GetFunctionPointerForDelegate<PointerAxisDelegate>(PointerAxis),
            Frame = Marshal.GetFunctionPointerForDelegate<PointerFrameDelegate>(PointerFrame),
        };
        _pointerListenerHandle = GCHandle.Alloc(_pointerListener, GCHandleType.Pinned);
        wl_pointer_add_listener(_pointer, _pointerListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private static void PointerEnter(IntPtr data, IntPtr pointer, uint serial, IntPtr surface, int x, int y)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window._pointerSerial = serial;
        window._pointerX = x / 256.0f;
        window._pointerY = y / 256.0f;
    }

    private static void PointerLeave(IntPtr data, IntPtr pointer, uint serial, IntPtr surface) { }

    private static void PointerMotion(IntPtr data, IntPtr pointer, uint time, int x, int y)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window._pointerX = x / 256.0f;
        window._pointerY = y / 256.0f;
        window.PointerMoved?.Invoke(window, new PointerEventArgs((int)window._pointerX, (int)window._pointerY));
    }

    private static void OnPointerButton(IntPtr data, IntPtr pointer, uint serial, uint time, uint button, uint state)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        var ptrButton = button switch
        {
            BTN_LEFT => Microsoft.Maui.Platform.PointerButton.Left,
            BTN_RIGHT => Microsoft.Maui.Platform.PointerButton.Right,
            BTN_MIDDLE => Microsoft.Maui.Platform.PointerButton.Middle,
            _ => Microsoft.Maui.Platform.PointerButton.None
        };

        var args = new PointerEventArgs((int)window._pointerX, (int)window._pointerY, ptrButton);

        if (state == WL_POINTER_BUTTON_STATE_PRESSED)
            window.PointerPressed?.Invoke(window, args);
        else
            window.PointerReleased?.Invoke(window, args);
    }

    private static void PointerAxis(IntPtr data, IntPtr pointer, uint time, uint axis, int value)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        float delta = value / 256.0f / 10.0f;
        if (axis == 0) // Vertical
            window.Scroll?.Invoke(window, new ScrollEventArgs((int)window._pointerX, (int)window._pointerY, 0, delta));
        else // Horizontal
            window.Scroll?.Invoke(window, new ScrollEventArgs((int)window._pointerX, (int)window._pointerY, delta, 0));
    }

    private static void PointerFrame(IntPtr data, IntPtr pointer) { }

    private void SetupKeyboard()
    {
        if (_keyboard == IntPtr.Zero) return;

        _keyboardListener = new WlKeyboardListener
        {
            Keymap = Marshal.GetFunctionPointerForDelegate<KeyboardKeymapDelegate>(KeyboardKeymap),
            Enter = Marshal.GetFunctionPointerForDelegate<KeyboardEnterDelegate>(KeyboardEnter),
            Leave = Marshal.GetFunctionPointerForDelegate<KeyboardLeaveDelegate>(KeyboardLeave),
            Key = Marshal.GetFunctionPointerForDelegate<KeyboardKeyDelegate>(KeyboardKey),
            Modifiers = Marshal.GetFunctionPointerForDelegate<KeyboardModifiersDelegate>(KeyboardModifiers),
        };
        _keyboardListenerHandle = GCHandle.Alloc(_keyboardListener, GCHandleType.Pinned);
        wl_keyboard_add_listener(_keyboard, _keyboardListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private static void KeyboardKeymap(IntPtr data, IntPtr keyboard, uint format, int fd, uint size)
    {
        close(fd);
    }

    private static void KeyboardEnter(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface, IntPtr keys)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window.FocusGained?.Invoke(window, EventArgs.Empty);
    }

    private static void KeyboardLeave(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window.FocusLost?.Invoke(window, EventArgs.Empty);
    }

    private static void KeyboardKey(IntPtr data, IntPtr keyboard, uint serial, uint time, uint keycode, uint state)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        // Convert Linux keycode to Key enum (add 8 for X11 compat)
        var key = KeyMapping.FromLinuxKeycode(keycode + 8);
        var modifiers = (KeyModifiers)window._modifiers;
        var args = new KeyEventArgs(key, modifiers);

        if (state == WL_KEYBOARD_KEY_STATE_PRESSED)
        {
            window.KeyDown?.Invoke(window, args);

            // Generate text input for printable keys
            char? ch = KeyMapping.ToChar(key, modifiers);
            if (ch.HasValue)
                window.TextInput?.Invoke(window, new TextInputEventArgs(ch.Value.ToString()));
        }
        else
        {
            window.KeyUp?.Invoke(window, args);
        }
    }

    private static void KeyboardModifiers(IntPtr data, IntPtr keyboard, uint serial, uint modsDepressed, uint modsLatched, uint modsLocked, uint group)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;
        window._modifiers = modsDepressed | modsLatched;
    }

    private void SetupXdgWmBase()
    {
        if (_xdgWmBase == IntPtr.Zero) return;

        _wmBaseListener = new XdgWmBaseListener
        {
            Ping = Marshal.GetFunctionPointerForDelegate<XdgWmBasePingDelegate>(XdgWmBasePing)
        };
        _wmBaseListenerHandle = GCHandle.Alloc(_wmBaseListener, GCHandleType.Pinned);
        xdg_wm_base_add_listener(_xdgWmBase, _wmBaseListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private static void XdgWmBasePing(IntPtr data, IntPtr wmBase, uint serial)
    {
        xdg_wm_base_pong(wmBase, serial);
    }

    private static void XdgSurfaceConfigure(IntPtr data, IntPtr xdgSurface, uint serial)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        xdg_surface_ack_configure(xdgSurface, serial);
        window._lastConfigureSerial = serial;

        if (!window._configured)
        {
            window._configured = true;
            if (window._pendingWidth > 0 && window._pendingHeight > 0)
            {
                window.ResizeBuffer(window._pendingWidth, window._pendingHeight);
            }
            window.Exposed?.Invoke(window, EventArgs.Empty);
        }
    }

    private static void XdgToplevelConfigure(IntPtr data, IntPtr toplevel, int width, int height, IntPtr states)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        if (width > 0 && height > 0)
        {
            window._pendingWidth = width;
            window._pendingHeight = height;

            if (window._configured)
            {
                window.ResizeBuffer(width, height);
            }
        }
    }

    private static void XdgToplevelClose(IntPtr data, IntPtr toplevel)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        window.CloseRequested?.Invoke(window, EventArgs.Empty);
        window._isRunning = false;
    }

    private static void BufferRelease(IntPtr data, IntPtr buffer)
    {
        // Buffer is available for reuse
    }

    #endregion

    #region Public Methods

    public void Show()
    {
        _isRunning = true;

        // Attach buffer and commit
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
            xdg_toplevel_set_title(_xdgToplevel, title);
    }

    public void Resize(int width, int height)
    {
        ResizeBuffer(width, height);
    }

    public void ProcessEvents()
    {
        if (!_isRunning || _display == IntPtr.Zero) return;

        wl_display_dispatch_pending(_display);
        wl_display_flush(_display);
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

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
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

        // Free listener GCHandles
        if (_registryListenerHandle.IsAllocated) _registryListenerHandle.Free();
        if (_seatListenerHandle.IsAllocated) _seatListenerHandle.Free();
        if (_pointerListenerHandle.IsAllocated) _pointerListenerHandle.Free();
        if (_keyboardListenerHandle.IsAllocated) _keyboardListenerHandle.Free();
        if (_wmBaseListenerHandle.IsAllocated) _wmBaseListenerHandle.Free();
        if (_xdgSurfaceListenerHandle.IsAllocated) _xdgSurfaceListenerHandle.Free();
        if (_toplevelListenerHandle.IsAllocated) _toplevelListenerHandle.Free();
        if (_bufferListenerHandle.IsAllocated) _bufferListenerHandle.Free();

        if (_thisHandle.IsAllocated)
            _thisHandle.Free();

        GC.SuppressFinalize(this);
    }

    ~WaylandWindow()
    {
        Dispose();
    }

    #endregion
}
