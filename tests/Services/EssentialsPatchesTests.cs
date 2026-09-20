// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Platform.Linux.Services;
using Microsoft.Maui.Storage;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Verifies that <see cref="EssentialsPatches.Apply"/> swaps the static
/// Essentials facades over to the Linux services so app code that calls
/// <c>FileSystem.Current</c>, <c>SemanticScreenReader.Announce</c> or
/// <c>Accelerometer.Default.IsSupported</c> no longer hits the portable
/// reference-assembly stubs.
/// </summary>
public class EssentialsPatchesTests
{
    public EssentialsPatchesTests()
    {
        EssentialsPatches.Apply();
    }

    [Fact]
    public void FileSystem_Current_IsLinuxService()
    {
        // Type check only: reading the directories would create
        // ~/.local/share/<testhost> on the developer's machine.
        FileSystem.Current.Should().BeOfType<FileSystemService>();
    }

    [Fact]
    public void SemanticScreenReader_Default_IsLinuxService_AndAnnounceDoesNotThrow()
    {
        SemanticScreenReader.Default.Should().BeOfType<SemanticScreenReaderService>();
        var act = () => SemanticScreenReader.Announce("Clicked 1 time");
        act.Should().NotThrow();
    }

    [Fact]
    public void WebAuthenticator_Default_IsLinuxService()
    {
        WebAuthenticator.Default.Should().BeOfType<WebAuthenticatorService>();
    }

    [Fact]
    public void Sensors_Default_ReportUnsupported()
    {
        Accelerometer.Default.IsSupported.Should().BeFalse();
        Barometer.Default.IsSupported.Should().BeFalse();
        Compass.Default.IsSupported.Should().BeFalse();
        Gyroscope.Default.IsSupported.Should().BeFalse();
        Magnetometer.Default.IsSupported.Should().BeFalse();
        OrientationSensor.Default.IsSupported.Should().BeFalse();

        Accelerometer.Default.Invoking(a => a.Start(SensorSpeed.Default)).Should().Throw<FeatureNotSupportedException>();
        Accelerometer.Default.Invoking(a => a.Stop()).Should().NotThrow();
    }

    [Fact]
    public void FilePickerFileType_statics_are_usable_and_carry_Linux_extensions()
    {
        // Unpatched, the portable build's type initializer throws on the first
        // touch of any of these (TypeInitializationException) and the type is
        // dead for the rest of the process.
        var images = FilePickerFileType.Images;
        var videos = FilePickerFileType.Videos;

        images.Should().NotBeNull();
        images.Value.Should().Contain(new[] { ".png", ".jpg", ".jpeg" });
        videos.Value.Should().Contain(new[] { ".mp4", ".mkv", ".webm" });
        FilePickerFileType.Png.Value.Should().Equal(".png");
        FilePickerFileType.Jpeg.Value.Should().BeEquivalentTo(new[] { ".jpg", ".jpeg" });
        FilePickerFileType.Pdf.Value.Should().Equal(".pdf");
    }

    [Fact]
    public void FilePickerFileType_statics_feed_the_picker_filters()
    {
        var options = new PickOptions { FileTypes = FilePickerFileType.Images };

        var extensions = PortalFilePickerService.GetExtensionsFromFileType(options.FileTypes);

        extensions.Should().NotBeNull();
        extensions.Should().Contain(".png");
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        var act = () => { EssentialsPatches.Apply(); EssentialsPatches.Apply(); };
        act.Should().NotThrow();
        FileSystem.Current.Should().BeOfType<FileSystemService>();
    }
}
