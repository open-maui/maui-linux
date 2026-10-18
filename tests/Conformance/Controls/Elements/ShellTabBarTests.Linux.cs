// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Reflection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit.Sdk;

namespace Microsoft.Maui.DeviceTests
{
	// Linux counterpart of ShellTabBarTests.Windows.cs: as on Windows, SkiaShell shows the current
	// ShellItem's sections as tabs in a top navigation pane (WinUI's NavigationView in Top mode,
	// an icon beside a title per tab, coloured as ShellItemHandler.UpdateAppearance colours them).
	// SkiaShell.TabBarItemBounds gives the icon and title of the tab standing for a section (its
	// own, or its content's for the section MAUI made around a bare ShellContent).
	public partial class ShellTests
	{
		Task ValidateTabBarIconColor(Microsoft.Maui.Controls.ShellSection item, Color iconColor, bool hasColor) =>
			ValidateTabBarColor(item, iconColor, hasColor, icon: true);

		Task ValidateTabBarTextColor(Microsoft.Maui.Controls.ShellSection item, Color textColor, bool hasColor) =>
			ValidateTabBarColor(item, textColor, hasColor, icon: false);

		async Task ValidateTabBarColor(Microsoft.Maui.Controls.ShellSection item, Color color, bool hasColor, bool icon)
		{
			// The tab is drawn by the next frame; its icon once its image source has loaded.
			await AssertHelpers.AssertEventually(() =>
			{
				var (host, shell) = FindShellOf(item);
				host.RunFrame();
				return !PartOf(shell, item, icon).IsEmpty;
			}, message: $"SkiaShell draws no tab {(icon ? "icon" : "title")} for ShellSection '{item.Title}'.");

			await InvokeOnMainThreadAsync(() =>
			{
				var (host, shell) = FindShellOf(item);
				var part = PartOf(shell, item, icon);
				using var frame = host.Snapshot();
				var rect = new RectF(part.Left, part.Top, part.Width, part.Height);
				if (hasColor)
					frame.AssertContainsColor(color, _ => rect);
				else
					Assert.Throws<XunitException>(() => frame.AssertContainsColor(color, _ => rect));
			});
		}

		static (HeadlessWindowHost Host, SkiaShell Shell) FindShellOf(Microsoft.Maui.Controls.ShellSection item)
		{
			var mauiShell = item.Parent?.Parent as Microsoft.Maui.Controls.Shell
				?? throw new XunitException($"ShellSection '{item.Title}' is not in a Shell.");
			var shell = mauiShell.Handler?.PlatformView as SkiaShell
				?? throw new XunitException("The Shell has no SkiaShell.");
			var host = HeadlessWindowHost.HostOf(shell) ?? throw new XunitException("The Shell is not in an open window.");
			return (host, shell);
		}

		static SKRect PartOf(SkiaShell shell, Microsoft.Maui.Controls.ShellSection item, bool icon)
		{
			var method = typeof(SkiaShell).GetMethod("TabBarItemBounds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new XunitException("SkiaShell.TabBarItemBounds not found (OpenMaui changed; update the Linux helper).");
			var (iconBounds, titleBounds) = ((SKRect, SKRect))method.Invoke(shell, new object[] { item })!;
			return icon ? iconBounds : titleBounds;
		}
	}
}
