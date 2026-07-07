// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.MediaElement.Native;
using Microsoft.Maui.Platform.Linux.Services;
using static Microsoft.Maui.Platform.Linux.MediaElement.Native.GStreamerInterop;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Services;

/// <summary>
/// Caller's hardware-acceleration intent for the MediaElement pipeline. Set on
/// <see cref="LinuxMediaElementOptions"/> before <c>UseLinuxMediaElement()</c>
/// runs; <see cref="MediaHardwareAccelerationService.Apply"/> translates the
/// choice into GStreamer plugin-rank changes that influence playbin's automatic
/// decoder selection.
/// </summary>
public enum MediaHardwareAcceleration
{
    /// <summary>
    /// Default. Trust playbin's auto-negotiation — works correctly on every
    /// modern installation. Same behavior as 10.0.60.13 → 10.0.70.1.
    /// </summary>
    Auto,

    /// <summary>
    /// Boost the rank of known HW decoder factories (VA-API for Intel/AMD,
    /// NVDEC for NVIDIA, V4L2 stateless for embedded, MediaSDK for some Intel
    /// stacks) so playbin prefers them over SW. Silently no-op if the plugin
    /// package isn't installed.
    /// </summary>
    Prefer,

    /// <summary>
    /// Demote HW decoders so playbin always picks SW. Useful when a flaky GPU
    /// driver corrupts frames or hangs the pipeline.
    /// </summary>
    Disable,
}

/// <summary>
/// Applies <see cref="MediaHardwareAcceleration"/> to the GStreamer registry.
/// Run once at <c>UseLinuxMediaElement()</c> time; idempotent across pipelines.
/// Factory ranks are process-global GStreamer state — a change here affects
/// every pipeline in the process (including e.g. WebView-embedded media), not
/// just MediaElement.
/// </summary>
public static class MediaHardwareAccelerationService
{
    // Original rank of every factory we've modified, keyed by factory name,
    // so Apply(Auto) can restore stock behavior. Guarded by s_lock.
    private static readonly Dictionary<string, uint> s_originalRanks = new();
    private static readonly Lock s_lock = new();

    // Well-known HW decoder factory names. Each entry covers one stack; not
    // every plugin will be installed (and that's fine — we look up by name and
    // silently skip what's not registered). The bin variants (vaapidecodebin,
    // nvdec / nvh264dec) take precedence on the actual decode path; the
    // *postproc / converter factories are kept here so we can rank them too
    // when present (some pipelines benefit from the matching post-proc).
    private static readonly string[] s_hwDecoderFactories =
    {
        // VA-API (Intel iGPU + AMD via mesa)
        "vaapidecodebin",
        "vaapih264dec",
        "vaapih265dec",
        "vaapivp9dec",
        "vaapivp8dec",
        "vaapimpeg2dec",
        // NVIDIA (proprietary driver)
        "nvdec",
        "nvh264dec",
        "nvh265dec",
        "nvvp9dec",
        // V4L2 stateless (embedded SoCs)
        "v4l2slh264dec",
        "v4l2slh265dec",
        "v4l2slvp9dec",
        "v4l2slvp8dec",
        // Intel MediaSDK / oneVPL
        "msdkh264dec",
        "msdkh265dec",
        "msdkvp9dec",
    };

    /// <summary>
    /// Apply the requested acceleration mode by tweaking factory ranks in the
    /// GStreamer registry. Safe to call repeatedly — gst_init must already
    /// have run (a no-op with a warning otherwise). <see
    /// cref="MediaHardwareAcceleration.Auto"/> restores the stock rank of any
    /// factory a previous call modified.
    /// </summary>
    public static void Apply(MediaHardwareAcceleration mode)
    {
        // gst_registry_get() lazily creates an *empty* registry before
        // gst_init, so a null check alone can't catch call-order mistakes —
        // we'd silently tweak nothing and lose the intent when the real init
        // populates the registry with default ranks.
        if (!gst_is_initialized())
        {
            DiagnosticLog.Warn("MediaHardwareAccelerationService", "gst_init has not run — registry is unpopulated, ignoring Apply()");
            return;
        }

        var registry = gst_registry_get();
        if (registry == IntPtr.Zero)
        {
            DiagnosticLog.Warn("MediaHardwareAccelerationService", "gst_registry_get() returned null");
            return;
        }

        lock (s_lock)
        {
            if (mode == MediaHardwareAcceleration.Auto)
            {
                RestoreOriginalRanks(registry);
                return;
            }

            uint targetRank = mode switch
            {
                MediaHardwareAcceleration.Prefer => GST_RANK_PRIMARY + 64,   // edges out the SW factories' GST_RANK_PRIMARY
                MediaHardwareAcceleration.Disable => GST_RANK_NONE,
                _ => GST_RANK_PRIMARY,
            };

            int adjusted = 0;
            var found = new List<string>();
            foreach (var name in s_hwDecoderFactories)
            {
                var feature = gst_registry_lookup_feature(registry, name);
                if (feature == IntPtr.Zero) continue;

                // Snapshot the stock rank the first time we touch a factory so
                // Apply(Auto) can undo Prefer/Disable.
                if (!s_originalRanks.ContainsKey(name))
                    s_originalRanks[name] = gst_plugin_feature_get_rank(feature);

                gst_plugin_feature_set_rank(feature, targetRank);
                gst_object_unref(feature);   // lookup_feature transfers a ref to us
                found.Add(name);
                adjusted++;
            }

            var verb = mode == MediaHardwareAcceleration.Prefer ? "boosted" : "demoted";
            if (adjusted == 0)
            {
                DiagnosticLog.Debug("MediaHardwareAccelerationService",
                    $"No HW decoder factories found to {verb}. Install gstreamer1-vaapi (Intel/AMD), nvidia-gst-plugins (NVIDIA), or similar.");
            }
            else
            {
                DiagnosticLog.Debug("MediaHardwareAccelerationService",
                    $"{verb} {adjusted} HW decoder factor{(adjusted == 1 ? "y" : "ies")}: {string.Join(", ", found)}");
            }
        }
    }

    private static void RestoreOriginalRanks(IntPtr registry)
    {
        if (s_originalRanks.Count == 0) return;   // nothing was ever modified

        int restored = 0;
        foreach (var (name, rank) in s_originalRanks)
        {
            var feature = gst_registry_lookup_feature(registry, name);
            if (feature == IntPtr.Zero) continue;
            gst_plugin_feature_set_rank(feature, rank);
            gst_object_unref(feature);
            restored++;
        }
        s_originalRanks.Clear();
        DiagnosticLog.Debug("MediaHardwareAccelerationService",
            $"Restored stock rank on {restored} HW decoder factor{(restored == 1 ? "y" : "ies")}");
    }

    /// <summary>
    /// Diagnostic helper — returns the list of HW decoder factories currently
    /// registered (and therefore available for playbin to select). Useful for
    /// surfacing "what's installed?" in app settings or logs.
    /// </summary>
    public static IReadOnlyList<string> EnumerateAvailableHardwareDecoders()
    {
        if (!gst_is_initialized()) return Array.Empty<string>();
        var registry = gst_registry_get();
        if (registry == IntPtr.Zero) return Array.Empty<string>();

        var found = new List<string>();
        foreach (var name in s_hwDecoderFactories)
        {
            var feature = gst_registry_lookup_feature(registry, name);
            if (feature == IntPtr.Zero) continue;
            found.Add(name);
            gst_object_unref(feature);
        }
        return found;
    }
}
