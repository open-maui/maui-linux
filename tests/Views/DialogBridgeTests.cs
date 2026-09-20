// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests;

using KeyEventArgs = Microsoft.Maui.Platform.KeyEventArgs;
using Key = Microsoft.Maui.Platform.Key;

/// <summary>
/// Headless tests for the Page.DisplayAlert / DisplayPromptAsync /
/// DisplayActionSheet bridge: MAUI's AlertManager resolves the keyed
/// delegates registered by <see cref="LinuxAlertManager"/>, which show the
/// Skia dialogs in <see cref="LinuxDialogService"/>; the tests drive the
/// top dialog through the same input paths the native windows use.
/// </summary>
[Collection("LinuxApplication.Current")]
public class DialogBridgeTests : IDisposable
{
    private readonly ContentPage _page = new() { Content = new Label { Text = "root" } };
    private readonly HeadlessMauiHost _host;

    public DialogBridgeTests()
    {
        _host = new HeadlessMauiHost(_page);
    }

    public void Dispose()
    {
        // Never leave a dialog behind for the next test (the service is static).
        while (LinuxDialogService.TopDialog is { } dialog)
        {
            dialog.OnKeyDown(new KeyEventArgs(Key.Escape, Microsoft.Maui.Platform.KeyModifiers.None));
            if (LinuxDialogService.TopDialog == dialog)
                break;
        }
        _host.Dispose();
    }

    /// <summary>
    /// Draws the active dialogs once so their button bounds (computed during
    /// Draw, consulted during hit-test) are populated.
    /// </summary>
    private static void LayoutDialogs()
    {
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        LinuxDialogService.DrawDialogsOnly(surface.Canvas, new SKRect(0, 0, 800, 600));
    }

    private static void PressKey(Key key)
    {
        LinuxDialogService.TopDialog!.OnKeyDown(new KeyEventArgs(key, Microsoft.Maui.Platform.KeyModifiers.None));
    }

    #region Wiring

    [Fact]
    public void AdoptingWindow_AttachesWindowHandler_WithoutClobberingSize()
    {
        _host.Window.Handler.Should().NotBeNull("MAUI's AlertManager only subscribes once the Window has a handler");
        _host.Window.Handler!.MauiContext.Should().BeSameAs(_host.MauiContext);
        double.IsNaN(_host.Window.Width).Should().BeTrue("the passive SkiaWindow wrapper must not echo a size back into the MAUI window");
        double.IsNaN(_host.Window.Height).Should().BeTrue();
    }

    [Fact]
    public void DisplayAlert_ShowsSkiaAlertDialog()
    {
        var task = _page.DisplayAlert("Title", "Message", "OK", "Cancel");

        LinuxDialogService.HasActiveDialog.Should().BeTrue();
        LinuxDialogService.TopDialog.Should().BeOfType<SkiaAlertDialog>();
        task.IsCompleted.Should().BeFalse();
    }

    #endregion

    #region DisplayAlert

