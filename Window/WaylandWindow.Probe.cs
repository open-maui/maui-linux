// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>
/// Diagnostic probes (openmaui doctor). These open their OWN short-lived
/// wl_display and never touch a live WaylandWindow, its registry, or the
/// static interface tables the window path initialises.
/// </summary>
public partial class WaylandWindow
{
    /// <summary>One advertised Wayland global.</summary>
    internal readonly record struct WaylandGlobalInfo(string Interface, uint Version);

    /// <summary>Result of <see cref="ProbeCompositor"/>.</summary>
    internal sealed class WaylandProbeResult
    {
        public List<WaylandGlobalInfo> Globals { get; } = new();

        /// <summary>Compositor process (SO_PEERCRED on the display socket), e.g. "kwin_wayland".</summary>
        public string? CompositorProcess { get; set; }

        public int? CompositorPid { get; set; }
    }

    private const int SOL_SOCKET = 1;
    private const int SO_PEERCRED = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct UCred
    {
        public int Pid;
        public uint Uid;
        public uint Gid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo
    {
        public IntPtr FileName;
        public IntPtr FileBase;
        public IntPtr SymbolName;
        public IntPtr SymbolAddress;
    }

    [LibraryImport("libc.so.6", EntryPoint = "getsockopt")]
    private static partial int getsockopt_peercred(int fd, int level, int optname, out UCred cred, ref uint optlen);

    [LibraryImport("libc.so.6", EntryPoint = "dladdr")]
    private static partial int dladdr(IntPtr address, out DlInfo info);

    /// <summary>
    /// Connects a private wl_display, lists every advertised global after one
    /// roundtrip, identifies the compositor process, runs
    /// <paramref name="whileConnected"/> with the display (so EGL can be probed
    /// on it), then tears everything down. Throws when libwayland-client is
    /// missing or the connection fails; callers wrap it.
    /// </summary>
    internal static WaylandProbeResult ProbeCompositor(Action<IntPtr>? whileConnected = null)
    {
        var result = new WaylandProbeResult();

        // Resolve wl_registry_interface into a LOCAL: _wl_registry_interface is
        // the "already loaded" sentinel for LoadInterfaceSymbols, so setting it
        // here would make a later window skip loading the other tables.
        var lib = dlopen(LibWaylandClient, RTLD_NOW | RTLD_GLOBAL);
        if (lib == IntPtr.Zero)
            throw new DllNotFoundException($"{LibWaylandClient} could not be loaded");
        var registryInterface = dlsym(lib, "wl_registry_interface");
        if (registryInterface == IntPtr.Zero)
            throw new EntryPointNotFoundException("wl_registry_interface not exported by libwayland-client");

        var display = wl_display_connect(null);
        if (display == IntPtr.Zero)
            throw new InvalidOperationException("wl_display_connect failed (is WAYLAND_DISPLAY valid and the compositor running?)");

        IntPtr registry = IntPtr.Zero;
        GCHandle listHandle = default;
        GCHandle listenerHandle = default;
        RegistryGlobalDelegate onGlobal = ProbeRegistryGlobal;
        RegistryGlobalRemoveDelegate onRemove = static (_, _, _) => { };
        try
        {
            try
            {
                int fd = wl_display_get_fd(display);
                uint len = (uint)Marshal.SizeOf<UCred>();
                if (fd >= 0 && getsockopt_peercred(fd, SOL_SOCKET, SO_PEERCRED, out var cred, ref len) == 0 && cred.Pid > 0)
                {
                    result.CompositorPid = cred.Pid;
                    var comm = $"/proc/{cred.Pid}/comm";
                    if (File.Exists(comm))
                        result.CompositorProcess = File.ReadAllText(comm).Trim();
                }
            }
            catch
            {
                // Compositor identity is best-effort.
            }

            registry = wl_proxy_marshal_constructor(display, WL_DISPLAY_GET_REGISTRY, registryInterface, IntPtr.Zero);
            if (registry == IntPtr.Zero)
                throw new InvalidOperationException("wl_display.get_registry failed");

            listHandle = GCHandle.Alloc(result.Globals);
            var listener = new WlRegistryListener
            {
                Global = Marshal.GetFunctionPointerForDelegate(onGlobal),
                GlobalRemove = Marshal.GetFunctionPointerForDelegate(onRemove),
            };
            listenerHandle = GCHandle.Alloc(listener, GCHandleType.Pinned);
            wl_proxy_add_listener(registry, listenerHandle.AddrOfPinnedObject(), GCHandle.ToIntPtr(listHandle));

            if (wl_display_roundtrip(display) < 0)
                throw new InvalidOperationException("wl_display_roundtrip failed");

            // Globals are all delivered by the first roundtrip; detach the
            // registry before anything else (EGL) uses the connection.
            wl_proxy_destroy(registry);
            registry = IntPtr.Zero;

            whileConnected?.Invoke(display);
        }
        finally
        {
            if (registry != IntPtr.Zero) wl_proxy_destroy(registry);
            wl_display_disconnect(display);
            if (listenerHandle.IsAllocated) listenerHandle.Free();
            if (listHandle.IsAllocated) listHandle.Free();
            GC.KeepAlive(onGlobal);
            GC.KeepAlive(onRemove);
        }

        result.Globals.Sort((a, b) => string.CompareOrdinal(a.Interface, b.Interface));
        return result;
    }

    private static void ProbeRegistryGlobal(IntPtr data, IntPtr registry, uint name, IntPtr iface, uint version)
    {
        try
        {
            if (GCHandle.FromIntPtr(data).Target is List<WaylandGlobalInfo> list)
                list.Add(new WaylandGlobalInfo(Marshal.PtrToStringAnsi(iface) ?? "?", version));
        }
        catch
        {
            // Never let an exception unwind into libwayland.
        }
    }

    /// <summary>
    /// Loads libopenmaui_wl.so through the same search the window path uses
    /// and returns the file it resolved to (null when it cannot be loaded).
    /// </summary>
    internal static string? ProbeProtocolShim(out IReadOnlyList<string> missingSymbols)
    {
        var missing = new List<string>();
        missingSymbols = missing;

        var handle = TryLoadProtocols();
        if (handle == IntPtr.Zero)
            return null;

        string? path = null;
        foreach (var symbol in new[]
        {
            "xdg_wm_base_interface",
            "zxdg_decoration_manager_v1_interface",
            "wp_fractional_scale_manager_v1_interface",
            "wp_viewporter_interface",
            "zwp_text_input_manager_v3_interface",
            "zwp_primary_selection_device_manager_v1_interface",
        })
        {
            var address = dlsym(handle, symbol);
            if (address == IntPtr.Zero)
            {
                missing.Add(symbol);
                continue;
            }
            if (path == null && dladdr(address, out var info) != 0 && info.FileName != IntPtr.Zero)
                path = Marshal.PtrToStringUTF8(info.FileName);
        }

        return path ?? "libopenmaui_wl.so";
    }
}
