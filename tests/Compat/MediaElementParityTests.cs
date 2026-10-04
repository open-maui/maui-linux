// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.MediaElement.Handlers;
using Microsoft.Maui.Platform.Linux.MediaElement.Hosting;
using Microsoft.Maui.Platform.Linux.MediaElement.Services;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;
using SkiaSharp;
using Tmds.DBus;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// CommunityToolkit.Maui.MediaElement behaves on Linux as the toolkit's
/// Windows MediaManager makes it behave: MediaOpened / MediaFailed /
/// MediaEnded / PositionChanged / StateChanged, MediaWidth and MediaHeight,
/// Speed, HttpHeaders, ShouldKeepScreenOn, ShouldShowPlaybackControls, MPRIS
/// metadata and teardown in DisconnectHandler. Real GStreamer pipelines over
/// generated media (a silent WAV; a VP8 clip when ffmpeg is installed).
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class MediaElementParityTests : IDisposable
{
    private readonly string _wav = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.wav");
    private readonly List<string> _files = new();
    private readonly Func<IScreenWakeLock> _previousWakeLock = MediaScreenWakeLock.Factory;
    private readonly MprisService _previousMpris = MprisService.Shared;
    private readonly FakeWakeLock _wakeLock = new();

    public MediaElementParityTests()
    {
        WriteSilentWav(_wav, seconds: 1);
        _files.Add(_wav);
        MediaScreenWakeLock.Factory = () => _wakeLock;
        // No session bus traffic from these tests: registration fails quietly
        // and the D-Bus object is driven directly.
        MprisService.Shared = new MprisService(() => Task.FromException<Connection>(new InvalidOperationException("no bus in tests")));
    }

    public void Dispose()
    {
        MediaScreenWakeLock.Factory = _previousWakeLock;
        MprisService.Shared = _previousMpris;
        foreach (var file in _files)
            try { File.Delete(file); } catch (IOException) { }
    }

    private static CompatHost Host(MediaElement media, int width = 800, int height = 600)
        => new(new ContentPage { Content = media },
               b => b.UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false).UseLinuxMediaElement(),
               width, height);

    private static async Task<bool> PumpUntil(CompatHost host, Func<bool> condition, int timeoutMs = 8000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            host.Render();
            if (condition()) return true;
            await Task.Delay(30);
        }
        host.Render();
        return condition();
    }

    [Fact]
    public async Task Opening_a_source_raises_MediaOpened_with_the_duration_and_settles_paused()
    {
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true };
        var states = new List<MediaElementState>();
        int opened = 0;
        media.StateChanged += (_, e) => states.Add(e.NewState);
        media.MediaOpened += (_, _) => opened++;
        using var host = Host(media);

        media.Source = MediaSource.FromFile(_wav);

        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue($"states: {string.Join(",", states)}");
        opened.Should().Be(1, "MediaOpened is raised once the media is open, playing or not");
        media.Duration.TotalSeconds.Should().BeApproximately(1.0, 0.1);
        states.Should().ContainInOrder(MediaElementState.Opening, MediaElementState.Paused);
        media.MediaWidth.Should().Be(0, "a WAV has no video");
    }

    [Fact]
    public async Task Play_pause_stop_and_the_end_drive_CurrentState_and_the_events()
    {
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true };
        var states = new List<MediaElementState>();
        int ended = 0, positionChanged = 0;
        media.StateChanged += (_, e) => states.Add(e.NewState);
        media.MediaEnded += (_, _) => ended++;
        media.PositionChanged += (_, _) => positionChanged++;
        using var host = Host(media);
        media.Source = MediaSource.FromFile(_wav);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();

        media.Play();
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue();
        media.Pause();
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();
        media.Stop();
        media.CurrentState.Should().Be(MediaElementState.Stopped, "Stop reports Stopped at once, as MediaManager.PlatformStop does");

        // The audio-only end comes from the bus (no video sink is linked).
        media.Play();
        (await PumpUntil(host, () => ended == 1)).Should().BeTrue($"states: {string.Join(",", states)}");
        media.CurrentState.Should().Be(MediaElementState.Stopped, "the toolkit sets Stopped at the end");
        positionChanged.Should().BeGreaterThan(0, "the position is pumped while playing");

        // Played again after the end: from the beginning.
        media.Play();
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue();
        (await PumpUntil(host, () => ended == 2)).Should().BeTrue("it played to the end a second time");
    }

    [Fact]
    public async Task A_missing_file_raises_MediaFailed_and_the_Failed_state()
    {
        var media = new MediaElement { ShouldAutoPlay = true, ShouldMute = true };
        MediaFailedEventArgs? failure = null;
        media.MediaFailed += (_, e) => failure = e;
        using var host = Host(media);

        media.Source = MediaSource.FromFile(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mp4"));

        (await PumpUntil(host, () => failure != null)).Should().BeTrue();
        failure!.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        media.CurrentState.Should().Be(MediaElementState.Failed);
    }

    [Fact]
    public async Task A_video_reports_MediaWidth_and_MediaHeight()
    {
        var video = TempVideo(320, 180, seconds: 1);
        if (video == null) return; // no ffmpeg
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true };
        using var host = Host(media);

        media.Source = MediaSource.FromFile(video);

        (await PumpUntil(host, () => media.MediaWidth > 0)).Should().BeTrue();
        media.MediaWidth.Should().Be(320);
        media.MediaHeight.Should().Be(180);

        media.Source = null;
        media.MediaWidth.Should().Be(0);
        media.CurrentState.Should().Be(MediaElementState.None);
    }

    [Fact]
    public async Task Speed_sets_the_rate_and_zero_pauses_until_played_again_at_normal_speed()
    {
        var wav = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.wav");
        _files.Add(wav);
        WriteSilentWav(wav, seconds: 6);
        var media = new MediaElement { ShouldAutoPlay = true, ShouldMute = true, Speed = 2.0 };
        using var host = Host(media);
        var skia = (SkiaMediaElement)CompatHost.PlatformOf(media);

        media.Source = MediaSource.FromFile(wav);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue();
        skia.Rate.Should().Be(2.0);
        var start = skia.Position;
        var clock = Stopwatch.StartNew();
        await PumpUntil(host, () => clock.ElapsedMilliseconds > 1000, 1500);
        var advanced = (skia.Position - start).TotalSeconds / clock.Elapsed.TotalSeconds;
        advanced.Should().BeGreaterThan(1.5, "the media plays at twice the speed");

        media.Speed = 0;
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue("Speed 0 pauses, as PlaybackRate 0 does");

        media.Play();
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing && media.Speed == 1.0)).Should().BeTrue(
            "playing after Speed 0 restores rate 1 and Speed follows (MediaManager.OnPlaybackSessionPlaybackStateChanged)");
        skia.Rate.Should().Be(1.0);
    }

    [Fact]
    public async Task HttpHeaders_are_sent_with_the_request()
    {
        using var server = new HeaderEchoServer(File.ReadAllBytes(_wav));
        var source = (UriMediaSource)MediaSource.FromUri(server.Url);
        source.HttpHeaders["Authorization"] = "Bearer parity-token";
        source.HttpHeaders["User-Agent"] = "OpenMauiParity/1.0";
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true };
        int opened = 0;
        media.MediaOpened += (_, _) => opened++;
        using var host = Host(media);

        media.Source = source;

        (await PumpUntil(host, () => server.Requests.Count > 0 && opened > 0)).Should().BeTrue();
        server.Requests.Should().Contain(r => r.Authorization == "Bearer parity-token");
        server.Requests.Should().Contain(r => r.UserAgent == "OpenMauiParity/1.0");
    }

    [Fact]
    public async Task ShouldKeepScreenOn_inhibits_the_screen_saver_while_playing()
    {
        var wav = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.wav");
        _files.Add(wav);
        WriteSilentWav(wav, seconds: 5);
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true, ShouldKeepScreenOn = true, Source = MediaSource.FromFile(wav) };
        using var host = Host(media);
        var handler = (LinuxMediaElementHandler)media.Handler!;
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();
        _wakeLock.Held.Should().BeFalse("nothing plays yet");

        media.Play();
        _wakeLock.Held.Should().BeTrue("MediaManager.PlatformPlay requests the display");
        media.Pause();
        _wakeLock.Held.Should().BeFalse();

        media.ShouldKeepScreenOn = false;
        media.Play();
        _wakeLock.Held.Should().BeFalse("not asked to keep the screen on");
        media.ShouldKeepScreenOn = true;
        _wakeLock.Held.Should().BeTrue("turned on while playing");
        handler.IsKeepingScreenOn.Should().BeTrue();

        media.Handler = null;
        _wakeLock.Held.Should().BeFalse("DisconnectHandler releases it");
        _wakeLock.Acquired.Should().Be(_wakeLock.Released, "every request is balanced");
    }

    [Fact]
    public async Task DisconnectHandler_tears_the_pipeline_down()
    {
        var media = new MediaElement { ShouldAutoPlay = true, ShouldMute = true, Source = MediaSource.FromFile(_wav) };
        using var host = Host(media);
        var skia = (SkiaMediaElement)CompatHost.PlatformOf(media);
        skia.ShowPlaybackControls.Should().BeFalse("ShouldShowPlaybackControls is off by default (MediaElementDefaults)");
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue();

        media.Handler = null;

        skia.IsDisposed.Should().BeTrue();
        skia.IsPlaying.Should().BeFalse();
        skia.Position.Should().Be(TimeSpan.Zero, "the playbin is gone");
    }

    [Fact]
    public async Task Playback_controls_show_while_paused_and_play_on_a_click()
    {
        var wav = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.wav");
        _files.Add(wav);
        WriteSilentWav(wav, seconds: 5);
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true, ShouldShowPlaybackControls = true, Source = MediaSource.FromFile(wav) };
        using var host = Host(media, 640, 360);
        var skia = (SkiaMediaElement)CompatHost.PlatformOf(media);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();

        skia.ShowPlaybackControls.Should().BeTrue();
        host.Render();
        var bounds = new SKRect((float)skia.Bounds.Left, (float)skia.Bounds.Top, (float)skia.Bounds.Right, (float)skia.Bounds.Bottom);
        var bar = MediaTransportControls.BarRect(bounds);
        var barRect = new SKRectI((int)bar.Left, (int)bar.Top + 2, (int)bar.Right, (int)bar.Bottom - 2);
        host.CountPixelsNot(SKColors.Black, barRect).Should().BeGreaterThan(50, "the controls are drawn over the (black) picture");

        var play = MediaTransportControls.ButtonRect(bounds, MediaTransportControls.Part.PlayPause);
        host.DisplayWindow.RaisePointerPressed(play.MidX, play.MidY);
        host.DisplayWindow.RaisePointerReleased(play.MidX, play.MidY);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue("the play button plays");

        media.ShouldShowPlaybackControls = false;
        skia.ShowPlaybackControls.Should().BeFalse();
        media.Pause();
        host.Render();
        host.CountPixelsNot(SKColors.Black, barRect).Should().Be(0, "no controls without ShouldShowPlaybackControls");
    }

    [Fact]
    public async Task Metadata_is_published_over_MPRIS_and_media_keys_reach_the_element()
    {
        var wav = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.wav");
        _files.Add(wav);
        WriteSilentWav(wav, seconds: 5);
        var media = new MediaElement
        {
            ShouldAutoPlay = false,
            ShouldMute = true,
            MetadataTitle = "Parity Title",
            MetadataArtist = "Parity Artist",
            MetadataArtworkUrl = "https://example.invalid/art.png",
            Source = MediaSource.FromFile(wav),
        };
        using var host = Host(media);
        var handler = (LinuxMediaElementHandler)media.Handler!;
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();

        var mpris = MprisService.Shared;
        mpris.Active.Should().BeSameAs(handler.MprisPlayer, "opening media with metadata publishes it (MediaManager.UpdateMetadata)");
        var props = await mpris.DBusObject.GetAllPlayerAsync();
        var metadata = (IDictionary<string, object>)props["Metadata"];
        metadata["xesam:title"].Should().Be("Parity Title");
        ((string[])metadata["xesam:artist"]).Should().Equal("Parity Artist");
        metadata["mpris:artUrl"].Should().Be("https://example.invalid/art.png");
        ((long)metadata["mpris:length"]).Should().BeInRange(4_900_000, 5_100_000);
        props["PlaybackStatus"].Should().Be("Paused");

        await mpris.DBusObject.PlayPauseAsync();
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue("the play/pause media key plays");
        (await mpris.DBusObject.GetPlayerAsync("PlaybackStatus")).Should().Be("Playing");
        await mpris.DBusObject.PauseAsync();
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();

        media.Handler = null;
        mpris.Active.Should().BeNull("a disconnected element is withdrawn");
    }

    [Fact]
    public async Task An_element_without_metadata_is_not_published()
    {
        var media = new MediaElement { ShouldAutoPlay = true, ShouldMute = true, Source = MediaSource.FromFile(_wav) };
        using var host = Host(media);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue();
        MprisService.Shared.Active.Should().BeNull("a background video without metadata never shows in the desktop's media controls");
    }

    [Fact]
    public async Task MPRIS_player_is_reachable_on_a_session_bus()
    {
        // Started without awaiting: the test must reach the CompatHost on the
        // xunit thread it started on (an await here could resume elsewhere and
        // leave the host's GLib synchronization context on a pool thread).
        using var bus = PrivateBus.Start();
        if (bus == null) return; // no dbus-daemon here
        var service = new MprisService(async () =>
        {
            var c = new Connection(new ClientConnectionOptions(bus.Address) { RunContinuationsAsynchronously = true });
            await c.ConnectAsync().WaitAsync(Timeout);
            return c;
        });
        MprisService.Shared = service;
        var wav = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.wav");
        _files.Add(wav);
        WriteSilentWav(wav, seconds: 5);
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true, MetadataTitle = "On the bus", Source = MediaSource.FromFile(wav) };
        using var host = Host(media);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Paused)).Should().BeTrue();
        await service.WhenSettled().WaitAsync(Timeout);
        service.BusName.Should().StartWith("org.mpris.MediaPlayer2.");

        using var client = new Connection(new ClientConnectionOptions(bus.Address) { RunContinuationsAsynchronously = true });
        await client.ConnectAsync().WaitAsync(Timeout);
        var player = client.CreateProxy<IMprisPlayerInterface>(service.BusName!, new ObjectPath(MprisService.ObjectPathValue));
        var metadata = (IDictionary<string, object>)await player.GetPlayerAsync("Metadata").WaitAsync(Timeout);
        metadata["xesam:title"].Should().Be("On the bus");

        await player.PlayAsync().WaitAsync(Timeout);
        (await PumpUntil(host, () => media.CurrentState == MediaElementState.Playing)).Should().BeTrue("Play over D-Bus plays");

        media.Handler = null;
        await service.WhenSettled().WaitAsync(Timeout);
        service.BusName.Should().BeNull("the bus name is released when no player is left");
    }

    // ---- helpers ------------------------------------------------------------

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private sealed class FakeWakeLock : IScreenWakeLock
    {
        public int Acquired, Released;
        public bool Held => Acquired > Released;
        public void Acquire() => Acquired++;
        public void Release() => Released++;
    }

    private string? TempVideo(int width, int height, int seconds)
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-parity-{Guid.NewGuid():N}.webm");
        try
        {
            using var ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg",
                $"-v error -y -f lavfi -i color=c=red:s={width}x{height}:d={seconds} -c:v libvpx -b:v 100k \"{path}\"") { RedirectStandardError = true });
            ffmpeg!.WaitForExit(30000);
            if (ffmpeg.ExitCode != 0 || !File.Exists(path)) return null;
            _files.Add(path);
            return path;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static void WriteSilentWav(string path, int seconds)
    {
        const int rate = 8000;
        int dataBytes = rate * 2 * seconds;
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes); w.Write(new byte[dataBytes]);
    }

    /// <summary>Serves one body over HTTP (with Range support) and records each request's headers.</summary>
    private sealed class HeaderEchoServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly byte[] _body;

        public record Request(string? Authorization, string? UserAgent);

        public List<Request> Requests { get; } = new();
        public string Url { get; }

        public HeaderEchoServer(byte[] body)
        {
            _body = body;
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}/clip.wav";
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception) { return; }
                lock (Requests)
                    Requests.Add(new Request(context.Request.Headers["Authorization"], context.Request.UserAgent));
                try
                {
                    var response = context.Response;
                    response.ContentType = "audio/x-wav";
                    response.AddHeader("Accept-Ranges", "bytes");
                    int start = 0;
                    var range = context.Request.Headers["Range"];
                    if (range != null && range.StartsWith("bytes=", StringComparison.Ordinal))
                    {
                        var from = range[6..].Split('-')[0];
                        if (int.TryParse(from, out var s) && s < _body.Length) start = s;
                        response.StatusCode = 206;
                        response.AddHeader("Content-Range", $"bytes {start}-{_body.Length - 1}/{_body.Length}");
                    }
                    response.ContentLength64 = _body.Length - start;
                    await response.OutputStream.WriteAsync(_body.AsMemory(start));
                    response.Close();
                }
                catch (Exception)
                {
                    // client went away
                }
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch (ObjectDisposedException) { }
            _listener.Close();
        }
    }

    /// <summary>A throwaway dbus-daemon, so the MPRIS test never touches the user's session.</summary>
    private sealed class PrivateBus : IDisposable
    {
        private readonly Process _process;
        public string Address { get; }

        private PrivateBus(Process process, string address)
        {
            _process = process;
            Address = address;
        }

        public static PrivateBus? Start()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo("dbus-daemon", "--session --nofork --print-address")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                });
                if (process == null) return null;
                var read = Task.Run(() => process.StandardOutput.ReadLine());
                var line = read.Wait(TimeSpan.FromSeconds(5)) ? read.Result : null;
                if (string.IsNullOrWhiteSpace(line))
                {
                    process.Kill();
                    return null;
                }
                return new PrivateBus(process, line.Trim());
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            try { _process.Kill(); } catch (InvalidOperationException) { }
            _process.Dispose();
        }
    }
}
