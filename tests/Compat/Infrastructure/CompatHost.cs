// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Linux.Tests;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Rendering;
using SkiaSharp;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// A complete MAUI app hosted headlessly on OpenMaui the way a real app starts:
/// <c>MauiApp.CreateBuilder()</c> with MAUI's defaults, <c>UseLinux()</c> (every
/// platform handler and service), then whatever the third-party library asks
/// the app to add (<c>UseMauiCommunityToolkit()</c>, <c>UseSkiaSharp()</c>, ...).
/// The window's page is rendered by the production <see cref="LinuxViewRenderer"/>
/// into a <see cref="WindowContext"/> over a fake display window with a raster
/// engine, so tests can assert on handlers, layout, input and pixels.
/// </summary>
/// <remarks>
/// Differs from the main suite's HeadlessMauiHost only in the builder: that
/// host registers a handful of handlers by hand; this one runs the real
/// registration path so third-party registrations interact with it exactly
/// as in an application. Sets LinuxApplication.Current and Application.Current:
/// tests using it belong to the <see cref="Collection"/> collection.
/// </remarks>
internal sealed class CompatHost : IDisposable
{
    public const string Collection = "CompatHost";

    public LinuxApplication LinuxApp { get; }
    public MauiApp MauiApp { get; }
    public LinuxMauiContext MauiContext { get; }
    public Microsoft.Maui.Controls.Application Application { get; }
    public Microsoft.Maui.Controls.Window Window { get; }
    public WindowContext Context { get; }
    public HeadlessMauiHost.FakeDisplayWindow DisplayWindow { get; }
    public SkiaView? RootView { get; private set; }
    public Page Page { get; }

    /// <param name="rootPage">
    /// Builds the page from the app's services (so pages and view models can
    /// come from DI, as frameworks such as Prism and ReactiveUI expect).
    /// </param>
    /// <param name="configure">Library registrations, applied after <c>UseLinux()</c>.</param>
    public CompatHost(
        Func<IServiceProvider, Page> rootPage,
        Action<MauiAppBuilder>? configure = null,
        int width = 800,
        int height = 600)
        : this(b => b.UseMauiApp<HostApplication>(), rootPage, configure, width, height)
    {
    }

    public CompatHost(Page rootPage, Action<MauiAppBuilder>? configure = null, int width = 800, int height = 600)
        : this(_ => rootPage, configure, width, height)
    {
    }

    /// <summary>
    /// Starts <typeparamref name="TApp"/> the way OpenMaui starts an application:
    /// its startup window comes from <c>IApplication.CreateWindow</c> (so
    /// frameworks that create the window themselves, like Prism, or apps that
    /// take their root page by constructor injection, run their own path).
    /// </summary>
    public static CompatHost StartApp<TApp>(Action<MauiAppBuilder>? configure = null, int width = 800, int height = 600)
        where TApp : Microsoft.Maui.Controls.Application
        => new(b => b.UseMauiApp<TApp>(), null, configure, width, height);

    private CompatHost(
        Action<MauiAppBuilder> useApp,
        Func<IServiceProvider, Page>? rootPage,
        Action<MauiAppBuilder>? configure,
        int width,
        int height)
    {
        DispatcherProvider.SetCurrent(InlineDispatcherProvider.Instance);
        // As LinuxApplication.Run does: the test thread is the main thread, so
        // Essentials' MainThread.BeginInvokeOnMainThread (used by libraries such
        // as LiveCharts from background threads) posts to the GLib main context,
        // which Render() drains the way the real main loop would.
        Microsoft.Maui.Platform.Linux.Dispatching.LinuxDispatcher.Initialize();
        LinuxApp = new LinuxApplication();

        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        useApp(builder);
        // Registered first so UseLinux's TryAdd keeps it: dispatches run inline,
        // since no GLib main loop runs in a test.
        builder.Services.AddSingleton<IDispatcherProvider>(InlineDispatcherProvider.Instance);
        builder.UseLinux(_ => { });
        configure?.Invoke(builder);
        MauiApp = builder.Build();

        MauiContext = new LinuxMauiContext(MauiApp.Services, LinuxApp);
        LinuxApp.MauiContext = MauiContext;

        Application = MauiApp.Services.GetRequiredService<IApplication>() as Microsoft.Maui.Controls.Application
            ?? throw new InvalidOperationException("IApplication is not a Controls.Application");
        Application.UserAppTheme = AppTheme.Light; // pixel assertions must not follow the desktop theme
        Microsoft.Maui.Controls.Application.Current = Application;

        if (rootPage != null && Application is HostApplication hostApp)
            hostApp.StartupWindow = new Microsoft.Maui.Controls.Window(rootPage(MauiApp.Services)) { FlowDirection = Microsoft.Maui.FlowDirection.LeftToRight };

        // The production startup path (LinuxApplication.Run uses the same factory).
        Window = StartupWindowFactory.Create(Application, MauiContext) as Microsoft.Maui.Controls.Window
            ?? throw new InvalidOperationException("CreateWindow did not return a Controls.Window");
        if (Window.FlowDirection == Microsoft.Maui.FlowDirection.MatchParent)
            Window.FlowDirection = Microsoft.Maui.FlowDirection.LeftToRight;
        Page = Window.Page ?? throw new InvalidOperationException("The startup window has no page");

        DisplayWindow = new HeadlessMauiHost.FakeDisplayWindow { Width = width, Height = height };
        var engine = new SkiaRenderingEngine(DisplayWindow);
        Context = LinuxApp.AttachWindowContext(DisplayWindow, engine, raisesMauiLifecycle: false);
        Context.WireInput();

        Context.MauiWindow = Window;
        RootView = new LinuxViewRenderer(MauiContext).RenderPage(Page);
        Context.RootView = RootView;
    }

