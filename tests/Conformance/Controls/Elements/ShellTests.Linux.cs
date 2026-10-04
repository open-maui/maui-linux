// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of ShellTests.Windows.cs / ShellFlyoutTests helpers: SkiaShell's state.
	public partial class ShellTests
	{
		protected Task CheckFlyoutState(ShellHandler handler, bool desiredState)
		{
			Assert.Equal(desiredState, LinuxPlatform.View<SkiaShell>(handler).FlyoutIsPresented);
			return Task.CompletedTask;
		}

		protected async Task OpenFlyout(ShellHandler shellRenderer, TimeSpan? timeOut = null)
		{
			var shell = LinuxPlatform.View<SkiaShell>(shellRenderer);
			if (!shell.FlyoutIsPresented)
				shell.FlyoutIsPresented = true;
			await AssertHelpers.AssertEventually(() => shell.FlyoutIsPresented, (int)(timeOut ?? TimeSpan.FromSeconds(2)).TotalMilliseconds);
		}

		/// <summary>The view's frame relative to the flyout panel SkiaShell draws (SkiaShell.FlyoutBounds).</summary>
		internal Rect GetFrameRelativeToFlyout(ShellHandler handler, IView view)
		{
			var shell = LinuxPlatform.View<SkiaShell>(handler);
			// The flyout's views are arranged when a frame draws the open flyout.
			HeadlessWindowHost.HostOf(shell)?.RunFrame();
			var platformView = view.Handler?.PlatformView as SkiaView
				?? throw new XunitException($"{view.GetType().Name} has no platform view.");
			var flyout = shell.FlyoutBounds;
			var bounds = platformView.Bounds;
			return new Rect(bounds.X - flyout.X, bounds.Y - flyout.Y, bounds.Width, bounds.Height);
		}

		/// <summary>Windows selects the page's tab in the navigation view; SkiaShell selects its section item.</summary>
		async Task TapToSelect(ContentPage page)
		{
			var shellContent = (Microsoft.Maui.Controls.ShellContent)page.Parent;
			var shellSection = (Microsoft.Maui.Controls.ShellSection)shellContent.Parent;
			var shellItem = (ShellItem)shellSection.Parent;
			var shell = (Shell)shellItem.Parent;

			await OnNavigatedToAsync(shell.CurrentPage);

			var skiaShell = LinuxPlatform.View<SkiaShell>(shell.Handler!);
			for (int s = 0; s < skiaShell.Sections.Count; s++)
			{
				var items = skiaShell.Sections[s].Items;
				for (int i = 0; i < items.Count; i++)
				{
					if (ReferenceEquals(items[i].MauiShellContent, shellContent))
					{
						skiaShell.SelectSection(s, i);
						await OnNavigatedToAsync(page);
						return;
					}
				}
			}
			throw new XunitException($"SkiaShell shows no item for ShellContent '{shellContent.Title}'.");
		}
	}
}
