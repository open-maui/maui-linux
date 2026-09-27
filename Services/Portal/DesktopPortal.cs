// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services.Portal;

/// <summary>How the portal layer is used, from OPENMAUI_PORTALS.</summary>
internal enum PortalMode
{
    /// <summary>
    /// Default. Portals are used where they add something without changing
    /// what an unsandboxed app does today (Settings, Inhibit, FileChooser,
    /// Location, Background); launch-style portals (OpenURI, Notification,
    /// Secret) are used first only inside a sandbox (Flatpak, Snap).
    /// </summary>
    Auto,

    /// <summary>OPENMAUI_PORTALS=prefer: every portal first, also unsandboxed.</summary>
    Prefer,

    /// <summary>OPENMAUI_PORTALS=off: never talk to the portal; legacy paths only.</summary>
    Off,
}

/// <summary>
/// Entry point of the portal layer: the shared <see cref="IDesktopPortal"/>
/// and the policy deciding when a service tries the portal before its
/// fallback.
/// </summary>
internal static class DesktopPortal
{
    private static IDesktopPortal? _current;
    private static readonly Lock _gate = new();

    /// <summary>
    /// The portal every service uses. Lazily the Tmds.DBus implementation, or
    /// <see cref="NullDesktopPortal"/> when <see cref="Mode"/> is Off. Tests
    /// replace it (the test assembly installs the null portal at load so no
    /// test can reach the real desktop by accident).
    /// </summary>
    internal static IDesktopPortal Current
    {
        get
        {
            var current = Volatile.Read(ref _current);
            if (current != null)
                return current;
            lock (_gate)
            {
                _current ??= Mode == PortalMode.Off ? NullDesktopPortal.Instance : TmdsDesktopPortal.Shared;
                return _current;
            }
        }
        set
        {
            lock (_gate)
                _current = value;
        }
    }

    /// <summary>Overrides the environment-derived mode (tests).</summary>
    internal static PortalMode? ModeOverride { get; set; }

    /// <summary>Overrides sandbox detection (tests).</summary>
    internal static bool? SandboxOverride { get; set; }

    internal static PortalMode Mode => ModeOverride ?? ParseMode(Environment.GetEnvironmentVariable("OPENMAUI_PORTALS"));

    /// <summary>True inside Flatpak (/.flatpak-info) or Snap ($SNAP).</summary>
    internal static bool IsSandboxed => SandboxOverride ?? DetectSandbox();

    internal static PortalMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PortalMode.Auto;
        return value.Trim().ToLowerInvariant() switch
        {
            "off" or "0" or "false" or "no" or "disabled" => PortalMode.Off,
            "prefer" or "on" or "1" or "true" or "yes" or "always" => PortalMode.Prefer,
            _ => PortalMode.Auto,
        };
    }

    /// <summary>
    /// Whether a portal of the given kind should be tried before the service's
    /// fallback.
    /// </summary>
    internal static bool ShouldTry(PortalUse use) => ShouldTry(use, Mode, IsSandboxed);

    internal static bool ShouldTry(PortalUse use, PortalMode mode, bool sandboxed) => mode switch
    {
        PortalMode.Off => false,
        PortalMode.Prefer => true,
        _ => use == PortalUse.Always || sandboxed,
    };

    private static bool DetectSandbox()
    {
        try
        {
            if (File.Exists("/.flatpak-info"))
                return true;
        }
        catch
        {
        }

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP"));
    }
}

/// <summary>How eagerly a service reaches for its portal (see <see cref="PortalMode.Auto"/>).</summary>
internal enum PortalUse
{
    /// <summary>Portal first whenever it is available.</summary>
    Always,

    /// <summary>Portal first only inside a sandbox, or with OPENMAUI_PORTALS=prefer.</summary>
    SandboxedOrPreferred,
}
