using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class PickerHandler : ViewHandler<IPicker, SkiaPicker>
{
	public static IPropertyMapper<IPicker, PickerHandler> Mapper = (IPropertyMapper<IPicker, PickerHandler>)(object)new PropertyMapper<IPicker, PickerHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Title"] = MapTitle,
		["TitleColor"] = MapTitleColor,
		["SelectedIndex"] = MapSelectedIndex,
		["TextColor"] = MapTextColor,
		["Font"] = MapFont,
		["CharacterSpacing"] = MapCharacterSpacing,
		["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
		["VerticalTextAlignment"] = MapVerticalTextAlignment,
		["Background"] = MapBackground,
		["ItemsSource"] = MapItemsSource
	};

	public static CommandMapper<IPicker, PickerHandler> CommandMapper = new CommandMapper<IPicker, PickerHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	private INotifyCollectionChanged? _itemsCollection;

	public PickerHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public PickerHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaPicker CreatePlatformView()
	{
		return new SkiaPicker();
	}

	protected override void ConnectHandler(SkiaPicker platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectedIndexChanged += OnSelectedIndexChanged;
		IPicker virtualView = base.VirtualView;
		Picker val = (Picker)(object)((virtualView is Picker) ? virtualView : null);
		if (val != null && val.Items is INotifyCollectionChanged itemsCollection)
		{
			_itemsCollection = itemsCollection;
			_itemsCollection.CollectionChanged += OnItemsCollectionChanged;
		}
		ReloadItems();
	}

	protected override void DisconnectHandler(SkiaPicker platformView)
	{
		platformView.SelectedIndexChanged -= OnSelectedIndexChanged;
		if (_itemsCollection != null)
		{
			_itemsCollection.CollectionChanged -= OnItemsCollectionChanged;
			_itemsCollection = null;
		}
		base.DisconnectHandler(platformView);
	}

	private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		ReloadItems();
	}

	private void OnSelectedIndexChanged(object? sender, EventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null)
		{
			base.VirtualView.SelectedIndex = base.PlatformView.SelectedIndex;
		}
	}

	private void ReloadItems()
	{
		if (base.PlatformView != null && base.VirtualView != null)
		{
			string[] itemsAsArray = IPickerExtension.GetItemsAsArray(base.VirtualView);
			base.PlatformView.SetItems(itemsAsArray.Select((string i) => i?.ToString() ?? ""));
		}
	}

	public static void MapTitle(PickerHandler handler, IPicker picker)
	{
		if (((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.Title = picker.Title ?? "";
		}
	}

	public static void MapTitleColor(PickerHandler handler, IPicker picker)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView != null && picker.TitleColor != null)
		{
			((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.TitleColor = picker.TitleColor.ToSKColor();
		}
	}

	public static void MapSelectedIndex(PickerHandler handler, IPicker picker)
	{
		if (((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.SelectedIndex = picker.SelectedIndex;
		}
	}

	public static void MapTextColor(PickerHandler handler, IPicker picker)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView != null && ((ITextStyle)picker).TextColor != null)
		{
			((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.TextColor = ((ITextStyle)picker).TextColor.ToSKColor();
		}
	}

	public static void MapFont(PickerHandler handler, IPicker picker)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView != null)
		{
			Font font = ((ITextStyle)picker).Font;
			if (!string.IsNullOrEmpty(((Font)(ref font)).Family))
			{
				((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.FontFamily = ((Font)(ref font)).Family;
			}
			if (((Font)(ref font)).Size > 0.0)
			{
				((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.FontSize = (float)((Font)(ref font)).Size;
			}
			((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.Invalidate();
		}
	}

	public static void MapCharacterSpacing(PickerHandler handler, IPicker picker)
	{
	}

	public static void MapHorizontalTextAlignment(PickerHandler handler, IPicker picker)
	{
	}

	public static void MapVerticalTextAlignment(PickerHandler handler, IPicker picker)
	{
	}

	public static void MapBackground(PickerHandler handler, IPicker picker)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)picker).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IPicker, SkiaPicker>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapItemsSource(PickerHandler handler, IPicker picker)
	{
		handler.ReloadItems();
	}
}
