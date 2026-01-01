using System;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ShellHandler : ViewHandler<Shell, SkiaShell>
{
	public static IPropertyMapper<Shell, ShellHandler> Mapper = (IPropertyMapper<Shell, ShellHandler>)(object)new PropertyMapper<Shell, ShellHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper });

	public static CommandMapper<Shell, ShellHandler> CommandMapper = new CommandMapper<Shell, ShellHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public ShellHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ShellHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaShell CreatePlatformView()
	{
		return new SkiaShell();
	}

	protected override void ConnectHandler(SkiaShell platformView)
	{
		base.ConnectHandler(platformView);
		platformView.FlyoutIsPresentedChanged += OnFlyoutIsPresentedChanged;
		platformView.Navigated += OnNavigated;
		if (base.VirtualView != null)
		{
			base.VirtualView.Navigating += OnShellNavigating;
			base.VirtualView.Navigated += OnShellNavigated;
		}
	}

	protected override void DisconnectHandler(SkiaShell platformView)
	{
		platformView.FlyoutIsPresentedChanged -= OnFlyoutIsPresentedChanged;
		platformView.Navigated -= OnNavigated;
		if (base.VirtualView != null)
		{
			base.VirtualView.Navigating -= OnShellNavigating;
			base.VirtualView.Navigated -= OnShellNavigated;
		}
		base.DisconnectHandler(platformView);
	}

	private void OnFlyoutIsPresentedChanged(object? sender, EventArgs e)
	{
	}

	private void OnNavigated(object? sender, ShellNavigationEventArgs e)
	{
	}

	private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(36, 1);
		defaultInterpolatedStringHandler.AppendLiteral("[ShellHandler] Shell Navigating to: ");
		ShellNavigationState target = e.Target;
		defaultInterpolatedStringHandler.AppendFormatted((target != null) ? target.Location : null);
		Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
		if (base.PlatformView != null)
		{
			ShellNavigationState target2 = e.Target;
			if (((target2 != null) ? target2.Location : null) != null)
			{
				string text = e.Target.Location.ToString().TrimStart('/');
				Console.WriteLine("[ShellHandler] Routing to: " + text);
				base.PlatformView.GoToAsync(text);
			}
		}
	}

	private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(35, 1);
		defaultInterpolatedStringHandler.AppendLiteral("[ShellHandler] Shell Navigated to: ");
		ShellNavigationState current = e.Current;
		defaultInterpolatedStringHandler.AppendFormatted((current != null) ? current.Location : null);
		Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
	}
}
