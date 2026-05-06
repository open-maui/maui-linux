using System.Reflection;
using Microsoft.Maui.Hosting;

namespace Microsoft.Maui.Platform.Linux.Hosting;

/// <summary>
/// Cross-platform extension for adding Linux support to a MAUI app.
/// Call .UseLinux() unconditionally in your MauiProgram.cs — it activates
/// on Linux (when OpenMaui.Controls.Linux is present) and is a no-op elsewhere.
/// </summary>
public static class LinuxMauiAppBuilderExtensions
{
    private static bool _resolved;
    private static MethodInfo? _registerMethod;
    private static MethodInfo? _registerWithDisplayServerMethod;

    /// <summary>
    /// Adds Linux platform support to the MAUI app builder.
    /// Safe to call on all platforms — no-op on non-Linux.
    /// </summary>
    public static MauiAppBuilder UseLinux(this MauiAppBuilder builder)
    {
        if (!OperatingSystem.IsLinux())
            return builder;

        ResolveImplementation();

        if (_registerMethod != null)
        {
            _registerMethod.Invoke(null, [builder]);
        }

        return builder;
    }

    /// <summary>
    /// Adds Linux platform support and forces the X11/XWayland backend. Use as a drop-in
    /// replacement for <see cref="UseLinux"/> — call one or the other, not both. Safe to
    /// call on all platforms; no-op on non-Linux.
    /// </summary>
    public static MauiAppBuilder UseX11(this MauiAppBuilder builder)
        => UseDisplayServer(builder, "X11");

    /// <summary>
    /// Adds Linux platform support and prefers the native Wayland backend (with automatic
    /// X11/XWayland fallback if Wayland is unavailable). Use as a drop-in replacement for
    /// <see cref="UseLinux"/>. Safe to call on all platforms; no-op on non-Linux.
    /// </summary>
    public static MauiAppBuilder UseWayland(this MauiAppBuilder builder)
        => UseDisplayServer(builder, "Wayland");

    private static MauiAppBuilder UseDisplayServer(MauiAppBuilder builder, string serverName)
    {
        if (!OperatingSystem.IsLinux())
            return builder;

        ResolveImplementation();

        if (_registerWithDisplayServerMethod != null)
        {
            _registerWithDisplayServerMethod.Invoke(null, [builder, serverName]);
        }
        else if (_registerMethod != null)
        {
            // Older Linux runtime without the display-server entry point — fall back to
            // the standard register so the app still works (env-var detection still applies).
            _registerMethod.Invoke(null, [builder]);
        }

        return builder;
    }

    private static void ResolveImplementation()
    {
        if (_resolved) return;
        _resolved = true;

        try
        {
            var assembly = Assembly.Load("OpenMaui.Controls.Linux");
            var type = assembly.GetType("Microsoft.Maui.Platform.Linux.Hosting.LinuxPlatformRegistrar");
            _registerMethod = type?.GetMethod("Register", BindingFlags.Static | BindingFlags.Public);
            _registerWithDisplayServerMethod = type?.GetMethod("RegisterWithDisplayServer", BindingFlags.Static | BindingFlags.Public);
        }
        catch
        {
            // OpenMaui.Controls.Linux not available — Linux support not installed
        }
    }
}
