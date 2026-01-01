using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ContentPageHandler : PageHandler
{
	public new static IPropertyMapper<ContentPage, ContentPageHandler> Mapper = (IPropertyMapper<ContentPage, ContentPageHandler>)(object)new PropertyMapper<ContentPage, ContentPageHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)PageHandler.Mapper }) { ["Content"] = MapContent };

	public new static CommandMapper<ContentPage, ContentPageHandler> CommandMapper = new CommandMapper<ContentPage, ContentPageHandler>((CommandMapper)(object)PageHandler.CommandMapper);

	public ContentPageHandler()
		: base((IPropertyMapper?)(object)Mapper, (CommandMapper?)(object)CommandMapper)
	{
	}

	public ContentPageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper?)(((object)mapper) ?? ((object)Mapper)), (CommandMapper?)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaPage CreatePlatformView()
	{
		return new SkiaContentPage();
	}

	public static void MapContent(ContentPageHandler handler, ContentPage page)
	{
		if (((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView == null || ((ElementHandler)handler).MauiContext == null)
		{
			return;
		}
		View content = page.Content;
		if (content != null)
		{
			if (((VisualElement)content).Handler == null)
			{
				Console.WriteLine("[ContentPageHandler] Creating handler for content: " + ((object)content).GetType().Name);
				((VisualElement)content).Handler = ((IView)(object)content).ToViewHandler(((ElementHandler)handler).MauiContext);
			}
			IViewHandler handler2 = ((VisualElement)content).Handler;
			if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaView skiaView)
			{
				Console.WriteLine("[ContentPageHandler] Setting content: " + ((object)skiaView).GetType().Name);
				((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.Content = skiaView;
			}
			else
			{
				IViewHandler handler3 = ((VisualElement)content).Handler;
				Console.WriteLine("[ContentPageHandler] Content handler PlatformView is not SkiaView: " + (((handler3 == null) ? null : ((IElementHandler)handler3).PlatformView?.GetType().Name) ?? "null"));
			}
		}
		else
		{
			((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.Content = null;
		}
	}
}
