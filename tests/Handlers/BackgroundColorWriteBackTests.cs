// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A view's BackgroundColor survives getting its handler. MAUI's unset Background is
/// Brush.Default, a SolidColorBrush with no colour; mapping it as a colour wrote null through the
/// Skia view back to the view (a ContentView, a TitleBar, a NavigationPage or a SwipeView lost
/// the BackgroundColor it was given).
/// </summary>
[Collection("LinuxApplication.Current")]
public class BackgroundColorWriteBackTests
{
    [Fact]
    public void BackgroundColor_survives_the_handler()
    {
        using var host = new HeadlessMauiHost(new ContentPage());
        var views = new VisualElement[]
        {
            new ContentView { BackgroundColor = Colors.Red },
            new TitleBar { BackgroundColor = Colors.Red },
            new SwipeView { BackgroundColor = Colors.Red },
        };
        foreach (var view in views)
        {
            Microsoft.Maui.Platform.Linux.Hosting.MauiHandlerExtensions.ToHandler(view, host.MauiContext);
            view.BackgroundColor.Should().Be(Colors.Red, view.GetType().Name);
        }
    }
}
