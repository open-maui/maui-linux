using Microsoft.Maui.Hosting;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public static class HandlerMappingExtensions
{
	public static IMauiHandlersCollection AddHandler<TView, THandler>(this IMauiHandlersCollection handlers) where TView : class where THandler : class
	{
		MauiHandlersCollectionExtensions.AddHandler(handlers, typeof(TView), typeof(THandler));
		return handlers;
	}
}
