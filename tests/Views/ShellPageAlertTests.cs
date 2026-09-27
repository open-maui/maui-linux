// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Views;

/// <summary>
/// DisplayActionSheet from a Shell page: MAUI only forwards it when the page
/// is platform-enabled and in a window, otherwise it waits (or drops it)
/// silently and the awaiting code never resumes (MAToolbar's compressed
/// dropdown opened nothing).
/// </summary>
[Collection("LinuxApplication.Current")]
public class ShellPageAlertTests
{
    [Fact]
    public void A_shell_page_is_platform_enabled_and_in_the_window()
    {
        var page = new ContentPage { Content = new Label { Text = "x" } };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        page.Window.Should().NotBeNull();
        page.IsPlatformEnabled.Should().BeTrue();
    }

    [Fact]
    public void DisplayActionSheet_from_a_shell_page_shows_the_sheet()
    {
        var page = new ContentPage { Content = new Label { Text = "x" } };
        var shell = new Shell { FlyoutBehavior = FlyoutBehavior.Disabled };
        shell.Items.Add(new ShellContent { Content = page });
        using var host = new HeadlessMauiHost(shell, withEngine: true);
        host.Context.Render();

        _ = page.DisplayActionSheetAsync("Account", "Cancel", null, "one", "two");
        try
        {
            LinuxDialogService.HasActiveDialog.Should().BeTrue();
        }
        finally
        {
            while (LinuxDialogService.TopDialog is { } dialog)
                LinuxDialogService.HideDialog(dialog);
        }
    }
}