    /// <summary>
    /// Runs pending main-loop work (GLib idle/timeout sources), then lays out
    /// and draws a frame into <see cref="DisplayWindow"/>.
    /// </summary>
    public void Render()
    {
        Microsoft.Maui.Platform.Linux.Native.GLibNative.ProcessPendingEvents(50);
        Context.Render();
    }

    /// <summary>The Skia platform view a MAUI view resolved to.</summary>
    public static SkiaView PlatformOf(VisualElement view)
        => view.Handler?.PlatformView as SkiaView
           ?? throw new InvalidOperationException($"{view.GetType().Name} has no Skia platform view (handler: {view.Handler?.GetType().Name ?? "none"})");

    /// <summary>Centre of a view in window coordinates (after <see cref="Render"/>).</summary>
    public static (float X, float Y) CenterOf(VisualElement view)
    {
        var b = PlatformOf(view).ScreenBounds;
        return ((float)b.Center.X, (float)b.Center.Y);
    }

    /// <summary>
    /// Counts presented pixels inside <paramref name="rect"/> (window pixels)
    /// that differ from <paramref name="background"/> by more than a small tolerance.
    /// </summary>
    public int CountPixelsNot(SKColor background, SKRectI rect)
    {
        int count = 0;
        for (int y = Math.Max(0, rect.Top); y < Math.Min(DisplayWindow.Height, rect.Bottom); y++)
            for (int x = Math.Max(0, rect.Left); x < Math.Min(DisplayWindow.Width, rect.Right); x++)
            {
                var (r, g, b, _) = DisplayWindow.PixelAt(x, y);
                if (Math.Abs(r - background.Red) + Math.Abs(g - background.Green) + Math.Abs(b - background.Blue) > 24)
                    count++;
            }
        return count;
    }

    /// <summary>Counts presented pixels close to <paramref name="color"/> inside <paramref name="rect"/>.</summary>
    public int CountPixelsNear(SKColor color, SKRectI rect, int tolerance = 40)
    {
        int count = 0;
        for (int y = Math.Max(0, rect.Top); y < Math.Min(DisplayWindow.Height, rect.Bottom); y++)
            for (int x = Math.Max(0, rect.Left); x < Math.Min(DisplayWindow.Width, rect.Right); x++)
            {
                var (r, g, b, _) = DisplayWindow.PixelAt(x, y);
                if (Math.Abs(r - color.Red) + Math.Abs(g - color.Green) + Math.Abs(b - color.Blue) <= tolerance)
                    count++;
            }
        return count;
    }

    /// <summary>Writes the last presented frame as a PNG (diagnostics).</summary>
    public void SaveFrame(string path)
    {
        var frame = DisplayWindow.LastFrame ?? throw new InvalidOperationException("No frame presented");
        var info = new SKImageInfo(DisplayWindow.Width, DisplayWindow.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap();
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(frame, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), DisplayWindow.LastFrameStride);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }
        finally
        {
            handle.Free();
        }
    }

    public SKRectI WindowRect => new(0, 0, DisplayWindow.Width, DisplayWindow.Height);

    public static SKRectI RectOf(VisualElement view)
    {
        var b = PlatformOf(view).ScreenBounds;
        return new SKRectI((int)b.Left, (int)b.Top, (int)Math.Ceiling(b.Right), (int)Math.Ceiling(b.Bottom));
    }

    public void Tap(VisualElement view)
    {
        var (x, y) = CenterOf(view);
        DisplayWindow.RaisePointerPressed(x, y);
        DisplayWindow.RaisePointerReleased(x, y);
    }

    public void Dispose()
    {
        LinuxApp.Dispose();
        if (ReferenceEquals(Microsoft.Maui.Controls.Application.Current, Application))
            Microsoft.Maui.Controls.Application.Current = null;
        (MauiApp as IDisposable)?.Dispose();
    }

    /// <summary>Application whose CreateWindow returns the host's window.</summary>
    public sealed class HostApplication : Microsoft.Maui.Controls.Application
    {
        public Microsoft.Maui.Controls.Window? StartupWindow { get; set; }

        protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
            => StartupWindow ?? throw new InvalidOperationException("StartupWindow not set");
    }

    /// <summary>Runs every dispatch inline on the calling thread.</summary>
    internal sealed class InlineDispatcherProvider : IDispatcherProvider
    {
        public static readonly InlineDispatcherProvider Instance = new();
        private readonly InlineDispatcher _dispatcher = new();
        public IDispatcher? GetForCurrentThread() => _dispatcher;
    }

    private sealed class InlineDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
        public IDispatcherTimer CreateTimer() => new InlineTimer();
    }

    /// <summary>Timer that never fires on its own; tests drive time explicitly.</summary>
    private sealed class InlineTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsRepeating { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler? Tick;
        public void Start() { IsRunning = true; _ = Tick; }
        public void Stop() => IsRunning = false;
    }
}

internal static class CompatModule
{
    /// <summary>
    /// A MAUI app has a dispatcher before any view exists; tests construct
    /// views before the host, so install the inline one at load time.
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => DispatcherProvider.SetCurrent(CompatHost.InlineDispatcherProvider.Instance);
}

/// <summary>Serialises every test that touches Application.Current / LinuxApplication.Current.</summary>
[CollectionDefinition(CompatHost.Collection, DisableParallelization = true)]
public sealed class CompatHostCollection
{
}
