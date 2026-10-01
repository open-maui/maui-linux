// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// An item's content from an items view's ItemTemplate, as MAUI's platforms make it: a
/// <see cref="DataTemplateSelector"/> chooses the template for each item first. Calling
/// CreateContent on the selector itself gives an empty Label for every item.
/// </summary>
internal static class ItemTemplateContent
{
    public static object? Create(DataTemplate? template, object? item, BindableObject container)
    {
        if (template is DataTemplateSelector selector)
            template = item == null ? null : selector.SelectTemplate(item, container);
        return template?.CreateContent();
    }
}
