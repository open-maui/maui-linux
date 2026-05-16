// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.MediaElement.Handlers;

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
    public static MauiAppBuilder UseLinuxMediaElement(this MauiAppBuilder builder)
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

        return builder;
    }
}
