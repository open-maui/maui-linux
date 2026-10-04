// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Linux counterpart of ControlsHandlerTestBase.Windows.cs / .Android.cs: the
	/// window a test's page goes in is a headless OpenMaui window
	/// (HeadlessWindowHost), opened the way an app's first window is, and the
	/// navigation-bar helpers read the bar the window shows.
	///
	/// MAUI's toolbar element gets a Linux ToolbarHandler (the window keeps its
	/// platform element, SkiaWindow.Toolbar), but the bar itself is drawn by the page
	/// that owns it, SkiaShell's navigation bar for a Shell and the current SkiaPage's
	/// bar inside a SkiaNavigationPage.
	/// The helpers find that page in the window (top modal first, through
	/// flyout and tabbed pages) and read its state.
	/// </summary>
	public partial class ControlsHandlerTestBase
	{
		Task SetupWindowForTests<THandler>(IWindow window, Func<Task> runTests, IMauiContext? mauiContext = null)
			where THandler : class, IElementHandler
		{
			mauiContext ??= MauiContext;
			return InvokeOnMainThreadAsync(async () =>
			{
				var host = HeadlessWindowHost.Open(window, mauiContext);
				try
				{
					await runTests();
				}
				finally
				{
					host.Close();
				}
			});
		}

		/// <summary>What shows the navigation bar in the window now.</summary>
		sealed class LinuxBar
		{
			public SkiaView Owner { get; init; } = null!;
			public bool IsVisible { get; init; }
			public bool BackButtonVisible { get; init; }
			public string? Title { get; init; }
			public IReadOnlyList<(string Text, ToolbarItemOrder Order)> Items { get; init; } = new List<(string, ToolbarItemOrder)>();
		}

		static HeadlessWindowHost HostFor(IElementHandler? handler)
		{
			if (handler?.PlatformView is SkiaView view && HeadlessWindowHost.HostOf(view) is { } own)
				return own;
			return HeadlessWindowHost.Current ?? throw new XunitException("No window is open.");
		}

		static LinuxBar? FindBar(HeadlessWindowHost host)
		{
			host.RunFrame();
			var modals = host.Context.ModalViews;
			var view = modals.Count > 0 ? modals[modals.Count - 1] : host.Root;
			while (view is not null)
			{
				switch (view)
				{
					case SkiaShell shell:
						return new LinuxBar
						{
							Owner = shell,
							IsVisible = shell.NavBarIsVisible,
							BackButtonVisible = shell.NavBarIsVisible && shell.IsBackButtonVisible,
							Title = shell.Title,
							Items = ShellToolbarItems(shell),
						};
					case SkiaFlyoutPage flyout:
						view = flyout.Detail;
						continue;
					case SkiaTabbedPage tabbed:
						view = tabbed.SelectedTab?.Content;
						continue;
					case SkiaNavigationPage navigation:
						var current = navigation.CurrentPage;
						if (current is null)
							return null;
						return new LinuxBar
						{
							Owner = current,
							IsVisible = current.ShowNavigationBar,
							BackButtonVisible = NavigationShowsBackButton(navigation),
							Title = current.Title,
							Items = PageToolbarItems(current),
						};
					case SkiaPage page:
						return new LinuxBar
						{
							Owner = page,
							IsVisible = page.ShowNavigationBar,
							BackButtonVisible = false,
							Title = page.Title,
							Items = PageToolbarItems(page),
						};
					default:
						return null;
				}
			}
			return null;
		}

		// SkiaNavigationPage draws its back arrow when IsBackButtonVisible.
		static bool NavigationShowsBackButton(SkiaNavigationPage navigation) => navigation.IsBackButtonVisible;

		static IReadOnlyList<(string, ToolbarItemOrder)> PageToolbarItems(SkiaPage page) =>
			page is SkiaContentPage content
				? content.ToolbarItems.Select(i => (i.Text, i.Order == SkiaToolbarItemOrder.Secondary ? ToolbarItemOrder.Secondary : ToolbarItemOrder.Primary)).ToList()
				: new List<(string, ToolbarItemOrder)>();

		static IReadOnlyList<(string, ToolbarItemOrder)> ShellToolbarItems(SkiaShell shell)
		{
			var property = typeof(SkiaShell).GetProperty("PresentedToolbarItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new XunitException("SkiaShell.PresentedToolbarItems not found (OpenMaui changed; update the Linux helper).");
			var items = (IEnumerable<ToolbarItem>)property.GetValue(shell)!;
			return items.Select(i => (i.Text ?? string.Empty, i.Order == ToolbarItemOrder.Secondary ? ToolbarItemOrder.Secondary : ToolbarItemOrder.Primary)).ToList();
		}

		protected bool IsBackButtonVisible(IElementHandler handler) =>
			FindBar(HostFor(handler))?.BackButtonVisible ?? false;

		public bool IsNavigationBarVisible(IElementHandler handler) =>
			FindBar(HostFor(handler))?.IsVisible ?? false;

		public bool IsNavigationBarVisible(IMauiContext mauiContext) =>
			FindBar(HostFor(null))?.IsVisible ?? false;

		protected string? GetToolbarTitle(IElementHandler handler)
		{
			var bar = FindBar(HostFor(handler)) ?? throw new XunitException("The window shows no navigation bar.");
			return bar.Title;
		}

		/// <summary>
		/// MAUI's platform toolbar: the window's (Windows reads its navigation root's,
		/// Android its toolbar view). OpenMaui's window keeps the platform element of the
		/// toolbar it shows (SkiaWindow.Toolbar, realized by the Linux ToolbarHandler);
		/// it is returned when the window shows a navigation bar.
		/// </summary>
		protected object? GetPlatformToolbar(IElementHandler handler)
		{
			var host = HostFor(handler);
			var bar = FindBar(host);
			if (bar is not { IsVisible: true })
				return null;
			return (host.Window?.Handler?.PlatformView as Microsoft.Maui.Platform.Linux.Handlers.SkiaWindow)?.Toolbar;
		}

		/// <summary>The TitleView the window's navigation bar shows (SkiaShell's; NavigationPage.TitleView is not drawn).</summary>
		protected object? GetTitleView(IElementHandler handler)
		{
			var bar = FindBar(HostFor(handler)) ?? throw new XunitException("The window shows no navigation bar.");
			if (bar.Owner is SkiaShell shell)
				return shell.TitleView;
			throw new XunitException("OpenMaui draws no NavigationPage.TitleView: only Shell.TitleView is rendered in its navigation bar.");
		}

		/// <summary>
		/// The space the bar gives its TitleView: the title's place, between the navigation
		/// icon and the toolbar items, the bar's full height (Windows: the whole header).
		/// </summary>
		protected Size GetTitleViewExpectedSize(IElementHandler handler)
		{
			var bar = FindBar(HostFor(handler)) ?? throw new XunitException("The window shows no navigation bar.");
			if (bar.Owner is not SkiaShell shell)
				throw new XunitException("OpenMaui draws no NavigationPage.TitleView: only Shell.TitleView is rendered in its navigation bar.");
			var bounds = (Rect)(typeof(SkiaShell).GetProperty("TitleViewBounds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new XunitException("SkiaShell.TitleViewBounds not found (OpenMaui changed; update the Linux helper).")).GetValue(shell)!;
			return bounds.Size;
		}

		/// <summary>
		/// MAUI compares the platform toolbar's commands to the items (primary and
		/// secondary, in order). OpenMaui's bars keep text and order only, so the
		/// items are matched by those.
		/// </summary>
		public bool ToolbarItemsMatch(IElementHandler handler, params ToolbarItem[] toolbarItems)
		{
			var bar = FindBar(HostFor(handler)) ?? throw new XunitException("The window shows no navigation bar.");
			var expected = toolbarItems.Select(i => (i.Text ?? string.Empty, i.Order == ToolbarItemOrder.Secondary ? ToolbarItemOrder.Secondary : ToolbarItemOrder.Primary)).ToList();
			var expectedPrimary = expected.Where(i => i.Item2 != ToolbarItemOrder.Secondary).ToList();
			var expectedSecondary = expected.Where(i => i.Item2 == ToolbarItemOrder.Secondary).ToList();
			var actualPrimary = bar.Items.Where(i => i.Order != ToolbarItemOrder.Secondary).Select(i => (i.Text, i.Order)).ToList();
			var actualSecondary = bar.Items.Where(i => i.Order == ToolbarItemOrder.Secondary).Select(i => (i.Text, i.Order)).ToList();
			Assert.Equal(expectedPrimary, actualPrimary);
			Assert.Equal(expectedSecondary, actualSecondary);
			return true;
		}
	}
}
