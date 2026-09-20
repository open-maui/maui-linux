// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A MauiContext built the way the real app builds it (Linux services and
/// handler registrations) but with no display server: handlers can be created
/// for MAUI Controls views and their Skia platform views inspected directly.
/// </summary>
internal static class HeadlessMauiContext
{
    private static readonly Lazy<IMauiContext> s_instance = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IMauiContext Instance => s_instance.Value;

    /// <summary>
    /// BindableObject captures its dispatcher at construction, so the inline
    /// provider must be in place before any test creates a view.
    /// </summary>
    [ModuleInitializer]
    internal static void InstallInlineDispatcher() =>
        DispatcherProvider.SetCurrent(new InlineDispatcherProvider());

    private static IMauiContext Create()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseLinux(_ => { });
        builder.ConfigureMauiHandlers(handlers =>
        {
            // Mirrors the registrations LinuxMauiAppBuilderExtensions carries
            // for the template plumbing.
            handlers.AddHandler(typeof(ContentPresenter), typeof(ContentPresenterHandler));
            handlers.AddHandler(typeof(TemplatedView), typeof(TemplatedViewHandler));
        });
        var app = builder.Build();

        // Bindings and property propagation marshal through the element's
        // dispatcher; the Linux one only exists for the UI thread the app
        // starts, so tests (xunit worker threads) keep the inline dispatcher
        // that Build() just replaced with the Linux provider.
        InstallInlineDispatcher();

        return new LinuxMauiContext(app.Services, new LinuxApplication());
    }

    private sealed class InlineDispatcherProvider : IDispatcherProvider
    {
        private readonly InlineDispatcher _dispatcher = new();
        public IDispatcher? GetForCurrentThread() => _dispatcher;
    }

    private sealed class InlineDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            // Never inline: a self-rescheduling loop would otherwise recurse forever.
            _ = Task.Delay(delay).ContinueWith(_ => action(), TaskScheduler.Default);
            return true;
        }
        public IDispatcherTimer CreateTimer() => new InlineTimer();
    }

    private sealed class InlineTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsRepeating { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler? Tick;
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
    }

    /// <summary>
    /// Creates the Linux handler for <paramref name="element"/> exactly as the
    /// renderer does (Linux handler map first, MauiView back-reference wired).
    /// Named explicitly because MAUI's own <c>ElementExtensions.ToHandler</c>
    /// shadows the Linux one in test namespaces and skips that wiring.
    /// </summary>
    public static IElementHandler CreateHandler(Element element) =>
        MauiHandlerExtensions.ToHandler(element, Instance);

    /// <summary>
    /// Creates the Linux handler for <paramref name="view"/> and returns its
    /// Skia platform view.
    /// </summary>
    public static TPlatform Realize<TPlatform>(View view) where TPlatform : SkiaView
    {
        var handler = CreateHandler(view);
        return (TPlatform)handler.PlatformView!;
    }
}
