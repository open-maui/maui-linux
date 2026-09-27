// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// A system family (not registered by the app) is resolved through fontconfig once and
/// kept: every text measurement asked fontconfig again, with a new typeface each call,
/// about 300 ms a frame on a page of Syncfusion chips and buttons while it animated.
/// </summary>
public class LinuxFontManagerCacheTests
{
    [Fact]
    public void A_system_family_resolves_to_the_same_typeface_each_time()
    {
        var fonts = new LinuxFontManager();
        var a = fonts.GetTypeface("Sans", SKFontStyle.Bold);
        var b = new LinuxFontManager().GetTypeface("Sans", SKFontStyle.Bold);
        b.Should().BeSameAs(a);
        fonts.GetTypeface("Sans", SKFontStyle.Normal).Should().NotBeSameAs(a, "another style is another typeface");
    }
}
