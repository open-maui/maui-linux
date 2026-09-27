// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform.Linux;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Hosting;

/// <summary>
/// IPlatformApplication.Current: the service locator MAUI apps use from
/// XAML-created views that cannot take constructor injection.
/// </summary>
[Collection("LinuxApplication.Current")]
public class PlatformApplicationTests
{
    private sealed class Marker { }

    [Fact]
    public void LinuxApplication_exposes_root_services()
    {
        var services = new ServiceCollection().AddSingleton<Marker>().BuildServiceProvider();
        using var app = new LinuxApplication { RootServices = services };
        var previous = IPlatformApplication.Current;
        try
        {
            IPlatformApplication.Current = app;
            IPlatformApplication.Current!.Services.GetRequiredService<Marker>().Should().NotBeNull();
        }
        finally
        {
            IPlatformApplication.Current = previous;
        }
    }

    [Fact]
    public void Without_a_built_app_services_explain_themselves()
    {
        using var app = new LinuxApplication();
        var act = () => ((IPlatformApplication)app).Services;
        act.Should().Throw<InvalidOperationException>().WithMessage("*not built*");
    }
}
