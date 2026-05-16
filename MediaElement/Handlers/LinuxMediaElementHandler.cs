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
/// every Map* call) and routes property/command changes into the GStreamer
/// pipeline owned by SkiaMediaElement.
///
/// Property mapping is straightforward — each MAUI property has a matching
/// setter on SkiaMediaElement that translates into a g_object_set or pipeline
/// state change. The toolkit's mapper pipeline calls these on every change.
///
/// Two property mapping subtleties to keep in mind for future maintainers:
///   1. MAUI doesn't have a "MediaSourceChanged" notification for the
///      `MediaSource.Uri` BindableProperty — the toolkit raises an internal
///      SourceChanged event on the MediaSource. Re-mapping via MapSource each
///      time the URL changes is the simplest approach since SetSource is a
///      no-op when the URI is unchanged.
///   2. ShouldShowPlaybackControls is intentionally unimplemented — drawing
///      controls in Skia is a larger feature (overlay buttons, progress bar
///      with hit-test, time labels). Until then the app is expected to render
///      its own controls around the MediaElement (see MediaDemo).
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

    // Periodic position/duration pump. 250ms cadence matches what the toolkit's
    // iOS/Android backends use; responsive enough for slider scrub-to-position
    // feedback without burning CPU. Driven by SkiaMediaElement's own timer
    // (started when a pipeline is active) and fired on the main thread.
    private TimeSpan _lastDuration = TimeSpan.MinValue;

    protected override object CreatePlatformView()
    {
        var platform = new SkiaMediaElement();
        // SkiaMediaElement runs a 250ms timer for the duration of any active
        // pipeline and fires StatusTick on the main thread. We push the
        // current Position + Duration onto the toolkit MediaElement here so
        // its bound sliders / labels see live updates.
        platform.StatusTick += PumpStatus;
        return platform;
    }

    private void PumpStatus()
    {
        if (PlatformView is not SkiaMediaElement skia) return;
        if (VirtualView is not CommunityToolkit.Maui.Views.MediaElement media) return;

        // Position has an internal setter on the interface — go through it via
        // reflection rather than requiring InternalsVisibleTo from the toolkit.
        var iface = typeof(CommunityToolkit.Maui.Core.IMediaElement);
        iface.GetProperty("Position")?.GetSetMethod(nonPublic: true)
            ?.Invoke(media, new object[] { skia.Position });

        // Duration's setter is on the IMediaElement interface (not the
        // concrete class, which exposes it read-only). Cast through the
        // interface so we hit the public setter. Only push on change — bound
        // sliders re-fire NotifyPropertyChanged on every assignment and we
        // don't want to thrash the UI 4 times per second.
        var dur = skia.Duration;
        if (dur != _lastDuration)
        {
            _lastDuration = dur;
            ((CommunityToolkit.Maui.Core.IMediaElement)media).Duration = dur;
        }
    }

    // ----- helpers -----------------------------------------------------------

    private static SkiaMediaElement? GetSkia(object handler)
        => (handler as LinuxMediaElementHandler)?.PlatformView as SkiaMediaElement;

    /// <summary>
    /// Resolve a MediaSource into a URI playbin can consume.
    /// - UriMediaSource → its Uri.AbsoluteUri (http/https/rtsp/file pass through).
    /// - FileMediaSource → "file://" + absolute path.
    /// - ResourceMediaSource → resolve relative to the executable's directory
    ///   (toolkit's stub returns null on Linux without a platform resolver;
    ///   we approximate by mapping to the app base directory).
    /// </summary>
    private static string? ResolveSourceUri(MediaSource? source)
    {
        if (source == null) return null;
        switch (source)
        {
            case UriMediaSource uriSrc:
                return uriSrc.Uri?.AbsoluteUri;
            case FileMediaSource fileSrc when !string.IsNullOrEmpty(fileSrc.Path):
                var p = fileSrc.Path!;
                if (!Path.IsPathRooted(p))
                    p = Path.Combine(AppContext.BaseDirectory, p);
                return new Uri(p).AbsoluteUri;
            case ResourceMediaSource resSrc when !string.IsNullOrEmpty(resSrc.Path):
                var rp = Path.Combine(AppContext.BaseDirectory, "Resources", "Raw", resSrc.Path!);
                return File.Exists(rp) ? new Uri(rp).AbsoluteUri : null;
            default:
                return null;
        }
    }

    // ----- property mappers --------------------------------------------------

    public new static void MapAspect(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.Aspect = media.Aspect;
    }

    public new static void MapSource(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is not { } skia) return;
        var uri = ResolveSourceUri(media.Source);
        skia.SetSource(uri);
        if (uri != null && media.ShouldAutoPlay)
            skia.Play();
    }

    public new static void MapPlayRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is { } skia) skia.Play();
    }

    public new static void MapSpeed(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        // Variable playback rate requires gst_element_seek with a rate parameter,
        // which is more involved than seek_simple. Stub for now; v2 implements
        // proper variable-rate seek + audio pitch correction (which is fairly
        // platform-specific — playbin will speed-up audio noticeably).
    }

    public new static void MapVolume(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.SetVolume(media.Volume);
    }

    public new static void MapShouldMute(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.SetMute(media.ShouldMute);
    }

    public new static void MapShouldShowPlaybackControls(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        // Skia-drawn overlay controls (play/pause/seekbar/time) are a v2
        // feature — for now the app supplies its own controls (see MediaDemo).
    }

    public new static void MapShouldKeepScreenOn(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        // Screen-wake inhibit needs zxdg_idle_inhibit on Wayland or org.freedesktop.ScreenSaver
        // over DBus on X11. Stubbed; the typical desktop user has a generous
        // idle timeout so this is rarely user-blocking. v2 wires both paths.
    }

    // ----- command mappers ---------------------------------------------------

    public new static void MapPauseRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is { } skia) skia.Pause();
    }

    public new static void MapStopRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is { } skia) skia.Stop();
    }

    public new static void MapSeekRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is not { } skia || args == null) return;
        // MediaSeekRequestedEventArgs is internal in the toolkit so we read its
        // RequestedPosition via reflection — the property is stable across
        // toolkit versions, and avoiding a reference to an internal type keeps
        // us out of binary-compat headaches when toolkit ships major bumps.
        var pos = args.GetType().GetProperty("RequestedPosition")?.GetValue(args);
        if (pos is TimeSpan ts)
            skia.SeekTo(ts);

        // CRITICAL: SeekTo on the toolkit's MediaElement awaits a
        // TaskCompletionSource that the PLATFORM HANDLER is contractually
        // required to complete after issuing the seek. The TCS is exposed via
        // IAsynchronousMediaElementHandler.SeekCompletedTCS. Without
        // TrySetResult here, the first SeekTo() never returns, the awaiter
        // hangs, the internal semaphore stays held, and every subsequent seek
        // request queues behind a Task that will never finish. (Calling the
        // event-raising IMediaElement.SeekCompleted() is NOT enough — it only
        // fires the public SeekCompleted event, doesn't complete the TCS.)
        ((CommunityToolkit.Maui.Core.IAsynchronousMediaElementHandler)media)
            .SeekCompletedTCS.TrySetResult();
    }

    public new static void MapStatusUpdated(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is not { } skia) return;
        // Surface current position to the MediaElement so PositionChanged event
        // listeners (sliders, time labels) see live updates. IMediaElement.Position
        // has an internal setter; we route around it via reflection rather than
        // requiring InternalsVisibleTo from the toolkit.
        var iface = typeof(CommunityToolkit.Maui.Core.IMediaElement);
        var positionProp = iface.GetProperty("Position");
        positionProp?.GetSetMethod(nonPublic: true)?.Invoke(media, new object[] { skia.Position });
    }
}
