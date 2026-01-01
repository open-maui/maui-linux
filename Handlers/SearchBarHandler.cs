using System;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class SearchBarHandler : ViewHandler<ISearchBar, SkiaSearchBar>
{
	public static IPropertyMapper<ISearchBar, SearchBarHandler> Mapper = (IPropertyMapper<ISearchBar, SearchBarHandler>)(object)new PropertyMapper<ISearchBar, SearchBarHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Text"] = MapText,
		["TextColor"] = MapTextColor,
		["Font"] = MapFont,
		["Placeholder"] = MapPlaceholder,
		["PlaceholderColor"] = MapPlaceholderColor,
		["CancelButtonColor"] = MapCancelButtonColor,
		["Background"] = MapBackground
	};

	public static CommandMapper<ISearchBar, SearchBarHandler> CommandMapper = new CommandMapper<ISearchBar, SearchBarHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public SearchBarHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public SearchBarHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaSearchBar CreatePlatformView()
	{
		return new SkiaSearchBar();
	}

	protected override void ConnectHandler(SkiaSearchBar platformView)
	{
		base.ConnectHandler(platformView);
		platformView.TextChanged += OnTextChanged;
		platformView.SearchButtonPressed += OnSearchButtonPressed;
	}

	protected override void DisconnectHandler(SkiaSearchBar platformView)
	{
		platformView.TextChanged -= OnTextChanged;
		platformView.SearchButtonPressed -= OnSearchButtonPressed;
		base.DisconnectHandler(platformView);
	}

	private void OnTextChanged(object? sender, TextChangedEventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null && ((ITextInput)base.VirtualView).Text != e.NewTextValue)
		{
			((ITextInput)base.VirtualView).Text = e.NewTextValue ?? string.Empty;
		}
	}

	private void OnSearchButtonPressed(object? sender, EventArgs e)
	{
		ISearchBar virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.SearchButtonPressed();
		}
	}

	public static void MapText(SearchBarHandler handler, ISearchBar searchBar)
	{
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null && ((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.Text != ((ITextInput)searchBar).Text)
		{
			((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.Text = ((ITextInput)searchBar).Text ?? string.Empty;
		}
	}

	public static void MapTextColor(SearchBarHandler handler, ISearchBar searchBar)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null && ((ITextStyle)searchBar).TextColor != null)
		{
			((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.TextColor = ((ITextStyle)searchBar).TextColor.ToSKColor();
		}
	}

	public static void MapFont(SearchBarHandler handler, ISearchBar searchBar)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null)
		{
			Font font = ((ITextStyle)searchBar).Font;
			if (((Font)(ref font)).Size > 0.0)
			{
				((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.FontSize = (float)((Font)(ref font)).Size;
			}
			if (!string.IsNullOrEmpty(((Font)(ref font)).Family))
			{
				((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.FontFamily = ((Font)(ref font)).Family;
			}
		}
	}

	public static void MapPlaceholder(SearchBarHandler handler, ISearchBar searchBar)
	{
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.Placeholder = ((IPlaceholder)searchBar).Placeholder ?? string.Empty;
		}
	}

	public static void MapPlaceholderColor(SearchBarHandler handler, ISearchBar searchBar)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null && ((IPlaceholder)searchBar).PlaceholderColor != null)
		{
			((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.PlaceholderColor = ((IPlaceholder)searchBar).PlaceholderColor.ToSKColor();
		}
	}

	public static void MapCancelButtonColor(SearchBarHandler handler, ISearchBar searchBar)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null && searchBar.CancelButtonColor != null)
		{
			((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.ClearButtonColor = searchBar.CancelButtonColor.ToSKColor();
		}
	}

	public static void MapBackground(SearchBarHandler handler, ISearchBar searchBar)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)searchBar).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}
}
