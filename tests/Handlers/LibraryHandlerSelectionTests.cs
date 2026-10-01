// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Hosting;
using Xunit;
using LinuxLabelHandler = Microsoft.Maui.Platform.Linux.Handlers.LabelHandler;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A library's own control (a subclass of a framework control) keeps the handler the library
/// registered for it when that handler is written for Linux, as MarketAlly.ViewEngine's WebView
/// keeps its LinuxWebViewHandler; a handler on MAUI's platform-neutral base is still replaced.
/// </summary>
[Collection("LinuxApplication.Current")]
public class LibraryHandlerSelectionTests
{
    public sealed class LinuxAwareLabel : Label { }
    public sealed class LinuxAwareLabelHandler : LinuxLabelHandler { }

    public sealed class StockLabel : Label { }
    public sealed class StockLabelHandler : Microsoft.Maui.Handlers.LabelHandler { }

    public sealed class PlainLabel : Label { }

    private static HeadlessMauiHost Host(params View[] views) => new(
        new ContentPage { Content = new VerticalStackLayout { Children = { views[0], views[1], views[2] } } },
        withEngine: false,
        configure: b => b.ConfigureMauiHandlers(h =>
        {
            h.AddHandler(typeof(LinuxAwareLabel), typeof(LinuxAwareLabelHandler));
            h.AddHandler(typeof(StockLabel), typeof(StockLabelHandler));
        }));

    [Fact]
    public void A_linux_handler_a_library_registers_for_its_subclass_is_used()
    {
        var aware = new LinuxAwareLabel { Text = "a" };
        var stock = new StockLabel { Text = "b" };
        var plain = new PlainLabel { Text = "c" };
        using var host = Host(aware, stock, plain);

        aware.Handler.Should().BeOfType<LinuxAwareLabelHandler>();
        stock.Handler.Should().BeOfType<LinuxLabelHandler>("a handler on MAUI's platform-neutral base cannot draw here");
        plain.Handler.Should().BeOfType<LinuxLabelHandler>();
    }
}
