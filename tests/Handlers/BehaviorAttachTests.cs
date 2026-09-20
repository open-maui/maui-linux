// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

using PointerEventArgs = Microsoft.Maui.Platform.PointerEventArgs;

/// <summary>
/// Behavior&lt;T&gt; attach/detach against platform-hosted views: a behavior's
/// effect must reach the Skia platform view, and platform input must reach a
/// gesture recognizer a behavior installed.
/// </summary>
public class BehaviorAttachTests
{
    /// <summary>Colors the text red while the entry contains a digit.</summary>
    private sealed class DigitHighlightBehavior : Behavior<Entry>
    {
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }

        protected override void OnAttachedTo(Entry bindable)
        {
            base.OnAttachedTo(bindable);
            AttachCount++;
            bindable.TextChanged += OnTextChanged;
        }

        protected override void OnDetachingFrom(Entry bindable)
        {
            bindable.TextChanged -= OnTextChanged;
            DetachCount++;
            base.OnDetachingFrom(bindable);
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is Entry entry)
                entry.TextColor = e.NewTextValue.Any(char.IsDigit) ? Colors.Red : Colors.Black;
        }
    }

    /// <summary>Adds a TapGestureRecognizer that counts taps.</summary>
    private sealed class TapCounterBehavior : Behavior<View>
    {
        private readonly TapGestureRecognizer _recognizer = new();

        public int Taps { get; private set; }

        public TapCounterBehavior()
        {
            _recognizer.Tapped += (_, _) => Taps++;
        }

        protected override void OnAttachedTo(View bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.GestureRecognizers.Add(_recognizer);
        }

        protected override void OnDetachingFrom(View bindable)
        {
            bindable.GestureRecognizers.Remove(_recognizer);
            base.OnDetachingFrom(bindable);
        }
    }

    private static PointerEventArgs Pointer() => new(1, 1, PointerButton.Left);

    [Fact]
    public void Behavior_ReactingToPlatformTextInput_ReachesPlatformView()
    {
        var behavior = new DigitHighlightBehavior();
        var entry = new Entry { TextColor = Colors.Black };
        entry.Behaviors.Add(behavior);
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);
        behavior.AttachCount.Should().Be(1);

        // Text typed on the platform side flows Entry.TextChanged -> behavior -> TextColor -> mapper.
        platform.Text = "abc1";
        entry.Text.Should().Be("abc1");
        entry.TextColor.Should().Be(Colors.Red);
        platform.TextColor.Should().Be(Colors.Red);

        platform.Text = "abc";
        platform.TextColor.Should().Be(Colors.Black);

        entry.Behaviors.Remove(behavior);
        behavior.DetachCount.Should().Be(1);

        platform.Text = "abc2";
        platform.TextColor.Should().Be(Colors.Black, "a detached behavior no longer reacts");
    }

    [Fact]
    public void Behavior_AddingTapGesture_ReceivesPlatformTaps()
    {
        var behavior = new TapCounterBehavior();
        var label = new Label { Text = "tap" };
        label.Behaviors.Add(behavior);
        var platform = HeadlessMauiContext.Realize<SkiaLabel>(label);
        label.GestureRecognizers.Should().ContainSingle();

        platform.OnPointerPressed(Pointer());
        platform.OnPointerReleased(Pointer());
        behavior.Taps.Should().Be(1);

        label.Behaviors.Remove(behavior);
        label.GestureRecognizers.Should().BeEmpty();

        platform.OnPointerPressed(Pointer());
        platform.OnPointerReleased(Pointer());
        behavior.Taps.Should().Be(1, "the recognizer left with the behavior");
    }

    [Fact]
    public void Behavior_AttachedAfterRealize_StillWorks()
    {
        var entry = new Entry { TextColor = Colors.Black };
        var platform = HeadlessMauiContext.Realize<SkiaEntry>(entry);

        entry.Behaviors.Add(new DigitHighlightBehavior());

        platform.Text = "42";
        platform.TextColor.Should().Be(Colors.Red);
    }
}
