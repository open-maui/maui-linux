// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// MAToolbar's search box: an Entry with a transparent background (MAUI's
/// template style) in a Grid beside icon buttons. It must look like a text
/// box and take focus when clicked, as on Windows and Mac.
/// </summary>
[Collection("LinuxApplication.Current")]
public class EntryChromeTests
{
    private static (Entry entry, HeadlessMauiHost host) Build()
    {
        var entry = new Entry { Placeholder = "Filter repos...", BackgroundColor = Colors.Transparent, WidthRequest = 200, VerticalOptions = LayoutOptions.Center };
        var box = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 4, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center,
        };
        box.Add(new ImageButton { WidthRequest = 20, HeightRequest = 20, IsVisible = false }, 0, 0);
        box.Add(entry, 1, 0);
        box.Add(new ImageButton { WidthRequest = 20, HeightRequest = 20, IsVisible = false }, 2, 0);
        var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.Black, Content = new HorizontalStackLayout { HeightRequest = 60, Children = { box } } }, withEngine: true);
        host.Context.Render();
        return (entry, host);
    }

    [Fact]
    public void Clicking_the_entry_focuses_it()
    {
        var (entry, host) = Build();
        using var _ = host;
        var b = ((SkiaView)entry.Handler!.PlatformView!).Bounds;
        host.DisplayWindow.RaisePointerPressed((float)b.Center.X, (float)b.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)b.Center.X, (float)b.Center.Y);

        host.Context.FocusedView.Should().BeSameAs(entry.Handler!.PlatformView);
        entry.IsFocused.Should().BeTrue();

        // The caret (blue) shows in the empty, focused entry.
        host.Context.UpdateAnimations();
        host.Context.Render();
        bool caret = false;
        for (int x = (int)b.Left; x < (int)b.Right && !caret; x++)
            for (int y = (int)b.Top; y < (int)b.Bottom && !caret; y++)
            {
                var (r, g, bl, _) = host.DisplayWindow.PixelAt(x, y);
                caret = bl > 180 && r < 120;
            }
        caret.Should().BeTrue("a focused entry shows its caret");
    }

    [Fact]
    public void A_focused_empty_editor_with_a_placeholder_shows_its_caret()
    {
        var editor = new Editor { Placeholder = "Body (optional)", BackgroundColor = Colors.Black, HeightRequest = 90, WidthRequest = 300 };
        using var host = new HeadlessMauiHost(new ContentPage { BackgroundColor = Colors.Black, Content = new VerticalStackLayout { editor } }, withEngine: true);
        host.Context.Render();
        var b = ((SkiaView)editor.Handler!.PlatformView!).Bounds;
        host.DisplayWindow.RaisePointerPressed((float)b.Center.X, (float)b.Center.Y);
        host.DisplayWindow.RaisePointerReleased((float)b.Center.X, (float)b.Center.Y);
        host.Context.UpdateAnimations();
        host.Context.Render();

        bool caret = false;
        for (int x = (int)b.Left; x < (int)b.Right && !caret; x++)
            for (int y = (int)b.Top; y < (int)b.Bottom && !caret; y++)
            {
                var (r, _, bl, _) = host.DisplayWindow.PixelAt(x, y);
                caret = bl > 180 && r < 120;
            }
        caret.Should().BeTrue();
    }
}
