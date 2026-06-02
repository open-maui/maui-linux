// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.MediaElement.Handlers;
using Microsoft.Maui.Platform.Linux.MediaElement.Native;
using Microsoft.Maui.Platform.Linux.MediaElement.Services;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Hosting;

/// <summary>
/// Opt-in registration for the Linux MediaElement backend. Call AFTER
/// <c>UseMauiCommunityToolkitMediaElement()</c> and <c>UseLinux()</c>:
///
/// <code>
/// builder
///     .UseMauiApp&lt;App&gt;()
///     .UseMauiCommunityToolkitMediaElement()  // upstream toolkit
///     .UseLinux()                              // base Linux handlers
///     .UseLinuxMediaElement();                 // this — opt-in MediaElement backend
/// </code>
///
/// Safe to call on Windows / Android / iOS / macCatalyst — no-op there so the
/// same MauiProgram.cs works cross-platform. On Linux, registers the
/// LinuxMediaElementHandler that backs CommunityToolkit.Maui.MediaElement with
/// a GStreamer playbin pipeline rendered into a Skia surface.
///
/// Runtime requirements (Linux only):
///   - libgstreamer-1.0.so.0 and libgstapp-1.0.so.0 (always present on modern desktops)
///   - gstreamer1-plugins-good / -bad / -ugly / -libav for broad codec coverage
///   - gstreamer1-vaapi (Intel/AMD) or gstreamer1-nvdec (NVIDIA) for HW decode
/// </summary>
public static class LinuxMediaElementBuilderExtensions
{
    /// <summary>
    /// Default registration. Equivalent to
    /// <c>UseLinuxMediaElement(MediaHardwareAcceleration.Auto)</c> — playbin
    /// picks decoders unmodified, which works correctly on every modern install.
    /// </summary>
    public static MauiAppBuilder UseLinuxMediaElement(this MauiAppBuilder builder)
        => builder.UseLinuxMediaElement(MediaHardwareAcceleration.Auto);

    /// <summary>
    /// Register the Linux MediaElement backend with explicit hardware-acceleration
    /// intent. See <see cref="MediaHardwareAcceleration"/> for what each option
    /// does. <see cref="MediaHardwareAcceleration.Prefer"/> bumps the rank of
    /// known HW decoder factories (VA-API / NVDEC / V4L2 / MediaSDK) so playbin
    /// favors GPU decode whenever the plugin packages are installed; safely
    /// no-ops when nothing's available.
    /// </summary>
    public static MauiAppBuilder UseLinuxMediaElement(
        this MauiAppBuilder builder,
        MediaHardwareAcceleration hardwareAcceleration)
    {
        // No-op on non-Linux: the same MauiProgram.cs is meant to be cross-platform.
        // The toolkit's native handlers handle iOS/Android/Windows/macCatalyst;
        // we only need to register for Linux.
        if (!OperatingSystem.IsLinux())
            return builder;

        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler>();
        });

        // gst_init is normally called lazily by SkiaMediaElement's first
        // playback, but the rank tweak needs the registry to be populated. Run
        // it now (idempotent — gst_init is safe to call twice).
        try
        {
            GStreamerInterop.gst_init(IntPtr.Zero, IntPtr.Zero);
            MediaHardwareAccelerationService.Apply(hardwareAcceleration);
        }
        catch (DllNotFoundException)
        {
            // GStreamer isn't installed on this machine. The handler will
            // surface a clearer error at first playback.
        }

        return builder;
    }
}
