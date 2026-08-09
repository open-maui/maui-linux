// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using Microsoft.Maui.Platform.Linux.Services;

// Registers this platform as a .NET hot-reload metadata-update handler. The
// hot-reload agent (dotnet watch, or an attached debugger's EnC agent) scans
// loaded assemblies for this assembly-level attribute and calls the static
// ClearCache/UpdateApplication methods below whenever it applies a code or XAML
// delta. Registration is pure metadata: it costs nothing at runtime and the
// methods are only ever invoked while a hot-reload agent is attached, so this is
// inert in Release / normal runs.
[assembly: MetadataUpdateHandler(typeof(Microsoft.Maui.Platform.Linux.Diagnostics.HotReloadService))]

namespace Microsoft.Maui.Platform.Linux.Diagnostics;

/// <summary>
/// Bridges .NET Hot Reload to this platform's Skia render pipeline.
///
/// This platform renders MAUI pages through its own Skia handlers rather than
/// native controls, so MAUI's built-in "reload XAML into the live visual tree"
/// path (IHotReloadableView.Reload / MauiHotReloadHelper active views) does not
/// reach our view tree. Instead we hook the underlying .NET hot-reload signal
/// directly: on any applied delta the runtime calls
/// <see cref="UpdateApplication"/>, and we rebuild the affected page(s) by
/// re-running our normal render path. Because a rebuild creates a fresh Page
/// instance, its InitializeComponent re-runs and picks up hot-reloaded XAML as
/// well as C# edits.
///
/// Covers both hot-reload layers under <c>dotnet watch</c>:
///   * C# method-body edits (CoreCLR IL deltas)
///   * XAML edits (delivered as metadata updates when <c>MauiXamlHotReload</c>
///     is enabled by the watch tooling)
/// </summary>
public static class HotReloadService
{
    /// <summary>
    /// True when the app was launched with a hot-reload agent (e.g.
    /// <c>dotnet watch</c> or an attached debugger). When false, every entry
    /// point here is inert.
    /// </summary>
    public static bool IsActive => MetadataUpdater.IsSupported;

    /// <summary>
    /// Optional startup hook. Registration is done by the assembly-level
    /// <see cref="MetadataUpdateHandlerAttribute"/>, so this only logs the active
    /// state — safe and cheap to call unconditionally from app startup.
    /// </summary>
    public static void Initialize()
    {
        if (!IsActive) return;
        DiagnosticLog.Info("HotReload",
            "Hot-reload agent detected; XAML/C# edits will re-render the current page.");
    }

    /// <summary>
    /// .NET hot-reload contract. Invoked (before <see cref="UpdateApplication"/>)
    /// when the runtime clears caches for a delta. The re-render is driven from
    /// UpdateApplication so a single delta triggers exactly one rebuild; this is
    /// a deliberate no-op kept to satisfy the handler contract.
    /// </summary>
    public static void ClearCache(Type[]? updatedTypes)
    {
    }

    /// <summary>
    /// .NET hot-reload contract. Invoked after a code or XAML delta is applied.
    /// May run on a background (agent) thread, so it marshals to the UI thread
    /// before touching the view tree. Never throws — a throw here would tear down
    /// the hot-reload agent.
    /// </summary>
    public static void UpdateApplication(Type[]? updatedTypes)
    {
        if (!IsActive) return;
        try
        {
            LinuxApplication.OnHotReload(updatedTypes);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("HotReload", "UpdateApplication handler failed", ex);
        }
    }
}
