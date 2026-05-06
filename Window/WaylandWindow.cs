// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Maui.Platform.Linux.Input;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// Native Wayland window implementation using xdg-shell protocol.
/// Provides full Wayland support without XWayland dependency.
/// </summary>
public partial class WaylandWindow : Microsoft.Maui.Platform.Linux.Services.IDisplayWindow
{
    #region Native Interop - libwayland-client

    private const string LibWaylandClient = "libwayland-client.so.0";

    // Core display functions (actually exported)
    [LibraryImport(LibWaylandClient, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr wl_display_connect(string? name);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_display_disconnect(IntPtr display);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_display_dispatch(IntPtr display);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_display_dispatch_pending(IntPtr display);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_display_roundtrip(IntPtr display);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_display_flush(IntPtr display);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_display_get_fd(IntPtr display);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_display_get_error(IntPtr display);

    // Low-level proxy API (actually exported - used to implement protocol wrappers)
    [LibraryImport(LibWaylandClient)]
    private static partial IntPtr wl_proxy_marshal_constructor(
        IntPtr proxy, uint opcode, IntPtr iface, IntPtr arg);

    // Overload for signatures like "no" (new_id + object) — libwayland's variadic
    // walker reads one slot per signature char, so we must provide a NULL
    // placeholder for the 'n' arg followed by the actual object pointer for 'o'.
    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_constructor")]
    private static partial IntPtr wl_proxy_marshal_constructor(
        IntPtr proxy, uint opcode, IntPtr iface, IntPtr arg1, IntPtr arg2);

    [LibraryImport(LibWaylandClient)]
    private static partial IntPtr wl_proxy_marshal_constructor_versioned(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, IntPtr arg);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_marshal(IntPtr proxy, uint opcode);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_marshal(IntPtr proxy, uint opcode, IntPtr arg1);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_marshal(IntPtr proxy, uint opcode, int arg1, int arg2);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_marshal(IntPtr proxy, uint opcode, IntPtr arg1, int arg2, int arg3);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_marshal(IntPtr proxy, uint opcode, int arg1, int arg2, int arg3, int arg4);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_marshal(IntPtr proxy, uint opcode, uint arg1);

    [DllImport(LibWaylandClient)]
    private static extern void wl_proxy_marshal(IntPtr proxy, uint opcode,
        [MarshalAs(UnmanagedType.LPStr)] string arg1);

    [LibraryImport(LibWaylandClient)]
    private static partial IntPtr wl_proxy_marshal_array_constructor(
        IntPtr proxy, uint opcode, IntPtr args, IntPtr iface);

    [LibraryImport(LibWaylandClient)]
    private static partial IntPtr wl_proxy_marshal_array_constructor_versioned(
        IntPtr proxy, uint opcode, IntPtr args, IntPtr iface, uint version);

    [LibraryImport(LibWaylandClient)]
    private static partial int wl_proxy_add_listener(IntPtr proxy, IntPtr impl, IntPtr data);

    [LibraryImport(LibWaylandClient)]
    private static partial void wl_proxy_destroy(IntPtr proxy);

    [LibraryImport(LibWaylandClient)]
    private static partial uint wl_proxy_get_version(IntPtr proxy);

    // Interface globals (exported as data symbols)
    [LibraryImport(LibWaylandClient)]
    private static partial IntPtr wl_registry_interface_ptr();

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
    [LibraryImport("libdl.so.2", EntryPoint = "dlopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlopen(string? filename, int flags);

    [LibraryImport("libdl.so.2", EntryPoint = "dlsym", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlsym(IntPtr handle, string symbol);

    [LibraryImport("libdl.so.2", EntryPoint = "dlclose")]
    private static partial int dlclose(IntPtr handle);

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
    private const uint WL_SURFACE_SET_BUFFER_SCALE = 8;
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

        // Protocol-extension interfaces (xdg-shell, decoration, fractional-scale)
        // come from libopenmaui_wl.so, which we generate at build time from the
        // wayland-protocols XML via wayland-scanner. Doing it that way (instead of
        // hand-rolled wl_message tables in C#) avoids subtle marshaling bugs since
        // the C bindings are produced by the same scanner that the rest of the
        // ecosystem uses.
        //
        // The .so is shipped next to OpenMaui.Controls.Linux.dll (CopyToOutput in
        // the csproj, runtimes/linux-x64/native/ in the NuGet package). Try the
        // assembly's directory first; fall back to bare name so LD_LIBRARY_PATH
        // overrides still work.
        var protoHandle = TryLoadProtocols();
        if (protoHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                "Failed to load libopenmaui_wl.so (Wayland protocol bindings). " +
                "Expected next to OpenMaui.Controls.Linux.dll. " +
                "Run native/build.sh in the maui-linux source tree to regenerate.");

        _xdg_wm_base_interface = dlsym(protoHandle, "xdg_wm_base_interface");
        _xdg_surface_interface = dlsym(protoHandle, "xdg_surface_interface");
        _xdg_toplevel_interface = dlsym(protoHandle, "xdg_toplevel_interface");
        _zxdg_decoration_manager_v1_interface = dlsym(protoHandle, "zxdg_decoration_manager_v1_interface");
        _zxdg_toplevel_decoration_v1_interface = dlsym(protoHandle, "zxdg_toplevel_decoration_v1_interface");
        _wp_fractional_scale_manager_v1_interface = dlsym(protoHandle, "wp_fractional_scale_manager_v1_interface");
        _wp_fractional_scale_v1_interface = dlsym(protoHandle, "wp_fractional_scale_v1_interface");
        _wp_viewporter_interface = dlsym(protoHandle, "wp_viewporter_interface");
        _wp_viewport_interface = dlsym(protoHandle, "wp_viewport_interface");
    }

    private static IntPtr TryLoadProtocols()
    {
        // Path next to OpenMaui.Controls.Linux.dll
        var asmDir = Path.GetDirectoryName(typeof(WaylandWindow).Assembly.Location);
        if (!string.IsNullOrEmpty(asmDir))
        {
            var p = Path.Combine(asmDir, "libopenmaui_wl.so");
            if (File.Exists(p))
            {
                var h = dlopen(p, RTLD_NOW | RTLD_GLOBAL);
                if (h != IntPtr.Zero) return h;
            }
        }

        // Fall back to bare name so LD_LIBRARY_PATH or system install can serve.
        return dlopen("libopenmaui_wl.so", RTLD_NOW | RTLD_GLOBAL);
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
    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    private static partial IntPtr wl_proxy_marshal_flags(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags,
        uint name, IntPtr ifaceName, uint ifaceVersion);

    private static IntPtr wl_registry_bind(IntPtr registry, uint name, IntPtr iface, uint version)
    {
        // wl_registry.bind has a generic new_id arg — its wire signature expands to
        // (uint name, string interface_name, uint version, new_id). The first
        // variadic slot for the string MUST be the interface name `char*`, not the
        // wl_interface struct pointer. The wl_interface->name is at offset 0 of the
        // struct, so we read the first IntPtr of `iface` to get the name string ptr.
        var ifaceName = Marshal.ReadIntPtr(iface);
        return wl_proxy_marshal_flags(registry, WL_REGISTRY_BIND, iface, version, 0,
            name, ifaceName, version);
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
    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    private static partial IntPtr wl_proxy_marshal_flags_fd(
        IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags,
        IntPtr newId, int fd, int size);

    private static IntPtr wl_shm_create_pool(IntPtr shm, int fd, int size)
    {
        return wl_proxy_marshal_flags_fd(shm, WL_SHM_CREATE_POOL,
            _wl_shm_pool_interface, wl_proxy_get_version(shm), 0,
            IntPtr.Zero, fd, size);
    }

    // wl_shm_pool methods
    [LibraryImport(LibWaylandClient, EntryPoint = "wl_proxy_marshal_flags")]
    private static partial IntPtr wl_proxy_marshal_flags_buffer(
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
        // The interfaces are loaded directly from libopenmaui_wl.so in
        // LoadInterfaceSymbols above; this stub remains so existing call sites
        // (and any future ones) can opt-in without ordering concerns.
        if (_xdg_wm_base_interface != IntPtr.Zero) return;

        // xdg-shell interfaces aren't shipped in libwayland-client; we build full
        // method/event tables here so libwayland can marshal correctly. We bind at
        // version 2; method/event counts and signatures match v2 of the stable
        // xdg-shell protocol (xdg-shell.xml). Higher-version events (configure_bounds,
        // wm_capabilities) are intentionally absent: a v2-bound proxy will never
        // receive them, and listing them with stub signatures risks demarshal errors
        // if the compositor accidentally sends one.

        // We don't construct positioners or popups; their interfaces are stubs that
        // exist only so xdg_surface.get_popup / xdg_wm_base.create_positioner can
        // reference them in the methods table. Stubs are fine because we never
        // actually invoke those requests.
        var positionerStub = BuildInterface("xdg_positioner", 2,
            new MessageDef[] { new("destroy", "", Array.Empty<IntPtr>()) },
            Array.Empty<MessageDef>());
        var popupStub = BuildInterface("xdg_popup", 2,
            new MessageDef[] { new("destroy", "", Array.Empty<IntPtr>()) },
            Array.Empty<MessageDef>());

        // Forward declare addresses so each interface can reference the others.
        // Order: build wm_base last (it references xdg_surface and xdg_positioner).
        // Toplevel and surface must be built before wm_base; surface references
        // toplevel/popup; toplevel references wl_seat and wl_output (from libwayland).

        // xdg_toplevel — 14 requests, 2 events at v2
        _xdg_toplevel_interface = BuildInterface("xdg_toplevel", 2,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("set_parent", "?o", new[] { IntPtr.Zero /* xdg_toplevel — self-ref */ }),
                new("set_title", "s", new[] { IntPtr.Zero }),
                new("set_app_id", "s", new[] { IntPtr.Zero }),
                new("show_window_menu", "ouii", new[] { _wl_seat_interface, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero }),
                new("move", "ou", new[] { _wl_seat_interface, IntPtr.Zero }),
                new("resize", "ouu", new[] { _wl_seat_interface, IntPtr.Zero, IntPtr.Zero }),
                new("set_max_size", "ii", NullTypes(2)),
                new("set_min_size", "ii", NullTypes(2)),
                new("set_maximized", "", Array.Empty<IntPtr>()),
                new("unset_maximized", "", Array.Empty<IntPtr>()),
                new("set_fullscreen", "?o", new[] { IntPtr.Zero /* wl_output — not bound, NULL is fine */ }),
                new("unset_fullscreen", "", Array.Empty<IntPtr>()),
                new("set_minimized", "", Array.Empty<IntPtr>()),
            },
            events: new MessageDef[]
            {
                new("configure", "iia", NullTypes(3)),
                new("close", "", Array.Empty<IntPtr>()),
            });

        // xdg_surface — 5 requests, 1 event at v2
        _xdg_surface_interface = BuildInterface("xdg_surface", 2,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("get_toplevel", "n", new[] { _xdg_toplevel_interface }),
                new("get_popup", "n?oo", new[] { popupStub, IntPtr.Zero /* xdg_surface — self */, positionerStub }),
                new("set_window_geometry", "iiii", NullTypes(4)),
                new("ack_configure", "u", new[] { IntPtr.Zero }),
            },
            events: new MessageDef[]
            {
                new("configure", "u", new[] { IntPtr.Zero }),
            });

        // xdg_wm_base — 4 requests, 1 event at v2
        _xdg_wm_base_interface = BuildInterface("xdg_wm_base", 2,
            methods: new MessageDef[]
            {
                new("destroy", "", Array.Empty<IntPtr>()),
                new("create_positioner", "n", new[] { positionerStub }),
                new("get_xdg_surface", "no", new[] { _xdg_surface_interface, _wl_surface_interface }),
                new("pong", "u", new[] { IntPtr.Zero }),
            },
            events: new MessageDef[]
            {
                new("ping", "u", new[] { IntPtr.Zero }),
            });
    }

    private static IntPtr xdg_wm_base_get_xdg_surface(IntPtr wmBase, IntPtr surface)
    {
        // Signature "no" → IntPtr.Zero placeholder for the new_id, then the surface object.
        return wl_proxy_marshal_constructor(wmBase, XDG_WM_BASE_GET_XDG_SURFACE,
            _xdg_surface_interface, IntPtr.Zero, surface);
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

    [LibraryImport("libc", EntryPoint = "ftruncate")]
    private static partial int ftruncate(int fd, long length);

    [LibraryImport("libc", EntryPoint = "mmap")]
    private static partial IntPtr mmap(IntPtr addr, nuint length, int prot, int flags, int fd, long offset);

    [LibraryImport("libc", EntryPoint = "munmap")]
    private static partial int munmap(IntPtr addr, nuint length);

    [LibraryImport("libc", EntryPoint = "close")]
    private static partial int close(int fd);

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
    private IntPtr _viewporter;
    private IntPtr _viewport;
    private IntPtr _surface;
    private float _bufferToLogicalScale = 1.0f; // == LinuxApplication.DpiScale; cached on init.
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

        // Set title and app_id. app_id determines which .desktop file the
        // compositor matches against for the taskbar icon, so it must align with
        // the .desktop entry that LinuxApplication.InstallDesktopEntry writes
        // (basename = process name lowercased) and with the StartupWMClass
        // (process name as-is). KDE looks up icons by .desktop basename first.
        xdg_toplevel_set_title(_xdgToplevel, _title);
        xdg_toplevel_set_app_id(_xdgToplevel, ResolveAppId());

        // Server-side decorations (when offered by the compositor — KDE, Sway,
        // wlroots-based ones). GNOME announces no decoration manager, so this
        // simply skips and the window will be undecorated until CSD lands.
        RequestServerSideDecorations();

        // Fractional scale (when offered). Lets the compositor send us a precise
        // scale factor instead of forcing us to integer scaling.
        RequestFractionalScale();

        // wp_viewporter: declare a precise *logical* destination size for the
        // surface, decoupled from the buffer's pixel size. Without this, the
        // compositor treats the buffer as being in surface-logical pixels and
        // applies output scale on top — so a 1400x1050 buffer on a 1.75x display
        // would become 2450x1838 actual pixels (huge), or with set_buffer_scale=2
        // we'd get 1225 actual pixels (smaller than X11). With viewporter we
        // pin destination = logical size (e.g. 800x600), buffer can be 1400x1050,
        // and the compositor displays at 800*1.75 = 1400 actual pixels — exactly
        // matching the X11 path.
        if (LinuxApplication.Current is { DpiScale: > 1.01f } app && _viewporter != IntPtr.Zero)
        {
            _bufferToLogicalScale = app.DpiScale;
            // wp_viewporter.get_viewport: opcode 1, signature "no" (new_id, surface).
            _viewport = wl_proxy_marshal_constructor(
                _viewporter, 1, _wp_viewport_interface, IntPtr.Zero, _surface);
            if (_viewport != IntPtr.Zero)
            {
                int logicalW = Math.Max(1, (int)Math.Round(_width / _bufferToLogicalScale));
                int logicalH = Math.Max(1, (int)Math.Round(_height / _bufferToLogicalScale));
                // wp_viewport.set_destination: opcode 2, signature "ii".
                wl_proxy_marshal(_viewport, 2, logicalW, logicalH);
            }
        }

        // Commit empty surface to get initial configure
        wl_surface_commit(_surface);
        wl_display_roundtrip(_display);

        // Create shared memory buffer
        CreateShmBuffer();

        DiagnosticLog.Debug("WaylandWindow", $"Window created: {_width}x{_height}");
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
    private delegate void PointerAxisSourceDelegate(IntPtr data, IntPtr pointer, uint axisSource);
    private delegate void PointerAxisStopDelegate(IntPtr data, IntPtr pointer, uint time, uint axis);
    private delegate void PointerAxisDiscreteDelegate(IntPtr data, IntPtr pointer, uint axis, int discrete);
    private delegate void KeyboardKeymapDelegate(IntPtr data, IntPtr keyboard, uint format, int fd, uint size);
    private delegate void KeyboardEnterDelegate(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface, IntPtr keys);
    private delegate void KeyboardLeaveDelegate(IntPtr data, IntPtr keyboard, uint serial, IntPtr surface);
    private delegate void KeyboardKeyDelegate(IntPtr data, IntPtr keyboard, uint serial, uint time, uint key, uint state);
    private delegate void KeyboardModifiersDelegate(IntPtr data, IntPtr keyboard, uint serial, uint modsDepressed, uint modsLatched, uint modsLocked, uint group);
    private delegate void KeyboardRepeatInfoDelegate(IntPtr data, IntPtr keyboard, int rate, int delay);
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
        DiagnosticLog.Debug("WaylandWindow", $"Global: {interfaceName} v{version}");

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
            case "zxdg_decoration_manager_v1":
                LoadDecorationInterfaces();
                window._decorationManager = wl_registry_bind(registry, name, _zxdg_decoration_manager_v1_interface, 1);
                break;
            case "wp_fractional_scale_manager_v1":
                LoadFractionalScaleInterfaces();
                window._fractionalScaleManager = wl_registry_bind(registry, name, _wp_fractional_scale_manager_v1_interface, 1);
                break;
            case "wp_viewporter":
                window._viewporter = wl_registry_bind(registry, name, _wp_viewporter_interface, 1);
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

        // wl_pointer events at v5: enter, leave, motion, button, axis, frame,
        // axis_source, axis_stop, axis_discrete. libwayland aborts when an event
        // arrives at a NULL slot, so every opcode in the listener struct must
        // point at SOMETHING — even a no-op for events we don't act on.
        _pointerListener = new WlPointerListener
        {
            Enter = Marshal.GetFunctionPointerForDelegate<PointerEnterDelegate>(PointerEnter),
            Leave = Marshal.GetFunctionPointerForDelegate<PointerLeaveDelegate>(PointerLeave),
            Motion = Marshal.GetFunctionPointerForDelegate<PointerMotionDelegate>(PointerMotion),
            Button = Marshal.GetFunctionPointerForDelegate<PointerButtonDelegate>(OnPointerButton),
            Axis = Marshal.GetFunctionPointerForDelegate<PointerAxisDelegate>(PointerAxis),
            Frame = Marshal.GetFunctionPointerForDelegate<PointerFrameDelegate>(PointerFrame),
            AxisSource = Marshal.GetFunctionPointerForDelegate<PointerAxisSourceDelegate>(PointerAxisSource),
            AxisStop = Marshal.GetFunctionPointerForDelegate<PointerAxisStopDelegate>(PointerAxisStop),
            AxisDiscrete = Marshal.GetFunctionPointerForDelegate<PointerAxisDiscreteDelegate>(PointerAxisDiscrete),
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
        // Pointer coords arrive in surface-logical space (wl_fixed = 256ths of a
        // logical pixel). Hit-test bounds live in buffer pixel space. Multiply
        // by _bufferToLogicalScale so SkiaView.HitTest sees the right coordinate.
        var s = window._bufferToLogicalScale;
        window._pointerX = (x / 256.0f) * s;
        window._pointerY = (y / 256.0f) * s;
        // Wayland reverts to the compositor default cursor on every pointer.enter.
        // Re-apply our last-requested cursor so SetCursor's effect persists across
        // window re-entry.
        window.TryApplyCursor(window._pendingCursor);
    }

    private static void PointerLeave(IntPtr data, IntPtr pointer, uint serial, IntPtr surface) { }

    private static void PointerMotion(IntPtr data, IntPtr pointer, uint time, int x, int y)
    {
        // See PointerEnter for the buffer-vs-logical scaling rationale.
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) return;
        var window = (WaylandWindow)handle.Target!;

        var s = window._bufferToLogicalScale;
        window._pointerX = (x / 256.0f) * s;
        window._pointerY = (y / 256.0f) * s;
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

    // No-op handlers for v5 wl_pointer events we don't act on. They must exist
    // so libwayland has a valid function pointer to dispatch to; otherwise it
    // calls wl_abort() at the first event.
    private static void PointerAxisSource(IntPtr data, IntPtr pointer, uint axisSource) { }
    private static void PointerAxisStop(IntPtr data, IntPtr pointer, uint time, uint axis) { }
    private static void PointerAxisDiscrete(IntPtr data, IntPtr pointer, uint axis, int discrete) { }

    private void SetupKeyboard()
    {
        if (_keyboard == IntPtr.Zero) return;

        // Same NULL-slot abort hazard as wl_pointer (see SetupPointer for the
        // explanation). repeat_info was added in wl_keyboard v4 / wl_seat v4 —
        // we bind at v5, so we will receive it.
        _keyboardListener = new WlKeyboardListener
        {
            Keymap = Marshal.GetFunctionPointerForDelegate<KeyboardKeymapDelegate>(KeyboardKeymap),
            Enter = Marshal.GetFunctionPointerForDelegate<KeyboardEnterDelegate>(KeyboardEnter),
            Leave = Marshal.GetFunctionPointerForDelegate<KeyboardLeaveDelegate>(KeyboardLeave),
            Key = Marshal.GetFunctionPointerForDelegate<KeyboardKeyDelegate>(KeyboardKey),
            Modifiers = Marshal.GetFunctionPointerForDelegate<KeyboardModifiersDelegate>(KeyboardModifiers),
            RepeatInfo = Marshal.GetFunctionPointerForDelegate<KeyboardRepeatInfoDelegate>(KeyboardRepeatInfo),
        };
        _keyboardListenerHandle = GCHandle.Alloc(_keyboardListener, GCHandleType.Pinned);
        wl_keyboard_add_listener(_keyboard, _keyboardListenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(_thisHandle));
    }

    private static void KeyboardKeymap(IntPtr data, IntPtr keyboard, uint format, int fd, uint size)
    {
        var handle = GCHandle.FromIntPtr(data);
        if (!handle.IsAllocated) { close(fd); return; }
        var window = (WaylandWindow)handle.Target!;
        window.HandleKeymap(format, fd, size);
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

        var (key, text) = window.TranslateKey(keycode);
        var modifiers = (KeyModifiers)window._modifiers;
        var args = new KeyEventArgs(key, modifiers);

        if (state == WL_KEYBOARD_KEY_STATE_PRESSED)
        {
            window.KeyDown?.Invoke(window, args);
            if (text is not null)
                window.TextInput?.Invoke(window, new TextInputEventArgs(text));
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
        window.HandleModifiers(modsDepressed, modsLatched, modsLocked, group);
    }

    // wl_keyboard.repeat_info — fired by the compositor with the user's key-repeat
    // settings. We don't currently honor it (no auto-repeat) but the slot must be
    // populated to avoid wl_abort.
    private static void KeyboardRepeatInfo(IntPtr data, IntPtr keyboard, int rate, int delay) { }

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
            // configure event widths/heights are in *logical* pixels. The buffer
            // we render into is in physical pixels, scaled up by DpiScale so
            // SkiaRenderingEngine.DpiScale == 1 in surface space. Convert here so
            // resize math keeps the buffer pre-scaled.
            int bufferW = (int)Math.Round(width * window._bufferToLogicalScale);
            int bufferH = (int)Math.Round(height * window._bufferToLogicalScale);

            window._pendingWidth = bufferW;
            window._pendingHeight = bufferH;

            if (window._configured)
            {
                window.ResizeBuffer(bufferW, bufferH);
                // Update viewport destination so the compositor still sees the
                // requested logical size after we re-attach the new-size buffer.
                if (window._viewport != IntPtr.Zero)
                {
                    wl_proxy_marshal(window._viewport, 2, width, height);
                }
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

    public void SetCursor(CursorType cursorType)
    {
        _pendingCursor = cursorType;
        // The pointer may not be over our surface yet (no serial → can't call
        // set_cursor). When we get pointer.enter, we re-apply automatically.
        TryApplyCursor(cursorType);
    }

    public void SetIcon(string iconPath)
    {
        // Wayland has no per-window icon protocol; the desktop file's Icon=
        // entry (matched via app_id) provides the taskbar/launcher icon.
        // Kept on the interface for symmetry with X11.
    }

    public void SetWMClass(string resName, string resClass)
    {
        // Wayland's equivalent is xdg_toplevel.set_app_id (single string,
        // typically the .desktop file basename without extension).
        if (_xdgToplevel != IntPtr.Zero && !string.IsNullOrEmpty(resName))
            xdg_toplevel_set_app_id(_xdgToplevel, resName);
    }

    /// <summary>
    /// Build the app_id the same way X11Window builds WM_CLASS so the compositor's
    /// taskbar icon lookup (by .desktop basename) matches the .desktop entry that
    /// LinuxApplication.InstallDesktopEntry writes during startup.
    /// </summary>
    private static string ResolveAppId()
    {
        var appName = Environment.GetEnvironmentVariable("APPIMAGE_NAME");
        if (string.IsNullOrEmpty(appName))
            appName = System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MauiApp");
        return appName.Replace(" ", "").Replace("_", "");
    }

    public void Present(IntPtr pixels, int width, int height, int stride)
    {
        if (_pixelData == IntPtr.Zero || pixels == IntPtr.Zero) return;

        // Match the renderer's frame to our wl_shm buffer dimensions; if they
        // differ (e.g. mid-resize), grow/shrink the buffer first.
        if (width != _width || height != _height)
            ResizeBuffer(width, height);

        // Copy line-by-line so that source/destination strides can disagree
        // without producing a sheared frame.
        unsafe
        {
            byte* src = (byte*)pixels;
            byte* dst = (byte*)_pixelData;
            int bytesPerLine = Math.Min(stride, _stride);
            for (int y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(src + y * stride, dst + y * _stride, _stride, bytesPerLine);
            }
        }

        CommitFrame();
    }

    public void FlushDeferredResize() { /* X11-only; no deferred-resize coalescing on Wayland */ }

    public void AcknowledgeSync() { /* X11-only; xdg_surface_ack_configure handles sync for Wayland */ }

    private CursorType _pendingCursor = CursorType.Arrow;

    public void ProcessEvents()
    {
        if (!_isRunning || _display == IntPtr.Zero) return;

        wl_display_dispatch_pending(_display);
        wl_display_flush(_display);
    }

    /// <summary>
    /// After poll() reports readability on the Wayland fd, call this to read events
    /// off the wire and dispatch them. Skips the read if the queue is already non-empty
    /// (which happens when callbacks queue more events synchronously).
    /// </summary>
    public void DispatchReadEvents()
    {
        if (!_isRunning || _display == IntPtr.Zero) return;
        // wl_display_dispatch returns immediately if there are queued events; otherwise
        // it reads from the fd. Safe to call after poll() since fd is known readable.
        wl_display_dispatch(_display);
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

        DisposeCursor();
        DisposeXkb();
        DisposeDecoration();

        if (_viewport != IntPtr.Zero)
        {
            wl_proxy_destroy(_viewport);
            _viewport = IntPtr.Zero;
        }
        if (_viewporter != IntPtr.Zero)
        {
            wl_proxy_destroy(_viewporter);
            _viewporter = IntPtr.Zero;
        }
        DisposeFractionalScale();

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
