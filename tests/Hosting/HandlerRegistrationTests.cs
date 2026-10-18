// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Microsoft.Maui.Platform.Linux.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using Xunit;
using LinuxHandlers = Microsoft.Maui.Platform.Linux.Handlers;

namespace Microsoft.Maui.Controls.Linux.Tests.Hosting;

/// <summary>
/// The handler an app registers (<c>ConfigureMauiHandlers(h =&gt; h.AddHandler&lt;Label, MyLabelHandler&gt;())</c>)
/// is the one its views get, wherever they are (a page, a NavigationPage, a CollectionView row, a
/// Shell), as on every MAUI platform. OpenMaui's handlers are only the defaults; a registered
/// handler that cannot draw on Linux is reported and OpenMaui's is used instead.
/// </summary>
[Collection("LinuxApplication.Current")]
public class HandlerRegistrationTests
{
    public sealed class AppLabelHandler : LinuxHandlers.LabelHandler { }
    public sealed class SecondLabelHandler : LinuxHandlers.LabelHandler { }
    public sealed class AppButtonHandler : LinuxHandlers.TextButtonHandler { }
    public sealed class AppEntryHandler : LinuxHandlers.EntryHandler { }

    /// <summary>Not an OpenMaui handler, but it makes a Skia view: usable.</summary>
    public sealed class PlainSkiaLabelHandler : ViewHandler<ILabel, SkiaLabel>
    {
        public static readonly IPropertyMapper<ILabel, PlainSkiaLabelHandler> PlainMapper =
            new PropertyMapper<ILabel, PlainSkiaLabelHandler>(ViewHandler.ViewMapper)
            {
                [nameof(ILabel.Text)] = (handler, label) => handler.PlatformView.Text = label.Text ?? "",
            };

        public PlainSkiaLabelHandler() : base(PlainMapper) { }
        protected override SkiaLabel CreatePlatformView() => new();
    }

    /// <summary>A cross-platform handler on MAUI's portable LabelHandler: no platform view on Linux.</summary>
    public sealed class PortableLabelHandler : Microsoft.Maui.Handlers.LabelHandler { }

    /// <summary>Makes a platform view that is not a Skia view.</summary>
    public sealed class NonSkiaButtonHandler : ViewHandler<IButton, object>
    {
        public NonSkiaButtonHandler() : base(ViewHandler.ViewMapper) { }
        protected override object CreatePlatformView() => new object();
    }

    /// <summary>Written for Linux, but its platform view cannot be made.</summary>
    public sealed class ThrowingEntryHandler : ViewHandler<IEntry, SkiaEntry>
    {
        public ThrowingEntryHandler() : base(ViewHandler.ViewMapper) { }
        protected override SkiaEntry CreatePlatformView() => throw new InvalidOperationException("no entry today");
    }

    public sealed class FancyLabel : Label { }

    private static Action<MauiAppBuilder> Register(Action<IMauiHandlersCollection> handlers) =>
        builder => builder.ConfigureMauiHandlers(handlers);

    private static Action<IMauiHandlersCollection> AppHandlers => handlers =>
    {
        MauiHandlersCollectionExtensions.AddHandler<Label, AppLabelHandler>(handlers);
        MauiHandlersCollectionExtensions.AddHandler<Button, AppButtonHandler>(handlers);
        MauiHandlersCollectionExtensions.AddHandler<Entry, AppEntryHandler>(handlers);
    };

    private static (Label Label, Button Button, Entry Entry, VerticalStackLayout Stack) Controls()
    {
        var label = new Label { Text = "label" };
        var button = new Button { Text = "button" };
        var entry = new Entry { Text = "entry" };
        return (label, button, entry, new VerticalStackLayout { Children = { label, button, entry } });
    }

    private static void ShouldUseAppHandlers(Label label, Button button, Entry entry)
    {
        label.Handler.Should().BeOfType<AppLabelHandler>();
        button.Handler.Should().BeOfType<AppButtonHandler>();
        entry.Handler.Should().BeOfType<AppEntryHandler>();
        label.Handler!.PlatformView.Should().BeOfType<SkiaLabel>().Which.Text.Should().Be("label");
    }