    [Fact]
    public async Task DisplayAlert_Enter_ReturnsTrue()
    {
        var task = _page.DisplayAlert("Title", "Message", "OK", "Cancel");

        PressKey(Key.Enter);

        (await task).Should().BeTrue();
        LinuxDialogService.HasActiveDialog.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayAlert_Escape_ReturnsFalse()
    {
        var task = _page.DisplayAlert("Title", "Message", "OK", "Cancel");

        PressKey(Key.Escape);

        (await task).Should().BeFalse();
        LinuxDialogService.HasActiveDialog.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayAlert_ClickAccept_ThroughWindowInput_ReturnsTrue()
    {
        var task = _page.DisplayAlert("Title", "Message", "Yes", "No");
        LayoutDialogs();
        var dialog = (SkiaAlertDialog)LinuxDialogService.TopDialog!;
        var accept = dialog.AcceptButtonBounds;
        accept.Width.Should().BeGreaterThan(0);

        // Native pointer event -> WindowContext -> dialog routing.
        _host.DisplayWindow.RaisePointerPressed(accept.MidX, accept.MidY);

        (await task).Should().BeTrue();
    }

    [Fact]
    public async Task DisplayAlert_ClickCancel_ReturnsFalse()
    {
        var task = _page.DisplayAlert("Title", "Message", "Yes", "No");
        LayoutDialogs();
        var dialog = (SkiaAlertDialog)LinuxDialogService.TopDialog!;
        var cancel = dialog.CancelButtonBounds;

        _host.DisplayWindow.RaisePointerPressed(cancel.MidX, cancel.MidY);

        (await task).Should().BeFalse();
    }

    [Fact]
    public async Task DisplayAlert_ClickOutsideCard_DoesNotDismiss()
    {
        var task = _page.DisplayAlert("Title", "Message", "Yes", "No");
        LayoutDialogs();

        _host.DisplayWindow.RaisePointerPressed(2, 2);

        task.IsCompleted.Should().BeFalse("the alert is modal");
        LinuxDialogService.HasActiveDialog.Should().BeTrue();
        PressKey(Key.Escape);
        (await task).Should().BeFalse();
    }

    [Fact]
    public async Task DisplayAlert_SingleButtonOverload_CompletesOnEnter()
    {
        var task = _page.DisplayAlert("Title", "Message", "OK");

        PressKey(Key.Enter);

        await task;
        LinuxDialogService.HasActiveDialog.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayAlert_SingleButtonOverload_CompletesOnEscape()
    {
        var task = _page.DisplayAlert("Title", "Message", "OK");

        PressKey(Key.Escape);

        await task;
        LinuxDialogService.HasActiveDialog.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayAlert_WhileModalIsActive_KeyboardGoesToDialogNotPage()
    {
        // A dialog on top takes the window's keyboard input away from the page.
        var task = _page.DisplayAlert("Title", "Message", "OK", "Cancel");

        _host.DisplayWindow.RaiseKeyDown(Key.Enter);

        (await task).Should().BeTrue();
    }

    #endregion

    #region DisplayPromptAsync

    [Fact]
    public async Task DisplayPrompt_TypedText_ThenEnter_ReturnsText()
    {
        var task = _page.DisplayPromptAsync("Name", "Enter your name");
        LinuxDialogService.TopDialog.Should().BeOfType<SkiaAlertDialog>()
            .Which.Input.Should().Be(string.Empty, "prompt dialogs carry a text field");

        _host.DisplayWindow.RaiseTextInput("Ada");
        _host.DisplayWindow.RaiseTextInput(" L");
        _host.DisplayWindow.RaiseKeyDown(Key.Enter);

        (await task).Should().Be("Ada L");
    }

    [Fact]
    public async Task DisplayPrompt_InitialValue_IsPrefilled_AndEditable()
    {
        var task = _page.DisplayPromptAsync("Name", "Enter", initialValue: "pre");

        _host.DisplayWindow.RaiseKeyDown(Key.Backspace);
        _host.DisplayWindow.RaiseTextInput("fix");
        PressKey(Key.Enter);

        (await task).Should().Be("prfix");
    }

    [Fact]
    public async Task DisplayPrompt_Cancel_ReturnsNull()
    {
        var task = _page.DisplayPromptAsync("Name", "Enter");

        _host.DisplayWindow.RaiseTextInput("ignored");
        PressKey(Key.Escape);

        (await task).Should().BeNull();
    }

    [Fact]
    public async Task DisplayPrompt_MaxLength_IsEnforced()
    {
        var task = _page.DisplayPromptAsync("Code", "Enter", maxLength: 4);

        _host.DisplayWindow.RaiseTextInput("12");
        _host.DisplayWindow.RaiseTextInput("3456789");
        PressKey(Key.Enter);

        (await task).Should().Be("1234");
    }

    [Fact]
    public async Task DisplayPrompt_Placeholder_IsCarriedToDialog()
    {
        var task = _page.DisplayPromptAsync("Code", "Enter", placeholder: "type here", keyboard: Keyboard.Numeric);

        var dialog = (SkiaAlertDialog)LinuxDialogService.TopDialog!;
        dialog.Placeholder.Should().Be("type here");
        LayoutDialogs(); // placeholder drawing path must not throw

        PressKey(Key.Escape);
        (await task).Should().BeNull();
    }

    #endregion

    #region DisplayActionSheet

    [Fact]
    public void DisplayActionSheet_ShowsSheet_WithDestructionFirstAndCancelLast()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", "Delete", "Copy", "Move");

        var sheet = LinuxDialogService.TopDialog.Should().BeOfType<SkiaActionSheetDialog>().Which;
        sheet.Options.Should().Equal("Delete", "Copy", "Move", "Cancel");
        task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayActionSheet_ClickOption_ReturnsItsText()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", "Delete", "Copy", "Move");
        LayoutDialogs();
        var sheet = (SkiaActionSheetDialog)LinuxDialogService.TopDialog!;
        var moveBounds = sheet.ButtonBounds[Array.IndexOf(sheet.Options.ToArray(), "Move")];

        _host.DisplayWindow.RaisePointerPressed(moveBounds.MidX, moveBounds.MidY);

        (await task).Should().Be("Move");
        LinuxDialogService.HasActiveDialog.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayActionSheet_ClickDestruction_ReturnsDestructionText()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", "Delete", "Copy");
        LayoutDialogs();
        var sheet = (SkiaActionSheetDialog)LinuxDialogService.TopDialog!;
        var deleteBounds = sheet.ButtonBounds[0];

        _host.DisplayWindow.RaisePointerPressed(deleteBounds.MidX, deleteBounds.MidY);

        (await task).Should().Be("Delete");
    }

    [Fact]
    public async Task DisplayActionSheet_ClickCancel_ReturnsCancelText()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", null, "Copy");
        LayoutDialogs();
        var sheet = (SkiaActionSheetDialog)LinuxDialogService.TopDialog!;
        var cancelBounds = sheet.ButtonBounds[sheet.Options.Count - 1];

        _host.DisplayWindow.RaisePointerPressed(cancelBounds.MidX, cancelBounds.MidY);

        (await task).Should().Be("Cancel");
    }

    [Fact]
    public async Task DisplayActionSheet_Escape_ReturnsCancelText()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", "Delete", "Copy");

        _host.DisplayWindow.RaiseKeyDown(Key.Escape);

        (await task).Should().Be("Cancel");
    }

    [Fact]
    public async Task DisplayActionSheet_Escape_WithoutCancel_ReturnsNull()
    {
        var task = _page.DisplayActionSheet("Pick", null, null, "Copy", "Move");

        PressKey(Key.Escape);

        (await task).Should().BeNull();
    }

    [Fact]
    public async Task DisplayActionSheet_ArrowKeysAndEnter_PickHighlightedOption()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", "Delete", "Copy", "Move");

        PressKey(Key.Down); // Delete
        PressKey(Key.Down); // Copy
        PressKey(Key.Enter);

        (await task).Should().Be("Copy");
    }

    [Fact]
    public async Task DisplayActionSheet_ClickOutsideCard_DoesNotDismiss()
    {
        var task = _page.DisplayActionSheet("Pick", "Cancel", null, "Copy");
        LayoutDialogs();

        _host.DisplayWindow.RaisePointerPressed(1, 1);

        task.IsCompleted.Should().BeFalse();
        PressKey(Key.Escape);
        (await task).Should().Be("Cancel");
    }

    #endregion

    #region Direct service API

    [Fact]
    public async Task ShowActionSheetAsync_SkipsEmptyButtons()
    {
        var task = LinuxDialogService.ShowActionSheetAsync("T", "Cancel", null, new[] { "A", "", null!, "B" });
        var sheet = (SkiaActionSheetDialog)LinuxDialogService.TopDialog!;

        sheet.Options.Should().Equal("A", "B", "Cancel");

        PressKey(Key.Escape);
        (await task).Should().Be("Cancel");
    }

    #endregion
}
