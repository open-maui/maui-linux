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
}

/// <summary>
/// Picks the render target for a window. Resolution order: the
/// <c>OPENMAUI_RENDERER</c> environment variable (<c>gpu</c>, <c>raster</c>,
/// <c>auto</c>), then <see cref="LinuxApplicationOptions.Renderer"/>. A GPU
/// target that fails to initialise (no libEGL, software-only driver, headless,
/// unusable config) falls back to <see cref="RasterRenderTarget"/> so the app
/// always renders.
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
            if (preference == RendererPreference.Gpu)
                DiagnosticLog.Error("RenderTargetFactory", $"GPU renderer requested but unavailable: {ex.Message}");
            else
                DiagnosticLog.Info("RenderTargetFactory", $"GPU renderer unavailable ({ex.Message}); using raster");
        }

        return new RasterRenderTarget(window);
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
            case "auto":
                return RendererPreference.Auto;
            default:
                DiagnosticLog.Warn("RenderTargetFactory", $"Unknown {EnvironmentVariable}='{env}' (expected gpu|raster|auto); ignoring");
                return configured;
        }
    }
}
