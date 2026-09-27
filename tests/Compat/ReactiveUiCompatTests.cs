// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using ReactiveUI;
using ReactiveUI.Builder;
using ReactiveUI.Maui;
using ReactiveUI.Primitives;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// ReactiveUI.Maui (24.x, generic net10.0 asset) initialised the documented way
/// (<c>UseReactiveUI(rx =&gt; rx.WithMaui())</c>). Pass = a ReactiveContentPage is
/// activated by OpenMaui's page lifecycle, and its WhenActivated bindings
/// (Bind, OneWayBind, BindCommand) connect the view model to the Skia views in
/// both directions, including input from the display window.
/// </summary>
[Collection(CompatHost.Collection)]
public class ReactiveUiCompatTests
{
    public sealed class GreeterViewModel : ReactiveObject
    {
        private string _name = "";
        private readonly ObservableAsPropertyHelper<string> _greeting;

        public GreeterViewModel()
        {
            _greeting = this.WhenAnyValue(x => x.Name)
                .Select(n => string.IsNullOrEmpty(n) ? "Who are you?" : $"Hello {n}")
                .ToProperty(this, x => x.Greeting);
            var canGreet = this.WhenAnyValue(x => x.Name).Select(n => !string.IsNullOrEmpty(n));
            Greet = ReactiveCommand.Create(() => { Greeted++; }, canGreet);
        }

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Greeting => _greeting.Value;

        public int Greeted { get; private set; }

        public ReactiveCommand<RxVoid, RxVoid> Greet { get; }
    }

    public sealed class GreeterPage : ReactiveContentPage<GreeterViewModel>
    {
        public Entry NameEntry { get; } = new() { WidthRequest = 200, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        public Label GreetingLabel { get; } = new() { HeightRequest = 30 };
        public Button GreetButton { get; } = new() { Text = "Greet", WidthRequest = 120, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        public int Activations { get; private set; }

        public GreeterPage()
        {
            BackgroundColor = Colors.White;
            Content = new VerticalStackLayout { Children = { NameEntry, GreetingLabel, GreetButton } };
            this.WhenActivated(d =>
            {
                Activations++;
                d(this.Bind(ViewModel, vm => vm.Name, v => v.NameEntry.Text));
                d(this.OneWayBind(ViewModel, vm => vm.Greeting, v => v.GreetingLabel.Text));
                d(this.BindCommand(ViewModel, vm => vm.Greet, v => v.GreetButton));
            });
        }
    }

    private static (CompatHost Host, GreeterPage Page, GreeterViewModel Vm) Build()
    {
        // Created after startup, as in an app: ReactiveUI must be initialised
        // (UseReactiveUI) before any ReactiveObject is constructed.
        var host = new CompatHost(_ => new GreeterPage { ViewModel = new GreeterViewModel() }, b => b.UseReactiveUI(rx => rx.WithMaui()));
        host.Render();
        var page = (GreeterPage)host.Page;
        return (host, page, page.ViewModel!);
    }

    [Fact]
    public void ReactiveContentPage_is_activated_by_the_platform_lifecycle()
    {
        var (host, page, _) = Build();
        using (host)
        {
            page.Activations.Should().Be(1, "OpenMaui raises Appearing for the window's page, which ReactiveUI treats as activation");
            ((SkiaLabel)CompatHost.PlatformOf(page.GreetingLabel)).Text.Should().Be("Who are you?", "OneWayBind pushed the initial value");
        }
    }

    [Fact]
    public void OneWayBind_and_Bind_follow_the_view_model()
    {
        var (host, page, vm) = Build();
        using (host)
        {
            vm.Name = "Ada";

            ((SkiaEntry)CompatHost.PlatformOf(page.NameEntry)).Text.Should().Be("Ada");
            ((SkiaLabel)CompatHost.PlatformOf(page.GreetingLabel)).Text.Should().Be("Hello Ada");
        }
    }

    [Fact]
    public void Typed_text_updates_the_view_model_and_enables_the_bound_command()
    {
        var (host, page, vm) = Build();
        using (host)
        {
            page.GreetButton.IsEnabled.Should().BeFalse("BindCommand follows the command's CanExecute (empty name)");

            host.Tap(page.NameEntry);
            host.DisplayWindow.RaiseTextInput("Bob");
            host.Render();

            vm.Name.Should().Be("Bob");
            ((SkiaLabel)CompatHost.PlatformOf(page.GreetingLabel)).Text.Should().Be("Hello Bob");
            page.GreetButton.IsEnabled.Should().BeTrue();

            host.Tap(page.GreetButton);
            vm.Greeted.Should().Be(1, "a real click executes the ReactiveCommand");
        }
    }
}
