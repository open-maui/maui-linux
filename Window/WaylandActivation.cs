// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Window;

/// <summary>One request sent on an xdg_activation_token_v1 before its commit.</summary>
internal enum WaylandActivationTokenStep
{
    /// <summary>set_serial(serial, seat): the input event that justifies the activation.</summary>
    SetSerial,
    /// <summary>set_surface(surface): the requesting surface, the one with keyboard focus.</summary>
    SetSurface,
    /// <summary>commit: the compositor answers with done(token).</summary>
    Commit,
}

/// <summary>
/// The decisions behind Wayland window activation (xdg_activation_v1), kept free of libwayland so
/// they can be tested headless. A token is worth most when requested by the app's focused
/// window, with that window's latest input serial and surface; the target window then activates
/// itself with it. Each OpenMaui Wayland window owns its connection, and tokens are plain
/// strings, so a token requested on one connection activates a surface on another.
/// </summary>
internal static class WaylandActivation
{
    /// <summary>
    /// The window that requests the token: the focused one when it can request tokens (bound
    /// xdg_activation_v1), otherwise the target itself.
    /// </summary>
    public static T? ChooseRequester<T>(T target, T? focused, Func<T, bool> canRequest) where T : class
    {
        if (focused != null && canRequest(focused))
            return focused;
        return canRequest(target) ? target : null;
    }

    /// <summary>
    /// The requests a token gets: the serial (with the seat) only when there was input, the
    /// surface only when the requester has keyboard focus, then commit.
    /// </summary>
    public static IReadOnlyList<WaylandActivationTokenStep> TokenSteps(uint lastInputSerial, bool hasSeat, bool requesterFocused)
    {
        var steps = new List<WaylandActivationTokenStep>(3);
        if (lastInputSerial != 0 && hasSeat)
            steps.Add(WaylandActivationTokenStep.SetSerial);
        if (requesterFocused)
            steps.Add(WaylandActivationTokenStep.SetSurface);
        steps.Add(WaylandActivationTokenStep.Commit);
        return steps;
    }

    /// <summary>
    /// The launcher's activation token for the first window: XDG_ACTIVATION_TOKEN, else
    /// DESKTOP_STARTUP_ID (launchers that predate xdg-activation pass it there, as GTK accepts).
    /// </summary>
    public static string? StartupToken(Func<string, string?> getEnvironmentVariable)
    {
        var token = getEnvironmentVariable("XDG_ACTIVATION_TOKEN");
        if (string.IsNullOrEmpty(token))
            token = getEnvironmentVariable("DESKTOP_STARTUP_ID");
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
