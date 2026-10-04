// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of MAUI's Stubs/StubBaseHandler.cs (the handler its ViewHandlerTests
	/// drive): the smallest Linux view handler, a <see cref="LinuxViewHandler{TVirtualView, TPlatformView}"/>
	/// over MAUI's <see cref="StubBase"/> with a bare Skia view, so the view tests (MapFrame on
	/// arrange, tooltips, and the generic view tests) run against OpenMaui's view handler base
	/// and its <see cref="ViewHandler.ViewMapper"/> mappings instead of MAUI's platform-neutral
	/// ViewHandler, whose arrange and mappings do nothing. Declared in the tests' namespace, so
	/// the name binds here before the Stubs namespace MAUI's file imports. MAUI's own file
	/// is not compiled (its platform view is a bare object).
	/// </summary>
	public class StubBaseHandler : LinuxViewHandler<StubBase, StubPlatformView>, IPlatformViewHandler
	{
		public static IPropertyMapper<StubBase, StubBaseHandler> StubMapper =
			new PropertyMapper<StubBase, StubBaseHandler>(ViewMapper)
			{
			};

		public static CommandMapper<StubBase, StubBaseHandler> StubCommandMapper =
			new(ViewCommandMapper)
			{
			};

		public StubBaseHandler()
			: this(null, null)
		{
		}

		public StubBaseHandler(IPropertyMapper? mapper = null, CommandMapper? commandMapper = null)
			: base(
				new PropertyMapper<StubBase, StubBaseHandler>(mapper ?? StubMapper),
				new CommandMapper<StubBase, StubBaseHandler>(commandMapper ?? ViewCommandMapper))
		{
		}

		// As MAUI's stub: the handler's own command mapper, so a test can extend it.
		public CommandMapper<StubBase, StubBaseHandler>? CommandMapper =>
			_commandMapper as CommandMapper<StubBase, StubBaseHandler>;

		public PropertyMapper<StubBase, StubBaseHandler>? PropertyMapper =>
			_mapper as PropertyMapper<StubBase, StubBaseHandler>;

		protected override StubPlatformView CreatePlatformView() => new StubPlatformView(MauiContext!);
	}

	/// <summary>A Skia view that draws nothing but its background (MAUI's stub platform view).</summary>
	public class StubPlatformView : SkiaView
	{
		public StubPlatformView(IMauiContext mauiContext)
		{
			MauiContext = mauiContext;
		}

		public IMauiContext MauiContext { get; }
	}
}
