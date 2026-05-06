// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

public class WaylandDisplayWindow : IDisplayWindow, IDisposable
{
    private readonly WaylandWindow _window;

    public int Width => _window.Width;

    public int Height => _window.Height;

    public bool IsRunning => _window.IsRunning;

    public IntPtr PixelData => _window.PixelData;

    public int Stride => _window.Stride;

    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<TextInputEventArgs>? TextInput;
    public event EventHandler<PointerEventArgs>? PointerMoved;
    public event EventHandler<PointerEventArgs>? PointerPressed;
    public event EventHandler<PointerEventArgs>? PointerReleased;
    public event EventHandler<ScrollEventArgs>? Scroll;
    public event EventHandler? Exposed;
    public event EventHandler<(int Width, int Height)>? Resized;
    public event EventHandler? CloseRequested;

    public WaylandDisplayWindow(string title, int width, int height)
    {
        _window = new WaylandWindow(title, width, height);
        _window.KeyDown += (s, e) => KeyDown?.Invoke(this, e);
        _window.KeyUp += (s, e) => KeyUp?.Invoke(this, e);
        _window.TextInput += (s, e) => TextInput?.Invoke(this, e);
        _window.PointerMoved += (s, e) => PointerMoved?.Invoke(this, e);
        _window.PointerPressed += (s, e) => PointerPressed?.Invoke(this, e);
        _window.PointerReleased += (s, e) => PointerReleased?.Invoke(this, e);
        _window.Scroll += (s, e) => Scroll?.Invoke(this, e);
        _window.Exposed += (s, e) => Exposed?.Invoke(this, e);
        _window.Resized += (s, e) => Resized?.Invoke(this, e);
        _window.CloseRequested += (s, e) => CloseRequested?.Invoke(this, e);
    }

    public void Show() => _window.Show();

    public void Hide() => _window.Hide();

    public void SetTitle(string title) => _window.SetTitle(title);

    public void Resize(int width, int height) => _window.Resize(width, height);

    public void ProcessEvents() => _window.ProcessEvents();

    public void Stop() => _window.Stop();

    public void CommitFrame() => _window.CommitFrame();

    public void Dispose() => _window.Dispose();
}
