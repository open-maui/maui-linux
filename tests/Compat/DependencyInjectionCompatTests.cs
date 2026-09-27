// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Microsoft.Extensions.DependencyInjection page resolution, the pattern MAUI's
/// templates teach: pages and view models registered on
/// <c>builder.Services</c>, the App taking its root page by constructor
/// injection, and Shell resolving routed pages (and their dependencies) from
/// the container. Pass = every page OpenMaui renders came out of the container
/// with its dependencies, with the registered lifetimes.
/// </summary>
[Collection(CompatHost.Collection)]
public class DependencyInjectionCompatTests
{
    public interface IGreetingService
    {
        string Greet(string name);
    }

    public sealed class GreetingService : IGreetingService
    {
        public string Greet(string name) => $"Hi {name}";
    }

    public sealed class MainViewModel(IGreetingService greetings)
    {
        public string Greeting { get; } = greetings.Greet("main");
    }

    public sealed class MainPage : ContentPage
    {
        public Label GreetingLabel { get; } = new();

        public MainPage(MainViewModel vm)
        {
            BindingContext = vm;
            GreetingLabel.SetBinding(Label.TextProperty, nameof(MainViewModel.Greeting));
            Content = GreetingLabel;
        }
    }

    public sealed class DetailsPage : ContentPage
    {
        public static int Instances;

        public Label Body { get; } = new();

        public DetailsPage(IGreetingService greetings)
        {
            Instances++;
            Body.Text = greetings.Greet("details");
            Content = Body;
        }
    }

    /// <summary>App whose root page comes from constructor injection (template pattern).</summary>
    public sealed class DiApp(MainPage mainPage) : Application
    {
        protected override Window CreateWindow(IActivationState? activationState) => new(mainPage);
    }

    public sealed class DiShellApp(AppShell shell) : Application
    {
        protected override Window CreateWindow(IActivationState? activationState) => new(shell);
    }

    public sealed class AppShell : Shell
    {
        public AppShell()
        {
            Items.Add(new Microsoft.Maui.Controls.ShellContent { Title = "Main", Route = "main", ContentTemplate = new DataTemplate(typeof(MainPage)) });
            Routing.RegisterRoute("details", typeof(DetailsPage));
        }
    }

    private static void Register(Microsoft.Maui.Hosting.MauiAppBuilder b)
    {
        b.Services.AddSingleton<IGreetingService, GreetingService>();
        b.Services.AddTransient<MainViewModel>();
        b.Services.AddTransient<MainPage>();
        b.Services.AddTransient<DetailsPage>();
        b.Services.AddSingleton<AppShell>();
    }

    [Fact]
    public void App_root_page_is_resolved_from_the_container()
    {
        using var host = CompatHost.StartApp<DiApp>(Register);
        host.Render();

        var main = host.Page.Should().BeOfType<MainPage>().Subject;
        ((SkiaLabel)CompatHost.PlatformOf(main.GreetingLabel)).Text.Should().Be("Hi main");
    }

    [Fact]
    public async Task Shell_resolves_content_and_routed_pages_from_the_container()
    {
        DetailsPage.Instances = 0;
        using var host = CompatHost.StartApp<DiShellApp>(Register);
        host.Render();
        var shell = host.Page.Should().BeOfType<AppShell>().Subject;
        shell.CurrentPage.Should().BeOfType<MainPage>("the ShellContent template is created through the container (MainPage has no parameterless constructor)");

        await shell.GoToAsync("details");
        host.Render();

        var details = shell.CurrentPage.Should().BeOfType<DetailsPage>().Subject;
        details.Body.Text.Should().Be("Hi details");
        DetailsPage.Instances.Should().Be(1);
        host.MauiApp.Services.GetRequiredService<AppShell>().Should().BeSameAs(shell, "singleton lifetime is honoured");
        Routing.UnRegisterRoute("details");
    }
}
