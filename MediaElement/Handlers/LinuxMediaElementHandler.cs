// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Dispatching;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.MediaElement.Services;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;

namespace Microsoft.Maui.Platform.Linux.MediaElement.Handlers;

/// <summary>
/// Linux backend for CommunityToolkit.Maui.MediaElement. Subclasses the
/// upstream toolkit's net10.0 stub (which throws NotImplementedException on
/// every Map* call) and routes property/command changes into the GStreamer
/// pipeline owned by SkiaMediaElement, the way the toolkit's Windows
/// MediaManager drives MediaPlayerElement:
///
///   - events: MediaOpened (with Duration), MediaFailed, MediaEnded,
///     PositionChanged (Position pumped while Playing, Paused or Stopped),
///     CurrentState / StateChanged (Opening, Buffering, Playing, Paused,
///     Stopped, None), MediaWidth / MediaHeight;
///   - Speed (playback rate; 0 pauses, as MediaPlayer.PlaybackRate = 0 does),
///     UriMediaSource.HttpHeaders, ShouldKeepScreenOn (screen-saver
///     inhibition), ShouldShowPlaybackControls (the controls overlay),
///     MetadataTitle / MetadataArtist / MetadataArtworkUrl (published over
///     MPRIS, the SystemMediaTransportControls counterpart);
///   - DisconnectHandler tears the pipeline down, releases the screen-saver
///     inhibition and withdraws the MPRIS player.
///
/// MAUI doesn't have a "MediaSourceChanged" notification for the
/// <c>MediaSource.Uri</c> BindableProperty: the toolkit raises an internal
/// SourceChanged event on the MediaSource (also when HttpHeaders change) that
/// re-maps Source. SetSource is a no-op when the URI and headers are unchanged.
/// </summary>
public class LinuxMediaElementHandler : MediaElementHandler, ISkiaLayoutBridge, IDisposable
{
    public new static IPropertyMapper<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler> PropertyMapper =
        new PropertyMapper<CommunityToolkit.Maui.Views.MediaElement, LinuxMediaElementHandler>(MediaElementHandler.PropertyMapper)
        {
            ["Aspect"] = MapAspect,
            ["Source"] = MapSource,
            ["Speed"] = MapSpeed,
            ["Volume"] = MapVolume,
            ["ShouldMute"] = MapShouldMute,
            ["ShouldLoopPlayback"] = MapShouldLoopPlayback,
            ["ShouldShowPlaybackControls"] = MapShouldShowPlaybackControls,
            ["ShouldKeepScreenOn"] = MapShouldKeepScreenOn,
            ["MetadataTitle"] = MapMetadata,
            ["MetadataArtist"] = MapMetadata,
            ["MetadataArtworkUrl"] = MapMetadata,
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

    /// <summary>
    /// Measures the Skia view when MAUI's layout managers measure the
    /// MediaElement (a Grid's cell), as the platform's own handlers do; the
    /// toolkit's portable handler reports 0x0.
    /// </summary>
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        if (PlatformView is SkiaView skia && VirtualView is IView view)
            return SkiaLayoutBridge.GetDesiredSize(skia, view, widthConstraint, heightConstraint);
        return base.GetDesiredSize(widthConstraint, heightConstraint);
    }

    /// <summary>
    /// Places the Skia view at the frame MAUI's layout gave the MediaElement;
    /// the toolkit's portable handler ignores it.
    /// </summary>
    public override void PlatformArrange(Rect frame)
    {
        if (PlatformView is SkiaView skia)
            SkiaLayoutBridge.PlatformArrange(skia, VirtualView, frame);
        else
            base.PlatformArrange(frame);
    }

