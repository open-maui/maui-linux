// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Blazor.Handlers;

/// <summary>
/// Blazor's renderer dispatcher over MAUI's UI-thread dispatcher. WebKit's
/// GLib API is single-threaded, so every callback that reaches the view
/// (SendMessage, navigation) must land on the OpenMaui run loop thread.
/// </summary>
internal sealed class LinuxBlazorDispatcher : AspNetCore.Components.Dispatcher
{
    private readonly IDispatcher _dispatcher;

    public LinuxBlazorDispatcher(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public override bool CheckAccess() => !_dispatcher.IsDispatchRequired;

    public override Task InvokeAsync(Action workItem)
    {
        if (CheckAccess())
        {
            try { workItem(); return Task.CompletedTask; }
            catch (Exception ex) { return Task.FromException(ex); }
        }
        return _dispatcher.DispatchAsync(workItem);
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (CheckAccess())
        {
            try { return workItem(); }
            catch (Exception ex) { return Task.FromException(ex); }
        }
        return _dispatcher.DispatchAsync(workItem);
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (CheckAccess())
        {
            try { return Task.FromResult(workItem()); }
            catch (Exception ex) { return Task.FromException<TResult>(ex); }
        }
        return _dispatcher.DispatchAsync(workItem);
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (CheckAccess())
        {
            try { return workItem(); }
            catch (Exception ex) { return Task.FromException<TResult>(ex); }
        }
        return _dispatcher.DispatchAsync(workItem);
    }
}
