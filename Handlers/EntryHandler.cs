using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class EntryHandler : ViewHandler<IEntry, SkiaEntry>
{
	public static IPropertyMapper<IEntry, EntryHandler> Mapper = (IPropertyMapper<IEntry, EntryHandler>)(object)new PropertyMapper<IEntry, EntryHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Text"] = MapText,
		["TextColor"] = MapTextColor,
		["Font"] = MapFont,
		["CharacterSpacing"] = MapCharacterSpacing,
		["Placeholder"] = MapPlaceholder,
		["PlaceholderColor"] = MapPlaceholderColor,
		["IsReadOnly"] = MapIsReadOnly,
		["MaxLength"] = MapMaxLength,
		["CursorPosition"] = MapCursorPosition,
		["SelectionLength"] = MapSelectionLength,
		["IsPassword"] = MapIsPassword,
		["ReturnType"] = MapReturnType,
		["ClearButtonVisibility"] = MapClearButtonVisibility,
		["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
		["VerticalTextAlignment"] = MapVerticalTextAlignment,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public static CommandMapper<IEntry, EntryHandler> CommandMapper = new CommandMapper<IEntry, EntryHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public EntryHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public EntryHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaEntry CreatePlatformView()
	{
		return new SkiaEntry();
	}

	protected override void ConnectHandler(SkiaEntry platformView)
	{
		base.ConnectHandler(platformView);
		platformView.TextChanged += OnTextChanged;
		platformView.Completed += OnCompleted;
	}

	protected override void DisconnectHandler(SkiaEntry platformView)
	{
		platformView.TextChanged -= OnTextChanged;
		platformView.Completed -= OnCompleted;
		base.DisconnectHandler(platformView);
	}

	private void OnTextChanged(object? sender, TextChangedEventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null && ((ITextInput)base.VirtualView).Text != e.NewTextValue)
		{
			((ITextInput)base.VirtualView).Text = e.NewTextValue ?? string.Empty;
		}
	}

	private void OnCompleted(object? sender, EventArgs e)
	{
		IEntry virtualView = base.VirtualView;
		if (virtualView != null)
		{
			virtualView.Completed();
		}
	}

	public static void MapText(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null && ((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.Text != ((ITextInput)entry).Text)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.Text = ((ITextInput)entry).Text ?? string.Empty;
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.Invalidate();
		}
	}

	public static void MapTextColor(EntryHandler handler, IEntry entry)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null && ((ITextStyle)entry).TextColor != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.TextColor = ((ITextStyle)entry).TextColor.ToSKColor();
		}
	}

	public static void MapFont(EntryHandler handler, IEntry entry)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Invalid comparison between Unknown and I4
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Invalid comparison between Unknown and I4
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Invalid comparison between Unknown and I4
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			Font font = ((ITextStyle)entry).Font;
			if (((Font)(ref font)).Size > 0.0)
			{
				((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.FontSize = (float)((Font)(ref font)).Size;
			}
			if (!string.IsNullOrEmpty(((Font)(ref font)).Family))
			{
				((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.FontFamily = ((Font)(ref font)).Family;
			}
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.IsBold = (int)((Font)(ref font)).Weight >= 700;
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.IsItalic = (int)((Font)(ref font)).Slant == 1 || (int)((Font)(ref font)).Slant == 2;
		}
	}

	public static void MapCharacterSpacing(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.CharacterSpacing = (float)((ITextStyle)entry).CharacterSpacing;
		}
	}

	public static void MapPlaceholder(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.Placeholder = ((IPlaceholder)entry).Placeholder ?? string.Empty;
		}
	}

	public static void MapPlaceholderColor(EntryHandler handler, IEntry entry)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null && ((IPlaceholder)entry).PlaceholderColor != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.PlaceholderColor = ((IPlaceholder)entry).PlaceholderColor.ToSKColor();
		}
	}

	public static void MapIsReadOnly(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.IsReadOnly = ((ITextInput)entry).IsReadOnly;
		}
	}

	public static void MapMaxLength(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.MaxLength = ((ITextInput)entry).MaxLength;
		}
	}

	public static void MapCursorPosition(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.CursorPosition = ((ITextInput)entry).CursorPosition;
		}
	}

	public static void MapSelectionLength(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.SelectionLength = ((ITextInput)entry).SelectionLength;
		}
	}

	public static void MapIsPassword(EntryHandler handler, IEntry entry)
	{
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.IsPassword = entry.IsPassword;
		}
	}

	public static void MapReturnType(EntryHandler handler, IEntry entry)
	{
		_ = ((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView;
	}

	public static void MapClearButtonVisibility(EntryHandler handler, IEntry entry)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.ShowClearButton = (int)entry.ClearButtonVisibility == 1;
		}
	}

	public static void MapHorizontalTextAlignment(EntryHandler handler, IEntry entry)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			SkiaEntry platformView = ((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView;
			TextAlignment horizontalTextAlignment = ((ITextAlignment)entry).HorizontalTextAlignment;
			platformView.HorizontalTextAlignment = (int)horizontalTextAlignment switch
			{
				0 => TextAlignment.Start, 
				1 => TextAlignment.Center, 
				2 => TextAlignment.End, 
				_ => TextAlignment.Start, 
			};
		}
	}

	public static void MapVerticalTextAlignment(EntryHandler handler, IEntry entry)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			SkiaEntry platformView = ((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView;
			TextAlignment verticalTextAlignment = ((ITextAlignment)entry).VerticalTextAlignment;
			platformView.VerticalTextAlignment = (int)verticalTextAlignment switch
			{
				0 => TextAlignment.Start, 
				1 => TextAlignment.Center, 
				2 => TextAlignment.End, 
				_ => TextAlignment.Center, 
			};
		}
	}

	public static void MapBackground(EntryHandler handler, IEntry entry)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)entry).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackgroundColor(EntryHandler handler, IEntry entry)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView == null)
		{
			return;
		}
		Entry val = (Entry)(object)((entry is Entry) ? entry : null);
		if (val != null)
		{
			Console.WriteLine($"[EntryHandler] MapBackgroundColor: {((VisualElement)val).BackgroundColor}");
			if (((VisualElement)val).BackgroundColor != null)
			{
				SKColor val2 = ((VisualElement)val).BackgroundColor.ToSKColor();
				Console.WriteLine($"[EntryHandler] Setting EntryBackgroundColor to: {val2}");
				((ViewHandler<IEntry, SkiaEntry>)(object)handler).PlatformView.EntryBackgroundColor = val2;
			}
		}
	}
}
