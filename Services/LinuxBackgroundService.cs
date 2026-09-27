// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services.Portal;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>Outcome of <see cref="LinuxBackgroundService.RequestAsync"/>.</summary>
public sealed class LinuxBackgroundResult
{
    internal LinuxBackgroundResult(bool portalAvailable, bool cancelled, bool background, bool autostart)
    {
        PortalAvailable = portalAvailable;
        Cancelled = cancelled;
        Background = background;
        Autostart = autostart;
    }

    /// <summary>False when xdg-desktop-portal (or its Background interface) is not available.</summary>
    public bool PortalAvailable { get; }

    /// <summary>True when the user dismissed the permission prompt.</summary>
    public bool Cancelled { get; }

    /// <summary>Whether the app may keep running with no open window.</summary>
    public bool Background { get; }

    /// <summary>Whether the app was registered to start at login.</summary>
    public bool Autostart { get; }
}

/// <summary>
/// Linux-specific: asks the desktop, through the xdg-desktop-portal
/// Background interface, for permission to keep running in the background and
/// optionally to start at login. This is the sandbox-friendly way to register
/// autostart (the portal writes the autostart entry itself); an unsandboxed
/// app can also ship its own XDG autostart .desktop file instead.
/// See docs/PORTALS.md.
/// </summary>
public static class LinuxBackgroundService
{
    /// <summary>
    /// Requests background (and optionally autostart) permission. The desktop
    /// may show a prompt; the task completes when the user answers.
    /// </summary>
    /// <param name="reason">Shown to the user in the permission prompt.</param>
    /// <param name="autostart">Also ask to start the app at login.</param>
    /// <param name="commandLine">Command to run at login (defaults to the app's own command line when null).</param>
    /// <param name="dbusActivatable">Start the app through D-Bus activation instead of the command line.</param>
    /// <param name="cancellationToken">Cancels (and closes) the portal request.</param>
    public static async Task<LinuxBackgroundResult> RequestAsync(
        string? reason = null,
        bool autostart = false,
        IReadOnlyList<string>? commandLine = null,
        bool dbusActivatable = false,
        CancellationToken cancellationToken = default)
    {
        if (DesktopPortal.Mode == PortalMode.Off)
            return new LinuxBackgroundResult(false, false, false, false);

        var result = await new PortalBackground(DesktopPortal.Current)
            .RequestAsync(reason, autostart, commandLine ?? DefaultCommandLine(), dbusActivatable, cancellationToken)
            .ConfigureAwait(false);
        return ToPublic(result);
    }

    internal static LinuxBackgroundResult ToPublic(PortalBackgroundResult result) => new(
        portalAvailable: result.Outcome != PortalOutcome.Unavailable,
        cancelled: result.Outcome == PortalOutcome.Cancelled,
        background: result.Outcome == PortalOutcome.Completed && result.Background,
        autostart: result.Outcome == PortalOutcome.Completed && result.Autostart);

    /// <summary>The running app's command line (process path plus its arguments), or null.</summary>
    internal static IReadOnlyList<string>? DefaultCommandLine()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return null;
            // Under "dotnet app.dll" the first argument is the dll; keep it.
            var list = new List<string> { exe };
            var rest = args.Skip(1);
            if (args.Length > 0 && args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                list.Add(args[0]);
            list.AddRange(rest);
            return list;
        }
        catch
        {
            return null;
        }
    }
}
