using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ApplicationHandler : ElementHandler<IApplication, LinuxApplicationContext>
{
	public static IPropertyMapper<IApplication, ApplicationHandler> Mapper = (IPropertyMapper<IApplication, ApplicationHandler>)(object)new PropertyMapper<IApplication, ApplicationHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ElementHandler.ElementMapper });

	public static CommandMapper<IApplication, ApplicationHandler> CommandMapper = new CommandMapper<IApplication, ApplicationHandler>((CommandMapper)(object)ElementHandler.ElementCommandMapper)
	{
		["OpenWindow"] = MapOpenWindow,
		["CloseWindow"] = MapCloseWindow
	};

	public ApplicationHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public ApplicationHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override LinuxApplicationContext CreatePlatformElement()
	{
		return new LinuxApplicationContext();
	}

	protected override void ConnectHandler(LinuxApplicationContext platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Application = base.VirtualView;
	}

	protected override void DisconnectHandler(LinuxApplicationContext platformView)
	{
		platformView.Application = null;
		base.DisconnectHandler(platformView);
	}

	public static void MapOpenWindow(ApplicationHandler handler, IApplication application, object? args)
	{
		IWindow val = (IWindow)((args is IWindow) ? args : null);
		if (val != null)
		{
			((ElementHandler<IApplication, LinuxApplicationContext>)(object)handler).PlatformView?.OpenWindow(val);
		}
	}

	public static void MapCloseWindow(ApplicationHandler handler, IApplication application, object? args)
	{
		IWindow val = (IWindow)((args is IWindow) ? args : null);
		if (val != null)
		{
			((ElementHandler<IApplication, LinuxApplicationContext>)(object)handler).PlatformView?.CloseWindow(val);
		}
	}
}
