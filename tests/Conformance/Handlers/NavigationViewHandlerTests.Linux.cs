// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of NavigationViewHandlerTests.Windows.cs: the platform view is a
	// SkiaNavigationPage (Windows: a Frame whose back stack holds the pages below the current one).
	public partial class NavigationViewHandlerTests
	{
		int GetNativeNavigationStackCount(NavigationViewHandler navigationViewHandler) =>
			navigationViewHandler.PlatformView.StackDepth;

		/// <summary>
		/// As the Windows partial. A navigation view reports NavigationFinished on the next
		/// iteration of the main loop (MAUI's dispatchers always queue); this suite's dispatcher
		/// runs work inline, so the handler gets a context whose dispatcher queues, and the
		/// queue is run after the request returns, as the main loop would.
		/// </summary>
		Task CreateNavigationViewHandlerAsync(IStackNavigationView navigationView, Func<NavigationViewHandler, Task> action)
		{
			return InvokeOnMainThreadAsync(async () =>
			{
				var loop = new LoopDispatcher();
				var context = new LoopContext(MauiContext, loop);
				var handler = CreateHandler<NavigationViewHandler>(navigationView, context);
				await handler.PlatformView.AttachAndRun(async () =>
				{
					if (navigationView is NavigationViewStub nvs && nvs.NavigationStack?.Count > 0)
					{
						navigationView.RequestNavigation(new NavigationRequest(nvs.NavigationStack, false));
						var finished = nvs.OnNavigationFinished;
						loop.RunPending();
						await finished;
					}

					await action(handler);
				});
			});
		}

		sealed class LoopContext : IMauiContext, IServiceProvider
		{
			readonly IMauiContext _inner;
			readonly IDispatcher _dispatcher;

			public LoopContext(IMauiContext inner, IDispatcher dispatcher)
			{
				_inner = inner;
				_dispatcher = dispatcher;
			}

			public IServiceProvider Services => this;

			public IMauiHandlersFactory Handlers => _inner.Handlers;

			public object? GetService(Type serviceType) =>
				serviceType == typeof(IDispatcher) ? _dispatcher : _inner.Services.GetService(serviceType);
		}

		sealed class LoopDispatcher : IDispatcher
		{
			readonly Queue<Action> _pending = new();

			public bool IsDispatchRequired => false;

			public bool Dispatch(Action action)
			{
				_pending.Enqueue(action);
				return true;
			}

			public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

			public IDispatcherTimer CreateTimer() => TestUtils.DeviceTests.Runners.TestDispatcher.Current.CreateTimer();

			public void RunPending()
			{
				while (_pending.Count > 0)
					_pending.Dequeue()();
			}
		}
	}
}
