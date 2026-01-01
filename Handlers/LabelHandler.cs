using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform.Linux.Window;
using Microsoft.Maui.Primitives;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class LabelHandler : ViewHandler<ILabel, SkiaLabel>
{
	public static IPropertyMapper<ILabel, LabelHandler> Mapper = (IPropertyMapper<ILabel, LabelHandler>)(object)new PropertyMapper<ILabel, LabelHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Text"] = MapText,
		["TextColor"] = MapTextColor,
		["Font"] = MapFont,
		["CharacterSpacing"] = MapCharacterSpacing,
		["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
		["VerticalTextAlignment"] = MapVerticalTextAlignment,
		["TextDecorations"] = MapTextDecorations,
		["LineHeight"] = MapLineHeight,
		["LineBreakMode"] = MapLineBreakMode,
		["MaxLines"] = MapMaxLines,
		["Padding"] = MapPadding,
		["Background"] = MapBackground,
		["VerticalLayoutAlignment"] = MapVerticalLayoutAlignment,
		["HorizontalLayoutAlignment"] = MapHorizontalLayoutAlignment,
		["FormattedText"] = MapFormattedText
	};

	public static CommandMapper<ILabel, LabelHandler> CommandMapper = new CommandMapper<ILabel, LabelHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public LabelHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public LabelHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaLabel CreatePlatformView()
	{
		return new SkiaLabel();
	}

	protected override void ConnectHandler(SkiaLabel platformView)
	{
		base.ConnectHandler(platformView);
		ILabel virtualView = base.VirtualView;
		View val = (View)(object)((virtualView is View) ? virtualView : null);
		if (val != null)
		{
			platformView.MauiView = val;
			if (val.GestureRecognizers.OfType<TapGestureRecognizer>().Any())
			{
				platformView.CursorType = CursorType.Hand;
			}
		}
		platformView.Tapped += OnPlatformViewTapped;
	}

	protected override void DisconnectHandler(SkiaLabel platformView)
	{
		platformView.Tapped -= OnPlatformViewTapped;
		platformView.MauiView = null;
		base.DisconnectHandler(platformView);
	}

	private void OnPlatformViewTapped(object? sender, EventArgs e)
	{
		ILabel virtualView = base.VirtualView;
		View val = (View)(object)((virtualView is View) ? virtualView : null);
		if (val != null)
		{
			GestureManager.ProcessTap(val, 0.0, 0.0);
		}
	}

	public static void MapText(LabelHandler handler, ILabel label)
	{
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.Text = ((IText)label).Text ?? string.Empty;
		}
	}

	public static void MapTextColor(LabelHandler handler, ILabel label)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null && ((ITextStyle)label).TextColor != null)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.TextColor = ((ITextStyle)label).TextColor.ToSKColor();
		}
	}

	public static void MapFont(LabelHandler handler, ILabel label)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Invalid comparison between Unknown and I4
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Invalid comparison between Unknown and I4
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Invalid comparison between Unknown and I4
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			Font font = ((ITextStyle)label).Font;
			if (((Font)(ref font)).Size > 0.0)
			{
				((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.FontSize = (float)((Font)(ref font)).Size;
			}
			if (!string.IsNullOrEmpty(((Font)(ref font)).Family))
			{
				((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.FontFamily = ((Font)(ref font)).Family;
			}
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.IsBold = (int)((Font)(ref font)).Weight >= 700;
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.IsItalic = (int)((Font)(ref font)).Slant == 1 || (int)((Font)(ref font)).Slant == 2;
		}
	}

	public static void MapCharacterSpacing(LabelHandler handler, ILabel label)
	{
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.CharacterSpacing = (float)((ITextStyle)label).CharacterSpacing;
		}
	}

	public static void MapHorizontalTextAlignment(LabelHandler handler, ILabel label)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			SkiaLabel platformView = ((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView;
			TextAlignment horizontalTextAlignment = ((ITextAlignment)label).HorizontalTextAlignment;
			platformView.HorizontalTextAlignment = (int)horizontalTextAlignment switch
			{
				0 => TextAlignment.Start, 
				1 => TextAlignment.Center, 
				2 => TextAlignment.End, 
				_ => TextAlignment.Start, 
			};
		}
	}

	public static void MapVerticalTextAlignment(LabelHandler handler, ILabel label)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected I4, but got Unknown
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			SkiaLabel platformView = ((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView;
			TextAlignment verticalTextAlignment = ((ITextAlignment)label).VerticalTextAlignment;
			platformView.VerticalTextAlignment = (int)verticalTextAlignment switch
			{
				0 => TextAlignment.Start, 
				1 => TextAlignment.Center, 
				2 => TextAlignment.End, 
				_ => TextAlignment.Center, 
			};
		}
	}

	public static void MapTextDecorations(LabelHandler handler, ILabel label)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Invalid comparison between Unknown and I4
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.IsUnderline = (label.TextDecorations & 1) > 0;
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.IsStrikethrough = (label.TextDecorations & 2) > 0;
		}
	}

	public static void MapLineHeight(LabelHandler handler, ILabel label)
	{
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.LineHeight = (float)label.LineHeight;
		}
	}

	public static void MapLineBreakMode(LabelHandler handler, ILabel label)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected I4, but got Unknown
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			Label val = (Label)(object)((label is Label) ? label : null);
			if (val != null)
			{
				SkiaLabel platformView = ((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView;
				LineBreakMode lineBreakMode = val.LineBreakMode;
				platformView.LineBreakMode = (int)lineBreakMode switch
				{
					0 => LineBreakMode.NoWrap, 
					1 => LineBreakMode.WordWrap, 
					2 => LineBreakMode.CharacterWrap, 
					3 => LineBreakMode.HeadTruncation, 
					4 => LineBreakMode.TailTruncation, 
					5 => LineBreakMode.MiddleTruncation, 
					_ => LineBreakMode.TailTruncation, 
				};
			}
		}
	}

	public static void MapMaxLines(LabelHandler handler, ILabel label)
	{
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			Label val = (Label)(object)((label is Label) ? label : null);
			if (val != null)
			{
				((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.MaxLines = val.MaxLines;
			}
		}
	}

	public static void MapPadding(LabelHandler handler, ILabel label)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			Thickness padding = ((IPadding)label).Padding;
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.Padding = new SKRect((float)((Thickness)(ref padding)).Left, (float)((Thickness)(ref padding)).Top, (float)((Thickness)(ref padding)).Right, (float)((Thickness)(ref padding)).Bottom);
		}
	}

	public static void MapBackground(LabelHandler handler, ILabel label)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)label).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapVerticalLayoutAlignment(LabelHandler handler, ILabel label)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected I4, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			SkiaLabel platformView = ((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView;
			LayoutAlignment verticalLayoutAlignment = ((IView)label).VerticalLayoutAlignment;
			platformView.VerticalOptions = (LayoutOptions)((int)verticalLayoutAlignment switch
			{
				1 => LayoutOptions.Start, 
				2 => LayoutOptions.Center, 
				3 => LayoutOptions.End, 
				0 => LayoutOptions.Fill, 
				_ => LayoutOptions.Start, 
			});
		}
	}

	public static void MapHorizontalLayoutAlignment(LabelHandler handler, ILabel label)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected I4, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView != null)
		{
			SkiaLabel platformView = ((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView;
			LayoutAlignment horizontalLayoutAlignment = ((IView)label).HorizontalLayoutAlignment;
			platformView.HorizontalOptions = (LayoutOptions)((int)horizontalLayoutAlignment switch
			{
				1 => LayoutOptions.Start, 
				2 => LayoutOptions.Center, 
				3 => LayoutOptions.End, 
				0 => LayoutOptions.Fill, 
				_ => LayoutOptions.Start, 
			});
		}
	}

	public static void MapFormattedText(LabelHandler handler, ILabel label)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Invalid comparison between Unknown and I4
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Invalid comparison between Unknown and I4
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView == null)
		{
			return;
		}
		Label val = (Label)(object)((label is Label) ? label : null);
		if (val == null)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.FormattedSpans = null;
			return;
		}
		FormattedString formattedText = val.FormattedText;
		if (formattedText == null || formattedText.Spans.Count == 0)
		{
			((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.FormattedSpans = null;
			return;
		}
		List<SkiaTextSpan> list = new List<SkiaTextSpan>();
		foreach (Span span in formattedText.Spans)
		{
			SkiaTextSpan skiaTextSpan = new SkiaTextSpan
			{
				Text = (span.Text ?? ""),
				IsBold = ((Enum)span.FontAttributes).HasFlag((Enum)(object)(FontAttributes)1),
				IsItalic = ((Enum)span.FontAttributes).HasFlag((Enum)(object)(FontAttributes)2),
				IsUnderline = ((span.TextDecorations & 1) > 0),
				IsStrikethrough = ((span.TextDecorations & 2) > 0),
				CharacterSpacing = (float)span.CharacterSpacing,
				LineHeight = (float)span.LineHeight
			};
			if (span.TextColor != null)
			{
				skiaTextSpan.TextColor = span.TextColor.ToSKColor();
			}
			if (span.BackgroundColor != null)
			{
				skiaTextSpan.BackgroundColor = span.BackgroundColor.ToSKColor();
			}
			if (!string.IsNullOrEmpty(span.FontFamily))
			{
				skiaTextSpan.FontFamily = span.FontFamily;
			}
			if (span.FontSize > 0.0)
			{
				skiaTextSpan.FontSize = (float)span.FontSize;
			}
			list.Add(skiaTextSpan);
		}
		((ViewHandler<ILabel, SkiaLabel>)(object)handler).PlatformView.FormattedSpans = list;
	}
}
