using System;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class TabbedPageHandler : ViewHandler<ITabbedView, SkiaTabbedPage>
{
	public static IPropertyMapper<ITabbedView, TabbedPageHandler> Mapper = (IPropertyMapper<ITabbedView, TabbedPageHandler>)(object)new PropertyMapper<ITabbedView, TabbedPageHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper });

	public static CommandMapper<ITabbedView, TabbedPageHandler> CommandMapper = new CommandMapper<ITabbedView, TabbedPageHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public TabbedPageHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public TabbedPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaTabbedPage CreatePlatformView()
	{
		return new SkiaTabbedPage();
	}

	protected override void ConnectHandler(SkiaTabbedPage platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectedIndexChanged += OnSelectedIndexChanged;
	}

	protected override void DisconnectHandler(SkiaTabbedPage platformView)
	{
		platformView.SelectedIndexChanged -= OnSelectedIndexChanged;
		platformView.ClearTabs();
		base.DisconnectHandler(platformView);
	}

	private void OnSelectedIndexChanged(object? sender, EventArgs e)
	{
	}
}
