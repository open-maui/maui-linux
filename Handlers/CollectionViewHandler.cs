using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Hosting;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class CollectionViewHandler : ViewHandler<CollectionView, SkiaCollectionView>
{
	private bool _isUpdatingSelection;

	public static IPropertyMapper<CollectionView, CollectionViewHandler> Mapper = (IPropertyMapper<CollectionView, CollectionViewHandler>)(object)new PropertyMapper<CollectionView, CollectionViewHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["ItemsSource"] = MapItemsSource,
		["ItemTemplate"] = MapItemTemplate,
		["EmptyView"] = MapEmptyView,
		["HorizontalScrollBarVisibility"] = MapHorizontalScrollBarVisibility,
		["VerticalScrollBarVisibility"] = MapVerticalScrollBarVisibility,
		["SelectedItem"] = MapSelectedItem,
		["SelectedItems"] = MapSelectedItems,
		["SelectionMode"] = MapSelectionMode,
		["Header"] = MapHeader,
		["Footer"] = MapFooter,
		["ItemsLayout"] = MapItemsLayout,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public static CommandMapper<CollectionView, CollectionViewHandler> CommandMapper = new CommandMapper<CollectionView, CollectionViewHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper) { ["ScrollTo"] = MapScrollTo };

	public CollectionViewHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public CollectionViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaCollectionView CreatePlatformView()
	{
		return new SkiaCollectionView();
	}

	protected override void ConnectHandler(SkiaCollectionView platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectionChanged += OnSelectionChanged;
		platformView.Scrolled += OnScrolled;
		platformView.ItemTapped += OnItemTapped;
	}

	protected override void DisconnectHandler(SkiaCollectionView platformView)
	{
		platformView.SelectionChanged -= OnSelectionChanged;
		platformView.Scrolled -= OnScrolled;
		platformView.ItemTapped -= OnItemTapped;
		base.DisconnectHandler(platformView);
	}

	private void OnSelectionChanged(object? sender, CollectionSelectionChangedEventArgs e)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Invalid comparison between Unknown and I4
		if (base.VirtualView == null || _isUpdatingSelection)
		{
			return;
		}
		try
		{
			_isUpdatingSelection = true;
			if ((int)((SelectableItemsView)base.VirtualView).SelectionMode == 1)
			{
				object obj = e.CurrentSelection.FirstOrDefault();
				if (!object.Equals(((SelectableItemsView)base.VirtualView).SelectedItem, obj))
				{
					((SelectableItemsView)base.VirtualView).SelectedItem = obj;
				}
			}
			else
			{
				if ((int)((SelectableItemsView)base.VirtualView).SelectionMode != 2)
				{
					return;
				}
				((SelectableItemsView)base.VirtualView).SelectedItems.Clear();
				{
					foreach (object item in e.CurrentSelection)
					{
						((SelectableItemsView)base.VirtualView).SelectedItems.Add(item);
					}
					return;
				}
			}
		}
		finally
		{
			_isUpdatingSelection = false;
		}
	}

	private void OnScrolled(object? sender, ItemsScrolledEventArgs e)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		CollectionView virtualView = base.VirtualView;
		if (virtualView != null)
		{
			((ItemsView)virtualView).SendScrolled(new ItemsViewScrolledEventArgs
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
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Invalid comparison between Unknown and I4
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Invalid comparison between Unknown and I4
		if (base.VirtualView == null || _isUpdatingSelection)
		{
			return;
		}
		try
		{
			_isUpdatingSelection = true;
			Console.WriteLine($"[CollectionViewHandler] OnItemTapped index={e.Index}, item={e.Item}, SelectionMode={((SelectableItemsView)base.VirtualView).SelectionMode}");
			SkiaView skiaView = base.PlatformView?.GetItemView(e.Index);
			Console.WriteLine($"[CollectionViewHandler] GetItemView({e.Index}) returned: {((object)skiaView)?.GetType().Name ?? "null"}, MauiView={((object)skiaView?.MauiView)?.GetType().Name ?? "null"}");
			if (skiaView?.MauiView != null)
			{
				Console.WriteLine($"[CollectionViewHandler] Found MauiView: {((object)skiaView.MauiView).GetType().Name}, GestureRecognizers={skiaView.MauiView.GestureRecognizers?.Count ?? 0}");
				if (GestureManager.ProcessTap(skiaView.MauiView, 0.0, 0.0))
				{
					Console.WriteLine("[CollectionViewHandler] Gesture processed successfully");
					return;
				}
			}
			if ((int)((SelectableItemsView)base.VirtualView).SelectionMode == 1)
			{
				((SelectableItemsView)base.VirtualView).SelectedItem = e.Item;
			}
			else if ((int)((SelectableItemsView)base.VirtualView).SelectionMode == 2)
			{
				if (((SelectableItemsView)base.VirtualView).SelectedItems.Contains(e.Item))
				{
					((SelectableItemsView)base.VirtualView).SelectedItems.Remove(e.Item);
				}
				else
				{
					((SelectableItemsView)base.VirtualView).SelectedItems.Add(e.Item);
				}
			}
		}
		finally
		{
			_isUpdatingSelection = false;
		}
	}

	public static void MapItemsSource(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.ItemsSource = ((ItemsView)collectionView).ItemsSource;
		}
	}

	public static void MapItemTemplate(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView == null || ((ElementHandler)handler).MauiContext == null)
		{
			return;
		}
		DataTemplate template = ((ItemsView)collectionView).ItemTemplate;
		if (template != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.ItemViewCreator = delegate(object item)
			{
				try
				{
					object obj = ((ElementTemplate)template).CreateContent();
					View val = (View)((obj is View) ? obj : null);
					if (val != null)
					{
						((BindableObject)val).BindingContext = item;
						PropagateBindingContext(val, item);
						if (((VisualElement)val).Handler == null && ((ElementHandler)handler).MauiContext != null)
						{
							((VisualElement)val).Handler = ((IView)(object)val).ToViewHandler(((ElementHandler)handler).MauiContext);
						}
						IViewHandler handler2 = ((VisualElement)val).Handler;
						if (((handler2 != null) ? ((IElementHandler)handler2).PlatformView : null) is SkiaView skiaView)
						{
							skiaView.MauiView = val;
							Console.WriteLine($"[CollectionViewHandler.ItemViewCreator] Set MauiView={((object)val).GetType().Name} on {((object)skiaView).GetType().Name}, GestureRecognizers={val.GestureRecognizers?.Count ?? 0}");
							return skiaView;
						}
					}
					else
					{
						ViewCell val2 = (ViewCell)((obj is ViewCell) ? obj : null);
						if (val2 != null)
						{
							((BindableObject)val2).BindingContext = item;
							View view = val2.View;
							if (view != null)
							{
								if (((VisualElement)view).Handler == null && ((ElementHandler)handler).MauiContext != null)
								{
									((VisualElement)view).Handler = ((IView)(object)view).ToViewHandler(((ElementHandler)handler).MauiContext);
								}
								IViewHandler handler3 = ((VisualElement)view).Handler;
								if (((handler3 != null) ? ((IElementHandler)handler3).PlatformView : null) is SkiaView result)
								{
									return result;
								}
							}
						}
					}
				}
				catch
				{
				}
				return (SkiaView?)null;
			};
		}
		((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.Invalidate();
	}

	public static void MapEmptyView(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.EmptyView = ((ItemsView)collectionView).EmptyView;
			if (((ItemsView)collectionView).EmptyView is string emptyViewText)
			{
				((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.EmptyViewText = emptyViewText;
			}
		}
	}

	public static void MapHorizontalScrollBarVisibility(CollectionViewHandler handler, CollectionView collectionView)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected I4, but got Unknown
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.HorizontalScrollBarVisibility = (ScrollBarVisibility)((ItemsView)collectionView).HorizontalScrollBarVisibility;
		}
	}

	public static void MapVerticalScrollBarVisibility(CollectionViewHandler handler, CollectionView collectionView)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected I4, but got Unknown
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.VerticalScrollBarVisibility = (ScrollBarVisibility)((ItemsView)collectionView).VerticalScrollBarVisibility;
		}
	}

	public static void MapSelectedItem(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView == null || handler._isUpdatingSelection)
		{
			return;
		}
		try
		{
			handler._isUpdatingSelection = true;
			if (!object.Equals(((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.SelectedItem, ((SelectableItemsView)collectionView).SelectedItem))
			{
				((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.SelectedItem = ((SelectableItemsView)collectionView).SelectedItem;
			}
		}
		finally
		{
			handler._isUpdatingSelection = false;
		}
	}

	public static void MapSelectedItems(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView == null || handler._isUpdatingSelection)
		{
			return;
		}
		try
		{
			handler._isUpdatingSelection = true;
			IList<object> selectedItems = ((SelectableItemsView)collectionView).SelectedItems;
			if (selectedItems != null && selectedItems.Count > 0)
			{
				((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.SelectedItem = selectedItems.First();
			}
		}
		finally
		{
			handler._isUpdatingSelection = false;
		}
	}

	public static void MapSelectionMode(CollectionViewHandler handler, CollectionView collectionView)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			SkiaCollectionView platformView = ((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView;
			SelectionMode selectionMode = ((SelectableItemsView)collectionView).SelectionMode;
			platformView.SelectionMode = (int)selectionMode switch
			{
				0 => SkiaSelectionMode.None, 
				1 => SkiaSelectionMode.Single, 
				2 => SkiaSelectionMode.Multiple, 
				_ => SkiaSelectionMode.None, 
			};
		}
	}

	public static void MapHeader(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.Header = ((StructuredItemsView)collectionView).Header;
		}
	}

	public static void MapFooter(CollectionViewHandler handler, CollectionView collectionView)
	{
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.Footer = ((StructuredItemsView)collectionView).Footer;
		}
	}

	public static void MapItemsLayout(CollectionViewHandler handler, CollectionView collectionView)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView == null)
		{
			return;
		}
		IItemsLayout itemsLayout = ((StructuredItemsView)collectionView).ItemsLayout;
		LinearItemsLayout val = (LinearItemsLayout)(object)((itemsLayout is LinearItemsLayout) ? itemsLayout : null);
		if (val != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.Orientation = (((int)((ItemsLayout)val).Orientation != 0) ? ItemsLayoutOrientation.Horizontal : ItemsLayoutOrientation.Vertical);
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.SpanCount = 1;
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.ItemSpacing = (float)val.ItemSpacing;
			return;
		}
		GridItemsLayout val2 = (GridItemsLayout)(object)((itemsLayout is GridItemsLayout) ? itemsLayout : null);
		if (val2 != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.Orientation = (((int)((ItemsLayout)val2).Orientation != 0) ? ItemsLayoutOrientation.Horizontal : ItemsLayoutOrientation.Vertical);
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.SpanCount = val2.Span;
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.ItemSpacing = (float)val2.VerticalItemSpacing;
		}
	}

	public static void MapBackground(CollectionViewHandler handler, CollectionView collectionView)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null && ((VisualElement)collectionView).BackgroundColor == null)
		{
			Brush background = ((VisualElement)collectionView).Background;
			SolidColorBrush val = (SolidColorBrush)(object)((background is SolidColorBrush) ? background : null);
			if (val != null)
			{
				((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackgroundColor(CollectionViewHandler handler, CollectionView collectionView)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView != null && ((VisualElement)collectionView).BackgroundColor != null)
		{
			((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.BackgroundColor = ((VisualElement)collectionView).BackgroundColor.ToSKColor();
		}
	}

	public static void MapScrollTo(CollectionViewHandler handler, CollectionView collectionView, object? args)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		if (((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView == null)
		{
			return;
		}
		ScrollToRequestEventArgs e = (ScrollToRequestEventArgs)((args is ScrollToRequestEventArgs) ? args : null);
		if (e != null)
		{
			if ((int)e.Mode == 1)
			{
				((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.ScrollToIndex(e.Index, e.IsAnimated);
			}
			else if (e.Item != null)
			{
				((ViewHandler<CollectionView, SkiaCollectionView>)(object)handler).PlatformView.ScrollToItem(e.Item, e.IsAnimated);
			}
		}
	}

	private static void PropagateBindingContext(View view, object? bindingContext)
	{
		((BindableObject)view).BindingContext = bindingContext;
		Layout val = (Layout)(object)((view is Layout) ? view : null);
		if (val != null)
		{
			foreach (IView child in val.Children)
			{
				View val2 = (View)(object)((child is View) ? child : null);
				if (val2 != null)
				{
					PropagateBindingContext(val2, bindingContext);
				}
			}
			return;
		}
		ContentView val3 = (ContentView)(object)((view is ContentView) ? view : null);
		if (val3 != null && val3.Content != null)
		{
			PropagateBindingContext(val3.Content, bindingContext);
			return;
		}
		Border val4 = (Border)(object)((view is Border) ? view : null);
		if (val4 != null)
		{
			View content = val4.Content;
			if (content != null)
			{
				PropagateBindingContext(content, bindingContext);
			}
		}
	}
}
