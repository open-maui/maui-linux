// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>One request the X11 window sends to bring itself to the front.</summary>
internal enum X11ActivationStep
{
    /// <summary>XMapRaised: shows an unmapped (minimized or hidden) window on top.</summary>
    MapRaised,
    /// <summary>_NET_ACTIVE_WINDOW to the root window (EWMH window managers).</summary>
    SendNetActiveWindow,
    /// <summary>XRaiseWindow (no EWMH).</summary>
    RaiseWindow,
    /// <summary>XSetInputFocus (no EWMH, window viewable).</summary>
    SetInputFocus,
    /// <summary>XSetInputFocus once MapNotify arrives (no EWMH; focusing an unviewable window is BadMatch).</summary>
    FocusOnMap,
}

/// <summary>
/// The decisions and message payloads behind <see cref="X11Window.Activate"/> and X11 startup
/// notification (DESKTOP_STARTUP_ID), kept free of Xlib so they can be tested headless.
/// </summary>
internal static class X11Activation
{
    /// <summary>EWMH source indication: a normal application.</summary>
    public const long SourceApplication = 1;

    /// <summary>The requests that activate the window.</summary>
    public static IReadOnlyList<X11ActivationStep> Plan(bool wmSupportsNetActiveWindow, bool isMapped)
    {
        var steps = new List<X11ActivationStep>(3);
        if (!isMapped)
            steps.Add(X11ActivationStep.MapRaised);
        if (wmSupportsNetActiveWindow)
        {
            steps.Add(X11ActivationStep.SendNetActiveWindow);
        }
        else if (isMapped)
        {
            steps.Add(X11ActivationStep.RaiseWindow);
            steps.Add(X11ActivationStep.SetInputFocus);
        }
        else
        {
            steps.Add(X11ActivationStep.FocusOnMap);
        }
        return steps;
    }

    /// <summary>
    /// data.l of the _NET_ACTIVE_WINDOW client message: source indication, the timestamp of the
    /// user action that caused the request (0 = CurrentTime) and the requestor's currently
    /// active window (0 = none).
    /// </summary>
    public static long[] NetActiveWindowData(ulong userTime, long currentActiveWindow = 0)
        => new long[] { SourceApplication, unchecked((long)userTime), currentActiveWindow, 0, 0 };

    /// <summary>
    /// The launch timestamp a startup id carries (<c>..._TIME&lt;n&gt;</c>, the convention of
    /// libstartup-notification and GLib), used as the window's first user time.
    /// </summary>
    public static bool TryParseStartupTime(string? startupId, out ulong time)
    {
        time = 0;
        if (string.IsNullOrEmpty(startupId))
            return false;
        int index = startupId.LastIndexOf("_TIME", StringComparison.Ordinal);
        if (index < 0)
            return false;
        var digits = startupId.AsSpan(index + 5);
        int length = 0;
        while (length < digits.Length && char.IsAsciiDigit(digits[length]))
            length++;
        return length > 0 && ulong.TryParse(digits[..length], out time);
    }

    /// <summary>
    /// The startup-notification "remove" message for <paramref name="startupId"/>, as the
    /// 20-byte format-8 pieces it travels in: the first as _NET_STARTUP_INFO_BEGIN, the rest as
    /// _NET_STARTUP_INFO. The message is NUL-terminated and the last piece zero-padded.
    /// </summary>
    public static List<byte[]> StartupRemoveChunks(string startupId)
    {
        var message = Encoding.UTF8.GetBytes("remove: ID=" + QuoteStartupValue(startupId));
        var bytes = new byte[message.Length + 1];
        message.CopyTo(bytes, 0);

        var chunks = new List<byte[]>();
        for (int offset = 0; offset < bytes.Length; offset += 20)
        {
            var chunk = new byte[20];
            Array.Copy(bytes, offset, chunk, 0, Math.Min(20, bytes.Length - offset));
            chunks.Add(chunk);
        }
        return chunks;
    }

    /// <summary>A startup-notification value: quoted, with backslash and quote escaped.</summary>
    public static string QuoteStartupValue(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            if (c is '"' or '\\')
                sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Packs a 20-byte format-8 piece into the first three data.l slots of an XClientMessageEvent
    /// (Xlib's data union: b[20] overlays l[0], l[1] and the low half of l[2] on LP64).
    /// </summary>
    public static (long L0, long L1, long L2) PackFormat8(byte[] chunk)
    {
        if (chunk.Length != 20)
            throw new ArgumentException("A format-8 client message carries 20 bytes.", nameof(chunk));
        return (BitConverter.ToInt64(chunk, 0), BitConverter.ToInt64(chunk, 8), (long)BitConverter.ToUInt32(chunk, 16));
    }
}
