// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Platform.Linux.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public class LinuxMauiContext : IMauiContext
{
    private readonly IServiceProvider _services;
    private readonly IMauiHandlersFactory _handlers;
    private readonly LinuxApplication _linuxApp;
    private IAnimationManager? _animationManager;
    private IDispatcher? _dispatcher;

    public IServiceProvider Services => _services;

    public IMauiHandlersFactory Handlers => _handlers;

    public LinuxApplication LinuxApp => _linuxApp;

    public IAnimationManager AnimationManager
    {
        get
        {
            _animationManager ??= _services.GetService<IAnimationManager>()
                ?? new LinuxAnimationManager(new LinuxTicker());
            return _animationManager;
        }
    }

    public IDispatcher Dispatcher
    {
        get
        {
            _dispatcher ??= _services.GetService<IDispatcher>()
                ?? new LinuxDispatcher();
            return _dispatcher;
        }
    }

    public LinuxMauiContext(IServiceProvider services, LinuxApplication linuxApp)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _linuxApp = linuxApp ?? throw new ArgumentNullException(nameof(linuxApp));
        _handlers = services.GetRequiredService<IMauiHandlersFactory>();
    }
}
