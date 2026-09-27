// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Hosting;
using Syncfusion.Maui.Core;
using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Graphics.Internals;

namespace Microsoft.Maui.Platform.Linux.Syncfusion;

/// <summary>
/// Opt-in Syncfusion support for OpenMaui apps. Linux apps build plain net10.0
/// and so get Syncfusion's platform-neutral assemblies, whose handlers have no
/// platform view (and whose <c>ConfigureSyncfusionCore</c> registers a stub
/// that MAUI rejects at startup). Call <see cref="UseLinuxSyncfusion"/> in its
/// place:
/// <code>
/// builder.UseMauiApp&lt;App&gt;()
///        .UseLinux()
///        .UseLinuxSyncfusion();   // instead of .ConfigureSyncfusionCore()
/// </code>
/// On other platforms it calls <c>ConfigureSyncfusionCore</c>, so one call
/// serves every head.
/// <para>
/// Covered: every <c>SfView</c>-based control (layout, drawing, touch, tap,
/// double-tap, right-tap, long-press) and the <c>ScrollView</c>-based list
/// hosts, which OpenMaui's own ScrollView handles. Not covered yet: controls
/// with dedicated native views (SignaturePad, ImageEditor, Carousel, Rotator,
/// Syncfusion's MediaElement), popups hosted in Syncfusion's window overlay,
/// and keyboard navigation.
/// </para>
/// </summary>
public static class LinuxSyncfusionBuilderExtensions
{
    public static MauiAppBuilder UseLinuxSyncfusion(this MauiAppBuilder builder)
    {
        if (!OperatingSystem.IsLinux())
            return builder.ConfigureSyncfusionCore();

        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(IDrawableLayout), typeof(SfLayoutBridgeHandler));
            handlers.AddHandler(typeof(SfView), typeof(SfLayoutBridgeHandler));
            handlers.AddHandler(typeof(IDrawableView), typeof(SfDrawableBridgeHandler));
        });

        // The fonts ConfigureSyncfusionCore registers (icon glyphs used by
        // the controls' own drawing).
        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("MauiMaterialAssets.ttf", "MauiMaterialAssets");
            fonts.AddFont("Boogaloo.ttf", "Boogaloo");
            fonts.AddFont("Handlee.ttf", "Handlee");
            fonts.AddFont("KaushanScript.ttf", "KaushanScript");
            fonts.AddFont("PinyonScript.ttf", "PinyonScript");
        });

        SfInputBridge.Install();
        SfListViewPatches.Install();
        return builder;
    }
}
