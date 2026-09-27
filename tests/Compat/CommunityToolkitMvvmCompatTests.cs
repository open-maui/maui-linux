// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// CommunityToolkit.Mvvm (8.4, netstandard2.1/net8.0 asset) with its source
/// generators, bound to MAUI controls rendered by OpenMaui. Pass = generated
/// observable properties and relay commands drive the Skia views through MAUI
/// bindings, and user input flows back into the view model.
/// </summary>
[Collection(CompatHost.Collection)]
public partial class CommunityToolkitMvvmCompatTests
{
    public partial class CounterViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [NotifyCanExecuteChangedFor(nameof(IncrementCommand))]
        private int _count;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private string _name = "";

        public int Limit { get; set; } = 3;

        public string Summary => $"{Name}:{Count}";

        [RelayCommand(CanExecute = nameof(CanIncrement))]
        private void Increment() => Count++;

        private bool CanIncrement() => Count < Limit;

        [RelayCommand]
        private async Task LoadAsync()
        {
            await Task.Yield();
            Name = "loaded";
        }
    }

    private sealed record CountChanged(int Value);

    private static (CompatHost Host, Button Button, Label Label, Entry Entry, CounterViewModel Vm) Build()
    {
        var vm = new CounterViewModel();
        var label = new Label { HeightRequest = 30 };
        label.SetBinding(Label.TextProperty, nameof(CounterViewModel.Summary));
        var entry = new Entry { HeightRequest = 40, WidthRequest = 200, HorizontalOptions = LayoutOptions.Start };
        entry.SetBinding(Entry.TextProperty, nameof(CounterViewModel.Name), BindingMode.TwoWay);
        var button = new Button { Text = "+1", HeightRequest = 40, WidthRequest = 120, HorizontalOptions = LayoutOptions.Start };
        button.SetBinding(Button.CommandProperty, nameof(CounterViewModel.IncrementCommand));
        var page = new ContentPage
        {
            BindingContext = vm,
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout { Children = { label, entry, button } },
        };
        var host = new CompatHost(page);
        host.Render();
        return (host, button, label, entry, vm);
    }

    [Fact]
    public void Generated_observable_property_updates_the_rendered_label()
    {
        var (host, _, label, _, vm) = Build();
        using (host)
        {
            vm.Name = "n";
            vm.Count = 2;

            ((SkiaLabel)CompatHost.PlatformOf(label)).Text.Should().Be("n:2", "[NotifyPropertyChangedFor] re-evaluates the Summary binding");
        }
    }

    [Fact]
    public void Generated_relay_command_runs_on_a_real_click_and_disables_the_button_at_its_limit()
    {
        var (host, button, label, _, vm) = Build();
        using (host)
        {
            for (int i = 0; i < 5; i++)
            {
                host.Tap(button);
                host.Render();
            }

            vm.Count.Should().Be(3, "CanExecute stops the command at the limit");
            button.IsEnabled.Should().BeFalse("[NotifyCanExecuteChangedFor] re-queries CanExecute and MAUI disables the button");
            CompatHost.PlatformOf(button).IsEnabled.Should().BeFalse("the Skia button mirrors the disabled state");
            ((SkiaLabel)CompatHost.PlatformOf(label)).Text.Should().Be(":3");
        }
    }

    [Fact]
    public void Typed_text_flows_back_into_the_generated_property()
    {
        var (host, _, label, entry, vm) = Build();
        using (host)
        {
            host.Tap(entry);
            host.DisplayWindow.RaiseTextInput("ab");

            vm.Name.Should().Be("ab", "two-way binding writes keyboard input to the view model");
            ((SkiaLabel)CompatHost.PlatformOf(label)).Text.Should().Be("ab:0");
        }
    }

    [Fact]
    public async Task Async_relay_command_completes_and_reports_running_state()
    {
        var (host, _, label, _, vm) = Build();
        using (host)
        {
            vm.LoadCommand.IsRunning.Should().BeFalse();

            await vm.LoadCommand.ExecuteAsync(null);

            vm.Name.Should().Be("loaded");
            ((SkiaLabel)CompatHost.PlatformOf(label)).Text.Should().Be("loaded:0");
        }
    }

    [Fact]
    public void Messenger_delivers_to_a_recipient_that_updates_the_ui()
    {
        var (host, _, label, _, vm) = Build();
        using (host)
        {
            var messenger = new WeakReferenceMessenger();
            messenger.Register<CounterViewModel, CountChanged>(vm, (r, m) => r.Count = m.Value);

            messenger.Send(new CountChanged(2));

            ((SkiaLabel)CompatHost.PlatformOf(label)).Text.Should().Be(":2");
        }
    }
}
