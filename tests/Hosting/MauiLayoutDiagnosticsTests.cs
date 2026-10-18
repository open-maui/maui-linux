// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Hosting;

/// <summary>
/// MAUI instruments every Measure and Arrange (an activity, layout metrics, tags) whenever its
/// diagnostics manager is registered. A Linux app never gets the switch the MAUI SDK uses to turn
/// that off, so UseLinux removes it unless the app asks for MAUI's diagnostics.
/// </summary>
public class MauiLayoutDiagnosticsTests
{
    private static int DiagnosticsRegistrations(MauiAppBuilder builder) =>
        builder.Services.Count(d => d.ServiceType.FullName is "Microsoft.Maui.Diagnostics.IDiagnosticsManager"
            or "Microsoft.Maui.Diagnostics.IDiagnosticMetrics" or "Microsoft.Maui.Diagnostics.IDiagnosticTagger");

    [Fact]
    public void UseLinux_drops_MAUIs_layout_instrumentation_by_default()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<Application>();
        DiagnosticsRegistrations(builder).Should().BeGreaterThan(0, "UseMauiApp registers MAUI's diagnostics while meters are supported");

        LinuxMauiAppBuilderExtensionsInternal.RemoveMauiLayoutDiagnostics(builder.Services);

        DiagnosticsRegistrations(builder).Should().Be(0);
    }
}
