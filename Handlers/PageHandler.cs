using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class PageHandler : ViewHandler<Page, SkiaPage>
{
	public static IPropertyMapper<Page, PageHandler> Mapper = (IPropertyMapper<Page, PageHandler>)(object)new PropertyMapper<Page, PageHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Title"] = MapTitle,
		["BackgroundImageSource"] = MapBackgroundImageSource,
		["Padding"] = MapPadding,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public static CommandMapper<Page, PageHandler> CommandMapper = new CommandMapper<Page, PageHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public PageHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public PageHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaPage CreatePlatformView()
	{
		return new SkiaPage();
	}

	protected override void ConnectHandler(SkiaPage platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Appearing += OnAppearing;
		platformView.Disappearing += OnDisappearing;
	}

	protected override void DisconnectHandler(SkiaPage platformView)
	{
		platformView.Appearing -= OnAppearing;
		platformView.Disappearing -= OnDisappearing;
		base.DisconnectHandler(platformView);
	}

	private void OnAppearing(object? sender, EventArgs e)
	{
		Page virtualView = base.VirtualView;
		Console.WriteLine("[PageHandler] OnAppearing received for: " + ((virtualView != null) ? virtualView.Title : null));
		Page virtualView2 = base.VirtualView;
		if (virtualView2 != null)
		{
			((IPageController)virtualView2).SendAppearing();
		}
	}

	private void OnDisappearing(object? sender, EventArgs e)
	{
		Page virtualView = base.VirtualView;
		if (virtualView != null)
		{
			((IPageController)virtualView).SendDisappearing();
		}
	}

	public static void MapTitle(PageHandler handler, Page page)
	{
		if (((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView != null)
		{
			((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.Title = page.Title ?? "";
		}
	}

	public static void MapBackgroundImageSource(PageHandler handler, Page page)
	{
		((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView?.Invalidate();
	}

	public static void MapPadding(PageHandler handler, Page page)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView != null)
		{
			Thickness padding = page.Padding;
			((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.PaddingLeft = (float)((Thickness)(ref padding)).Left;
			((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.PaddingTop = (float)((Thickness)(ref padding)).Top;
			((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.PaddingRight = (float)((Thickness)(ref padding)).Right;
			((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.PaddingBottom = (float)((Thickness)(ref padding)).Bottom;
		}
	}

	public static void MapBackground(PageHandler handler, Page page)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView != null)
		{
			Brush background = ((VisualElement)page).Background;
			SolidColorBrush val = (SolidColorBrush)(object)((background is SolidColorBrush) ? background : null);
			if (val != null)
			{
				((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackgroundColor(PageHandler handler, Page page)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView != null)
		{
			Color backgroundColor = ((VisualElement)page).BackgroundColor;
			if (backgroundColor != null && backgroundColor != Colors.Transparent)
			{
				((ViewHandler<Page, SkiaPage>)(object)handler).PlatformView.BackgroundColor = backgroundColor.ToSKColor();
				Console.WriteLine($"[PageHandler] MapBackgroundColor: {backgroundColor}");
			}
		}
	}
}