    [Fact]
    public void App_handlers_are_used_inside_a_ContentPage()
    {
        var (label, button, entry, stack) = Controls();
        using var host = new HeadlessMauiHost(new ContentPage { Content = stack }, configure: Register(AppHandlers));

        ShouldUseAppHandlers(label, button, entry);
        stack.Handler.Should().BeOfType<LinuxHandlers.StackLayoutHandler>("types the app did not register keep OpenMaui's handler");
    }

    [Fact]
    public void App_handlers_are_used_inside_a_NavigationPage()
    {
        var (label, button, entry, stack) = Controls();
        using var host = new HeadlessMauiHost(new NavigationPage(new ContentPage { Content = stack }), withEngine: true, configure: Register(AppHandlers));
        host.Context.Render();

        ShouldUseAppHandlers(label, button, entry);
    }

    [Fact]
    public void App_handlers_are_used_in_CollectionView_item_templates()
    {
        var list = new CollectionView
        {
            ItemsSource = new[] { "first", "second" },
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                return label;
            }),
        };
        using var host = new HeadlessMauiHost(new ContentPage { Content = list }, withEngine: true, configure: Register(AppHandlers));
        host.Context.Render();

        var skia = (SkiaCollectionView)list.Handler!.PlatformView!;
        var row = skia.GetItemView(0)!.MauiView.Should().BeOfType<Label>().Subject;
        row.Handler.Should().BeOfType<AppLabelHandler>();
        row.Text.Should().Be("first");
    }

    [Fact]
    public void App_handlers_are_used_in_Shell()
    {
        var (label, button, entry, stack) = Controls();
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Route = "main", Content = new ContentPage { Content = stack } });
        using var host = new HeadlessMauiHost(shell, withEngine: true, configure: Register(AppHandlers));
        host.Context.Render();

        ShouldUseAppHandlers(label, button, entry);
    }

    [Fact]
    public void A_registration_for_a_subclass_is_used_for_it_and_not_for_its_base()
    {
        var fancy = new FancyLabel { Text = "fancy" };
        var plain = new Label { Text = "plain" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { Children = { fancy, plain } } },
            configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<FancyLabel, AppLabelHandler>(h)));

        fancy.Handler.Should().BeOfType<AppLabelHandler>();
        plain.Handler.Should().BeOfType<LinuxHandlers.LabelHandler>();
    }

    [Fact]
    public void A_registration_for_a_control_also_serves_its_subclasses()
    {
        var fancy = new FancyLabel { Text = "fancy" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = fancy },
            configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Label, AppLabelHandler>(h)));

        fancy.Handler.Should().BeOfType<AppLabelHandler>("MAUI resolves a handler through the view's base types");
    }

    [Fact]
    public void The_last_registration_for_a_type_wins()
    {
        var label = new Label { Text = "x" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = label }, configure: builder =>
        {
            builder.ConfigureMauiHandlers(h => MauiHandlersCollectionExtensions.AddHandler<Label, AppLabelHandler>(h));
            builder.ConfigureMauiHandlers(h => MauiHandlersCollectionExtensions.AddHandler<Label, SecondLabelHandler>(h));
        });

        label.Handler.Should().BeOfType<SecondLabelHandler>();
    }

    [Fact]
    public void A_factory_registration_is_used()
    {
        var label = new Label { Text = "x" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = label },
            configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Label>(h, _ => new SecondLabelHandler())));

        label.Handler.Should().BeOfType<SecondLabelHandler>();
    }

    [Fact]
    public void A_handler_that_is_not_OpenMauis_is_used_when_it_makes_a_Skia_view()
    {
        var label = new Label { Text = "plain skia" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = new VerticalStackLayout { Children = { label } } },
            withEngine: true, configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Label, PlainSkiaLabelHandler>(h)));
        host.Context.Render();

        label.Handler.Should().BeOfType<PlainSkiaLabelHandler>();
        var skia = label.Handler!.PlatformView.Should().BeOfType<SkiaLabel>().Subject;
        skia.Text.Should().Be("plain skia");
        skia.MauiView.Should().BeSameAs(label);
    }

    [Fact]
    public void App_registrations_made_before_UseLinux_are_kept()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);
        builder.UseMauiApp<Application>();
        builder.ConfigureMauiHandlers(h => MauiHandlersCollectionExtensions.AddHandler<Label, AppLabelHandler>(h));
        builder.UseLinux(_ => { });
        var app = builder.Build();
        Microsoft.Maui.Controls.Linux.Tests.Handlers.HeadlessMauiContext.InstallInlineDispatcher(); // as HeadlessMauiContext does after Build

        app.Services.GetRequiredService<IMauiHandlersFactory>().GetHandlerType(typeof(Label)).Should().Be(typeof(AppLabelHandler));
        app.Services.GetRequiredService<IMauiHandlersFactory>().GetHandlerType(typeof(Button)).Should().Be(typeof(LinuxHandlers.TextButtonHandler),
            "OpenMaui's handlers are still the defaults, replacing MAUI's portable ones");

        var context = new LinuxMauiContext(app.Services, new LinuxApplication());
        MauiHandlerExtensions.ToHandler(new Label(), context).Should().BeOfType<AppLabelHandler>();
        MauiHandlerExtensions.ToHandler(new Button(), context).Should().BeOfType<LinuxHandlers.TextButtonHandler>();
    }

    [Fact]
    public void Mapper_customisations_reach_a_registered_handler()
    {
        var seen = new List<string?>();
        LinuxHandlers.LabelHandler.Mapper.AppendToMapping("HandlerRegistrationTests.Probe", (handler, view) =>
        {
            if (view is Label { AutomationId: "mapper-probe" } label && handler is AppLabelHandler)
                seen.Add(label.Text);
        });

        var probe = new Label { Text = "mapped", AutomationId = "mapper-probe" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = probe },
            configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Label, AppLabelHandler>(h)));

        probe.Handler.Should().BeOfType<AppLabelHandler>();
        seen.Should().Contain("mapped");
    }

    [Fact]
    public void A_portable_handler_falls_back_to_OpenMauis_and_is_reported()
    {
        var label = new Label { Text = "portable" };
        using var host = new HeadlessMauiHost(new ContentPage { Content = label },
            configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Label, PortableLabelHandler>(h)));

        label.Handler.Should().BeOfType<LinuxHandlers.LabelHandler>();
        label.Handler!.PlatformView.Should().BeOfType<SkiaLabel>().Which.Text.Should().Be("portable");
        MauiHandlerExtensions.UnusableHandlers.Should().ContainKey(typeof(PortableLabelHandler))
            .WhoseValue.Should().Contain("portable handler");
    }

    [Fact]
    public void A_handler_whose_platform_view_is_not_a_Skia_view_falls_back_with_an_error()
    {
        var errors = new List<string>();
        void OnError(string tag, string message) => errors.Add(message);
        DiagnosticLog.ErrorLogged += OnError;
        try
        {
            var button = new Button { Text = "not skia" };
            using var host = new HeadlessMauiHost(new ContentPage { Content = button },
                configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Button, NonSkiaButtonHandler>(h)));

            button.Handler.Should().BeOfType<LinuxHandlers.TextButtonHandler>();
            button.Handler!.PlatformView.Should().BeAssignableTo<SkiaView>();
            errors.Should().ContainSingle(m => m.Contains(nameof(NonSkiaButtonHandler)))
                .Which.Should().Contain("cannot be used on Linux");
        }
        finally
        {
            DiagnosticLog.ErrorLogged -= OnError;
        }
    }

    [Fact]
    public void A_handler_that_throws_falls_back_with_an_error()
    {
        var errors = new List<string>();
        void OnError(string tag, string message) => errors.Add(message);
        DiagnosticLog.ErrorLogged += OnError;
        try
        {
            var entry = new Entry { Text = "throws" };
            using var host = new HeadlessMauiHost(new ContentPage { Content = entry },
                configure: Register(h => MauiHandlersCollectionExtensions.AddHandler<Entry, ThrowingEntryHandler>(h)));

            entry.Handler.Should().BeOfType<LinuxHandlers.EntryHandler>();
            errors.Should().ContainSingle(m => m.Contains(nameof(ThrowingEntryHandler)))
                .Which.Should().Contain("no entry today");
            MauiHandlerExtensions.UnusableHandlers.Should().ContainKey(typeof(ThrowingEntryHandler));
        }
        finally
        {
            DiagnosticLog.ErrorLogged -= OnError;
        }
    }
}
