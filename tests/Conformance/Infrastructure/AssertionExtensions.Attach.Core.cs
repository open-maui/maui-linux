// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Core suite: how a platform view is "attached" (shared helpers in
	/// AssertionExtensions.Linux.cs). The Controls suite hosts views in a real
	/// headless window instead (Controls/Infrastructure).
	/// </summary>
	public static partial class AssertionExtensions
	{
		public static Task AttachAndRun(this object view, Action action) =>
			view.AttachAndRun<bool>(() => { action(); return Task.FromResult(true); });

		public static Task AttachAndRun(this object view, Func<Task> action) =>
			view.AttachAndRun<bool>(async () => { await action(); return true; });

		public static Task<T> AttachAndRun<T>(this object view, Func<T> action) =>
			view.AttachAndRun<T>(() => Task.FromResult(action()));

		/// <summary>
		/// Puts the view in a window for the duration of <paramref name="action"/>:
		/// the root of a headless window context of a LinuxApplication (no display
		/// connection), as the platform's window would hold it, laid out at its
		/// size. Window services (focus, the visual tree's root) work as in an app.
		/// </summary>
		public static async Task<T> AttachAndRun<T>(this object view, Func<Task<T>> action)
		{
			var skia = view.AsSkia();
			var root = skia;
			while (root.Parent is not null)
				root = root.Parent;
			var app = HeadlessWindow.App;
			var previous = app.RootView;
			app.RootView = root;
			try
			{
				EnsureLaidOut(skia);
				return await action();
			}
			finally
			{
				app.RootView = previous;
			}
		}

		static class HeadlessWindow
		{
			static Microsoft.Maui.Platform.Linux.LinuxApplication s_app;

			public static Microsoft.Maui.Platform.Linux.LinuxApplication App =>
				s_app ??= Microsoft.Maui.Platform.Linux.LinuxApplication.Current ?? new Microsoft.Maui.Platform.Linux.LinuxApplication();
		}

		public static object? GetParent(this object? view) => (view as SkiaView)?.Parent;

		public static bool IsLoaded(this object? view) => view is SkiaView { Parent: not null } or SkiaView { Bounds.Width: > 0 };

		public static IDisposable OnLoaded(this object view, Action action)
		{
			action();
			return new ActionDisposable(() => { });
		}

		public static IDisposable OnUnloaded(this object view, Action action) =>
			new ActionDisposable(() => { });

		sealed class ActionDisposable : IDisposable
		{
			readonly Action _action;
			public ActionDisposable(Action action) => _action = action;
			public void Dispose() => _action();
		}
	}
}
