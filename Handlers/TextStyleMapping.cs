// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform.Linux.Handlers;

/// <summary>
/// Font conversions shared by the handlers that map <see cref="ITextStyle.Font"/> onto a Skia
/// view's FontSize / FontFamily / FontAttributes trio.
/// </summary>
internal static class TextStyleMapping
{
    /// <summary>
    /// The FontAttributes a Skia view draws for a MAUI <see cref="Font"/>. Font carries weight
    /// and slant, not FontAttributes: anything from Bold up is bold, and oblique draws as italic
    /// because the Skia views only distinguish upright from italic.
    /// </summary>
    public static FontAttributes ToFontAttributes(Font font)
    {
        var attributes = FontAttributes.None;
        if (font.Weight >= FontWeight.Bold)
            attributes |= FontAttributes.Bold;
        if (font.Slant == FontSlant.Italic || font.Slant == FontSlant.Oblique)
            attributes |= FontAttributes.Italic;
        return attributes;
    }
}
