// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

public class AppImageDetectionTests
{
    [Theory]
    [InlineData("/home/u/App.AppImage", "/tmp/.mount_AppXyz", "/tmp/.mount_AppXyz/usr/bin/App", true)]
    // $APPIMAGE inherited from a terminal an AppImage app opened: not inside it.
    [InlineData("/home/u/Other.AppImage", "/tmp/.mount_Other", "/home/u/src/App/bin/Debug/App", false)]
    [InlineData("/home/u/Other.AppImage", null, "/home/u/src/App/bin/Debug/App", false)]
    [InlineData(null, null, "/usr/bin/App", false)]
    public void AppImage_detection_needs_the_process_inside_the_mount(string? appImage, string? appDir, string processPath, bool expected)
    {
        AppInfoService.IsRunningFromAppImage(appImage, appDir, processPath).Should().Be(expected);
    }
}
