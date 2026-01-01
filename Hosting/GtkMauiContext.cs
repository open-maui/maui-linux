using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public class GtkMauiContext : IMauiContext
{
	private readonly IServiceProvider _services;

	private readonly IMauiHandlersFactory _handlers;

	private IAnimationManager? _animationManager;

	private IDispatcher? _dispatcher;

	public IServiceProvider Services => _services;

	public IMauiHandlersFactory Handlers => _handlers;

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

	public GtkMauiContext(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException("services");
		_handlers = services.GetRequiredService<IMauiHandlersFactory>();
		if (LinuxApplication.Current == null)
		{
			new LinuxApplication();
		}
	}
}
