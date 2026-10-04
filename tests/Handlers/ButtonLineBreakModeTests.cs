// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Handlers;

/// <summary>
/// A MAUI Button's LineBreakMode as drawn, through its handler: a truncating mode cuts the
/// text to the room beside the icon, and WordWrap / CharacterWrap break a long text onto
/// lines and make the button tall enough for them, as a WinUI button's text block does.
/// </summary>
[Collection("LinuxApplication.Current")]
public class ButtonLineBreakModeTests
{
    private const string Long = "START thinking about a long line of reasoning that cannot fit END";

    private static SkiaButton Show(Button button, out HeadlessMauiHost host)
    {
        var page = new ContentPage { Content = new VerticalStackLayout { button } };
        host = new HeadlessMauiHost(page, withEngine: true);
        host.Context.Render();
        host.Context.Render();
        return (SkiaButton)button.Handler!.PlatformView!;
    }

    private static float Width(SkiaButton view, string text)
    {
        using var font = view.CreateTextFont();
        return view.MeasureTextWidth(text, font);
    }

    private static string WriteIcon()
    {
        var path = Path.Combine(Path.GetTempPath(), $"openmaui-button-icon-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(48, 48);
        bitmap.Erase(SKColors.SteelBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [Fact]
    public void A_tail_truncated_button_draws_the_cut_text()
    {
        var view = Show(new Button { Text = Long, LineBreakMode = LineBreakMode.TailTruncation, WidthRequest = 160, HorizontalOptions = LayoutOptions.Start }, out var host);
        using (host)
        {
            view.DisplayedLines.Should().ContainSingle();
            var shown = view.DisplayedLines[0];
            shown.Should().StartWith("START").And.EndWith("…");
            Width(view, shown).Should().BeLessThanOrEqualTo(view.ContentWidth + 0.5f);
        }
    }

    [Fact]
    public async Task An_icon_leaves_the_truncated_text_less_room()
    {
        var icon = WriteIcon();
        try
        {
            var plainView = Show(new Button { Text = Long, LineBreakMode = LineBreakMode.TailTruncation, WidthRequest = 200, HorizontalOptions = LayoutOptions.Start }, out var plainHost);
            string plain;
            using (plainHost)
                plain = plainView.DisplayedLines[0];

            var withIcon = new Button { Text = Long, ImageSource = icon, LineBreakMode = LineBreakMode.TailTruncation, WidthRequest = 200, HorizontalOptions = LayoutOptions.Start };
            var iconView = Show(withIcon, out var iconHost);
            using (iconHost)
            {
                for (int i = 0; i < 50 && iconView.LoadedImage == null; i++)
                {
                    await Task.Delay(20);
                    iconHost.Context.Render();
                }
                iconView.LoadedImage.Should().NotBeNull("the icon loads through the image-source service");
                iconHost.Context.Render();

                var shown = iconView.DisplayedLines[0];
                shown.Should().EndWith("…");
                shown.Length.Should().BeLessThan(plain.Length, "the icon and its spacing take room from the text");
                float room = iconView.ContentWidth - 24 - (float)iconView.ContentLayout.Spacing;
                Width(iconView, shown).Should().BeLessThanOrEqualTo(room + 0.5f);
            }
        }
        finally
        {
            File.Delete(icon);
        }
    }

    [Fact]
    public void Word_wrap_breaks_between_words_and_grows_the_button()
    {
        var oneLine = Show(new Button { Text = "Short", WidthRequest = 160, HorizontalOptions = LayoutOptions.Start }, out var oneHost);
        double oneLineHeight;
        using (oneHost)
            oneLineHeight = oneLine.Bounds.Height;

        var view = Show(new Button { Text = Long, LineBreakMode = LineBreakMode.WordWrap, WidthRequest = 160, HorizontalOptions = LayoutOptions.Start }, out var host);
        using (host)
        {
            view.DisplayedLines.Count.Should().BeGreaterThan(1);
            string.Join(" ", view.DisplayedLines).Should().Be(Long, "word wrap breaks only between words");
            float room = view.ContentWidth;
            view.DisplayedLines.Should().OnlyContain(line => Width(view, line) <= room + 0.5f);
            view.Bounds.Height.Should().BeGreaterThan(oneLineHeight + 10, "the button is as tall as its lines");
        }
    }

    [Fact]
    public void Character_wrap_breaks_a_text_without_spaces()
    {
        const string NoSpaces = "Supercalifragilisticexpialidocious-and-then-some-more";
        var view = Show(new Button { Text = NoSpaces, LineBreakMode = LineBreakMode.CharacterWrap, WidthRequest = 120, HorizontalOptions = LayoutOptions.Start }, out var host);
        using (host)
        {
            view.DisplayedLines.Count.Should().BeGreaterThan(1);
            string.Concat(view.DisplayedLines).Should().Be(NoSpaces);
        }
    }

    [Fact]
    public void A_text_that_fits_and_the_single_line_modes_draw_one_line()
    {
        var fits = Show(new Button { Text = "OK", LineBreakMode = LineBreakMode.WordWrap }, out var fitsHost);
        using (fitsHost)
            fits.DisplayedLines.Should().Equal("OK");

        var noWrap = Show(new Button { Text = Long, LineBreakMode = LineBreakMode.NoWrap, WidthRequest = 160, HorizontalOptions = LayoutOptions.Start }, out var noWrapHost);
        using (noWrapHost)
            noWrap.DisplayedLines.Should().Equal(Long);
    }
}
