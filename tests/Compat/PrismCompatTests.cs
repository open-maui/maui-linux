// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Prism;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// Prism.DryIoc.Maui (9.0.537; ships a net8.0 asset only, consumed unchanged on
/// net10.0) bootstrapped the documented way: <c>UsePrism(...)</c> with
/// <c>CreateWindow("NavigationPage/HomePage")</c> and an App that does not
/// override CreateWindow. Pass = Prism builds the startup window through
/// MAUI's IWindowCreator hook when OpenMaui starts the app, view models are
/// auto-wired, and URI navigation with parameters pushes and pops pages that
/// OpenMaui renders.
/// </summary>
[Collection(CompatHost.Collection)]
public class PrismCompatTests
{
    public sealed class PrismApp : Application
    {
    }

    public sealed class HomeViewModel : BindableBase
    {
        private string _title = "Home";

        public HomeViewModel(INavigationService navigation) => Navigation = navigation;

        public INavigationService Navigation { get; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }

    public sealed class DetailViewModel : BindableBase, INavigationAware
    {
        private string _item = "";

        public DetailViewModel(INavigationService navigation) => Navigation = navigation;

        public INavigationService Navigation { get; }

        public string Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        public void OnNavigatedTo(INavigationParameters parameters)
            => Item = parameters.GetValue<string>("item");

        public void OnNavigatedFrom(INavigationParameters parameters)
        {
        }
    }

    public sealed class HomePage : ContentPage
    {
        public Label TitleLabel { get; } = new();

        public HomePage()
        {
            TitleLabel.SetBinding(Label.TextProperty, nameof(HomeViewModel.Title));
            Content = TitleLabel;
        }
    }

    public sealed class DetailPage : ContentPage
    {
        public Label ItemLabel { get; } = new();

        public DetailPage()
        {
            ItemLabel.SetBinding(Label.TextProperty, nameof(DetailViewModel.Item));
            Content = ItemLabel;
        }
    }

    private static CompatHost StartPrismApp()
        => CompatHost.StartApp<PrismApp>(b => b.UsePrism(prism => prism
            .RegisterTypes(c =>
            {
                c.RegisterForNavigation<NavigationPage>();
                c.RegisterForNavigation<HomePage, HomeViewModel>();
                c.RegisterForNavigation<DetailPage, DetailViewModel>();
            })
            .CreateWindow("NavigationPage/HomePage")));

    [Fact]
    public void Prism_creates_the_startup_window_through_the_platform()
    {
        using var host = StartPrismApp();
        host.Render();

        var nav = host.Page.Should().BeOfType<NavigationPage>().Subject;
        var home = nav.CurrentPage.Should().BeOfType<HomePage>().Subject;
        home.BindingContext.Should().BeOfType<HomeViewModel>("Prism's ViewModelLocator auto-wires the view model");
        ((SkiaLabel)CompatHost.PlatformOf(home.TitleLabel)).Text.Should().Be("Home");
    }

    [Fact]
    public async Task Prism_navigates_with_parameters_and_back()
    {
        using var host = StartPrismApp();
        host.Render();
        var nav = (NavigationPage)host.Page;
        var homeVm = (HomeViewModel)nav.CurrentPage.BindingContext;

        var result = await homeVm.Navigation.NavigateAsync("DetailPage", new NavigationParameters { { "item", "42" } });
        host.Render();

        result.Success.Should().BeTrue(result.Exception?.ToString());
        var detail = nav.CurrentPage.Should().BeOfType<DetailPage>().Subject;
        ((DetailViewModel)detail.BindingContext).Item.Should().Be("42");
        ((SkiaLabel)CompatHost.PlatformOf(detail.ItemLabel)).Text.Should().Be("42", "the pushed page is rendered by OpenMaui's NavigationPage handler");

        var back = await ((DetailViewModel)detail.BindingContext).Navigation.GoBackAsync();

        back.Success.Should().BeTrue(back.Exception?.ToString());
        nav.CurrentPage.Should().BeOfType<HomePage>();
        nav.Navigation.NavigationStack.Should().HaveCount(1);
    }
}
