// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Platform.Linux.Services;

namespace Microsoft.Maui.Platform.Linux;

/// <summary>
/// Presents <c>Navigation.PushModalAsync</c> / <c>PopModalAsync</c> pages on Linux through
/// MAUI's modal-platform extension point (<see cref="IModalNavigationPlatformFactory"/>, MAUI 11).
/// </summary>
/// <remarks>
/// MAUI keeps the cross-platform modal stack, the page lifecycle events and the reconciliation
/// of the requested stack with the presented one; this platform shows and removes one modal
/// page at a time as a full-window layer of the window's <see cref="WindowContext"/>. Presenting
/// here, rather than from <c>Window.ModalPushed</c>, gives MAUI's platform ordering (the modal is
/// on screen before <c>PushModalAsync</c> completes and before NavigatedTo and ModalPushed are
/// raised; it is off screen before the page leaves the window's logical children), and lets an
/// app or library register its own <see cref="IModalNavigationPlatformFactory"/> instead, as
/// on any MAUI platform (<see cref="Register"/> only adds this one when none is registered).
/// </remarks>
public sealed class LinuxModalNavigationPlatform : IModalNavigationPlatform
{
    // The platform of each window, so the window's context can ask MAUI to present the modal
    // pages pushed before it adopted the window (RequestSync).
    private static readonly ConditionalWeakTable<Microsoft.Maui.Controls.Window, LinuxModalNavigationPlatform> s_byWindow = new();

    // The window context that shows each window (WindowContext.MauiWindow).
    private static readonly ConditionalWeakTable<Microsoft.Maui.Controls.Window, WindowContext> s_contexts = new();

    private readonly IModalNavigationHost _host;
    private readonly List<Microsoft.Maui.Controls.Page> _presented = new();
    private WindowContext? _presentedIn;
    private bool _disposed;

    private LinuxModalNavigationPlatform(IModalNavigationHost host)
    {
        _host = host;
        s_byWindow.AddOrUpdate(host.Window, this);
    }

    /// <summary>
    /// Registers the Linux modal platform unless the app already registered an
    /// <see cref="IModalNavigationPlatformFactory"/> (a registration made later wins too:
    /// MAUI resolves the last one).
    /// </summary>
    public static void Register(IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddSingleton<IModalNavigationPlatformFactory, Factory>();
    }

    /// <summary>A window context now shows <paramref name="window"/>: its modal pages are presented there.</summary>
    internal static void WindowAdopted(Microsoft.Maui.Controls.Window window, WindowContext context) =>
        s_contexts.AddOrUpdate(window, context);

    /// <summary>The context no longer shows <paramref name="window"/> (it adopted another, or closed).</summary>
    internal static void WindowReleased(Microsoft.Maui.Controls.Window window, WindowContext context)
    {
        if (s_contexts.TryGetValue(window, out var current) && ReferenceEquals(current, context))
            s_contexts.Remove(window);
    }

    /// <summary>
    /// Asks MAUI to apply the window's modal stack (called when a window context adopts the
    /// window: pages pushed before then were recorded by MAUI and wait for this platform).
    /// </summary>
    internal static void RequestSync(Microsoft.Maui.Controls.Window window)
    {
        if (s_byWindow.TryGetValue(window, out var platform) && !platform._disposed)
            platform._host.RequestSync();
    }

    /// <summary>Ready once the window has a page and handler (MAUI's side) and a Linux window context shows it.</summary>
    public bool IsReady => !_disposed && _host.IsWindowReady && ContextOf(_host.Window) != null;

    /// <summary>Shows <paramref name="modal"/> as the window's top layer; the task completes once it is presented.</summary>
    public Task PushModalAsync(Microsoft.Maui.Controls.Page modal, bool animated)
    {
        var context = ContextOf(_host.Window)
            ?? throw new InvalidOperationException("The window has no Linux window context to present the modal page in.");
        if (!context.PushModalView(modal))
            throw new InvalidOperationException($"The modal page {modal.GetType().Name} could not be presented.");
        _presented.Add(modal);
        _presentedIn = context;
        return Task.CompletedTask;
    }

    /// <summary>Removes <paramref name="modal"/>'s layer and disconnects its handler; nothing to do when it is already gone.</summary>
    public Task PopModalAsync(Microsoft.Maui.Controls.Page modal, bool animated)
    {
        _presented.Remove(modal);
        (ContextOf(_host.Window) ?? _presentedIn)?.PopModalView(modal);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The window's page has a handler (first shown, replaced, or the window reopened with a new
    /// handler): MAUI's modal stack is applied now, as Windows applies it when the window is
    /// activated, so the modal pages of a window shown again are presented again. Posted, not
    /// run inline: MAUI may be resolving this platform from inside its own reconciliation.
    /// Escape and the back arrow need no hook: the window context routes them itself.
    /// </summary>
    public void PageAttached()
    {
        var window = _host.Window;
        Microsoft.Maui.Dispatching.IDispatcher? dispatcher = null;
        try { dispatcher = window.Dispatcher; }
        catch (InvalidOperationException) { }
        if (dispatcher != null)
            dispatcher.Dispatch(() => { if (!_disposed) _host.RequestSync(); });
        else if (!_disposed)
            _host.RequestSync();
    }

    /// <summary>
    /// Removes the layers of the modal pages still presented (MAUI does not pop them when the
    /// window is destroyed or gets a new handler; a new platform presents them again then).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (s_byWindow.TryGetValue(_host.Window, out var current) && ReferenceEquals(current, this))
            s_byWindow.Remove(_host.Window);

        var context = _presentedIn;
        for (int i = _presented.Count - 1; i >= 0; i--)
        {
            try { context?.PopModalView(_presented[i]); }
            catch (Exception ex) { DiagnosticLog.Error("LinuxModalNavigationPlatform", "Removing a modal page on dispose failed", ex); }
        }
        _presented.Clear();
        _presentedIn = null;
    }

    private static WindowContext? ContextOf(Microsoft.Maui.Controls.Window window) =>
        s_contexts.TryGetValue(window, out var context) ? context : null;

    private sealed class Factory : IModalNavigationPlatformFactory
    {
        public IModalNavigationPlatform? CreateModalNavigationPlatform(IModalNavigationHost host) =>
            new LinuxModalNavigationPlatform(host);
    }
}
