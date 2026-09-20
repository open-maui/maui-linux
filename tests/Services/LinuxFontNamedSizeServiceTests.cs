// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>FontSize="Micro".."Header" in XAML depends on this service existing.</summary>
public class LinuxFontNamedSizeServiceTests
{
    [Theory]
    [InlineData(NamedSize.Micro, 10)]
    [InlineData(NamedSize.Small, 12)]
    [InlineData(NamedSize.Default, 14)]
    [InlineData(NamedSize.Medium, 14)]
    [InlineData(NamedSize.Body, 14)]
    [InlineData(NamedSize.Caption, 12)]
    [InlineData(NamedSize.Large, 18)]
    [InlineData(NamedSize.Subtitle, 20)]
    [InlineData(NamedSize.Title, 24)]
    [InlineData(NamedSize.Header, 46)]
    public void Named_sizes_follow_the_desktop_scale(NamedSize size, double expected)
    {
        new LinuxFontNamedSizeService().GetNamedSize(size, typeof(Label), useOldSizes: false).Should().Be(expected);
    }

    [Fact]
    public void Sizes_are_monotonic_from_Micro_to_Header()
    {
        var svc = new LinuxFontNamedSizeService();
        double Get(NamedSize s) => svc.GetNamedSize(s, typeof(Label), false);
        Get(NamedSize.Micro).Should().BeLessThan(Get(NamedSize.Small));
        Get(NamedSize.Small).Should().BeLessThan(Get(NamedSize.Large));
        Get(NamedSize.Large).Should().BeLessThan(Get(NamedSize.Subtitle));
        Get(NamedSize.Subtitle).Should().BeLessThan(Get(NamedSize.Title));
        Get(NamedSize.Title).Should().BeLessThan(Get(NamedSize.Header));
    }

    [Fact]
    public void XAML_font_size_converter_resolves_named_sizes_once_registered()
    {
        DependencyService.Register<Microsoft.Maui.Controls.Internals.IFontNamedSizeService, LinuxFontNamedSizeService>();
        var converter = new FontSizeConverter();
        converter.ConvertFromInvariantString("Large").Should().Be(18.0);
        converter.ConvertFromInvariantString("Header").Should().Be(46.0);
        converter.ConvertFromInvariantString("17").Should().Be(17.0);
    }
}
