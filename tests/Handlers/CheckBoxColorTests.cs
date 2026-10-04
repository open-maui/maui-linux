// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// CheckBox.Color reaches the box when it changes after the handler exists: MAUI maps "Color"
/// to a Foreground refresh, and the Linux handler had no "Color" mapping (found by
/// MapperParityTests).
/// </summary>
[Collection("LinuxApplication.Current")]
public class CheckBoxColorTests
{
    [Fact]
    public void A_color_set_after_the_handler_exists_reaches_the_box()
    {
        var checkBox = new CheckBox { Color = Colors.Red };
        using var host = new HeadlessMauiHost(new ContentPage { Content = checkBox }, withEngine: false);
        var platform = (SkiaCheckBox)checkBox.Handler!.PlatformView!;
        platform.CheckColor.Should().Be(Colors.Red);

        checkBox.Color = Colors.Green;
        platform.CheckColor.Should().Be(Colors.Green);
    }
}
