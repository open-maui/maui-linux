// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using SkiaSharp;
using static Microsoft.Maui.Platform.Linux.Services.Camera.GstCameraNative;

namespace Microsoft.Maui.Platform.Linux.Services.Camera;

/// <summary>
/// A running camera pipeline (<see cref="CameraPipelines"/>): a pump thread pulls the preview's
/// BGRA frames from the appsink and keeps the latest one; errors on the bus end the session.
/// </summary>
internal sealed class GstCameraSession : ICameraSession
{
    private readonly object _lock = new();
    private IntPtr _pipeline;
    private IntPtr _sink;
    private IntPtr _bus;
    private Thread? _pump;
    private volatile bool _running;
    private volatile bool _stopping;
    private byte[]? _frame;
    private int _width;
    private int _height;
    private int _stride;
    private long _frameCount;

    /// <summary>Raised on the pump thread after each new frame.</summary>
    public event Action? FrameArrived;

    /// <summary>Raised on the pump thread when the pipeline reports an error (camera busy, unplugged, ...).</summary>
    public event Action<string>? Failed;

    /// <summary>Frames received so far.</summary>
    public long FrameCount => Interlocked.Read(ref _frameCount);

    private GstCameraSession()
    {
    }

    /// <summary>Builds <paramref name="description"/> and sets it playing; throws InvalidOperationException when it cannot.</summary>
    public static GstCameraSession Start(string description)
    {
        if (!EnsureInitialized())
            throw new InvalidOperationException("GStreamer is not available.");

        var pipeline = gst_parse_launch(description, out var error);
        if (error != IntPtr.Zero)
        {
            var message = ErrorMessage(error);
            g_error_free(error);
            if (pipeline != IntPtr.Zero)
                gst_object_unref(pipeline);
            throw new InvalidOperationException($"Cannot build the camera pipeline: {message}");
        }
        if (pipeline == IntPtr.Zero)
            throw new InvalidOperationException("Cannot build the camera pipeline.");

        var session = new GstCameraSession { _pipeline = pipeline };
        session._sink = gst_bin_get_by_name(pipeline, CameraPipelines.PreviewSinkName);
        session._bus = gst_element_get_bus(pipeline);
        if (gst_element_set_state(pipeline, StatePlaying) == StateChangeFailure)
        {
            var reason = session.PopError(0) ?? "the camera could not be started";
            session.Dispose();
            throw new InvalidOperationException(reason);
        }

        session._running = true;
        session._pump = new Thread(session.Pump) { IsBackground = true, Name = "OpenMaui camera" };
        session._pump.Start();
        return session;
    }

