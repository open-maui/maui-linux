// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests.Stubs
{
	/// <summary>
	/// Stand-in for MAUI's ButtonWithContainerStub (a button whose handler always
	/// wraps it in a native WrapperView). Linux has no wrapper views; the tests
	/// that use it are listed in KnownSkips. It exists so LayoutHandlerTests.cs
	/// compiles unchanged.
	/// </summary>
	public class ButtonWithContainerStub : ButtonStub
	{
	}
}

namespace Microsoft.Maui.DeviceTests.Handlers.Layout
{
	public partial class LayoutHandlerTests
	{
		static string GetNativeText(SkiaView view) => (view as SkiaLabel)?.Text;

		double GetNativeChildCount(LayoutHandler handler) => handler.PlatformView.Children.Count;

		double GetNativeChildCount(object platformView) => ((SkiaLayoutView)platformView).Children.Count;

		/// <summary>
		/// The platform children in stacking order, which is what the native
		/// child collection is on the other platforms (MAUI reorders it by
		/// ZIndex). A Skia layout keeps its children in layout order and stacks
		/// them by ZIndex when drawing and hit-testing (SkiaLayoutView's
		/// ChildrenInZOrder, internal), so that is the list compared here.
		/// </summary>
		IReadOnlyList<SkiaView> GetNativeChildren(LayoutHandler handler)
		{
			var zOrder = typeof(SkiaLayoutView).GetMethod("ChildrenInZOrder",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			return zOrder is null
				? handler.PlatformView.Children.ToList()
				: ((SkiaView[])zOrder.Invoke(handler.PlatformView, null)).ToList();
		}

		async Task AssertZIndexOrder(IReadOnlyList<SkiaView> children)
		{
			string expected = await InvokeOnMainThreadAsync(() =>
				string.Join(", ", children.OrderBy(GetNativeText).Select(GetNativeText)));
			string actual = await InvokeOnMainThreadAsync(() =>
				string.Join(", ", children.Select(GetNativeText)));
			Assert.Equal(expected, actual);
		}
	}
}
