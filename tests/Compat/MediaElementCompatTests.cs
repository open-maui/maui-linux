// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.MediaElement.Handlers;
using Microsoft.Maui.Platform.Linux.MediaElement.Hosting;
using Microsoft.Maui.Platform.Linux.MediaElement.Views;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// CommunityToolkit.Maui.MediaElement (10.0, generic net10.0 asset) through
/// OpenMaui's GStreamer backend, registered the documented way
/// (<c>UseMauiCommunityToolkitMediaElement(...).UseLinuxMediaElement()</c>).
/// Pass = the stock MediaElement resolves to the Linux handler, its properties
/// map onto the Skia/GStreamer view, and play/pause/stop requests reach the
/// pipeline. Uses a generated, muted WAV file so no network or codec packs are
/// needed.
/// </summary>
[Collection(CompatHost.Collection)]
public sealed class MediaElementCompatTests : IDisposable
{
    private readonly string _wav = Path.Combine(Path.GetTempPath(), $"openmaui-compat-{Guid.NewGuid():N}.wav");

    public MediaElementCompatTests() => WriteSilentWav(_wav, seconds: 2);

    public void Dispose()
    {
        try { File.Delete(_wav); } catch (IOException) { }
    }

    private static CompatHost Host(MediaElement media)
        => new(new ContentPage { Content = media },
               b => b.UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false).UseLinuxMediaElement());

    [Fact]
    public void MediaElement_resolves_to_the_linux_handler_and_maps_properties()
    {
        var media = new MediaElement { Aspect = Microsoft.Maui.Aspect.AspectFill, ShouldAutoPlay = false, ShouldMute = true, Volume = 0.25 };
        using var host = Host(media);
        host.Render();

        media.Handler.Should().BeOfType<LinuxMediaElementHandler>();
        var skia = CompatHost.PlatformOf(media).Should().BeOfType<SkiaMediaElement>().Subject;
        skia.Aspect.Should().Be(Microsoft.Maui.Aspect.AspectFill);

        media.Aspect = Microsoft.Maui.Aspect.Fill;
        skia.Aspect.Should().Be(Microsoft.Maui.Aspect.Fill);
    }

    [Fact]
    public async Task AspectFill_crops_the_picture_to_the_view()
    {
        // A 16:9 red video in a 600 x 200 box: filled, it is 337 tall, and the 68 below the
        // box are cropped (CiteLynq's Daily Discovery video drew over the title under it).
        var video = Path.Combine(Path.GetTempPath(), $"openmaui-compat-{Guid.NewGuid():N}.webm");
        if (!WriteRedVideo(video))
            return; // no encoder on this machine
        try
        {
            var media = new MediaElement { Aspect = Microsoft.Maui.Aspect.AspectFill, ShouldAutoPlay = true, ShouldMute = true, Source = MediaSource.FromFile(video) };
            var page = new ContentPage
            {
                BackgroundColor = Microsoft.Maui.Graphics.Colors.Blue,
                Content = new Grid
                {
                    RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(new GridLength(200))),
                    Children = { new Grid { HeightRequest = 200, Children = { media } } },
                },
            };
            using var host = new CompatHost(page, b => b.UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false).UseLinuxMediaElement(), 600, 500);
            for (int i = 0; i < 50; i++)
            {
                host.Render();
                if (host.DisplayWindow.PixelAt(300, 100).R > 200)
                    break;
                await Task.Delay(100);
            }

            var inside = host.DisplayWindow.PixelAt(5, 100);
            (inside.R > 200 && inside.B < 60).Should().BeTrue($"the picture fills the box edge to edge, got {inside}");
            var below = host.DisplayWindow.PixelAt(300, 240);
            (below.B > 200 && below.R < 60).Should().BeTrue($"the picture stops at the box, got {below}");
        }
        finally
        {
            try { File.Delete(video); } catch (IOException) { }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_looping_video_starts_again_at_its_end_and_one_that_does_not_ends(bool loop)
    {
        var video = Path.Combine(Path.GetTempPath(), $"openmaui-compat-{Guid.NewGuid():N}.webm");
        if (!WriteRedVideo(video, seconds: 1))
            return;
        try
        {
            var media = new MediaElement { ShouldAutoPlay = true, ShouldMute = true, ShouldLoopPlayback = loop, Source = MediaSource.FromFile(video) };
            int ended = 0;
            media.MediaEnded += (s, e) => ended++;
            using var host = Host(media);
            var skia = (SkiaMediaElement)CompatHost.PlatformOf(media);
            for (int i = 0; i < 30; i++)
            {
                host.Render();
                await Task.Delay(100);
            }

            if (loop)
            {
                ended.Should().Be(0, "a looping video never ends");
                skia.IsPlaying.Should().BeTrue("it started again after its one second");
            }
            else
            {
                ended.Should().Be(1, "the toolkit hears the end once");
                skia.IsPlaying.Should().BeFalse();
            }
        }
        finally
        {
            try { File.Delete(video); } catch (IOException) { }
        }
    }

    private static bool WriteRedVideo(string path, int seconds = 3)
    {
        try
        {
            using var ffmpeg = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ffmpeg",
                $"-v error -y -f lavfi -i color=c=red:s=1280x720:d={seconds} -c:v libvpx -b:v 200k \"{path}\"") { RedirectStandardError = true });
            ffmpeg!.WaitForExit(30000);
            return ffmpeg.ExitCode == 0 && File.Exists(path);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [Fact]
    public void File_source_is_handed_to_the_pipeline_as_a_file_uri()
    {
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true };
        using var host = Host(media);
        var skia = (SkiaMediaElement)CompatHost.PlatformOf(media);

        media.Source = MediaSource.FromFile(_wav);

        skia.CurrentUri.Should().Be(new Uri(_wav).AbsoluteUri);

        media.Source = MediaSource.FromUri("https://example.invalid/clip.mp4");
        skia.CurrentUri.Should().Be("https://example.invalid/clip.mp4");

        media.Source = null;
        skia.CurrentUri.Should().BeNull();
    }

    [Fact]
    public void Play_pause_and_stop_requests_drive_the_pipeline()
    {
        var media = new MediaElement { ShouldAutoPlay = false, ShouldMute = true, Source = MediaSource.FromFile(_wav) };
        using var host = Host(media);
        var skia = (SkiaMediaElement)CompatHost.PlatformOf(media);
        skia.IsPlaying.Should().BeFalse();

        media.Play();
        skia.IsPlaying.Should().BeTrue("PlayRequested reaches the GStreamer playbin");

        media.Pause();
        skia.IsPlaying.Should().BeFalse();

        media.Play();
        media.Stop();
        skia.IsPlaying.Should().BeFalse();
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
}
