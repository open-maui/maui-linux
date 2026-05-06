// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform;

/// <summary>
/// Static helper for system clipboard access using xclip/xsel.
/// Provides synchronous access for use in UI event handlers.
/// </summary>
public static class SystemClipboard
{
    /// <summary>
    /// Gets text from the system clipboard.
    /// </summary>
    public static string? GetText()
    {
        // Try xclip first
        var result = TryGetWithXclip();
        if (result != null) return result;

        // Try xsel as fallback
        result = TryGetWithXsel();
        if (result != null) return result;

        // Try wl-paste for Wayland
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

        // Try xclip first
        if (TrySetWithXclip(text)) return;

        // Try xsel as fallback
        if (TrySetWithXsel(text)) return;

        // Try wl-copy for Wayland
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
