// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit.Sdk;

// The parts of MAUI's xUnitCustomizations.cs (src/TestUtils/src/DeviceTests)
// the Controls tests use beyond [Category] (shared, XunitCustomizations.Linux.cs):
// Fact/Theory attributes that take the display name positionally (defaulting to
// the method name), and SkipOnIOSVersion. MAUI's versions name discoverers in
// the device-runner assembly, which a `dotnet test` run does not load; these
// use xunit's own discoverers.
namespace Microsoft.Maui
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	[XunitTestCaseDiscoverer("Xunit.Sdk.FactDiscoverer", "xunit.execution.{Platform}")]
	public class FactAttribute : Xunit.FactAttribute
	{
		public FactAttribute([CallerMemberName] string displayName = "")
		{
			base.DisplayName = displayName;
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	[XunitTestCaseDiscoverer("Xunit.Sdk.TheoryDiscoverer", "xunit.execution.{Platform}")]
	public class TheoryAttribute : Xunit.TheoryAttribute
	{
		public TheoryAttribute([CallerMemberName] string displayName = "")
		{
			base.DisplayName = displayName;
		}
	}

	/// <summary>MAUI's iOS-version skip; never applies on Linux.</summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
	public class SkipOnIOSVersionAttribute : Attribute
	{
		public SkipOnIOSVersionAttribute(int major, string reason = "") { }
		public SkipOnIOSVersionAttribute(int major, int minor, string reason = "") { }
		public SkipOnIOSVersionAttribute(int major, int minor, int build, string reason = "") { }
	}
}

// Imported by several shared test files for the platform renderers (FrameRenderer,
// ShellRenderer, ...) used only inside their platform #if blocks. The namespace
// exists only on the platform TFMs; declaring it lets the imports resolve.
namespace Microsoft.Maui.Controls.Handlers.Compatibility
{
	/// <summary>
	/// iOS's NavigationPage renderer, which VisualElementTests registers in its
	/// non-Windows/Android branch; on Linux the NavigationPage handler.
	/// </summary>
	public class NavigationRenderer : Microsoft.Maui.Platform.Linux.Handlers.NavigationPageHandler
	{
	}

	/// <summary>
	/// MAUI's cell renderers (ListView/TableView cells as handlers). OpenMaui has no
	/// cell handlers: the ListView and TableView handlers draw cells themselves
	/// (CellViewFactory). Registering one is harmless; creating one fails visibly.
	/// </summary>
	public abstract class LinuxNoCellHandler : Microsoft.Maui.Handlers.ElementHandler<Microsoft.Maui.Controls.Cell, object>
	{
		protected LinuxNoCellHandler() : base(new PropertyMapper<Microsoft.Maui.Controls.Cell, LinuxNoCellHandler>()) { }

		protected override object CreatePlatformElement() =>
			throw new NotSupportedException($"OpenMaui has no {GetType().Name}: cells are drawn by the ListView/TableView handler.");
	}

	public class SwitchCellRenderer : LinuxNoCellHandler { }
	public class TextCellRenderer : LinuxNoCellHandler { }
	public class ViewCellRenderer : LinuxNoCellHandler { }
	public class ImageCellRenderer : LinuxNoCellHandler { }
	public class EntryCellRenderer : LinuxNoCellHandler { }
}
