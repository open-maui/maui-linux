// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;

/// <summary>
/// MAUI's Trigger family lives entirely in the Controls layer; these tests
/// prove the property changes they make reach the Skia platform view through
/// the Linux handler mappers, and that platform interaction feeds the trigger
/// conditions (focus, click).
/// </summary>
public class TriggerPropagationTests
{
    private sealed class ViewModel : INotifyPropertyChanged
    {
        private bool _isAlert;
        private int _count;

        public bool IsAlert
        {
            get => _isAlert;
            set { _isAlert = value; OnPropertyChanged(); }
        }

        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class SetTextAction : TriggerAction<Button>
    {
        public int Invocations { get; private set; }

        protected override void Invoke(Button sender)
        {
            Invocations++;
            sender.Text = "Clicked";
            sender.TextColor = Colors.Red;
        }
    }

    private static PointerEventArgs Pointer() => new(1, 1, PointerButton.Left);

    [Fact]
    public void Trigger_OnIsFocused_ReachesPlatformWhenPlatformFocuses()
    {
        var entry = new Entry { BackgroundColor = Colors.White };
        entry.Triggers.Add(new Trigger(typeof(Entry))
        {
            Property = VisualElement.IsFocusedProperty,
            Value = true,
            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Red } }
        });
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);
        platform.BackgroundColor.Should().Be(Colors.White);

        platform.OnFocusGained();
        entry.IsFocused.Should().BeTrue();
        platform.BackgroundColor.Should().Be(Colors.Red);

        platform.OnFocusLost();
        platform.BackgroundColor.Should().Be(Colors.White, "the trigger's setters unapply when the condition clears");
    }

    [Fact]
    public void Trigger_OnText_ReachesPlatformViaMapper()
    {
        var entry = new Entry { TextColor = Colors.Black };
        entry.Triggers.Add(new Trigger(typeof(Entry))
        {
            Property = Entry.TextProperty,
            Value = "secret",
            Setters = { new Setter { Property = Entry.TextColorProperty, Value = Colors.Green } }
        });
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);

        entry.Text = "secret";
        platform.TextColor.Should().Be(Colors.Green);

        entry.Text = "other";
        platform.TextColor.Should().Be(Colors.Black);
    }

    [Fact]
    public void DataTrigger_BoundToViewModel_ReachesPlatform()
    {
        var vm = new ViewModel();
        var label = new Label { Text = "status", TextColor = Colors.Black, BindingContext = vm };
        label.Triggers.Add(new DataTrigger(typeof(Label))
        {
            Binding = new Binding(nameof(ViewModel.IsAlert)),
            Value = true,
            Setters =
            {
                new Setter { Property = Label.TextColorProperty, Value = Colors.Red },
                new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold },
            }
        });
        var platform = HeadlessMauiContext.Realize<SkiaLabel>(label);
        platform.TextColor.Should().Be(Colors.Black);

        vm.IsAlert = true;
        label.TextColor.Should().Be(Colors.Red);
        platform.TextColor.Should().Be(Colors.Red);
        platform.FontAttributes.Should().Be(FontAttributes.Bold);

        vm.IsAlert = false;
        platform.TextColor.Should().Be(Colors.Black);
        platform.FontAttributes.Should().Be(FontAttributes.None);
    }

    [Fact]
    public void MultiTrigger_AllConditionsMet_ReachesPlatform()
    {
        var vm = new ViewModel();
        var entry = new Entry { BindingContext = vm, TextColor = Colors.Black };
        entry.Triggers.Add(new MultiTrigger(typeof(Entry))
        {
            Conditions =
            {
                new PropertyCondition { Property = Entry.TextProperty, Value = "go" },
                new BindingCondition { Binding = new Binding(nameof(ViewModel.Count)), Value = 2 },
            },
            Setters = { new Setter { Property = Entry.TextColorProperty, Value = Colors.Purple } }
        });
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);

        entry.Text = "go";
        platform.TextColor.Should().Be(Colors.Black, "only one condition holds");

        vm.Count = 2;
        platform.TextColor.Should().Be(Colors.Purple);

        entry.Text = "stop";
        platform.TextColor.Should().Be(Colors.Black);
    }

    [Fact]
    public void EventTrigger_OnClicked_RunsActionAfterPlatformClick()
    {
        var action = new SetTextAction();
        var button = new Button { Text = "Press", TextColor = Colors.Black };
        button.Triggers.Add(new EventTrigger { Event = nameof(Button.Clicked), Actions = { action } });
        var platform = HeadlessMauiContext.Realize<SkiaButton>(button);
        platform.Text.Should().Be("Press");

        platform.OnPointerPressed(Pointer());
        platform.OnPointerReleased(Pointer());

        action.Invocations.Should().Be(1);
        button.Text.Should().Be("Clicked");
        platform.Text.Should().Be("Clicked");
        platform.TextColor.Should().Be(Colors.Red);
    }

    [Fact]
    public void StyleTrigger_AppliedThroughStyle_ReachesPlatform()
    {
        var style = new Style(typeof(Label))
        {
            Triggers =
            {
                new Trigger(typeof(Label))
                {
                    Property = VisualElement.IsEnabledProperty,
                    Value = false,
                    Setters = { new Setter { Property = Label.TextColorProperty, Value = Colors.Gray } }
                }
            }
        };
        var label = new Label { Text = "x", TextColor = Colors.Black, Style = style };
        var platform = HeadlessMauiContext.Realize<SkiaLabel>(label);

        label.IsEnabled = false;
        platform.TextColor.Should().Be(Colors.Gray);

        label.IsEnabled = true;
        platform.TextColor.Should().Be(Colors.Black);
    }
}
