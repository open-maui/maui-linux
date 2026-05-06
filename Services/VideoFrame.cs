// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public class VideoFrame : IDisposable
{
    private bool _disposed;
    private Action? _releaseCallback;

    public int Width { get; init; }

    public int Height { get; init; }

    public IntPtr DataY { get; init; }

    public IntPtr DataU { get; init; }

    public IntPtr DataV { get; init; }

    public int StrideY { get; init; }

    public int StrideU { get; init; }

    public int StrideV { get; init; }

    public long Timestamp { get; init; }

    public bool IsKeyFrame { get; init; }

    internal void SetReleaseCallback(Action callback)
    {
        _releaseCallback = callback;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _releaseCallback?.Invoke();
            _disposed = true;
        }
    }
}
