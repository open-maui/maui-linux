// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Single choke point for the Essentials services that hand work to an external
/// desktop helper (xdg-open, spd-say, xdg-screensaver, zenity, ...). Production
/// behaviour is a plain <see cref="Process.Start(ProcessStartInfo)"/>; tests set
/// <see cref="LaunchOverride"/> to capture the <see cref="ProcessStartInfo"/> and
/// decide the outcome without touching the desktop.
/// </summary>
internal static class ExternalProcess
{
    /// <summary>
    /// When set, replaces the real launch. Return <c>true</c> to simulate a helper
    /// that started and exited successfully, <c>false</c> to simulate a launch
    /// failure (binary missing, non-zero exit). Always reset in a finally block.
    /// </summary>
    internal static Func<ProcessStartInfo, bool>? LaunchOverride;

    /// <summary>
    /// Fire-and-forget launch. Returns <c>true</c> when the helper process was
    /// started; the caller does not wait for it to exit.
    /// </summary>
    public static bool TryStart(ProcessStartInfo psi)
    {
        var over = LaunchOverride;
        if (over != null)
            return over(psi);

        try
        {
            using var process = Process.Start(psi);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Launch and wait for exit. Returns the exit code, or <c>null</c> when the
    /// helper could not be started. Under <see cref="LaunchOverride"/> a
    /// <c>true</c> result maps to exit code 0 and <c>false</c> to <c>null</c>.
    /// Exceptions from starting the process propagate so callers that surface
    /// launch failures (Email, Share) keep their existing error behaviour.
    /// </summary>
    public static async Task<int?> RunAsync(ProcessStartInfo psi, CancellationToken cancellationToken = default)
    {
        var over = LaunchOverride;
        if (over != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return over(psi) ? 0 : null;
        }

        using var process = Process.Start(psi);
        if (process == null)
            return null;

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}

/// <summary>
/// Root of the sysfs tree the hardware-backed Essentials services read
/// (battery, vibrator, torch). Tests point it at a temp directory with fake
/// nodes; production leaves it at <c>/sys</c>.
/// </summary>
internal static class Sysfs
{
    internal static string Root = "/sys";

    internal static string PathOf(params string[] segments)
        => Path.Combine(new[] { Root }.Concat(segments).ToArray());
}
