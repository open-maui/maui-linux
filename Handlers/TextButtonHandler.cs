using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Platform.Linux.Handlers;

public class TextButtonHandler : ButtonHandler
{
	public new static IPropertyMapper<ITextButton, TextButtonHandler> Mapper = (IPropertyMapper<ITextButton, TextButtonHandler>)(object)new PropertyMapper<ITextButton, TextButtonHandler>((IPropertyMapper[])(object)new IPropertyMapper[1] { (IPropertyMapper)ButtonHandler.Mapper })
	{
		["Text"] = MapText,
		["TextColor"] = MapTextColor,
		["Font"] = MapFont,
		["CharacterSpacing"] = MapCharacterSpacing
	};

	public TextButtonHandler()
		: base((IPropertyMapper?)(object)Mapper)
	{
	}

	protected override void ConnectHandler(SkiaButton platformView)
	{
		base.ConnectHandler(platformView);
		IButton virtualView = ((ViewHandler<IButton, SkiaButton>)(object)this).VirtualView;
		ITextButton val = (ITextButton)(object)((virtualView is ITextButton) ? virtualView : null);
		if (val != null)
		{
			MapText(this, val);
			MapTextColor(this, val);
			MapFont(this, val);
			MapCharacterSpacing(this, val);
		}
	}

	public static void MapText(TextButtonHandler handler, ITextButton button)
	{
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.Text = ((IText)button).Text ?? string.Empty;
		}
	}

	public static void MapTextColor(TextButtonHandler handler, ITextButton button)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null && ((ITextStyle)button).TextColor != null)
		{
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.TextColor = ((ITextStyle)button).TextColor.ToSKColor();
		}
	}

	public static void MapFont(TextButtonHandler handler, ITextButton button)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Invalid comparison between Unknown and I4
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Invalid comparison between Unknown and I4
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Invalid comparison between Unknown and I4
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			Font font = ((ITextStyle)button).Font;
			if (((Font)(ref font)).Size > 0.0)
			{
				((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.FontSize = (float)((Font)(ref font)).Size;
			}
			if (!string.IsNullOrEmpty(((Font)(ref font)).Family))
			{
				((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.FontFamily = ((Font)(ref font)).Family;
			}
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.IsBold = (int)((Font)(ref font)).Weight >= 700;
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.IsItalic = (int)((Font)(ref font)).Slant == 1 || (int)((Font)(ref font)).Slant == 2;
		}
	}

	public static void MapCharacterSpacing(TextButtonHandler handler, ITextButton button)
	{
		if (((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView != null)
		{
			((ViewHandler<IButton, SkiaButton>)(object)handler).PlatformView.CharacterSpacing = (float)((ITextStyle)button).CharacterSpacing;
		}
	}
}
