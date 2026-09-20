// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests;

/// <summary>
/// Headless MAUI host for handler-level tests: a real <see cref="MauiApp"/>
/// built with the Linux platform registrations (so handlers, services and
/// the Linux <see cref="IMauiContext"/> resolve exactly as in an app), an
/// inline dispatcher (MAUI's Shell navigation dispatches pops through
/// <c>BindableObject.Dispatcher</c>), and a window host so pages get the
/// Application-rooted parent chain that <c>Page.SendAppearing</c> and
/// <c>Shell.Current</c> require. No display server is touched: the
/// application handler is never created, so no native window opens.
/// </summary>
/// <remarks>
/// Tests using this host belong to the <see cref="Collection"/> collection:
/// hosting sets the process-wide <see cref="Application.Current"/> and
/// <see cref="DispatcherProvider"/> statics.
/// </remarks>
internal static class HeadlessMaui
{
    public const string Collection = "HeadlessMaui";

    private static readonly Lazy<MauiApp> s_app = new(() =>
    {
        DispatcherProvider.SetCurrent(new InlineDispatcherProvider());
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<HeadlessApplication>().UseLinux(_ => { });
        // Handlers the platform registries do not list yet (see the
        // REGISTRATION NEEDED notes on TableViewHandler / ListViewHandler).
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(TableView), typeof(Microsoft.Maui.Platform.Linux.Handlers.TableViewHandler));
#pragma warning disable CS0618 // ListView is deprecated but still supported
            handlers.AddHandler(typeof(ListView), typeof(Microsoft.Maui.Platform.Linux.Handlers.ListViewHandler));
#pragma warning restore CS0618
        });
        return builder.Build();
    });

    /// <summary>A MauiContext over the shared headless app's services.</summary>
    public static IMauiContext CreateContext() => new MauiContext(s_app.Value.Services);

    /// <summary>
    /// Attaches a specific handler to <paramref name="element"/>, bypassing
    /// the platform's type map (which may route the element elsewhere until
    /// the registries are updated).
    /// </summary>
    public static THandler AttachHandler<THandler>(Element element, IMauiContext context)
        where THandler : IElementHandler, new()
    {
        var handler = new THandler();
        handler.SetMauiContext(context);
        handler.SetVirtualView(element);
        return handler;
    }

    /// <summary>
    /// Hosts <paramref name="page"/> in a window of a fresh application that
    /// becomes <see cref="Application.Current"/>, the way the startup path
    /// does (through <see cref="IApplication.CreateWindow"/>).
    /// </summary>
    public static HeadlessApplication HostInWindow(Page page)
    {
        _ = s_app.Value; // dispatcher provider must be installed first
        // Pin the theme: SkiaTheme.IsDarkMode otherwise follows the machine's
        // system theme through Application.Current, which would make text
        // colours (and pixel assertions) depend on the desktop running the tests.
        var app = new HeadlessApplication { NextWindowPage = page, UserAppTheme = AppTheme.Light };
        ((IApplication)app).CreateWindow(null!);
        return app;
    }

    /// <summary>
    /// Application whose <see cref="CreateWindow"/> wraps the page a test
    /// asked to host. Constructing it makes it <see cref="Application.Current"/>.
    /// </summary>
    public sealed class HeadlessApplication : Application
    {
        public Page? NextWindowPage { get; set; }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(NextWindowPage ?? new ContentPage());
    }

    /// <summary>Runs every dispatch inline on the calling thread.</summary>
    private sealed class InlineDispatcherProvider : IDispatcherProvider
    {
        private readonly InlineDispatcher _dispatcher = new();
        public IDispatcher? GetForCurrentThread() => _dispatcher;
    }

    private sealed class InlineDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            action();
            return true;
        }

        public IDispatcherTimer CreateTimer() => new InlineTimer();
    }

    /// <summary>Timer that never fires; tests drive time explicitly.</summary>
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
