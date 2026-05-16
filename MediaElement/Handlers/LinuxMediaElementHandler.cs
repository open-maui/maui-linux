// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Handlers;

/// <summary>
/// Linux backend for CommunityToolkit.Maui.MediaElement. Subclasses the
/// upstream toolkit's net10.0 stub (which throws NotImplementedException on
/// every Map* call) with a Skia + GStreamer implementation.
///
/// Property mappers below scaffold the routing; the actual pipeline wiring
/// lands in S4.2 (GStreamer P/Invoke) and S4.4 (mapper implementations).
/// For now the handler creates a SkiaMediaElement so MAUI can lay out and
/// draw the empty video surface without throwing.
/// </summary>
public class LinuxMediaElementHandler : MediaElementHandler
{
    public new static IPropertyMapper<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler> PropertyMapper =
        new PropertyMapper<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler>(MediaElementHandler.PropertyMapper)
        {
            ["Aspect"] = MapAspect,
            ["Source"] = MapSource,
            ["Speed"] = MapSpeed,
            ["Volume"] = MapVolume,
            ["ShouldMute"] = MapShouldMute,
            ["ShouldShowPlaybackControls"] = MapShouldShowPlaybackControls,
            ["ShouldKeepScreenOn"] = MapShouldKeepScreenOn,
        };

    public new static CommandMapper<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler> CommandMapper =
        new CommandMapper<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler>(MediaElementHandler.CommandMapper)
        {
            ["StatusUpdated"] = MapStatusUpdated,
            ["PlayRequested"] = MapPlayRequested,
            ["PauseRequested"] = MapPauseRequested,
            ["SeekRequested"] = MapSeekRequested,
            ["StopRequested"] = MapStopRequested,
        };

    public LinuxMediaElementHandler() : base(PropertyMapper, CommandMapper) { }

    public LinuxMediaElementHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? PropertyMapper, commandMapper ?? CommandMapper) { }

    protected override object CreatePlatformView()
    {
        // SkiaMediaElement owns the GStreamer pipeline and renders the latest
        // decoded frame. Construction is cheap; the pipeline starts when
        // MapSource fires with a non-null Source.
        return new SkiaMediaElement();
    }

    // Concrete mapper implementations land in S4.4 — placeholder no-ops here
    // override the toolkit's NotImplementedException stubs so the app doesn't
    // throw the first time MAUI's mapper pipeline runs against the handler.
    public new static void MapAspect(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }
    public new static void MapSource(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }
    public new static void MapSpeed(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }
    public new static void MapVolume(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }
    public new static void MapShouldMute(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }
    public new static void MapShouldShowPlaybackControls(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }
    public new static void MapShouldKeepScreenOn(object handler, CommunityToolkit.Maui.Views.MediaElement media) { /* TODO S4.4 */ }

    public new static void MapStatusUpdated(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args) { /* TODO S4.4 */ }
    public new static void MapPlayRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args) { /* TODO S4.4 */ }
    public new static void MapPauseRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args) { /* TODO S4.4 */ }
    public new static void MapSeekRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args) { /* TODO S4.4 */ }
    public new static void MapStopRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args) { /* TODO S4.4 */ }
}
