// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.Maui.Platform.Linux.Services.Camera;

/// <summary>Where the camera's frames come from.</summary>
internal enum CameraSourceKind
{
    /// <summary>A V4L2 capture node opened directly (an unsandboxed app).</summary>
    V4l2,

    /// <summary>A PipeWire remote the xdg-desktop-portal Camera interface handed out (a sandboxed app).</summary>
    PipeWire,
}

/// <summary>A camera to open: a V4L2 device path, or a PipeWire remote's file descriptor.</summary>
internal readonly record struct CameraSource(CameraSourceKind Kind, string? Device, int Fd)
{
    public static CameraSource ForDevice(string device) => new(CameraSourceKind.V4l2, device, -1);

    public static CameraSource ForPipeWire(int fd) => new(CameraSourceKind.PipeWire, null, fd);
}

/// <summary>The encoders a video recording uses: container, video and (optional) audio.</summary>
internal sealed record CameraVideoProfile(string Extension, string Muxer, string VideoEncoder, string? AudioEncoder);

/// <summary>
/// The GStreamer pipelines of a capture: the live preview (BGRA frames into an appsink the
/// dialog draws), and a recording, which keeps the preview and also encodes to a file.
/// The source is decoded with decodebin, which takes a webcam's MJPEG (the only format most
/// offer at 720p and up at full frame rate) as well as raw YUV.
/// </summary>
internal static class CameraPipelines
{
    internal const string PreviewSinkName = "openmaui_preview";

    /// <summary>The source element for <paramref name="source"/>.</summary>
    internal static string Source(CameraSource source) => source.Kind switch
    {
        CameraSourceKind.PipeWire => $"pipewiresrc fd={source.Fd.ToString(CultureInfo.InvariantCulture)}",
        _ => $"v4l2src device={Quote(source.Device ?? "/dev/video0")}",
    };

    private const string PreviewBranch =
        "queue leaky=downstream max-size-buffers=2 ! videoconvert ! video/x-raw,format=BGRA ! appsink name=" + PreviewSinkName + " max-buffers=2 drop=true sync=false";

    /// <summary>The preview pipeline.</summary>
    internal static string Preview(CameraSource source)
        => $"{Source(source)} ! decodebin ! videoconvert ! {PreviewBranch}";

    /// <summary>
    /// The recording pipeline: the preview branch, the encoded video branch and, when the
    /// profile has an audio encoder and <paramref name="withAudio"/>, the default microphone,
    /// all into the muxer writing <paramref name="path"/>.
    /// </summary>
    internal static string Recording(CameraSource source, CameraVideoProfile profile, string path, bool withAudio)
    {
        var description = $"{Source(source)} ! decodebin ! videoconvert ! tee name=t "
            + $"t. ! {PreviewBranch} "
            + $"t. ! queue ! videoconvert ! {profile.VideoEncoder} ! queue ! {profile.Muxer} name=mux ! filesink location={Quote(path)}";
        if (withAudio && profile.AudioEncoder != null)
            description += $" autoaudiosrc ! queue ! audioconvert ! audioresample ! {profile.AudioEncoder} ! queue ! mux.";
        return description;
    }

    /// <summary>
    /// The recording format: MP4 (H.264 and AAC, what Windows' camera records) when those
    /// encoders are installed, else WebM (VP8 and Opus or Vorbis), else either without sound;
    /// null when no video encoder is installed.
    /// </summary>
    internal static CameraVideoProfile? SelectProfile(Func<string, bool> hasElement)
    {
        string? h264 = hasElement("openh264enc") ? "openh264enc"
            : hasElement("x264enc") ? "x264enc tune=zerolatency speed-preset=ultrafast"
            : null;
        var mp4 = h264 != null && hasElement("h264parse") && hasElement("mp4mux");
        // avenc_aac emits a first buffer without a timestamp, which mp4mux rejects.
        string? aac = hasElement("fdkaacenc") ? "fdkaacenc" : hasElement("voaacenc") ? "voaacenc" : null;
        if (aac != null && !hasElement("aacparse"))
            aac = null;
        var hasMic = hasElement("autoaudiosrc");

        var webm = hasElement("vp8enc") && hasElement("webmmux");
        string? webmAudio = hasElement("opusenc") ? "opusenc" : hasElement("vorbisenc") ? "vorbisenc" : null;

        if (mp4 && aac != null && hasMic)
            return new CameraVideoProfile(".mp4", "mp4mux", h264 + " ! h264parse", aac + " ! aacparse");
        if (webm && webmAudio != null && hasMic)
            return new CameraVideoProfile(".webm", "webmmux", "vp8enc deadline=1 cpu-used=8", webmAudio);
        if (mp4)
            return new CameraVideoProfile(".mp4", "mp4mux", h264 + " ! h264parse", null);
        if (webm)
            return new CameraVideoProfile(".webm", "webmmux", "vp8enc deadline=1 cpu-used=8", null);
        return null;
    }

    /// <summary>A gst-launch string literal.</summary>
    internal static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
