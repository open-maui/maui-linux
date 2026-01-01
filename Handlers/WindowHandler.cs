// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Handler for Window on Linux.
/// Maps IWindow to the Linux display window system.
/// </summary>
public partial class WindowHandler : ElementHandler<IWindow, SkiaWindow>
{
    public static IPropertyMapper<IWindow, WindowHandler> Mapper =
        new PropertyMapper<IWindow, WindowHandler>(ElementHandler.ElementMapper)
        {
            [nameof(IWindow.Title)] = MapTitle,
            [nameof(IWindow.Content)] = MapContent,
            [nameof(IWindow.X)] = MapX,
            [nameof(IWindow.Y)] = MapY,
            [nameof(IWindow.Width)] = MapWidth,
            [nameof(IWindow.Height)] = MapHeight,
            [nameof(IWindow.MinimumWidth)] = MapMinimumWidth,
            [nameof(IWindow.MinimumHeight)] = MapMinimumHeight,
            [nameof(IWindow.MaximumWidth)] = MapMaximumWidth,
            [nameof(IWindow.MaximumHeight)] = MapMaximumHeight,
        };

    public static CommandMapper<IWindow, WindowHandler> CommandMapper =
        new(ElementHandler.ElementCommandMapper)
        {
        };

    public WindowHandler() : base(Mapper, CommandMapper)
    {
    }

    public WindowHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
    {
    }

    protected override SkiaWindow CreatePlatformElement()
    {
        return new SkiaWindow();
    }

    protected override void ConnectHandler(SkiaWindow platformView)
    {
        base.ConnectHandler(platformView);
        platformView.CloseRequested += OnCloseRequested;
        platformView.SizeChanged += OnSizeChanged;
    }

    protected override void DisconnectHandler(SkiaWindow platformView)
    {
        platformView.CloseRequested -= OnCloseRequested;
        platformView.SizeChanged -= OnSizeChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        VirtualView?.Destroying();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        VirtualView?.FrameChanged(new Rect(0, 0, e.Width, e.Height));
    }

    public static void MapTitle(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Title = window.Title ?? "MAUI Application";
    }

    public static void MapContent(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;

        var content = window.Content;
        if (content?.Handler?.PlatformView is SkiaView skiaContent)
        {
            handler.PlatformView.Content = skiaContent;
        }
    }

    public static void MapX(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.X = (int)window.X;
    }

    public static void MapY(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Y = (int)window.Y;
    }

    public static void MapWidth(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Width = (int)window.Width;
    }

    public static void MapHeight(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.Height = (int)window.Height;
    }

    public static void MapMinimumWidth(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MinWidth = (int)window.MinimumWidth;
    }

    public static void MapMinimumHeight(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MinHeight = (int)window.MinimumHeight;
    }

    public static void MapMaximumWidth(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxWidth = (int)window.MaximumWidth;
    }

    public static void MapMaximumHeight(WindowHandler handler, IWindow window)
    {
        if (handler.PlatformView is null) return;
        handler.PlatformView.MaxHeight = (int)window.MaximumHeight;
    }
}

/// <summary>
/// Skia window wrapper for Linux display servers.
/// Handles rendering of content and popup overlays automatically.
/// </summary>
public class SkiaWindow
{
    private SkiaView? _content;
    private string _title = "MAUI Application";
    private int _x, _y;
    private int _width = 800;
    private int _height = 600;
    private int _minWidth = 100;
    private int _minHeight = 100;
    private int _maxWidth = int.MaxValue;
    private int _maxHeight = int.MaxValue;

    public SkiaView? Content
    {
        get => _content;
        set
        {
            _content = value;
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Renders the window content and popup overlays to the canvas.
    /// This should be called by the platform rendering loop.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        // Clear background
        canvas.Clear(SKColors.White);

        // Draw main content
        if (_content != null)
        {
            _content.Measure(new SKSize(_width, _height));
            _content.Arrange(new SKRect(0, 0, _width, _height));
            _content.Draw(canvas);
        }

        // Draw popup overlays on top (dropdowns, date pickers, etc.)
        // This ensures popups always render above all other content
        SkiaView.DrawPopupOverlays(canvas);
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int X
    {
        get => _x;
        set { _x = value; PositionChanged?.Invoke(this, EventArgs.Empty); }
    }

    public int Y
    {
        get => _y;
        set { _y = value; PositionChanged?.Invoke(this, EventArgs.Empty); }
    }

    public int Width
    {
        get => _width;
        set
        {
            _width = Math.Clamp(value, _minWidth, _maxWidth);
            SizeChanged?.Invoke(this, new SizeChangedEventArgs(_width, _height));
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            _height = Math.Clamp(value, _minHeight, _maxHeight);
            SizeChanged?.Invoke(this, new SizeChangedEventArgs(_width, _height));
        }
    }

    public int MinWidth
    {
        get => _minWidth;
        set { _minWidth = value; }
    }

    public int MinHeight
    {
        get => _minHeight;
        set { _minHeight = value; }
    }

    public int MaxWidth
    {
        get => _maxWidth;
        set { _maxWidth = value; }
    }

    public int MaxHeight
    {
        get => _maxHeight;
        set { _maxHeight = value; }
    }

    public event EventHandler? ContentChanged;
    public event EventHandler? TitleChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler<SizeChangedEventArgs>? SizeChanged;
    public event EventHandler? CloseRequested;

    public void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Event args for window size changes.
/// </summary>
public class SizeChangedEventArgs : EventArgs
{
    public int Width { get; }
    public int Height { get; }

    public SizeChangedEventArgs(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
