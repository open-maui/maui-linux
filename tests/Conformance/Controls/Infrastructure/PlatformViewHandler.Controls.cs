// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
namespace Microsoft.Maui.DeviceTests
{
	public static class PlatformViewHandlerExtensions
	{
		/// <summary>
		/// MAUI's platform ElementExtensions.ToHandler(IView) (platform TFMs only):
		/// MAUI's own ToHandler, which resolves the handler through the app's
		/// registrations, typed as a view handler.
		/// </summary>
		public static IPlatformViewHandler ToHandler(this IView view, IMauiContext context) =>
			(IPlatformViewHandler)Microsoft.Maui.Platform.ElementExtensions.ToHandler(view, context);

		public static object ToPlatform(this IElementHandler handler) => handler.PlatformView!;
	}
}
