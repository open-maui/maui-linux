// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Interop;

/// <summary>
/// XRandR (X Resize and Rotate) extension interop for multi-monitor support.
/// </summary>
internal static partial class XRandR
{
    private const string LibXrandr = "libXrandr.so.2";

    // RROutput and RRCrtc are XIDs (unsigned long)
    // RRMode is also an XID

    [LibraryImport(LibXrandr)]
    public static partial IntPtr XRRGetScreenResources(IntPtr display, IntPtr window);

    [LibraryImport(LibXrandr)]
    public static partial IntPtr XRRGetScreenResourcesCurrent(IntPtr display, IntPtr window);

    [LibraryImport(LibXrandr)]
    public static partial void XRRFreeScreenResources(IntPtr resources);

    [LibraryImport(LibXrandr)]
    public static partial IntPtr XRRGetOutputInfo(IntPtr display, IntPtr resources, ulong output);

    [LibraryImport(LibXrandr)]
    public static partial void XRRFreeOutputInfo(IntPtr outputInfo);

    [LibraryImport(LibXrandr)]
    public static partial IntPtr XRRGetCrtcInfo(IntPtr display, IntPtr resources, ulong crtc);

    [LibraryImport(LibXrandr)]
    public static partial void XRRFreeCrtcInfo(IntPtr crtcInfo);

    [LibraryImport(LibXrandr)]
    public static partial int XRRQueryExtension(IntPtr display, out int eventBase, out int errorBase);

    [LibraryImport(LibXrandr)]
    public static partial int XRRQueryVersion(IntPtr display, out int major, out int minor);

    [LibraryImport(LibXrandr)]
    public static partial void XRRSelectInput(IntPtr display, IntPtr window, int mask);

    // RRNotify mask values
    public const int RRScreenChangeNotifyMask = 1 << 0;
    public const int RRCrtcChangeNotifyMask = 1 << 1;
    public const int RROutputChangeNotifyMask = 1 << 2;
    public const int RROutputPropertyNotifyMask = 1 << 3;

    // Connection status
    public const int RR_Connected = 0;
    public const int RR_Disconnected = 1;
    public const int RR_UnknownConnection = 2;
}

/// <summary>
/// XRRScreenResources structure layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XRRScreenResources
{
    public ulong Timestamp;
    public ulong ConfigTimestamp;
    public int NCrtc;
    public IntPtr Crtcs;        // RRCrtc* (array of ulongs)
    public int NOutput;
    public IntPtr Outputs;      // RROutput* (array of ulongs)
    public int NMode;
    public IntPtr Modes;        // XRRModeInfo*
}

/// <summary>
/// XRROutputInfo structure layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XRROutputInfo
{
    public ulong Timestamp;
    public ulong Crtc;          // RRCrtc - current CRTC (0 if not connected)
    public IntPtr Name;         // char*
    public int NameLen;
    public ulong MmWidth;       // Physical width in mm
    public ulong MmHeight;      // Physical height in mm
    public ushort Connection;   // RRConnection status
    public ushort SubpixelOrder;
    public int NCrtc;
    public IntPtr Crtcs;        // RRCrtc* - possible CRTCs
    public int NClone;
    public IntPtr Clones;       // RROutput*
    public int NMode;
    public int NPreferred;
    public IntPtr Modes;        // RRMode*
}

/// <summary>
/// XRRCrtcInfo structure layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XRRCrtcInfo
{
    public ulong Timestamp;
    public int X;
    public int Y;
    public uint Width;
    public uint Height;
    public ulong Mode;          // RRMode - current mode
    public ushort Rotation;
    public int NOutput;
    public IntPtr Outputs;      // RROutput*
    public ushort Rotations;    // Possible rotations
    public int NPossible;
    public IntPtr Possible;     // RROutput*
}

/// <summary>
/// XRRModeInfo structure layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XRRModeInfo
{
    public ulong Id;            // RRMode
    public uint Width;
    public uint Height;
    public ulong DotClock;
    public uint HSyncStart;
    public uint HSyncEnd;
    public uint HTotal;
    public uint HSkew;
    public uint VSyncStart;
    public uint VSyncEnd;
    public uint VTotal;
    public IntPtr Name;         // char*
    public uint NameLength;
    public ulong ModeFlags;
}
