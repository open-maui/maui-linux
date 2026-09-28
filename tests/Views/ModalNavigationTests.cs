// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests;

using Key = Microsoft.Maui.Platform.Key;

/// <summary>
/// Headless tests for modal navigation: Navigation.PushModalAsync /
/// PopModalAsync route through MAUI's ModalNavigationManager (pure
/// bookkeeping on this TFM) whose Window.ModalPushed / ModalPopped events the
/// <see cref="WindowContext"/> turns into full-window Skia layers above the
/// root, with input redirected to the top-most layer.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ModalNavigationTests : IDisposable
{
    private readonly List<string> _lifecycle = new();
    private readonly ContentPage _root;
    private readonly HeadlessMauiHost _host;

    public ModalNavigationTests()
    {
        _root = MakePage("root", Colors.Red);
        _host = new HeadlessMauiHost(_root, withEngine: true);
    }

    public void Dispose() => _host.Dispose();

    private ContentPage MakePage(string name, Color background)
    {
        var page = new ContentPage
        {
            Title = name,
            BackgroundColor = background,
            Content = new Label { Text = name },
        };
        page.Appearing += (_, _) => _lifecycle.Add($"{name}:appearing");
        page.Disappearing += (_, _) => _lifecycle.Add($"{name}:disappearing");
        return page;
    }

    private static SkiaView RootAncestor(SkiaView view)
    {
        while (view.Parent != null)
            view = view.Parent;
        return view;
    }

    #region Transparency

    [Fact]
    public async Task A_transparent_modal_page_shows_the_page_beneath()
    {
        // Popups presented as modal pages (Mopups on plain net10.0) are
        // see-through apart from their content.
        var popup = new ContentPage
        {
            BackgroundColor = Colors.Transparent,
            Content = new Label { Text = "toast", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End },
        };
        await _root.Navigation.PushModalAsync(popup, animated: false);
        _host.Context.Render();

        var (r, g, b, _) = _host.DisplayWindow.PixelAt(10, 10);
        ((int)r).Should().BeGreaterThan(200, "the red root page shows through");
        ((int)g).Should().BeLessThan(60);
        ((int)b).Should().BeLessThan(60);
    }

    #endregion

    #region Popup layers

    [Fact]
    public void A_popup_layer_leaves_the_page_beneath_alone_and_routes_input()
    {
        // Popups (Mopups) are window layers, not modal navigation: the page
        // beneath gets no Disappearing/Appearing, a click on the popup's
        // content reaches it, and a click on its transparent backdrop is
        // reported as the popup's background.
        _lifecycle.Clear();
        var button = new Button { Text = "OK", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, WidthRequest = 80, HeightRequest = 40 };
        var popup = new ContentPage { BackgroundColor = Colors.Transparent, Content = button };

        _host.Context.PushPopupView(popup);
        _host.Context.Render();

        _lifecycle.Should().BeEmpty("the page beneath is not navigated away from");
        var b = ((SkiaView)button.Handler!.PlatformView!).Bounds;
        _host.Context.HitTestLayers((float)b.Center.X, (float)b.Center.Y, out var onContent)
            .Should().BeSameAs(button.Handler!.PlatformView);
        onContent.Should().BeNull();

        _host.Context.HitTestLayers(5, 5, out var backdrop);
        backdrop.Should().BeSameAs(popup, "the backdrop belongs to the popup");

        _host.Context.PopPopupView(popup).Should().BeTrue();
        _host.Context.ModalViews.Should().BeEmpty();
        _lifecycle.Should().BeEmpty();
    }

    #endregion

    #region Stack bookkeeping

    [Fact]
    public async Task PushModal_AddsToModalStack_AndPresentsLayer()
    {
        var modal = MakePage("modal", Colors.Blue);

        await _root.Navigation.PushModalAsync(modal, animated: false);

        _host.Window.Navigation.ModalStack.Should().ContainSingle().Which.Should().BeSameAs(modal);
        _host.Context.HasModal.Should().BeTrue();
        _host.Context.ModalStack.Should().ContainSingle().Which.Should().BeSameAs(modal);
        _host.Context.ModalViews.Should().ContainSingle();
        _host.Context.ModalViews[0].Should().BeOfType<SkiaContentPage>()
            .Which.MauiPage.Should().BeSameAs(modal);
        _host.Context.RootView.Should().BeSameAs(_host.RootView, "the root tree is untouched; the modal is a layer above it");
        modal.Handler.Should().NotBeNull("the modal page is rendered through the Linux handlers");
    }

    [Fact]
    public async Task PopModal_ReturnsThePage_AndRemovesLayer()
    {
        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);

        var popped = await _root.Navigation.PopModalAsync(animated: false);

        popped.Should().BeSameAs(modal);
        _host.Window.Navigation.ModalStack.Should().BeEmpty();
        _host.Context.HasModal.Should().BeFalse();
        _host.Context.ModalViews.Should().BeEmpty();
        _host.Context.InputRoot.Should().BeSameAs(_host.RootView);
    }

    [Fact]
    public async Task StackedModals_TopMostIsTheInputRoot_AndPopUnwindsInOrder()
    {
        var first = MakePage("first", Colors.Blue);
        var second = MakePage("second", Colors.Green);

        await _root.Navigation.PushModalAsync(first, animated: false);
        await _root.Navigation.PushModalAsync(second, animated: false);

        _host.Context.ModalStack.Should().Equal(first, second);
        _host.Context.InputRoot.Should().BeSameAs(_host.Context.ModalViews[1]);

        var popped = await _root.Navigation.PopModalAsync(animated: false);
        popped.Should().BeSameAs(second);
        _host.Context.ModalStack.Should().Equal(first);
        _host.Context.InputRoot.Should().BeSameAs(_host.Context.ModalViews[0]);

        await _root.Navigation.PopModalAsync(animated: false);
        _host.Context.HasModal.Should().BeFalse();
    }

    [Fact]
    public async Task PoppedPage_CanBePushedAgain()
    {
        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);
        await _root.Navigation.PopModalAsync(animated: false);

        await _root.Navigation.PushModalAsync(modal, animated: false);

        _host.Context.ModalStack.Should().Equal(modal);
        _host.Context.ModalViews.Should().ContainSingle()
            .Which.Should().BeOfType<SkiaContentPage>()
            .Which.MauiPage.Should().BeSameAs(modal);
        _host.Context.InputRoot.Should().BeSameAs(_host.Context.ModalViews[0]);
    }

    [Fact]
    public async Task PopModal_OnEmptyStack_Throws()
    {
        var act = () => _root.Navigation.PopModalAsync(animated: false);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Rendering

    [Fact]
    public async Task PushModal_RendersAboveTheRoot_AndPopRevealsItAgain()
    {
        var win = _host.DisplayWindow;

        _host.Context.Render();
        win.PixelAt(400, 300).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255), "the red root page fills the window");

        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);
        _host.Context.Render();
        win.PixelAt(400, 300).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255), "the blue modal page covers the root");
        win.PixelAt(795, 595).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255), "the modal layer is full-window");

        await _root.Navigation.PopModalAsync(animated: false);
        _host.Context.Render();
        win.PixelAt(400, 300).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255), "popping reveals the root again");
    }

    [Fact]
    public async Task ModalLayer_IsLaidOutToTheWindow_AndFollowsResize()
    {
        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);
        var layer = _host.Context.ModalViews[0];

        layer.Bounds.Width.Should().Be(800);
        layer.Bounds.Height.Should().Be(600);

        _host.DisplayWindow.RaiseResized(1024, 768);
        // Laid out by the next frame, as the root is: a resize only asks for one.
        _host.Context.Render();

        layer.Bounds.Width.Should().Be(1024);
        layer.Bounds.Height.Should().Be(768);
    }

    #endregion

    #region Input routing

    [Fact]
    public async Task PointerInput_GoesToTheModalTree_WhileModalIsPresented()
    {
        _host.DisplayWindow.RaisePointerMoved(400, 300);
        RootAncestor(_host.Context.HoveredView!).Should().BeSameAs(_host.RootView);

        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);

        _host.DisplayWindow.RaisePointerMoved(400, 300);
        _host.Context.HoveredView.Should().NotBeNull();
        RootAncestor(_host.Context.HoveredView!).Should().BeSameAs(_host.Context.ModalViews[0]);

        _host.DisplayWindow.RaisePointerPressed(400, 300);
        RootAncestor(_host.Context.CapturedView!).Should().BeSameAs(_host.Context.ModalViews[0]);

        await _root.Navigation.PopModalAsync(animated: false);

        _host.DisplayWindow.RaisePointerMoved(400, 300);
        RootAncestor(_host.Context.HoveredView!).Should().BeSameAs(_host.RootView);
    }

    [Fact]
    public async Task PushAndPop_DropFocusHoverAndCapture()
    {
        _host.DisplayWindow.RaisePointerMoved(10, 10);
        _host.DisplayWindow.RaisePointerPressed(10, 10);
        _host.Context.HoveredView.Should().NotBeNull();
        _host.Context.CapturedView.Should().NotBeNull();

        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);

        _host.Context.FocusedView.Should().BeNull("keyboard focus must not stay on the page beneath");
        _host.Context.HoveredView.Should().BeNull();
        _host.Context.CapturedView.Should().BeNull();

        _host.DisplayWindow.RaisePointerPressed(10, 10);
        _host.Context.CapturedView.Should().NotBeNull();

        await _root.Navigation.PopModalAsync(animated: false);

        _host.Context.FocusedView.Should().BeNull();
        _host.Context.CapturedView.Should().BeNull();
    }

    #endregion

    #region Lifecycle

    [Fact]
    public async Task PushModal_FiresRootDisappearing_ThenModalAppearing()
    {
        _lifecycle.Clear();
        var modal = MakePage("modal", Colors.Blue);

        await _root.Navigation.PushModalAsync(modal, animated: false);

        _lifecycle.Should().Equal("root:disappearing", "modal:appearing");
    }

    [Fact]
    public async Task PopModal_FiresModalDisappearing_ThenRootAppearing()
    {
        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);
        _lifecycle.Clear();

        await _root.Navigation.PopModalAsync(animated: false);

        _lifecycle.Should().Equal("modal:disappearing", "root:appearing");
    }

    [Fact]
    public async Task StackedModals_LifecycleFollowsTheTopOfStack()
    {
        var first = MakePage("first", Colors.Blue);
        var second = MakePage("second", Colors.Green);
        await _root.Navigation.PushModalAsync(first, animated: false);
        _lifecycle.Clear();

        await _root.Navigation.PushModalAsync(second, animated: false);
        _lifecycle.Should().Equal("first:disappearing", "second:appearing");

        _lifecycle.Clear();
        await _root.Navigation.PopModalAsync(animated: false);
        _lifecycle.Should().Equal("second:disappearing", "first:appearing");
    }

    [Fact]
    public async Task ModalPage_CanDisplayAlert()
    {
        // The modal's Window resolves through its logical parent chain, so
        // the same AlertManager subscription serves it.
        var modal = MakePage("modal", Colors.Blue);
        await _root.Navigation.PushModalAsync(modal, animated: false);

        var task = modal.DisplayAlert("From modal", "Message", "OK", "Cancel");
        LinuxDialogService.HasActiveDialog.Should().BeTrue();

        _host.DisplayWindow.RaiseKeyDown(Key.Enter);

        (await task).Should().BeTrue();
    }

    #endregion
}
