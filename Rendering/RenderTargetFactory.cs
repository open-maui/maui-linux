// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Rendering;

/// <summary>How the platform should present frames.</summary>
public enum RendererPreference
{
    /// <summary>GPU when EGL initialises cleanly, raster otherwise (default).</summary>
    Auto,
    /// <summary>GPU; a failure is logged as an error and raster is still used.</summary>
    Gpu,
    /// <summary>Always the CPU raster path (wl_shm / XPutImage).</summary>
    Raster,
    /// <summary>
    /// Vulkan (VK_KHR_wayland_surface / VK_KHR_xlib_surface swapchain with
    /// Skia's Vulkan backend). Opt-in; a failure is logged once and the EGL
    /// target is tried next, then raster.
    /// </summary>
    Vulkan,
}

/// <summary>
/// Picks the render target for a window. Resolution order: the
/// <c>OPENMAUI_RENDERER</c> environment variable (<c>gpu</c>, <c>vulkan</c>,
/// <c>raster</c>, <c>auto</c>), then <see cref="LinuxApplicationOptions.Renderer"/>.
/// A GPU target that fails to initialise (no libEGL / libvulkan, software-only
/// driver, headless, unusable config) falls back down the chain
/// Vulkan → EGL → <see cref="RasterRenderTarget"/> so the app always renders.
/// </summary>
public static class RenderTargetFactory
{
    public const string EnvironmentVariable = "OPENMAUI_RENDERER";

    public static IRenderTarget Create(IDisplayWindow window, RendererPreference preference = RendererPreference.Auto)
    {
        preference = ResolvePreference(preference);

        if (preference == RendererPreference.Raster)
        {
            DiagnosticLog.Debug("RenderTargetFactory", "Raster renderer selected by configuration");
            return new RasterRenderTarget(window);
        }

        if (preference == RendererPreference.Vulkan)
        {
            try
            {
                var vulkan = CreateVulkan(window);
                if (vulkan != null)
                    return vulkan;

                LogVulkanFallbackOnce($"no Vulkan target for {window.GetType().Name}");
            }
            catch (Exception ex)
            {
                LogVulkanFallbackOnce(ex.Message);
            }
        }

        try
        {
            var target = CreateGpu(window);
            if (target != null)
                return target;

            DiagnosticLog.Debug("RenderTargetFactory", $"No GPU target for {window.GetType().Name}; using raster");
        }
        catch (Exception ex)
        {
            // Gpu was explicitly requested: make the failure loud. Auto: it is
            // an expected condition (VMs, CI, missing drivers) so keep it quiet.
            if (preference is RendererPreference.Gpu or RendererPreference.Vulkan)
                DiagnosticLog.Error("RenderTargetFactory", $"GPU renderer requested but unavailable: {ex.Message}", ex);
            else
                DiagnosticLog.Info("RenderTargetFactory", $"GPU renderer unavailable ({ex.Message}); using raster");
        }

        return new RasterRenderTarget(window);
    }

    private static int s_vulkanFallbackLogged;

    /// <summary>
    /// Vulkan failures are logged once per process (every window would hit
    /// the same missing loader/driver) as a warning: the EGL target that
    /// follows is still a GPU path.
    /// </summary>
    private static void LogVulkanFallbackOnce(string reason)
    {
        if (Interlocked.Exchange(ref s_vulkanFallbackLogged, 1) == 0)
            DiagnosticLog.Warn("RenderTargetFactory", $"Vulkan renderer requested but unavailable ({reason}); falling back to EGL");
        else
            DiagnosticLog.Debug("RenderTargetFactory", $"Vulkan unavailable ({reason}); falling back to EGL");
    }

    private static IRenderTarget? CreateVulkan(IDisplayWindow window)
    {
        if (window is WaylandWindow wayland)
        {
            // Same hand-over as EGL: the swapchain attaches buffers from now
            // on; restore the shm path if Vulkan cannot take the surface.
            wayland.ExternalPresentation = true;
            try
            {
                return VulkanRenderTarget.CreateWayland(wayland, window.Width, window.Height);
            }
            catch
            {
                wayland.ExternalPresentation = false;
                throw;
            }
        }

        if (window is IX11Surface x11)
            return VulkanRenderTarget.CreateX11(x11, window.Width, window.Height);

        return null;
    }

    private static IRenderTarget? CreateGpu(IDisplayWindow window)
    {
        if (window is WaylandWindow wayland)
        {
            // Hand the surface to EGL before creating the target so the shm
            // buffer is released and Show() does not attach it. Restore the shm
            // path if EGL setup fails so the raster fallback has a buffer.
            wayland.ExternalPresentation = true;
            try
            {
                return new WaylandEglRenderTarget(wayland, window.Width, window.Height);
            }
            catch
            {
                wayland.ExternalPresentation = false;
                throw;
            }
        }

        if (window is IX11Surface x11)
            return new X11EglRenderTarget(x11, window.Width, window.Height);

        return null;
    }

    public static RendererPreference ResolvePreference(RendererPreference configured)
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(env))
            return configured;

        switch (env.Trim().ToLowerInvariant())
        {
            case "gpu":
            case "egl":
                return RendererPreference.Gpu;
            case "raster":
            case "cpu":
            case "software":
                return RendererPreference.Raster;
            case "vulkan":
            case "vk":
                return RendererPreference.Vulkan;
            case "auto":
                return RendererPreference.Auto;
            default:
                DiagnosticLog.Warn("RenderTargetFactory", $"Unknown {EnvironmentVariable}='{env}' (expected gpu|vulkan|raster|auto); ignoring");
                return configured;
        }
    }
}