    // The toolkit keeps these setters internal (IMediaElement.Position,
    // MediaWidth, MediaHeight and CurrentStateChanged); its platform managers
    // reach them as friends. Reflection here avoids InternalsVisibleTo.
    private static readonly MethodInfo? s_setPosition = typeof(IMediaElement).GetProperty(nameof(IMediaElement.Position))?.GetSetMethod(nonPublic: true);
    private static readonly MethodInfo? s_setMediaWidth = typeof(IMediaElement).GetProperty(nameof(IMediaElement.MediaWidth))?.GetSetMethod(nonPublic: true);
    private static readonly MethodInfo? s_setMediaHeight = typeof(IMediaElement).GetProperty(nameof(IMediaElement.MediaHeight))?.GetSetMethod(nonPublic: true);
    private static readonly MethodInfo? s_seekCompleted = typeof(IMediaElement).GetMethod("SeekCompleted",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly MethodInfo? s_currentStateChanged = typeof(IMediaElement).GetMethod("CurrentStateChanged",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    // States in which Windows pumps the position (MediaManager.allowUpdatePositionStates).
    private static bool AllowsPositionUpdate(MediaElementState state)
        => state is MediaElementState.Playing or MediaElementState.Paused or MediaElementState.Stopped;

    private TimeSpan _lastDuration = TimeSpan.MinValue;
    private double _playerRate = 1.0; // MediaPlayer.PlaybackRate (0 after Speed = 0)
    private IScreenWakeLock? _wakeLock;
    private bool _displayActiveRequested;
    private MprisBridge? _mpris;

    protected override object CreatePlatformView()
    {
        var platform = new SkiaMediaElement();
        // SkiaMediaElement runs a 250ms timer for the duration of any active
        // pipeline and fires StatusTick on the main thread. We push the
        // current Position + Duration onto the toolkit MediaElement here so
        // its bound sliders / labels see live updates.
        platform.StatusTick += PumpStatus;
        platform.MediaEnded += OnMediaEnded;
        platform.MediaOpened += OnMediaOpened;
        platform.MediaFailed += OnMediaFailed;
        platform.PlaybackStateChanged += OnPlaybackStateChanged;
        platform.VideoSizeChanged += OnVideoSizeChanged;
        platform.TransportCommand += OnTransportCommand;
        platform.SeekCompleted += OnSeekCompleted;
        return platform;
    }

    /// <summary>
    /// Tears the GStreamer pipeline down (MediaManager.Dispose and
    /// MauiMediaElement.Dispose on Windows), releases the screen-saver
    /// inhibition and withdraws the MPRIS player.
    /// </summary>
    protected override void DisconnectHandler(object platformView)
    {
        Dispose();
        if (platformView is SkiaMediaElement skia)
        {
            skia.StatusTick -= PumpStatus;
            skia.MediaEnded -= OnMediaEnded;
            skia.MediaOpened -= OnMediaOpened;
            skia.MediaFailed -= OnMediaFailed;
            skia.PlaybackStateChanged -= OnPlaybackStateChanged;
            skia.VideoSizeChanged -= OnVideoSizeChanged;
            skia.TransportCommand -= OnTransportCommand;
            skia.SeekCompleted -= OnSeekCompleted;
            skia.Dispose();
        }
        base.DisconnectHandler(platformView);
    }

    /// <summary>Releases the screen-saver inhibition and the MPRIS player (MediaElementHandler.Dispose on Windows).</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        ReleaseDisplay();
        if (_mpris != null)
        {
            MprisService.Shared.Withdraw(_mpris);
            _mpris = null;
        }
    }

    // ----- platform events -------------------------------------------------

    private IMediaElement? Media => VirtualView as IMediaElement;

    private void OnMediaOpened()
    {
        if (PlatformView is not SkiaMediaElement skia || Media is not { } media) return;
        // MediaManager.OnMediaElementMediaOpened: duration first, then the event, then metadata.
        _lastDuration = skia.Duration;
        media.Duration = skia.Duration;
        media.MediaOpened();
        UpdateMetadata(publish: true);
    }

    private void OnMediaFailed(string message)
    {
        // MediaManager.OnMediaElementMediaFailed: the toolkit sets Failed and raises MediaFailed.
        Media?.MediaFailed(new MediaFailedEventArgs(message));
        ReleaseDisplay(); // nothing will play
        UpdateMpris();
    }

    private void OnMediaEnded()
    {
        Media?.MediaEnded();
        // Nothing plays any more: let the screen saver run again.
        ReleaseDisplay();
        UpdateMpris();
    }

    private void OnPlaybackStateChanged(MediaElementState state)
    {
        if (Media is not { } media) return;
        s_currentStateChanged?.Invoke(media, new object[] { state });

        if (state == MediaElementState.Playing)
        {
            // MediaManager.OnPlaybackSessionPlaybackStateChanged: playing again
            // after Speed = 0 restores rate 1, and Speed follows it.
            if (_playerRate == 0)
            {
                _playerRate = 1.0;
                (PlatformView as SkiaMediaElement)?.SetRate(1.0);
                var dispatcher = (VirtualView as BindableObject)?.Dispatcher;
                if (dispatcher != null)
                    dispatcher.Dispatch(() => media.Speed = 1.0);
                else
                    media.Speed = 1.0;
            }
            if (media.ShouldKeepScreenOn)
                RequestDisplay();
            UpdateMetadata(publish: true);
        }
        else
        {
            UpdateMpris();
        }
    }

    /// <summary>MediaManager.OnPlaybackSessionSeekCompleted: the toolkit's SeekCompleted event.</summary>
    private void OnSeekCompleted()
    {
        if (Media is { } media)
            s_seekCompleted?.Invoke(media, null);
    }

    private void OnVideoSizeChanged(int width, int height)
    {
        if (Media is not { } media) return;
        s_setMediaWidth?.Invoke(media, new object[] { width });
        s_setMediaHeight?.Invoke(media, new object[] { height });
    }

    private void OnTransportCommand(MediaTransportCommand command, double value)
    {
        if (VirtualView is not CommunityToolkit.Maui.Views.MediaElement media) return;
        switch (command)
        {
            case MediaTransportCommand.PlayPause:
                if (media.CurrentState == MediaElementState.Playing || media.CurrentState == MediaElementState.Buffering)
                    media.Pause();
                else
                    media.Play();
                break;
            case MediaTransportCommand.Seek:
                _ = media.SeekTo(TimeSpan.FromSeconds(value));
                break;
            case MediaTransportCommand.ToggleMute:
                media.ShouldMute = !media.ShouldMute;
                break;
            case MediaTransportCommand.ToggleRepeat:
                media.ShouldLoopPlayback = !media.ShouldLoopPlayback;
                break;
            case MediaTransportCommand.SetRate:
                media.Speed = value;
                break;
        }
    }

    private void PumpStatus()
    {
        if (PlatformView is not SkiaMediaElement skia) return;
        if (Media is not { } media) return;

        UpdatePosition(skia, media);

        // Duration's setter is on the IMediaElement interface (not the
        // concrete class, which exposes it read-only). Only push on change —
        // bound sliders re-fire NotifyPropertyChanged on every assignment and
        // we don't want to thrash the UI 4 times per second.
        var dur = skia.Duration;
        if (dur != _lastDuration)
        {
            _lastDuration = dur;
            media.Duration = dur;
            UpdateMpris();
        }
    }

    /// <summary>MediaManager.PlatformUpdatePosition: only while Playing, Paused or Stopped.</summary>
    private void UpdatePosition(SkiaMediaElement skia, IMediaElement media)
    {
        if (!AllowsPositionUpdate(media.CurrentState)) return;
        var position = skia.Position;
        s_setPosition?.Invoke(media, new object[] { position });
        if (_mpris != null)
            MprisService.Shared.UpdatePosition(_mpris, position);
    }

    // ----- screen saver (ShouldKeepScreenOn) --------------------------------

    private void RequestDisplay()
    {
        if (_displayActiveRequested) return;
        _wakeLock ??= MediaScreenWakeLock.Factory();
        _wakeLock.Acquire();
        _displayActiveRequested = true;
    }

    private void ReleaseDisplay()
    {
        if (!_displayActiveRequested) return;
        _wakeLock?.Release();
        _displayActiveRequested = false;
    }

    /// <summary>True while this handler holds the screen-saver inhibition.</summary>
    internal bool IsKeepingScreenOn => _displayActiveRequested;

    // ----- MPRIS (metadata) --------------------------------------------------

    private static bool HasMetadata(IMediaElement media)
        => !string.IsNullOrEmpty(media.MetadataTitle) || !string.IsNullOrEmpty(media.MetadataArtist)
            || !string.IsNullOrEmpty(media.MetadataArtworkUrl);

    private MprisState SnapshotState(IMediaElement media) => new(
        media.MetadataTitle ?? "",
        media.MetadataArtist ?? "",
        media.MetadataArtworkUrl ?? "",
        media.Duration,
        MprisState.StatusOf(media.CurrentState),
        media.ShouldMute ? 0 : media.Volume,
        media.Speed,
        media.ShouldLoopPlayback,
        media.Duration > TimeSpan.Zero);

    /// <summary>
    /// MediaManager.UpdateMetadata: on Windows the SMTC display gets the
    /// title, artist and artwork when media opens. Here the element becomes
    /// the MPRIS player when it opens or plays media that has metadata.
    /// </summary>
    private void UpdateMetadata(bool publish)
    {
        if (Media is not { } media || !MprisService.IsEnabled) return;
        if (!HasMetadata(media))
        {
            if (_mpris != null)
                MprisService.Shared.Withdraw(_mpris);
            return;
        }
        _mpris ??= new MprisBridge(this);
        if (publish && (MprisService.Shared.Active != _mpris))
            MprisService.Shared.Publish(_mpris, SnapshotState(media));
        else
            MprisService.Shared.Update(_mpris, SnapshotState(media));
    }

    private void UpdateMpris()
    {
        if (_mpris != null && Media is { } media)
            MprisService.Shared.Update(_mpris, SnapshotState(media));
    }

    /// <summary>The MPRIS player of this handler, once it has one (tests).</summary>
    internal IMprisPlayer? MprisPlayer => _mpris;

    private sealed class MprisBridge(LinuxMediaElementHandler handler) : IMprisPlayer
    {
        public void Execute(MprisCommand command, double value)
        {
            void Run()
            {
                if (handler.VirtualView is not CommunityToolkit.Maui.Views.MediaElement media) return;
                switch (command)
                {
                    case MprisCommand.Play:
                        media.Play();
                        break;
                    case MprisCommand.Pause:
                        media.Pause();
                        break;
                    case MprisCommand.PlayPause:
                        if (media.CurrentState is MediaElementState.Playing or MediaElementState.Buffering)
                            media.Pause();
                        else
                            media.Play();
                        break;
                    case MprisCommand.Stop:
                        media.Stop();
                        break;
                    case MprisCommand.SeekTo:
                        _ = media.SeekTo(TimeSpan.FromSeconds(value));
                        break;
                    case MprisCommand.SetVolume:
                        media.Volume = Math.Clamp(value, 0, 1);
                        break;
                    case MprisCommand.SetRate:
                        media.Speed = value;
                        break;
                    case MprisCommand.SetLoop:
                        media.ShouldLoopPlayback = value != 0;
                        break;
                }
            }

            if (LinuxDispatcher.Main is { } main && !LinuxDispatcher.IsMainThread)
                main.Dispatch(Run);
            else
                Run();
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
        var headers = media.Source is UriMediaSource uriSource && uriSource.HttpHeaders.Count > 0
            ? uriSource.HttpHeaders.ToArray()
            : null;
        if (uri == skia.CurrentUri && (headers ?? Array.Empty<KeyValuePair<string, string>>()).SequenceEqual(skia.HttpHeaders))
            return;

        // MediaManager.PlatformUpdateSource: a cleared source reports 0 x 0 and
        // None (SetSource raises it); a new one starts at position and duration 0.
        if (uri != null && handler is LinuxMediaElementHandler h && h.Media is { } m)
        {
            s_setPosition?.Invoke(m, new object[] { TimeSpan.Zero });
            m.Duration = TimeSpan.Zero;
            h._lastDuration = TimeSpan.Zero;
        }
        skia.SetSource(uri, headers);
        if (uri != null && media.ShouldAutoPlay)
            skia.Play();
    }

    public new static void MapPlayRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is { } skia) skia.Play();
        // MediaManager.PlatformPlay: keep the display on while playing.
        if (handler is LinuxMediaElementHandler h && media.ShouldKeepScreenOn)
            h.RequestDisplay();
    }

