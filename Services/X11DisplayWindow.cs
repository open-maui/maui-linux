using System;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Platform.Linux.Services;

public class X11DisplayWindow : IDisplayWindow, IDisposable
{
	private readonly X11Window _window;

	public int Width => _window.Width;

	public int Height => _window.Height;

	public bool IsRunning => _window.IsRunning;

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

	public X11DisplayWindow(string title, int width, int height)
	{
		_window = new X11Window(title, width, height);
		_window.KeyDown += delegate(object? s, KeyEventArgs e)
		{
			this.KeyDown?.Invoke(this, e);
		};
		_window.KeyUp += delegate(object? s, KeyEventArgs e)
		{
			this.KeyUp?.Invoke(this, e);
		};
		_window.TextInput += delegate(object? s, TextInputEventArgs e)
		{
			this.TextInput?.Invoke(this, e);
		};
		_window.PointerMoved += delegate(object? s, PointerEventArgs e)
		{
			this.PointerMoved?.Invoke(this, e);
		};
		_window.PointerPressed += delegate(object? s, PointerEventArgs e)
		{
			this.PointerPressed?.Invoke(this, e);
		};
		_window.PointerReleased += delegate(object? s, PointerEventArgs e)
		{
			this.PointerReleased?.Invoke(this, e);
		};
		_window.Scroll += delegate(object? s, ScrollEventArgs e)
		{
			this.Scroll?.Invoke(this, e);
		};
		_window.Exposed += delegate(object? s, EventArgs e)
		{
			this.Exposed?.Invoke(this, e);
		};
		_window.Resized += delegate(object? s, (int Width, int Height) e)
		{
			this.Resized?.Invoke(this, e);
		};
		_window.CloseRequested += delegate(object? s, EventArgs e)
		{
			this.CloseRequested?.Invoke(this, e);
		};
	}

	public void Show()
	{
		_window.Show();
	}

	public void Hide()
	{
		_window.Hide();
	}

	public void SetTitle(string title)
	{
		_window.SetTitle(title);
	}

	public void Resize(int width, int height)
	{
		_window.Resize(width, height);
	}

	public void ProcessEvents()
	{
		_window.ProcessEvents();
	}

	public void Stop()
	{
		_window.Stop();
	}

	public void Dispose()
	{
		_window.Dispose();
	}
}
