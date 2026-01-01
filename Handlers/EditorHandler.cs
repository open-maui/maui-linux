using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class EditorHandler : ViewHandler<IEditor, SkiaEditor>
{
	public static IPropertyMapper<IEditor, EditorHandler> Mapper = (IPropertyMapper<IEditor, EditorHandler>)(object)new PropertyMapper<IEditor, EditorHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ViewHandler.ViewMapper })
	{
		["Text"] = MapText,
		["Placeholder"] = MapPlaceholder,
		["PlaceholderColor"] = MapPlaceholderColor,
		["TextColor"] = MapTextColor,
		["CharacterSpacing"] = MapCharacterSpacing,
		["IsReadOnly"] = MapIsReadOnly,
		["IsTextPredictionEnabled"] = MapIsTextPredictionEnabled,
		["MaxLength"] = MapMaxLength,
		["CursorPosition"] = MapCursorPosition,
		["SelectionLength"] = MapSelectionLength,
		["Keyboard"] = MapKeyboard,
		["HorizontalTextAlignment"] = MapHorizontalTextAlignment,
		["VerticalTextAlignment"] = MapVerticalTextAlignment,
		["Background"] = MapBackground,
		["BackgroundColor"] = MapBackgroundColor
	};

	public static CommandMapper<IEditor, EditorHandler> CommandMapper = new CommandMapper<IEditor, EditorHandler>((CommandMapper)(object)ViewHandler.ViewCommandMapper);

	public EditorHandler()
		: base((IPropertyMapper)(object)Mapper, (CommandMapper)(object)CommandMapper)
	{
	}

	public EditorHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
		: base((IPropertyMapper)(((object)mapper) ?? ((object)Mapper)), (CommandMapper)(((object)commandMapper) ?? ((object)CommandMapper)))
	{
	}

	protected override SkiaEditor CreatePlatformView()
	{
		return new SkiaEditor();
	}

	protected override void ConnectHandler(SkiaEditor platformView)
	{
		base.ConnectHandler(platformView);
		platformView.TextChanged += OnTextChanged;
		platformView.Completed += OnCompleted;
	}

	protected override void DisconnectHandler(SkiaEditor platformView)
	{
		platformView.TextChanged -= OnTextChanged;
		platformView.Completed -= OnCompleted;
		base.DisconnectHandler(platformView);
	}

	private void OnTextChanged(object? sender, EventArgs e)
	{
		if (base.VirtualView != null && base.PlatformView != null)
		{
			((ITextInput)base.VirtualView).Text = base.PlatformView.Text;
		}
	}

	private void OnCompleted(object? sender, EventArgs e)
	{
	}

	public static void MapText(EditorHandler handler, IEditor editor)
	{
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.Text = ((ITextInput)editor).Text ?? "";
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.Invalidate();
		}
	}

	public static void MapPlaceholder(EditorHandler handler, IEditor editor)
	{
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.Placeholder = ((IPlaceholder)editor).Placeholder ?? "";
		}
	}

	public static void MapPlaceholderColor(EditorHandler handler, IEditor editor)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null && ((IPlaceholder)editor).PlaceholderColor != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.PlaceholderColor = ((IPlaceholder)editor).PlaceholderColor.ToSKColor();
		}
	}

	public static void MapTextColor(EditorHandler handler, IEditor editor)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null && ((ITextStyle)editor).TextColor != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.TextColor = ((ITextStyle)editor).TextColor.ToSKColor();
		}
	}

	public static void MapCharacterSpacing(EditorHandler handler, IEditor editor)
	{
	}

	public static void MapIsReadOnly(EditorHandler handler, IEditor editor)
	{
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.IsReadOnly = ((ITextInput)editor).IsReadOnly;
		}
	}

	public static void MapIsTextPredictionEnabled(EditorHandler handler, IEditor editor)
	{
	}

	public static void MapMaxLength(EditorHandler handler, IEditor editor)
	{
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.MaxLength = ((ITextInput)editor).MaxLength;
		}
	}

	public static void MapCursorPosition(EditorHandler handler, IEditor editor)
	{
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.CursorPosition = ((ITextInput)editor).CursorPosition;
		}
	}

	public static void MapSelectionLength(EditorHandler handler, IEditor editor)
	{
	}

	public static void MapKeyboard(EditorHandler handler, IEditor editor)
	{
	}

	public static void MapHorizontalTextAlignment(EditorHandler handler, IEditor editor)
	{
	}

	public static void MapVerticalTextAlignment(EditorHandler handler, IEditor editor)
	{
	}

	public static void MapBackground(EditorHandler handler, IEditor editor)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			Paint background = ((IView)editor).Background;
			SolidPaint val = (SolidPaint)(object)((background is SolidPaint) ? background : null);
			if (val != null && val.Color != null)
			{
				((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.BackgroundColor = val.Color.ToSKColor();
			}
		}
	}

	public static void MapBackgroundColor(EditorHandler handler, IEditor editor)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView != null)
		{
			VisualElement val = (VisualElement)(object)((editor is VisualElement) ? editor : null);
			if (val != null && val.BackgroundColor != null)
			{
				((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.BackgroundColor = val.BackgroundColor.ToSKColor();
				((ViewHandler<IEditor, SkiaEditor>)(object)handler).PlatformView.Invalidate();
			}
		}
	}
}
