// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using Microsoft.Maui.Hosting;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// MAUI's IPlatformViewHandler only exists on the platform TFMs; the
	/// platform-neutral net10.0 Microsoft.Maui.Core (what OpenMaui builds on)
	/// does not declare it. The shared tests use it as "a view handler whose
	/// PlatformView is the native view", which every Linux handler is; the
	/// handler aliases (HandlerAliases.cs) implement this marker so the casts
	/// in MAUI's test code succeed.
	/// </summary>
	public interface IPlatformViewHandler : IViewHandler
	{
	}

	public static class PlatformViewHandlerExtensions
	{
		/// <summary>
		/// MAUI's platform ToHandler returns IPlatformViewHandler. Resolves the
		/// handler through the registered factory (stubs map to the Linux
		/// handler aliases) and sets it up as ElementExtensions.ToHandler does.
		/// </summary>
		public static IPlatformViewHandler ToHandler(this IView view, IMauiContext context)
		{
			if (view.Handler is IPlatformViewHandler existing)
				return existing;

			var handler = (IViewHandler)context.Handlers.GetHandler(view.GetType())
				?? throw new InvalidOperationException($"No handler registered for {view.GetType()}");
			if (handler is not IPlatformViewHandler pvh)
				throw new InvalidOperationException(
					$"{handler.GetType()} is not an aliased Linux handler (register it in HandlerAliases).");

			handler.SetMauiContext(context);
			view.Handler = handler;
			if (handler.VirtualView != view)
				handler.SetVirtualView(view);
			return pvh;
		}

		public static object ToPlatform(this IElementHandler handler) => handler.PlatformView!;
	}
}
