// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Core;
using FluentAssertions;
using Microsoft.Maui.Platform.Linux.MediaElement.Services;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;
using SkiaSharp;
using Tmds.DBus;
using Xunit;

namespace Microsoft.Maui.Platform.Tests.MediaElement;

/// <summary>The playback controls overlay (ShouldShowPlaybackControls): visibility, hit testing, commands.</summary>
public class MediaTransportControlsTests
{
    private static readonly SKRect Bounds = new(100, 50, 740, 410);
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private MediaTransportControls Create(bool playing = false)
    {
        var controls = new MediaTransportControls(() => _now)
        {
            Enabled = true,
            HasMedia = true,
            IsPlaying = playing,
            Duration = TimeSpan.FromSeconds(100),
        };
        return controls;
    }

    [Fact]
    public void Shown_while_not_playing_and_hidden_three_seconds_after_the_pointer_last_moved_while_playing()
    {
        var controls = Create(playing: false);
        controls.IsVisible.Should().BeTrue("paused media shows its controls");

        controls.IsPlaying = true;
        controls.IsVisible.Should().BeFalse("playing with no pointer activity");
        controls.PointerMoved(Bounds, 300, 200);
        controls.IsVisible.Should().BeTrue();
        controls.TimeUntilHide.Should().Be(MediaTransportControls.HideDelay);

        _now += TimeSpan.FromSeconds(3.1);
        controls.IsVisible.Should().BeFalse();
        controls.TimeUntilHide.Should().BeNull();
    }

