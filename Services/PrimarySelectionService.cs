// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Public Linux-specific surface for the X11/Wayland *primary selection* — the
/// middle-click paste buffer. Distinct from <see cref="ClipboardService"/>
/// which models the explicit Ctrl+C/Ctrl+V clipboard. On Linux desktops both
/// exist concurrently; this service is what apps use to keep the middle-click
/// behavior working when they manage selection-driven copies internally.
///
/// MAUI's <c>IClipboard</c> abstraction has no primary-selection concept (it's
/// Linux-specific), so this lives as a Linux-only service that consumers grab
/// via <see cref="PrimarySelectionService.Default"/> or DI. SkiaEntry / SkiaEditor
/// can opt into pushing their selection here on mouse-up and pulling on
/// middle-button-press.
///
/// Backend preference, matching <see cref="ClipboardService"/>:
///   1. Native <c>zwp_primary_selection_v1</c> (modern Wayland compositors).
///   2. <c>wl-paste --primary</c> / <c>wl-copy --primary</c> subprocess.
///   3. <c>xclip -selection primary</c> → <c>xsel --primary</c> (X11).
/// </summary>
public class PrimarySelectionService
{
    private static readonly Lazy<PrimarySelectionService> s_default = new(() => new PrimarySelectionService());

    /// <summary>Shared instance — the same one MAUI handlers/services use.</summary>
    public static PrimarySelectionService Default => s_default.Value;

    /// <summary>
    /// True when there's text currently in the primary selection. Answers from
    /// the native backend's already-tracked state only — no pipe I/O, no
    /// subprocesses — because a sync-over-async read here would block the GLib
    /// main loop and can deadlock against our own selection source. Returns
    /// false when only the subprocess backends are available (they cannot be
    /// probed without blocking).
    /// </summary>
    public bool HasText =>
        WaylandWindow.NativePrimarySelectionAvailable && WaylandWindow.NativePrimarySelectionHasText;

    /// <summary>Read the primary selection. Returns null on any failure.</summary>
    public async Task<string?> GetTextAsync()
    {
        if (WaylandWindow.NativePrimarySelectionAvailable)
        {
            var native = await WaylandWindow.TryGetPrimarySelectionTextAsync();
            if (native != null) return native;
        }

        if (IsWayland)
        {
            var wlResult = await TryGetWithWlPastePrimary();
            if (wlResult != null) return wlResult;
        }

        var xclipResult = await TryGetWithXclipPrimary();
        if (xclipResult != null) return xclipResult;

        return await TryGetWithXselPrimary();
    }

    /// <summary>
    /// Push text to the primary selection. Apps usually call this on mouse-up
    /// of a drag-selection so middle-click in another app pastes the latest.
    /// </summary>
    public async Task SetTextAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            // Null/empty clears the selection: set_selection with a NULL source
            // natively, --clear flags on the subprocess fallbacks (xclip has no
            // clear operation, so it is skipped).
            if (WaylandWindow.NativePrimarySelectionAvailable && WaylandWindow.TryClearPrimarySelection())
                return;
            if (IsWayland && await RunFeedStdin("wl-copy", "--primary --clear", string.Empty))
                return;
            await RunFeedStdin("xsel", "--primary --clear", string.Empty);
            return;
        }

        bool ok = false;
        if (WaylandWindow.NativePrimarySelectionAvailable)
            ok = WaylandWindow.TrySetPrimarySelectionText(text);

        if (!ok && IsWayland)
            ok = await TrySetWithWlCopyPrimary(text);

        if (!ok)
            ok = await TrySetWithXclipPrimary(text);

        if (!ok)
            await TrySetWithXselPrimary(text);
    }

    private static bool IsWayland =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private static async Task<string?> TryGetWithWlPastePrimary() =>
        await RunCaptureStdout("wl-paste", "--primary --no-newline");

    private static async Task<bool> TrySetWithWlCopyPrimary(string text) =>
        await RunFeedStdin("wl-copy", "--primary", text);

    private static async Task<string?> TryGetWithXclipPrimary() =>
        await RunCaptureStdout("xclip", "-selection primary -o");

    private static async Task<bool> TrySetWithXclipPrimary(string text) =>
        await RunFeedStdin("xclip", "-selection primary", text);

    private static async Task<string?> TryGetWithXselPrimary() =>
        await RunCaptureStdout("xsel", "--primary --output");

    private static async Task<bool> TrySetWithXselPrimary(string text) =>
        await RunFeedStdin("xsel", "--primary --input", text);

    private static async Task<string?> RunCaptureStdout(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (p == null) return null;
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return p.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    private static async Task<bool> RunFeedStdin(string file, string args, string text)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (p == null) return false;
            await p.StandardInput.WriteAsync(text);
            p.StandardInput.Close();
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
