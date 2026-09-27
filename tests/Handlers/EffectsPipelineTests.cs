// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MAUI's effects pipeline on OpenMaui: a RoutingEffect registered through
/// ConfigureEffects resolves to its PlatformEffect when the view's handler
/// connects, with the Skia platform view as Control; the platform effect sees
/// property changes, the view's routed pointer events, its key events and its
/// bounds, and is detached when the effect is removed.
/// </summary>
[Collection("LinuxApplication.Current")]
public class EffectsPipelineTests
{
    private sealed class HighlightEffect : RoutingEffect { }

    private sealed class HighlightPlatformEffect : Microsoft.Maui.Controls.Platform.PlatformEffect
    {
        public static readonly List<HighlightPlatformEffect> Instances = new();
        public int Attached, Detached;
        public object? ControlAtAttach;
        public List<string> Properties = new();
        public List<SkiaView.RoutedPointerKind> Pointer = new();
        public List<Key> Keys = new();
        public int BoundsChanges;

        public HighlightPlatformEffect() => Instances.Add(this);

        protected override void OnAttached()
        {
            Attached++;
            ControlAtAttach = Control;
            if (Control is SkiaView v)
            {
                v.PointerRouted += (_, e) => Pointer.Add(e.Kind);
                v.KeyDown += (_, e) => Keys.Add(e.Key);
                v.BoundsChanged += (_, _) => BoundsChanges++;
            }
        }

        protected override void OnDetached() => Detached++;

        protected override void OnElementPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnElementPropertyChanged(args);
            if (args.PropertyName != null) Properties.Add(args.PropertyName);
        }
    }

    private static void Register(MauiAppBuilder b) => b.ConfigureEffects(e => e.Add<HighlightEffect, HighlightPlatformEffect>());

    private static HighlightPlatformEffect Last => HighlightPlatformEffect.Instances[^1];

    [Fact]
    public void A_registered_effect_attaches_with_the_Skia_view_as_its_control()
    {
        var label = new Label { Text = "Hello" };
        label.Effects.Add(new HighlightEffect());
        using var host = new HeadlessMauiHost(new ContentPage { Content = label }, withEngine: true, configure: Register);
        host.Context.Render();

        var effect = Last;
        effect.Attached.Should().Be(1);
        effect.ControlAtAttach.Should().BeSameAs(label.Handler!.PlatformView);
        effect.Element.Should().BeSameAs(label);

        label.Text = "Changed";
        effect.Properties.Should().Contain(nameof(Label.Text));

        label.Effects.Clear();
        effect.Detached.Should().Be(1);
    }

    [Fact]
    public void An_effect_added_after_the_handler_exists_attaches_at_once()
    {
        var label = new Label { Text = "Hello" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = label }, withEngine: true, configure: Register);
        host.Context.Render();

        label.Effects.Add(new HighlightEffect());

        Last.Attached.Should().Be(1);
        Last.ControlAtAttach.Should().BeSameAs(label.Handler!.PlatformView);
    }

    [Fact]
    public void The_platform_effect_sees_routed_pointer_key_and_bounds_events()
    {
        // The effect on a Border hears a press on the Label inside it, as a native
        // platform's routed pointer events reach a container; keys go to the focused view.
        var label = new Label { Text = "Hello" };
        var border = new Border { Content = label, Padding = 10 };
        border.Effects.Add(new HighlightEffect());
        var entry = new Entry();
        entry.Effects.Add(new HighlightEffect());
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { border, entry } }, withEngine: true, configure: Register);
        host.Context.Render();
        var borderEffect = HighlightPlatformEffect.Instances[^2];
        var entryEffect = Last;

        var (x, y) = CenterOf(label);
        host.DisplayWindow.RaisePointerMoved(x, y);
        host.DisplayWindow.RaisePointerPressed(x, y);
        host.DisplayWindow.RaisePointerReleased(x, y);
        borderEffect.Pointer.Should().Contain(SkiaView.RoutedPointerKind.Pressed).And.Contain(SkiaView.RoutedPointerKind.Released);

        var (ex, ey) = CenterOf(entry);
        host.DisplayWindow.RaisePointerPressed(ex, ey);
        host.DisplayWindow.RaisePointerReleased(ex, ey);
        host.DisplayWindow.RaiseKeyDown(Key.Enter);
        entryEffect.Keys.Should().Contain(Key.Enter);

        host.DisplayWindow.RaiseResized(1000, 700);
        host.Context.Render();
        borderEffect.BoundsChanges.Should().BeGreaterThan(0);
    }

    [Fact]
    public void An_app_that_never_configured_effects_can_still_add_one()
    {
        // UseLinux registers the (empty) EffectsFactory; before, the first effect threw.
        var builder = MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauiApp<Application>();
        builder.UseLinux();
        using var app = builder.Build();

        app.Services.GetService(typeof(Effect).Assembly.GetType("Microsoft.Maui.Controls.Hosting.EffectsFactory")!)
            .Should().NotBeNull();
    }

    private static (float X, float Y) CenterOf(VisualElement view)
    {
        var b = ((SkiaView)view.Handler!.PlatformView!).ScreenBounds;
        return ((float)b.Center.X, (float)b.Center.Y);
    }
}