    [Fact]
    public void Nothing_shows_without_ShouldShowPlaybackControls_or_without_media()
    {
        var controls = Create();
        controls.Enabled = false;
        controls.IsVisible.Should().BeFalse();
        controls.HitTest(Bounds, Bounds.MidX, Bounds.Bottom - 10).Should().BeFalse("hidden controls never take a press");

        controls.Enabled = true;
        controls.HasMedia = false;
        controls.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void A_click_on_play_pause_raises_PlayPause_and_a_press_above_the_bar_is_not_taken()
    {
        var controls = Create();
        var commands = new List<(MediaTransportCommand, double)>();
        controls.Command += (c, v) => commands.Add((c, v));

        controls.PointerPressed(Bounds, Bounds.MidX, Bounds.Top + 20).Should().BeFalse("the picture itself is not a control");

        var play = MediaTransportControls.ButtonRect(Bounds, MediaTransportControls.Part.PlayPause);
        controls.PointerPressed(Bounds, play.MidX, play.MidY).Should().BeTrue();
        controls.PointerReleased(Bounds, play.MidX, play.MidY);
        commands.Should().Equal((MediaTransportCommand.PlayPause, 0));
    }

    [Fact]
    public void Releasing_away_from_the_button_is_not_a_click()
    {
        var controls = Create();
        int count = 0;
        controls.Command += (_, _) => count++;
        var mute = MediaTransportControls.ButtonRect(Bounds, MediaTransportControls.Part.Mute);
        controls.PointerPressed(Bounds, mute.MidX, mute.MidY);
        controls.PointerReleased(Bounds, Bounds.MidX, Bounds.Top + 10);
        count.Should().Be(0);
    }

    [Fact]
    public void Dragging_the_seek_bar_seeks_to_the_release_point()
    {
        var controls = Create();
        var commands = new List<(MediaTransportCommand Command, double Value)>();
        controls.Command += (c, v) => commands.Add((c, v));
        var track = MediaTransportControls.SeekTrackRect(Bounds);

        controls.PointerPressed(Bounds, track.Left + track.Width * 0.1f, track.MidY).Should().BeTrue();
        controls.PointerMoved(Bounds, track.Left + track.Width * 0.75f, track.MidY);
        controls.PointerReleased(Bounds, track.Left + track.Width * 0.75f, track.MidY);

        commands.Should().ContainSingle();
        commands[0].Command.Should().Be(MediaTransportCommand.Seek);
        commands[0].Value.Should().BeApproximately(75, 0.5);
    }

    [Fact]
    public void The_rate_menu_offers_the_WinUI_rates()
    {
        var controls = Create();
        var commands = new List<(MediaTransportCommand Command, double Value)>();
        controls.Command += (c, v) => commands.Add((c, v));
        var rate = MediaTransportControls.ButtonRect(Bounds, MediaTransportControls.Part.RateButton);
        controls.PointerPressed(Bounds, rate.MidX, rate.MidY);
        controls.PointerReleased(Bounds, rate.MidX, rate.MidY);
        commands.Should().BeEmpty("the button opens the menu");

        var item = MediaTransportControls.RateItemRect(Bounds, 3); // 1.5
        controls.PointerPressed(Bounds, item.MidX, item.MidY).Should().BeTrue();
        controls.PointerReleased(Bounds, item.MidX, item.MidY);
        commands.Should().Equal((MediaTransportCommand.SetRate, 1.5));
        MediaTransportControls.Rates.Should().Equal(0.25, 0.5, 1.0, 1.5, 2.0);
    }

    [Fact]
    public void Draws_into_the_bottom_bar_only()
    {
        var controls = Create();
        using var surface = SKSurface.Create(new SKImageInfo(840, 460));
        surface.Canvas.Clear(SKColors.Black);
        controls.Draw(surface.Canvas, Bounds);
        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();
        var bar = MediaTransportControls.BarRect(Bounds);
        pixels.GetPixelColor((int)Bounds.MidX, (int)(Bounds.Top + 20)).Should().Be(SKColors.Black, "the picture above the bar is untouched");
        var play = MediaTransportControls.ButtonRect(Bounds, MediaTransportControls.Part.PlayPause);
        pixels.GetPixelColor((int)play.MidX, (int)play.MidY).Red.Should().BeGreaterThan(200, "the white play glyph");
        bar.Height.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(3725, "1:02:05")]
    public void Times_read_like_WinUI(int seconds, string expected)
        => MediaTransportControls.FormatTime(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
}

/// <summary>The MPRIS object (metadata, status, commands) without a bus.</summary>
public class MprisServiceTests
{
    private sealed class Player : IMprisPlayer
    {
        public List<(MprisCommand, double)> Commands { get; } = new();
        public void Execute(MprisCommand command, double value) => Commands.Add((command, value));
    }

    private static MprisService NoBus() => new(() => Task.FromException<Connection>(new InvalidOperationException("no bus")));

    private static MprisState State(string status = "Paused") =>
        new("Title", "Artist", "file:///art.png", TimeSpan.FromSeconds(10), status, 0.5, 1.0, false, true);

    [Theory]
    [InlineData("CiteLynq", 42, "org.mpris.MediaPlayer2.CiteLynq.instance42")]
    [InlineData("My App-2", 7, "org.mpris.MediaPlayer2.MyApp2.instance7")]
    [InlineData("3D Viewer", 1, "org.mpris.MediaPlayer2._3DViewer.instance1")]
    [InlineData("", 1, "org.mpris.MediaPlayer2.openmaui.instance1")]
    public void Bus_names_are_valid_MPRIS_names(string app, int pid, string expected)
        => MprisService.BusNameFor(app, pid).Should().Be(expected);

    [Fact]
    public async Task Published_metadata_and_status_read_back_as_MPRIS_properties()
    {
        var service = NoBus();
        var player = new Player();
        service.Publish(player, State());

        var props = await service.DBusObject.GetAllPlayerAsync();
        var metadata = (IDictionary<string, object>)props["Metadata"];
        metadata["xesam:title"].Should().Be("Title");
        ((string[])metadata["xesam:artist"]).Should().Equal("Artist");
        metadata["mpris:artUrl"].Should().Be("file:///art.png");
        metadata["mpris:length"].Should().Be(10_000_000L);
        metadata["mpris:trackid"].Should().BeOfType<ObjectPath>();
        props["PlaybackStatus"].Should().Be("Paused");
        props["CanGoNext"].Should().Be(false, "MediaElement has no playlist");
        props["CanPlay"].Should().Be(true);
        props["CanSeek"].Should().Be(true);
        props["Volume"].Should().Be(0.5);
        (await service.DBusObject.GetRootAsync("Identity")).Should().BeOfType<string>();
    }

    [Fact]
    public async Task Changes_are_signalled_and_only_the_published_player_updates()
    {
        var service = NoBus();
        var player = new Player();
        var other = new Player();
        var changes = new List<PropertyChanges>();
        await service.DBusObject.WatchPlayerPropertiesAsync(c => changes.Add(c));
        service.Publish(player, State());
        changes.Clear();

        service.Update(player, State("Playing"));
        changes.Should().ContainSingle();
        changes[0].Changed.Select(c => c.Key).Should().Equal("PlaybackStatus");

        service.Update(other, State("Stopped"));
        (await service.DBusObject.GetPlayerAsync("PlaybackStatus")).Should().Be("Playing", "another element does not change the published one");

        service.Update(player, State("Playing"));
        changes.Should().ContainSingle("an unchanged state is not signalled");
    }

    [Fact]
    public async Task Media_keys_and_property_writes_reach_the_published_player()
    {
        var service = NoBus();
        var player = new Player();
        service.Publish(player, State());
        service.UpdatePosition(player, TimeSpan.FromSeconds(4));

        await service.DBusObject.PlayPauseAsync();
        await service.DBusObject.PlayAsync();
        await service.DBusObject.PauseAsync();
        await service.DBusObject.StopAsync();
        await service.DBusObject.SeekAsync(2_000_000);
        await service.DBusObject.SetPositionAsync(service.DBusObject.TrackId, 1_000_000);
        await service.DBusObject.SetPositionAsync(new ObjectPath("/stale"), 1_000_000);
        await service.DBusObject.SetPlayerAsync("Volume", 0.25);
        await service.DBusObject.SetPlayerAsync("LoopStatus", "Track");
        await service.DBusObject.NextAsync();

        player.Commands.Should().Equal(
            (MprisCommand.PlayPause, 0),
            (MprisCommand.Play, 0),
            (MprisCommand.Pause, 0),
            (MprisCommand.Stop, 0),
            (MprisCommand.SeekTo, 6.0),
            (MprisCommand.SeekTo, 1.0),
            (MprisCommand.SetVolume, 0.25),
            (MprisCommand.SetLoop, 1));
    }

    [Fact]
    public async Task A_withdrawn_player_leaves_an_empty_stopped_player()
    {
        var service = NoBus();
        var player = new Player();
        service.Publish(player, State("Playing"));
        service.Withdraw(player);

        service.Active.Should().BeNull();
        (await service.DBusObject.GetPlayerAsync("PlaybackStatus")).Should().Be("Stopped");
        (await service.DBusObject.GetPlayerAsync("CanPlay")).Should().Be(false);
        await service.DBusObject.PlayAsync();
        player.Commands.Should().BeEmpty();
    }

    [Theory]
    [InlineData(MediaElementState.Playing, "Playing")]
    [InlineData(MediaElementState.Buffering, "Playing")]
    [InlineData(MediaElementState.Paused, "Paused")]
    [InlineData(MediaElementState.Stopped, "Stopped")]
    [InlineData(MediaElementState.None, "Stopped")]
    [InlineData(MediaElementState.Failed, "Stopped")]
    public void Toolkit_states_map_to_MPRIS_status(MediaElementState state, string status)
        => MprisState.StatusOf(state).Should().Be(status);
}
