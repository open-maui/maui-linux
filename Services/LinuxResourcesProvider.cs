// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;

[assembly: Dependency(typeof(Microsoft.Maui.Platform.Linux.Services.LinuxResourcesProvider))]

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Provides system resources for the Linux platform.
/// </summary>
internal sealed class LinuxResourcesProvider : ISystemResourcesProvider
{
    private ResourceDictionary? _dictionary;

    public IResourceDictionary GetSystemResources()
    {
        _dictionary ??= CreateResourceDictionary();
        return _dictionary;
    }

    private ResourceDictionary CreateResourceDictionary()
    {
        var dictionary = new ResourceDictionary();

        // Add default styles
        dictionary[Device.Styles.BodyStyleKey] = new Style(typeof(Label));
        dictionary[Device.Styles.TitleStyleKey] = CreateTitleStyle();
        dictionary[Device.Styles.SubtitleStyleKey] = CreateSubtitleStyle();
        dictionary[Device.Styles.CaptionStyleKey] = CreateCaptionStyle();
        dictionary[Device.Styles.ListItemTextStyleKey] = new Style(typeof(Label));
        dictionary[Device.Styles.ListItemDetailTextStyleKey] = CreateCaptionStyle();

        return dictionary;
    }

    private static Style CreateTitleStyle() => new(typeof(Label))
    {
        Setters = { new Setter { Property = Label.FontSizeProperty, Value = 24.0 } }
    };

    private static Style CreateSubtitleStyle() => new(typeof(Label))
    {
        Setters = { new Setter { Property = Label.FontSizeProperty, Value = 18.0 } }
    };

    private static Style CreateCaptionStyle() => new(typeof(Label))
    {
        Setters = { new Setter { Property = Label.FontSizeProperty, Value = 12.0 } }
    };
}
