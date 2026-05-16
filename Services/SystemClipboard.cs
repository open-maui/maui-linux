// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Static helper for system clipboard access. Used directly by SkiaEntry / SkiaEditor
/// Ctrl+C / Ctrl+V handlers, which need a synchronous API rather than the
/// Task-returning IClipboard.
///
/// Backend preference matches ClipboardService:
///   1. Native wl_data_device_manager when WaylandWindow has wired it (zero
///      subprocess overhead, works without wl-clipboard package).
///   2. wl-paste / wl-copy / xclip / xsel subprocess fallbacks.
/// </summary>
public static class SystemClipboard
{
    /// <summary>
    /// Gets text from the system clipboard.
    /// </summary>
    public static string? GetText()
    {
        // Native Wayland path — synchronous wrapper around the async native call.
        // Safe to block here: the pipe read runs on a thread-pool thread and the
        // compositor writes asynchronously, so the main thread isn't deadlocked.
        if (WaylandWindow.NativeClipboardAvailable)
        {
            try
            {
                var native = WaylandWindow.TryGetClipboardTextAsync().GetAwaiter().GetResult();
                if (native != null) return native;
            }
            catch
            {
                // Fall through to subprocess paths
            }
        }

        // Try xclip first (X11)
        var result = TryGetWithXclip();
        if (result != null) return result;

        // Try xsel as fallback (X11)
        result = TryGetWithXsel();
        if (result != null) return result;

        // Try wl-paste for Wayland (when native path failed or isn't ready yet)
        return TryGetWithWlPaste();
    }

    /// <summary>
    /// Sets text to the system clipboard.
    /// </summary>
    public static void SetText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            ClearClipboard();
            return;
        }

        // Native Wayland path
        if (WaylandWindow.NativeClipboardAvailable)
        {
            try
            {
                if (WaylandWindow.TrySetClipboardText(text)) return;
            }
            catch
            {
                // Fall through to subprocess paths
            }
        }

        // Try xclip first (X11)
        if (TrySetWithXclip(text)) return;

        // Try xsel as fallback (X11)
        if (TrySetWithXsel(text)) return;

        // Try wl-copy for Wayland (when native path failed or isn't ready yet)
        TrySetWithWlCopy(text);
    }

    private static string? TryGetWithXclip()
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

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetWithXsel()
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

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetWithWlPaste()
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

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetWithXclip(string text)
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

            process.StandardInput.Write(text);
            process.StandardInput.Close();

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetWithXsel(string text)
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

            process.StandardInput.Write(text);
            process.StandardInput.Close();

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetWithWlCopy(string text)
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

            process.StandardInput.Write(text);
            process.StandardInput.Close();

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ClearClipboard()
    {
        try
        {
            // Try xclip
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
                process.WaitForExit(1000);
            }
        }
        catch
        {
            // Ignore errors when clearing
        }
    }
}