    /// <summary>
    /// MediaManager.PlatformUpdateSpeed: the rate follows Speed; Speed 0
    /// pauses the player and a positive Speed after 0 plays again.
    /// </summary>
    public new static void MapSpeed(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (handler is not LinuxMediaElementHandler h || GetSkia(handler) is not { } skia) return;
        double previous = h._playerRate;
        double speed = media.Speed;
        h._playerRate = speed;
        skia.SetRate(speed); // ignores 0
        if (speed == 0 && previous > 0)
            skia.Pause();
        else if (speed > 0 && previous == 0)
            media.Play();
        h.UpdateMpris();
    }

    public new static void MapVolume(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.SetVolume(media.Volume);
        (handler as LinuxMediaElementHandler)?.UpdateMpris();
    }

    public new static void MapShouldMute(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.SetMute(media.ShouldMute);
        (handler as LinuxMediaElementHandler)?.UpdateMpris();
    }

    public static void MapShouldLoopPlayback(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.ShouldLoopPlayback = media.ShouldLoopPlayback;
        (handler as LinuxMediaElementHandler)?.UpdateMpris();
    }

    /// <summary>
    /// MediaManager.PlatformUpdateShouldShowPlaybackControls
    /// (MediaPlayerElement.AreTransportControlsEnabled): the controls overlay.
    /// </summary>
    public new static void MapShouldShowPlaybackControls(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (GetSkia(handler) is { } skia) skia.ShowPlaybackControls = media.ShouldShowPlaybackControls;
    }

