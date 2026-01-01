using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;

namespace Microsoft.Maui.Platform.Linux.Services;

internal sealed class LinuxResourcesProvider : ISystemResourcesProvider
{
	private ResourceDictionary? _dictionary;

	public IResourceDictionary GetSystemResources()
	{
		if (_dictionary == null)
		{
			_dictionary = CreateResourceDictionary();
		}
		return (IResourceDictionary)(object)_dictionary;
	}

	private ResourceDictionary CreateResourceDictionary()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		return new ResourceDictionary
		{
			[Styles.BodyStyleKey] = (object)new Style(typeof(Label)),
			[Styles.TitleStyleKey] = CreateTitleStyle(),
			[Styles.SubtitleStyleKey] = CreateSubtitleStyle(),
			[Styles.CaptionStyleKey] = CreateCaptionStyle(),
			[Styles.ListItemTextStyleKey] = (object)new Style(typeof(Label)),
			[Styles.ListItemDetailTextStyleKey] = CreateCaptionStyle()
		};
	}

	private static Style CreateTitleStyle()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_003f: Expected O, but got Unknown
		return new Style(typeof(Label))
		{
			Setters = 
			{
				new Setter
				{
					Property = Label.FontSizeProperty,
					Value = 24.0
				}
			}
		};
	}

	private static Style CreateSubtitleStyle()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_003f: Expected O, but got Unknown
		return new Style(typeof(Label))
		{
			Setters = 
			{
				new Setter
				{
					Property = Label.FontSizeProperty,
					Value = 18.0
				}
			}
		};
	}

	private static Style CreateCaptionStyle()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_003f: Expected O, but got Unknown
		return new Style(typeof(Label))
		{
			Setters = 
			{
				new Setter
				{
					Property = Label.FontSizeProperty,
					Value = 12.0
				}
			}
		};
	}
}
