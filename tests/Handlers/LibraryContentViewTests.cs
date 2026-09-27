// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A library view that implements IContentView without deriving from
/// ContentView (Syncfusion's SfScheduler and charts) gets OpenMaui's content
/// handler. It resolved to MAUI's own ContentViewHandler, whose platform view
/// throws on plain net10.0, and the view was dropped from its parent.
/// </summary>
[Collection("LinuxApplication.Current")]
public class LibraryContentViewTests
{
    [Fact]
    public void A_view_that_implements_IContentView_is_shown_with_its_content()
    {
        var label = new Label { Text = "Agenda" };
        var view = new LibraryContentView { Content = label };
        var grid = new Grid { Children = { view } };
        using var host = new HeadlessMauiHost(new ContentPage { Content = grid }, withEngine: true);
        host.Context.Render();

        view.Handler.Should().BeOfType<Microsoft.Maui.Platform.Linux.Handlers.ContentViewHandler>();
        label.Handler.Should().NotBeNull();
        label.Width.Should().BeGreaterThan(0);
    }

    private sealed class LibraryContentView : View, IContentView, ICrossPlatformLayout
    {
        public View? Content { get; set; }
        object? IContentView.Content => Content;
        IView? IContentView.PresentedContent => Content;
        Thickness IPadding.Padding => Thickness.Zero;

        protected override void OnParentSet()
        {
            base.OnParentSet();
            if (Content != null) Content.Parent = this;
        }

        public Size CrossPlatformMeasure(double widthConstraint, double heightConstraint) =>
            Content is IView c ? c.Measure(widthConstraint, heightConstraint) : Size.Zero;

        public Size CrossPlatformArrange(Rect bounds)
        {
            (Content as IView)?.Arrange(bounds);
            return bounds.Size;
        }
    }
}
