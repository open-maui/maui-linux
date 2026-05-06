// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services;

public interface IDisplayWindow : IDisposable
{
    int Width { get; }

    int Height { get; }

    bool IsRunning { get; }

    event EventHandler<KeyEventArgs>? KeyDown;

    event EventHandler<KeyEventArgs>? KeyUp;

    event EventHandler<TextInputEventArgs>? TextInput;

    event EventHandler<PointerEventArgs>? PointerMoved;

    event EventHandler<PointerEventArgs>? PointerPressed;

    event EventHandler<PointerEventArgs>? PointerReleased;

    event EventHandler<ScrollEventArgs>? Scroll;

    event EventHandler? Exposed;

    event EventHandler<(int Width, int Height)>? Resized;

    event EventHandler? CloseRequested;

    void Show();

    void Hide();

    void SetTitle(string title);

    void Resize(int width, int height);

    void ProcessEvents();

    void Stop();
}
