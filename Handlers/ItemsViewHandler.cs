using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class ItemsViewHandler<TItemsView> : ViewHandler<TItemsView, SkiaItemsView> where TItemsView : ItemsView
{
	public static IPropertyMapper<TItemsView, ItemsViewHandler<TItemsView>> ItemsViewMapper = (IPropertyMapper<TItemsView, ItemsViewHandler<TItemsView>>)(object)new PropertyMapper<TItemsView, ItemsViewHandler<TItemsView>>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["ItemsSource"] = MapItemsSource,
		["ItemTemplate"] = MapItemTemplate,
		["EmptyView"] = MapEmptyView,
		["EmptyViewTemplate"] = MapEmptyViewTemplate,
		["HorizontalScrollBarVisibility"] = MapHorizontalScrollBarVisibility,
		["VerticalScrollBarVisibility"] = MapVerticalScrollBarVisibility,
		["Background"] = MapBackground
	};

	public static CommandMapper<TItemsView, ItemsViewHandler<TItemsView>> ItemsViewCommandMapper = new CommandMapper<TItemsView, ItemsViewHandler<TItemsView>>((CommandMapper)(object)ViewHandler.ViewCommandMapper) { ["ScrollTo"] = MapScrollTo };

	public ItemsViewHandler()
		: base((IPropertyMapper)(object)ItemsViewMapper, (CommandMapper)(object)ItemsViewCommandMapper)
	{
	}

	public ItemsViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)ItemsViewMapper)), (CommandMapper)(((object)commandMapper) ?? ((object)ItemsViewCommandMapper)))
	{
	}

	protected override SkiaItemsView CreatePlatformView()
	{
		return new SkiaItemsView();
	}

	protected override void ConnectHandler(SkiaItemsView platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Scrolled += OnScrolled;
		platformView.ItemTapped += OnItemTapped;
		platformView.ItemRenderer = RenderItem;
	}

	protected override void DisconnectHandler(SkiaItemsView platformView)
	{
		platformView.Scrolled -= OnScrolled;
		platformView.ItemTapped -= OnItemTapped;
		platformView.ItemRenderer = null;
		base.DisconnectHandler(platformView);
	}

	private void OnScrolled(object? sender, ItemsScrolledEventArgs e)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		object obj = base.VirtualView;
		if (obj != null)
		{
			((ItemsView)obj).SendScrolled(new ItemsViewScrolledEventArgs
			{
				VerticalOffset = e.ScrollOffset,
				VerticalDelta = 0.0,
				HorizontalOffset = 0.0,
				HorizontalDelta = 0.0
			});
		}
	}

	private void OnItemTapped(object? sender, ItemsViewItemTappedEventArgs e)
	{
	}

	protected virtual bool RenderItem(object item, int index, SKRect bounds, SKCanvas canvas, SKPaint paint)
	{
		object obj = base.VirtualView;
		if (obj != null)
		{
			_ = ((ItemsView)obj).ItemTemplate;
		}
		else
			_ = null;
		return false;
	}

	public static void MapItemsSource(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		if (((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.ItemsSource = ((ItemsView)itemsView).ItemsSource;
		}
	}

	public static void MapItemTemplate(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView?.Invalidate();
	}

	public static void MapEmptyView(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		if (((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.EmptyView = ((ItemsView)itemsView).EmptyView;
			if (((ItemsView)itemsView).EmptyView is string emptyViewText)
			{
				((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.EmptyViewText = emptyViewText;
			}
		}
	}

	public static void MapEmptyViewTemplate(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView?.Invalidate();
	}

	public static void MapHorizontalScrollBarVisibility(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected I4, but got Unknown
		if (((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.HorizontalScrollBarVisibility = (ScrollBarVisibility)((ItemsView)itemsView).HorizontalScrollBarVisibility;
		}
	}

	public static void MapVerticalScrollBarVisibility(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected I4, but got Unknown
		if (((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.VerticalScrollBarVisibility = (ScrollBarVisibility)((ItemsView)itemsView).VerticalScrollBarVisibility;
		}
	}

	public static void MapBackground(ItemsViewHandler<TItemsView> handler, TItemsView itemsView)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView != null)
		{
			Brush background = ((VisualElement)(object)itemsView).Background;
			SolidColorBrush val = (SolidColorBrush)(object)((background is SolidColorBrush) ? background : null);
			if (val != null)
			{
				((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapScrollTo(ItemsViewHandler<TItemsView> handler, TItemsView itemsView, object? args)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		if (((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView == null)
		{
			return;
		}
		ScrollToRequestEventArgs e = (ScrollToRequestEventArgs)((args is ScrollToRequestEventArgs) ? args : null);
		if (e != null)
		{
			if ((int)e.Mode == 1)
			{
				((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.ScrollToIndex(e.Index, e.IsAnimated);
			}
			else if (e.Item != null)
			{
				((ViewHandler<TItemsView, SkiaItemsView>)(object)handler).PlatformView.ScrollToItem(e.Item, e.IsAnimated);
			}
		}
	}
}
