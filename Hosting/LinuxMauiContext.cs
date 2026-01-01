using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Dispatching;

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
			if (_animationManager == null)
			{
				_animationManager = (IAnimationManager?)(((object)_services.GetService<IAnimationManager>()) ?? ((object)new LinuxAnimationManager((ITicker)(object)new LinuxTicker())));
			}
			return _animationManager;
		}
	}

	public IDispatcher Dispatcher
	{
		get
		{
			if (_dispatcher == null)
			{
				_dispatcher = (IDispatcher?)(((object)_services.GetService<IDispatcher>()) ?? ((object)new LinuxDispatcher()));
			}
			return _dispatcher;
		}
	}

	public LinuxMauiContext(IServiceProvider services, LinuxApplication linuxApp)
	{
		_services = services ?? throw new ArgumentNullException("services");
		_linuxApp = linuxApp ?? throw new ArgumentNullException("linuxApp");
		_handlers = services.GetRequiredService<IMauiHandlersFactory>();
	}
}
