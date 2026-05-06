// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Window;

// Helper for building wl_interface descriptors that libwayland-client can actually
// marshal against. The dlsym'd interfaces from libwayland-client (wl_compositor,
// wl_surface, wl_seat, etc.) come with full wl_message tables baked in. Protocol
// extensions like xdg-shell, zxdg-decoration-v1, wp-fractional-scale-v1 are not
// in libwayland-client and must be declared by the client — wayland-scanner does
// this in C apps; we do it manually here.
//
// libwayland reads `proxy->object.interface->methods[opcode].signature` on every
// outgoing request, so the methods table cannot be NULL or libwayland segfaults
// on the first call. Same for events and the events table during dispatch.
public partial class WaylandWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WlMessage
    {
        public IntPtr Name;       // const char*
        public IntPtr Signature;  // const char* (e.g. "n", "u", "?o", "iiii")
        public IntPtr Types;      // const wl_interface** — pointer to array of wl_interface*
    }

    /// <summary>
    /// One request or event in a protocol's wl_interface description.
    /// </summary>
    private readonly struct MessageDef
    {
        public readonly string Name;
        /// <summary>
        /// libwayland signature: "i" int, "u" uint, "f" fixed, "s" string,
        /// "o" object, "n" new_id, "a" array, "h" fd. Prefix "?" = nullable.
        /// Numbers like "2" before letters indicate version-since.
        /// </summary>
        public readonly string Signature;
        /// <summary>
        /// One <see cref="IntPtr"/> per arg in the signature; for primitive args
        /// (int/uint/string/etc.) use <see cref="IntPtr.Zero"/>; for object/new_id
        /// args, the wl_interface* of the referenced type.
        /// </summary>
        public readonly IntPtr[] Types;

        public MessageDef(string name, string signature, IntPtr[] types)
        {
            Name = name;
            Signature = signature;
            Types = types;
        }
    }

    // GCHandles for everything we pin so it stays alive for the process lifetime.
    // These interfaces never need to be freed; the cost is a fixed handful of
    // allocations at startup.
    private static readonly List<GCHandle> s_pinnedHandles = new();
    private static readonly List<IntPtr> s_pinnedStrings = new();

    /// <summary>
    /// Builds a complete wl_interface descriptor (with methods/events tables) and
    /// returns a pointer to the unmanaged struct. Uses direct AllocHGlobal +
    /// StructureToPtr so the layout is exactly what libwayland expects, with no
    /// boxing or padding from .NET object headers.
    /// </summary>
    private static IntPtr BuildInterface(string name, int version,
        MessageDef[] methods, MessageDef[] events)
    {
        var methodsPtr = BuildMessageArray(methods);
        var eventsPtr = BuildMessageArray(events);

        var iface = new WlInterface
        {
            Name = AllocPinnedAnsi(name),
            Version = version,
            MethodCount = methods.Length,
            Methods = methodsPtr,
            EventCount = events.Length,
            Events = eventsPtr,
        };

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<WlInterface>());
        Marshal.StructureToPtr(iface, ptr, fDeleteOld: false);
        s_pinnedStrings.Add(ptr); // track for lifetime; never freed
        return ptr;
    }

    private static IntPtr BuildMessageArray(MessageDef[] defs)
    {
        if (defs.Length == 0)
            return IntPtr.Zero;

        // Allocate a contiguous unmanaged array of wl_message structs so the
        // wl_interface->methods[opcode] indexing libwayland does is correct.
        int size = Marshal.SizeOf<WlMessage>();
        var arr = Marshal.AllocHGlobal(size * defs.Length);
        s_pinnedStrings.Add(arr);

        for (int i = 0; i < defs.Length; i++)
        {
            var msg = new WlMessage
            {
                Name = AllocPinnedAnsi(defs[i].Name),
                Signature = AllocPinnedAnsi(defs[i].Signature),
                Types = PinIntPtrArray(defs[i].Types),
            };
            Marshal.StructureToPtr(msg, arr + (i * size), fDeleteOld: false);
        }
        return arr;
    }

    private static IntPtr PinIntPtrArray(IntPtr[] arr)
    {
        if (arr.Length == 0)
            return IntPtr.Zero;
        // Copy into unmanaged memory so the layout is stable regardless of GC moves.
        var ptr = Marshal.AllocHGlobal(IntPtr.Size * arr.Length);
        s_pinnedStrings.Add(ptr);
        for (int i = 0; i < arr.Length; i++)
        {
            Marshal.WriteIntPtr(ptr, i * IntPtr.Size, arr[i]);
        }
        return ptr;
    }

    private static IntPtr AllocPinnedAnsi(string s)
    {
        var p = Marshal.StringToHGlobalAnsi(s);
        s_pinnedStrings.Add(p);
        return p;
    }

    // Convenience for arg types arrays where every slot is "no associated wl_interface"
    // (e.g. signature "iiii" or "uu"). Sized to the count of args in the signature.
    private static IntPtr[] NullTypes(int count)
    {
        var a = new IntPtr[count];
        for (int i = 0; i < count; i++) a[i] = IntPtr.Zero;
        return a;
    }
}
