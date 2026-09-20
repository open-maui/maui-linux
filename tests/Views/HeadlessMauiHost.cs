// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Platform.Linux.Window;

namespace Microsoft.Maui.Controls.Linux.Tests;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;
using PointerButton = Microsoft.Maui.Platform.PointerButton;
using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using TextInputEventArgs = Microsoft.Maui.Platform.TextInputEventArgs;
using ScrollEventArgs = Microsoft.Maui.Platform.ScrollEventArgs;
using Key = Microsoft.Maui.Platform.Key;

/// <summary>
/// A complete MAUI application hosted headlessly on the Linux platform: a
/// MauiApp with the Linux page/view handlers and the alert bridge registered,
/// a real <see cref="Microsoft.Maui.Controls.Application"/> owning a real
/// <see cref="Microsoft.Maui.Controls.Window"/>, and a
/// <see cref="WindowContext"/> over a fake display window into which the
/// window's page is rendered. This is the same wiring LinuxApplication.Run
/// performs, minus the native window and event loop, so Page.DisplayAlert
/// and Navigation.PushModalAsync exercise the production code paths.
/// </summary>
/// <remarks>
/// Sets LinuxApplication.Current and Application.Current; tests using it
/// must sit in the "LinuxApplication.Current" collection. Dispose clears both.
/// </remarks>
internal sealed class HeadlessMauiHost : IDisposable
{
    public LinuxApplication LinuxApp { get; }
    public MauiApp MauiApp { get; }
    public LinuxMauiContext MauiContext { get; }
    public HostApplication Application { get; }
    public Microsoft.Maui.Controls.Window Window { get; }
    public WindowContext Context { get; }
    public FakeDisplayWindow DisplayWindow { get; }
    public SkiaView? RootView { get; }

    /// <summary>
    /// Hosts <paramref name="rootPage"/>. With <paramref name="withEngine"/>
    /// the context gets a raster <see cref="SkiaRenderingEngine"/> whose
    /// frames land in <see cref="FakeDisplayWindow.LastFrame"/>.
    /// </summary>
    public HeadlessMauiHost(Page rootPage, bool withEngine = false, int width = 800, int height = 600)
    {
        LinuxApp = new LinuxApplication();

        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauiApp<HostApplication>();
        builder.ConfigureMauiHandlers(handlers =>
        {
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Page, PageHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<ContentPage, ContentPageHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<NavigationPage, NavigationPageHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Label, LabelHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Button, TextButtonHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Entry, EntryHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<VerticalStackLayout, StackLayoutHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<StackLayout, StackLayoutHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Grid, GridHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Microsoft.Maui.Controls.Application, ApplicationHandler>(handlers);
            Microsoft.Maui.Hosting.MauiHandlersCollectionExtensions.AddHandler<Microsoft.Maui.Controls.Window, WindowHandler>(handlers);
        });
        LinuxAlertManager.Register(builder.Services);
        MauiApp = builder.Build();

        MauiContext = new LinuxMauiContext(MauiApp.Services, LinuxApp);
        LinuxApp.MauiContext = MauiContext;

        Application = (HostApplication)MauiApp.Services.GetRequiredService<IApplication>();
        Application.UserAppTheme = AppTheme.Light; // pixel assertions must not follow the desktop theme
        Microsoft.Maui.Controls.Application.Current = Application;

        // Explicit flow direction: Window.OnHandlerChangingCore consults
        // AppInfo.Current for MatchParent, which throws on the generic TFM
        // unless the Essentials patches (applied by UseLinux) are in place.
        Window = new Microsoft.Maui.Controls.Window(rootPage) { FlowDirection = FlowDirection.LeftToRight };
        Application.StartupWindow = Window;
        ((IApplication)Application).CreateWindow(null!); // registers the window (Parent = Application)

        DisplayWindow = new FakeDisplayWindow { Width = width, Height = height };
        var engine = withEngine ? new SkiaRenderingEngine(DisplayWindow) : null;
        Context = LinuxApp.AttachWindowContext(DisplayWindow, engine, raisesMauiLifecycle: false);
        Context.WireInput();

        // Same order as LinuxApplication.Run: adopt the MAUI window (attaches
        // the WindowHandler, so AlertManager can subscribe when the page gets
        // its handler), then render the page.
        Context.MauiWindow = Window;
        RootView = new LinuxViewRenderer(MauiContext).RenderPage(rootPage);
        Context.RootView = RootView;
    }