    /// <summary>A copy of the latest frame, or null before the first one.</summary>
    public SKBitmap? SnapshotLatest()
    {
        lock (_lock)
        {
            if (_frame == null || _width <= 0 || _height <= 0)
                return null;
            var bitmap = new SKBitmap(new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul));
            var rowBytes = bitmap.RowBytes;
            var pixels = bitmap.GetPixels();
            for (int y = 0; y < _height; y++)
                Marshal.Copy(_frame, y * _stride, pixels + y * rowBytes, Math.Min(rowBytes, _width * 4));
            return bitmap;
        }
    }

    /// <summary>
    /// Ends the session. With <paramref name="finish"/>, sends end-of-stream first and waits up to
    /// <paramref name="timeout"/> for it to reach the sinks, so a recording's file is complete;
    /// returns whether it did.
    /// </summary>
    public Task<bool> StopAsync(bool finish, TimeSpan timeout) => Task.Run(() =>
    {
        var completed = true;
        if (finish && _pipeline != IntPtr.Zero && _running)
        {
            _stopping = true;
            gst_element_send_event(_pipeline, gst_event_new_eos());
            var message = gst_bus_timed_pop_filtered(_bus, (ulong)timeout.TotalMilliseconds * 1_000_000UL, MessageEos | MessageError);
            if (message == IntPtr.Zero)
            {
                completed = false;
            }
            else
            {
                // An error message here means the file is unusable.
                var error = ParseError(message);
                completed = error == null;
                if (error != null)
                    DiagnosticLog.Warn("Camera", $"Recording did not finish: {error}");
                gst_mini_object_unref(message);
            }
        }
        Dispose();
        return completed;
    });

    public void Dispose()
    {
        _running = false;
        var pump = _pump;
        if (pump != null && pump != Thread.CurrentThread)
            pump.Join(TimeSpan.FromSeconds(2));
        _pump = null;
        lock (_lock)
        {
            if (_pipeline != IntPtr.Zero)
            {
                gst_element_set_state(_pipeline, StateNull);
                if (_sink != IntPtr.Zero)
                    gst_object_unref(_sink);
                if (_bus != IntPtr.Zero)
                    gst_object_unref(_bus);
                gst_object_unref(_pipeline);
                _pipeline = IntPtr.Zero;
                _sink = IntPtr.Zero;
                _bus = IntPtr.Zero;
            }
        }
    }

    private void Pump()
    {
        while (_running)
        {
            if (!_stopping)
            {
                var error = PopError(0);
                if (error != null)
                {
                    _running = false;
                    Failed?.Invoke(error);
                    return;
                }
            }

            var sample = _sink == IntPtr.Zero ? IntPtr.Zero : gst_app_sink_try_pull_sample(_sink, Second / 10);
            if (sample == IntPtr.Zero)
            {
                // End of stream (or no sink) returns at once: do not spin.
                Thread.Sleep(10);
                continue;
            }
            try
            {
                CopyFrame(sample);
            }
            finally
            {
                gst_mini_object_unref(sample);
            }
            Interlocked.Increment(ref _frameCount);
            try
            {
                FrameArrived?.Invoke();
            }
            catch (Exception ex)
            {
                DiagnosticLog.Warn("Camera", $"Frame handler failed: {ex.Message}");
            }
        }
    }

    private void CopyFrame(IntPtr sample)
    {
        var caps = gst_sample_get_caps(sample);
        var buffer = gst_sample_get_buffer(sample);
        if (caps == IntPtr.Zero || buffer == IntPtr.Zero)
            return;
        var structure = gst_caps_get_structure(caps, 0);
        if (!gst_structure_get_int(structure, "width", out var width) || !gst_structure_get_int(structure, "height", out var height) || width <= 0 || height <= 0)
            return;
        if (!gst_buffer_map(buffer, out var map, 1))
            return;
        try
        {
            var size = (int)map.Size;
            var stride = size / height;
            if (stride < width * 4)
                return;
            lock (_lock)
            {
                if (_frame == null || _frame.Length != size)
                    _frame = new byte[size];
                Marshal.Copy(map.Data, _frame, 0, size);
                _width = width;
                _height = height;
                _stride = stride;
            }
        }
        finally
        {
            gst_buffer_unmap(buffer, ref map);
        }
    }

    private string? PopError(ulong timeout)
    {
        if (_bus == IntPtr.Zero)
            return null;
        var message = gst_bus_timed_pop_filtered(_bus, timeout, MessageError);
        if (message == IntPtr.Zero)
            return null;
        try
        {
            return ParseError(message) ?? "camera error";
        }
        finally
        {
            gst_mini_object_unref(message);
        }
    }

    /// <summary>The text of an error message; null for any other message (gst_message_parse_error only accepts errors).</summary>
    private static string? ParseError(IntPtr message)
    {
        // GstMessage: GstMiniObject (64 bytes on 64-bit), then the GstMessageType.
        var type = (uint)Marshal.ReadInt32(message, 64);
        if ((type & MessageError) == 0)
            return null;
        gst_message_parse_error(message, out var error, out var debug);
        var text = ErrorMessage(error);
        if (error != IntPtr.Zero)
            g_error_free(error);
        if (debug != IntPtr.Zero)
            g_free(debug);
        return text ?? "camera error";
    }
}
