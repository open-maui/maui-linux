// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Handlers;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// ControlTemplate on ContentView / TemplatedView: MAUI materialises the
/// template on the Controls side; the platform must render the template root
/// around the content through the ContentPresenter.
/// </summary>
public class ControlTemplateHandlerTests
{
    private sealed class HeaderedControl : TemplatedView
    {
        public static readonly BindableProperty HeaderTextProperty =
            BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(HeaderedControl), string.Empty);

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public Label? HeaderLabel { get; private set; }
        public int ApplyTemplateCount { get; private set; }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ApplyTemplateCount++;
            HeaderLabel = GetTemplateChild("PART_Header") as Label;
        }
    }

    private static ControlTemplate BorderAroundPresenter(double strokeThickness = 3) =>
        new(() => new Border
        {
            StrokeThickness = strokeThickness,
            Stroke = Colors.Red,
            Content = new ContentPresenter()
        });

    private static ControlTemplate HeaderedTemplate() =>
        new(() =>
        {
            var header = new Label();
            header.SetBinding(Label.TextProperty, new TemplateBinding(nameof(HeaderedControl.HeaderText)));

            var root = new Border { StrokeThickness = 1, Content = header };
            var scope = new NameScope();
            NameScope.SetNameScope(root, scope);
            ((INameScope)scope).RegisterName("PART_Header", header);
            return root;
        });

    [Fact]
    public void ContentView_WithTemplate_RendersBorderAroundPresentedContent()
    {
        var contentView = new ContentView
        {
            Content = new Label { Text = "Hello" },
            ControlTemplate = BorderAroundPresenter(),
        };

        var platform = HeadlessMauiContext.Realize<SkiaContentView>(contentView);

        var border = platform.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaBorder>().Subject;
        border.StrokeThickness.Should().Be(3);

        var presenter = border.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaContentView>().Subject;
        var label = presenter.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaLabel>().Subject;
        label.Text.Should().Be("Hello");
    }

    [Fact]
    public void ContentPresenter_ResolvesToLinuxHandler()
    {
        // The presenter is realized inside a template on purpose: MAUI's
        // ContentPresenter.OnContentChanged is async void and awaits its
        // templated parent, and xunit waits for pending async-void work, so a
        // parentless presenter would stall the test run.
        var contentView = new ContentView
        {
            Content = new Label { Text = "P" },
            ControlTemplate = BorderAroundPresenter(),
        };
        HeadlessMauiContext.Realize<SkiaContentView>(contentView);

        var border = (Border)((IContentView)contentView).PresentedContent!;
        var presenter = border.Content.Should().BeOfType<ContentPresenter>().Subject;

        presenter.Handler.Should().BeOfType<ContentPresenterHandler>();
        var platform = presenter.Handler!.PlatformView.Should().BeOfType<SkiaContentView>().Subject;
        platform.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaLabel>()
            .Which.Text.Should().Be("P");
    }

    [Fact]
    public void ContentView_TemplateSetAfterRealize_RerendersTemplate()
    {
        var contentView = new ContentView { Content = new Label { Text = "Hello" } };
        var platform = HeadlessMauiContext.Realize<SkiaContentView>(contentView);
        platform.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaLabel>();

        contentView.ControlTemplate = BorderAroundPresenter(5);

        var border = platform.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaBorder>().Subject;
        border.StrokeThickness.Should().Be(5);
        ((SkiaContentView)border.Children.Single()).Children.Single().Should().BeOfType<SkiaLabel>()
            .Which.Text.Should().Be("Hello");
    }

    [Fact]
    public void ContentView_ContentChangedUnderTemplate_UpdatesPresenter()
    {
        var contentView = new ContentView
        {
            ControlTemplate = BorderAroundPresenter(),
            Content = new Label { Text = "First" },
        };
        var platform = HeadlessMauiContext.Realize<SkiaContentView>(contentView);

        contentView.Content = new Label { Text = "Second" };

        var border = platform.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaBorder>().Subject;
        var presenter = (SkiaContentView)border.Children.Single();
        presenter.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaLabel>()
            .Which.Text.Should().Be("Second");
    }

    [Fact]
    public void TemplatedView_TemplateBindingAndGetTemplateChild_Work()
    {
        var control = new HeaderedControl
        {
            HeaderText = "Title",
            ControlTemplate = HeaderedTemplate(),
        };

        var handler = HeadlessMauiContext.CreateHandler(control);
        handler.Should().BeOfType<TemplatedViewHandler>();
        var platform = (SkiaContentView)handler.PlatformView!;

        control.ApplyTemplateCount.Should().Be(1);
        control.HeaderLabel.Should().NotBeNull();
        control.HeaderLabel!.Text.Should().Be("Title", "TemplateBinding pulls from the templated parent");

        var border = platform.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaBorder>().Subject;
        var label = border.Children.Should().ContainSingle().Which.Should().BeOfType<SkiaLabel>().Subject;
        label.Text.Should().Be("Title");

        control.HeaderText = "Renamed";
        label.Text.Should().Be("Renamed", "TemplateBinding updates flow through the label's handler");
    }
}
