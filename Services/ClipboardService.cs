// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux clipboard implementation using xclip/xsel command line tools.
/// </summary>
public class ClipboardService : IClipboard
{
    private string? _lastSetText;

    public bool HasText
    {
        get
        {
            try
            {
                var result = GetTextAsync().GetAwaiter().GetResult();
                return !string.IsNullOrEmpty(result);
            }
            catch
            {
                return false;
            }
        }
    }

    public event EventHandler<EventArgs>? ClipboardContentChanged;

    public async Task<string?> GetTextAsync()
    {
        // Try xclip first
        var result = await TryGetWithXclip();
        if (result != null) return result;

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

        // Try xclip first
        var success = await TrySetWithXclip(text);
        if (!success)
        {
            // Try xsel as fallback
            await TrySetWithXsel(text);
        }

        ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
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
            // Try xclip first
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
