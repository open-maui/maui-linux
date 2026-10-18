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
/// that off, so UseLinux removes it unless the app asks for MAUI's diagnostics. The switch is
/// process-wide (PageInvariants turns it on), so each test sets it and restores it, in the
/// collection PageInvariantsTests runs in.
/// </summary>
[Collection("LinuxApplication.Current")]
public class MauiLayoutDiagnosticsTests
{
    private const string Switch = "Microsoft.Maui.RuntimeFeature.EnableMauiDiagnostics";

    private static int DiagnosticsRegistrations(MauiAppBuilder builder) =>
        builder.Services.Count(d => d.ServiceType.FullName is "Microsoft.Maui.Diagnostics.IDiagnosticsManager"
            or "Microsoft.Maui.Diagnostics.IDiagnosticMetrics" or "Microsoft.Maui.Diagnostics.IDiagnosticTagger");

    private static int RegistrationsAfterUseLinux(bool mauiDiagnostics)
    {
        bool had = AppContext.TryGetSwitch(Switch, out var previous);
        AppContext.SetSwitch(Switch, mauiDiagnostics);
        try
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<Application>();
            DiagnosticsRegistrations(builder).Should().BeGreaterThan(0, "UseMauiApp registers MAUI's diagnostics while meters are supported");
            LinuxMauiAppBuilderExtensionsInternal.RemoveMauiLayoutDiagnostics(builder.Services);
            return DiagnosticsRegistrations(builder);
        }
        finally
        {
            // AppContext has no way to unset a switch; false is its default.
            AppContext.SetSwitch(Switch, had && previous);
        }
    }

    [Fact]
    public void UseLinux_drops_MAUIs_layout_instrumentation_by_default()
    {
        RegistrationsAfterUseLinux(mauiDiagnostics: false).Should().Be(0);
    }

    [Fact]
    public void An_app_that_asks_for_MAUIs_diagnostics_keeps_them()
    {
        RegistrationsAfterUseLinux(mauiDiagnostics: true).Should().BeGreaterThan(0);
    }
}
