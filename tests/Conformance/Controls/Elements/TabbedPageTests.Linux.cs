// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of TabbedPageTests.Windows.cs: the tab bar SkiaTabbedPage
	// draws (equal-width tabs across TabBarBounds, a title per tab and its icon).
	public partial class TabbedPageTests
	{
		static (HeadlessWindowHost Host, SkiaTabbedPage Tabs) FindTabs(TabbedPage tabbedPage)
		{
			var tabs = tabbedPage.Handler?.PlatformView as SkiaTabbedPage
				?? throw new XunitException($"TabbedPage's platform view is {tabbedPage.Handler?.PlatformView?.GetType().Name ?? "null"}, not a SkiaTabbedPage.");
			var host = HeadlessWindowHost.HostOf(tabs) ?? throw new XunitException("The TabbedPage is not in an open window.");
			return (host, tabs);
		}

		Task ValidateTabBarTextColor(TabbedPage tabbedPage, string tabText, Color color, bool hasColor) =>
			InvokeOnMainThreadAsync(() =>
			{
				var (host, tabs) = FindTabs(tabbedPage);
				var index = tabs.Tabs.ToList().FindIndex(t => t.Title == tabText);
				if (index < 0)
					throw new XunitException($"SkiaTabbedPage has no tab titled '{tabText}' (tabs: {string.Join(", ", tabs.Tabs.Select(t => t.Title))}).");
				using var frame = host.Snapshot();
				var bar = tabs.TabBarBounds;
				float width = bar.Width / tabs.Tabs.Count;
				var rect = new RectF(bar.Left + index * width, bar.Top, width, bar.Height);
				if (hasColor)
					frame.AssertContainsColor(color, _ => rect);
				else
					Assert.Throws<XunitException>(() => frame.AssertContainsColor(color, _ => rect));
			});

		/// <summary>The icon SkiaTabbedPage draws for the tab (tinted with the tab's colour).</summary>
		async Task ValidateTabBarIconColor(TabbedPage tabbedPage, string tabText, Color color, bool hasColor)
		{
			// The icon is drawn once its image source has loaded.
			await AssertHelpers.AssertEventually(() =>
			{
				var (_, tabs) = FindTabs(tabbedPage);
				var index = tabs.Tabs.ToList().FindIndex(t => t.Title == tabText);
				return index >= 0 && !IconBounds(tabs, index).IsEmpty;
			});
			await InvokeOnMainThreadAsync(() =>
			{
				var (host, tabs) = FindTabs(tabbedPage);
				var index = tabs.Tabs.ToList().FindIndex(t => t.Title == tabText);
				if (index < 0)
					throw new XunitException($"SkiaTabbedPage has no tab titled '{tabText}' (tabs: {string.Join(", ", tabs.Tabs.Select(t => t.Title))}).");
				var icon = IconBounds(tabs, index);
				using var frame = host.Snapshot();
				var rect = new RectF(icon.Left, icon.Top, icon.Width, icon.Height);
				if (hasColor)
					frame.AssertContainsColor(color, _ => rect);
				else
					Assert.Throws<XunitException>(() => frame.AssertContainsColor(color, _ => rect));
			});
		}

		static SKRect IconBounds(SkiaTabbedPage tabs, int index) =>
			(SKRect)(typeof(SkiaTabbedPage).GetMethod("TabIconBounds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
				?? throw new XunitException("SkiaTabbedPage.TabIconBounds not found (OpenMaui changed; update the Linux helper).")).Invoke(tabs, new object[] { index })!;
	}
}
