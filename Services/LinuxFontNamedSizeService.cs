// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Named font sizes (<c>FontSize="Micro"</c> ... <c>"Header"</c>) for XAML and
/// <c>Device.GetNamedSize</c>. MAUI's FontSizeConverter resolves them through
/// this dependency service and throws without it, which took a whole page
/// down. Values follow the desktop (Windows) scale so apps sized for desktop
/// look the same on Linux.
/// </summary>
public sealed class LinuxFontNamedSizeService : IFontNamedSizeService
{
    public double GetNamedSize(NamedSize size, Type targetElementType, bool useOldSizes)
    {
        return size switch
        {
            NamedSize.Default => 14,
            NamedSize.Micro => 10,
            NamedSize.Small => 12,
            NamedSize.Medium => 14,
            NamedSize.Large => 18,
            NamedSize.Body => 14,
            NamedSize.Caption => 12,
            NamedSize.Subtitle => 20,
            NamedSize.Title => 24,
            NamedSize.Header => 46,
            _ => 14,
        };
    }
}
