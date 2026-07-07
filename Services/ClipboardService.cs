// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux clipboard implementation.
///
/// Backend preference:
///   1. Native wl_data_device_manager (when WaylandWindow has wired it).
///      Zero subprocess overhead and works without `wl-clipboard` installed.
///   2. wl-paste / wl-copy subprocess (Wayland fallback for compositors that
///      don't expose wl_data_device_manager, or when the WaylandWindow hasn't
///      initialized the data device yet).
///   3. xclip → xsel (X11).
/// </summary>
public class ClipboardService : IClipboard
{
    private string? _lastSetText;

    // Non-blocking: answers from the native backend's tracked state (or the
    // last text we set ourselves on subprocess backends). A sync-over-async
    // read here would block the GLib main loop and can deadlock against our
    // own selection source's send event.
    public bool HasText =>
        WaylandWindow.NativeClipboardAvailable
            ? WaylandWindow.NativeClipboardHasText
            : !string.IsNullOrEmpty(_lastSetText);

    public event EventHandler<EventArgs>? ClipboardContentChanged;

    public async Task<string?> GetTextAsync()
    {
        // Native Wayland path — only available once WaylandWindow has bound
        // wl_data_device_manager and the seat. Zero subprocess overhead.
        if (WaylandWindow.NativeClipboardAvailable)
        {
            var native = await WaylandWindow.TryGetClipboardTextAsync();
            if (native != null) return native;
            // Fall through to wl-paste only if native returned no result
            // (no offer, no matching MIME, or read timed out).
        }

        // Wayland subprocess fallback
        if (IsWayland)
        {
            var result = await TryGetWithWlPaste();
            if (result != null) return result;
        }

        // Try xclip
        var xclipResult = await TryGetWithXclip();
        if (xclipResult != null) return xclipResult;

        // Try xsel as fallback
        return await TryGetWithXsel();
    }

    public async Task SetTextAsync(string? text)
    {
        _lastSetText = text;

        if (string.IsNullOrEmpty(text))
        {
            await ClearClipboard();
            return;
        }

        bool success = false;

        // Native Wayland path first.
        if (WaylandWindow.NativeClipboardAvailable)
        {
            success = WaylandWindow.TrySetClipboardText(text);
        }

        // Wayland subprocess fallback (when native path is unavailable or
        // failed — e.g. compositor doesn't expose wl_data_device_manager).
        if (!success && IsWayland)
        {
            success = await TrySetWithWlCopy(text);
        }

        if (!success)
        {
            // Try xclip
            success = await TrySetWithXclip(text);
        }

        if (!success)
        {
            // Try xsel as fallback
            await TrySetWithXsel(text);
        }

        ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsWayland =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private async Task<string?> TryGetWithWlPaste()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wl-paste",
                Arguments = "--no-newline",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> TrySetWithWlCopy(string text)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wl-copy",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> TryGetWithXclip()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xclip",
                Arguments = "-selection clipboard -o",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetWithXsel()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xsel",
                Arguments = "--clipboard --output",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> TrySetWithXclip(string text)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xclip",
                Arguments = "-selection clipboard",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TrySetWithXsel(string text)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xsel",
                Arguments = "--clipboard --input",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task ClearClipboard()
    {
        try
        {
            if (IsWayland)
            {
                await TrySetWithWlCopy("");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "xclip",
                Arguments = "-selection clipboard",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.StandardInput.Close();
                await process.WaitForExitAsync();
            }
        }
        catch
        {
            // Ignore errors when clearing
        }
    }
}