    /// <summary>
    /// MediaManager.PlatformUpdateShouldKeepScreenOn: turning it on while
    /// Playing, Paused or Stopped inhibits the screen saver at once; turning
    /// it off releases the inhibition.
    /// </summary>
    public new static void MapShouldKeepScreenOn(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (handler is not LinuxMediaElementHandler h) return;
        if (media.ShouldKeepScreenOn)
        {
            if (AllowsPositionUpdate(media.CurrentState))
                h.RequestDisplay();
        }
        else
        {
            h.ReleaseDisplay();
        }
    }

    /// <summary>Metadata changed: the published MPRIS player shows it.</summary>
    public static void MapMetadata(object handler, CommunityToolkit.Maui.Views.MediaElement media)
    {
        if (handler is LinuxMediaElementHandler h && media.CurrentState is MediaElementState.Playing or MediaElementState.Paused)
            h.UpdateMetadata(publish: media.CurrentState == MediaElementState.Playing);
    }

    // ----- command mappers ---------------------------------------------------

    public new static void MapPauseRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is { } skia) skia.Pause();
        (handler as LinuxMediaElementHandler)?.ReleaseDisplay();
    }

    public new static void MapStopRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is { } skia) skia.Stop();
        (handler as LinuxMediaElementHandler)?.ReleaseDisplay();
    }

    public new static void MapSeekRequested(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        if (GetSkia(handler) is not { } skia || args == null) return;
        // MediaSeekRequestedEventArgs is public in the toolkit, but reading
        // RequestedPosition by name keeps this working across toolkit majors.
        var pos = args.GetType().GetProperty("RequestedPosition")?.GetValue(args);
        if (pos is TimeSpan ts)
        {
            skia.SeekTo(ts);
            if (handler is LinuxMediaElementHandler { _mpris: { } mpris })
                MprisService.Shared.NotifySeeked(mpris, ts);
        }

        // CRITICAL: SeekTo on the toolkit's MediaElement awaits a
        // TaskCompletionSource that the PLATFORM HANDLER is contractually
        // required to complete after issuing the seek. The TCS is exposed via
        // IAsynchronousMediaElementHandler.SeekCompletedTCS. Without
        // TrySetResult here, the first SeekTo() never returns, the awaiter
        // hangs, the internal semaphore stays held, and every subsequent seek
        // request queues behind a Task that will never finish. (Calling the
        // event-raising IMediaElement.SeekCompleted() is NOT enough — it only
        // fires the public SeekCompleted event, doesn't complete the TCS.)
        ((IAsynchronousMediaElementHandler)media).SeekCompletedTCS.TrySetResult();
    }

    public new static void MapStatusUpdated(MediaElementHandler handler, CommunityToolkit.Maui.Views.MediaElement media, object? args)
    {
        // MediaManager.UpdateStatus → PlatformUpdatePosition.
        if (handler is LinuxMediaElementHandler h && GetSkia(handler) is { } skia)
            h.UpdatePosition(skia, media);
    }
}
