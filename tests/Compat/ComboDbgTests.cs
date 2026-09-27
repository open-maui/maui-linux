using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace OpenMaui.Compat.Tests;

[Collection(CompatHost.Collection)]
public sealed class ComboDbgTests
{
    private readonly ITestOutputHelper _out;
    public ComboDbgTests(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Dbg()
    {
        var combo = new Syncfusion.Maui.Inputs.SfComboBox
        {
            WidthRequest = 220, HeightRequest = 44, Margin = new Thickness(60, 50, 0, 0),
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            ItemsSource = new[] { "Alpha", "Bravo" }, SelectedIndex = 0,
        };
        using var host = new CompatHost(new ContentPage { BackgroundColor = Microsoft.Maui.Graphics.Colors.White, Content = new VerticalStackLayout { combo } }, b => b.UseLinuxSyncfusion(), 400, 200);
        for (int i = 0; i < 4; i++) host.Render();
        var root = host.RootView!;
        using var bmp = new SKBitmap(400, 200);
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.White);
        root.Draw(c);
        int n = 0; for (int y = 54; y < 90; y++) for (int x = 64; x < 276; x++) { var p = bmp.GetPixel(x, y); if (p.Red < 200) n++; }
        _out.WriteLine($"offscreen dark pixels={n}");
        using (var f = File.OpenWrite("/tmp/claude-1000/-home-logikonline-Documents-Gitea/f31e418a-42c5-4638-9e39-ccb0289874a1/scratchpad/off.png")) bmp.Encode(f, SKEncodedImageFormat.Png, 100);
        var entry = (SkiaView)((Microsoft.Maui.Platform.SkiaLayoutView)CompatHost.PlatformOf(combo)).Children[1];
        var input = (Entry)entry.MauiView!;
        _out.WriteLine($"entry text={(entry as SkiaEntry)?.Text} opacity={entry.Opacity} maui={combo.Text} tc={input.TextColor} skTc={(entry as SkiaEntry)?.TextColor} bg={input.Background} bgc={input.BackgroundColor} op={input.Opacity} fs={input.FontSize}");
        foreach (var ch in ((SkiaLayoutView)CompatHost.PlatformOf(combo)).Children)
        {
            using var b2 = new SKBitmap(400, 200); using var c2 = new SKCanvas(b2); c2.Clear(SKColors.White);
            ch.Draw(c2);
            int m = 0; for (int y = 0; y < 200; y++) for (int x = 0; x < 400; x++) { var p = b2.GetPixel(x, y); if (p.Red < 200 || p.Blue < 200) m++; }
            _out.WriteLine($"child {ch.GetType().Name}/{ch.MauiView?.GetType().Name} drawn px={m} clip={ch.MauiView?.Clip}");
        }
    }
}
