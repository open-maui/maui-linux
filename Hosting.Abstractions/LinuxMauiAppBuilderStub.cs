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

    private static void ResolveImplementation()
    {
        if (_resolved) return;
        _resolved = true;

        try
        {
            var assembly = Assembly.Load("OpenMaui.Controls.Linux");
            var type = assembly.GetType("Microsoft.Maui.Platform.Linux.Hosting.LinuxPlatformRegistrar");
            _registerMethod = type?.GetMethod("Register", BindingFlags.Static | BindingFlags.Public);
        }
        catch
        {
            // OpenMaui.Controls.Linux not available — Linux support not installed
        }
    }
}