    public void Dispose()
    {
        LinuxApp.Dispose();
        if (ReferenceEquals(Microsoft.Maui.Controls.Application.Current, Application))
            Microsoft.Maui.Controls.Application.Current = null;
    }

    /// <summary>Application whose CreateWindow returns a preset window.</summary>
    public sealed class HostApplication : Microsoft.Maui.Controls.Application
    {
        public Microsoft.Maui.Controls.Window? StartupWindow { get; set; }

        protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
            => StartupWindow ?? throw new InvalidOperationException("StartupWindow not set");
    }

    /// <summary>
    /// Minimal IDisplayWindow: tracks running state, exposes Raise helpers so
    /// tests can inject native events synchronously (as X11/Wayland do), and
    /// keeps a copy of the last presented raster frame for pixel assertions.
    /// </summary>
    public sealed class FakeDisplayWindow : IDisplayWindow
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public bool IsRunning { get; private set; } = true;
        public bool Disposed { get; private set; }
        public CursorType? LastCursor { get; private set; }

        /// <summary>BGRA8888 copy of the last frame handed to Present.</summary>
        public byte[]? LastFrame { get; private set; }
        public int LastFrameStride { get; private set; }
        public int PresentCount { get; private set; }

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
        public event EventHandler? FocusGained;
        public event EventHandler? FocusLost;

        public void Show() => IsRunning = true;
        public void Hide() { }
        public void SetTitle(string title) { }
        public void Resize(int width, int height) { Width = width; Height = height; }
        public void SetCursor(CursorType cursorType) => LastCursor = cursorType;
        public void SetIcon(string iconPath) { }
        public void SetWMClass(string resName, string resClass) { }
        public void ProcessEvents() { }
        public void Stop() => IsRunning = false;
        public int GetFileDescriptor() => -1;
        public void FlushDeferredResize() { }
        public void AcknowledgeSync() { }
        public void Dispose() => Disposed = true;

        public void Present(IntPtr pixels, int width, int height, int stride)
        {
            PresentCount++;
            var copy = new byte[stride * height];
            Marshal.Copy(pixels, copy, 0, copy.Length);
            LastFrame = copy;
            LastFrameStride = stride;
        }

        /// <summary>Reads a pixel of the last frame as (R, G, B, A).</summary>
        public (byte R, byte G, byte B, byte A) PixelAt(int x, int y)
        {
            if (LastFrame == null) throw new InvalidOperationException("No frame presented");
            int offset = y * LastFrameStride + x * 4;
            // BGRA8888 in memory.
            return (LastFrame[offset + 2], LastFrame[offset + 1], LastFrame[offset], LastFrame[offset + 3]);
        }

        public void RaisePointerPressed(float x, float y, PointerButton button = PointerButton.Left) =>
            PointerPressed?.Invoke(this, new PointerEventArgs(x, y, button));
        public void RaisePointerReleased(float x, float y) =>
            PointerReleased?.Invoke(this, new PointerEventArgs(x, y, PointerButton.Left));
        public void RaisePointerMoved(float x, float y) =>
            PointerMoved?.Invoke(this, new PointerEventArgs(x, y));
        public void RaiseKeyDown(Key key) =>
            KeyDown?.Invoke(this, new KeyEventArgs(key, Microsoft.Maui.Platform.KeyModifiers.None));
        public void RaiseTextInput(string text) =>
            TextInput?.Invoke(this, new TextInputEventArgs(text));
        public void RaiseFocusGained() => FocusGained?.Invoke(this, EventArgs.Empty);
        public void RaiseFocusLost() => FocusLost?.Invoke(this, EventArgs.Empty);
        public void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);
        public void RaiseResized(int w, int h) { Width = w; Height = h; Resized?.Invoke(this, (w, h)); }

        private void Touch()
        {
            _ = KeyUp; _ = Scroll; _ = Exposed;
        }
    }
}
