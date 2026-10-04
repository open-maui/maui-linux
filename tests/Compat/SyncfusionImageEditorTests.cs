// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform.Linux.Syncfusion;
using SkiaSharp;
using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.ImageEditor;
using Xunit;

namespace OpenMaui.Compat.Tests;

/// <summary>
/// SfImageEditor on Linux edits the picture, as its Windows build does: the edited image comes
/// back from GetImageStream and Save with its rotations, flips, crops and effects, undo restores
/// a crop, and the view shows what the edited image holds. The test picture is 200x100: red
/// top-left quarter, blue bottom-right quarter, grey bottom-left quarter, white elsewhere.
/// </summary>
[Collection("LinuxApplication.Current")]
public sealed class SyncfusionImageEditorTests
{
    private static readonly SKColor Grey = new(128, 128, 128);

    private static byte[] Picture()
    {
        using var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, 100, 50, paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(100, 50, 100, 50, paint);
            paint.Color = Grey;
            canvas.DrawRect(0, 50, 100, 50, paint);
        }
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static (CompatHost Host, SfImageEditor Editor) Host()
    {
        var png = Picture();
        var editor = new SfImageEditor { Source = ImageSource.FromStream(() => new MemoryStream(png)) };
        var host = new CompatHost(new ContentPage { Content = editor }, b => b.UseLinuxSyncfusion(), 600, 500);
        Pump(host);
        return (host, editor);
    }

    private static void Pump(CompatHost host, int frames = 15)
    {
        for (int i = 0; i < frames; i++)
        {
            Thread.Sleep(20);
            Microsoft.Maui.Platform.Linux.Hosting.LinuxTicker.PumpAll(); // the flip and rotate animations
            host.Render();
        }
    }

    private static SKBitmap Edited(SfImageEditor editor)
    {
        using var stream = editor.GetImageStream().GetAwaiter().GetResult();
        stream.Should().NotBeSameAs(Stream.Null, "the editor hands back the edited image");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return SKBitmap.Decode(copy.ToArray());
    }

    // The editor's image view (internal ImageViewExt): ImageEditLayout.imageLayout.imageView.
    private static Image ImageView(SfImageEditor editor)
    {
        const System.Reflection.BindingFlags Field = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var editLayout = Descendants(editor).First(e => e.GetType().Name == "ImageEditLayout");
        var imageLayout = editLayout.GetType().GetField("imageLayout", Field)!.GetValue(editLayout)!;
        return (Image)imageLayout.GetType().GetField("imageView", Field)!.GetValue(imageLayout)!;
    }

    [Fact]
    public void The_image_view_has_the_Linux_image_editor_handler()
    {
        var (host, editor) = Host();
        using (host)
            ImageView(editor).Handler.Should().BeOfType<SfImageEditorBridgeHandler>();
    }

    [Fact]
    public void GetImageStream_returns_the_picture()
    {
        var (host, editor) = Host();
        using (host)
        {
            using var image = Edited(editor);
            image.Width.Should().Be(200);
            image.Height.Should().Be(100);
            image.GetPixel(10, 10).Should().Be(SKColors.Red);
            image.GetPixel(190, 90).Should().Be(SKColors.Blue);
        }
    }

    [Fact]
    public void Rotate_and_flip_change_the_edited_image_and_what_the_view_shows()
    {
        var (host, editor) = Host();
        using (host)
        {
            editor.Rotate();
            Pump(host);
            using (var rotated = Edited(editor))
            {
                rotated.Width.Should().Be(100, "a quarter turn swaps the sides");
                rotated.Height.Should().Be(200);
                rotated.GetPixel(90, 10).Should().Be(SKColors.Red, "clockwise: the top-left quarter goes to the top right");
                rotated.GetPixel(10, 190).Should().Be(SKColors.Blue);
            }

            editor.Flip(ImageFlipDirection.Horizontal);
            Pump(host);
            using var flipped = Edited(editor);
            flipped.GetPixel(10, 10).Should().Be(SKColors.Red, "the flip mirrors the rotated picture left to right");
            flipped.GetPixel(90, 190).Should().Be(SKColors.Blue);

            // The view shows the same: the red quarter at the top left of the displayed picture.
            // The view is turned a quarter about its centre, so it shows portrait over landscape bounds.
            var bounds = CompatHost.RectOf(ImageView(editor));
            int w = bounds.Height, h = bounds.Width;
            var shown = new SKRectI(bounds.MidX - w / 2, bounds.MidY - h / 2, bounds.MidX + w / 2, bounds.MidY + h / 2);
            int quarterW = w / 2, quarterH = h / 2;
            host.CountPixelsNear(SKColors.Red, new SKRectI(shown.Left + 4, shown.Top + 4, shown.Left + quarterW - 4, shown.Top + quarterH - 4))
                .Should().BeGreaterThan((quarterW - 8) * (quarterH - 8) * 9 / 10);
            host.CountPixelsNear(SKColors.Blue, new SKRectI(shown.Left + quarterW + 4, shown.Top + quarterH + 4, shown.Right - 4, shown.Bottom - 4))
                .Should().BeGreaterThan((quarterW - 8) * (quarterH - 8) * 9 / 10);
        }
    }

    [Fact]
    public void A_crop_replaces_the_image_and_undo_restores_it()
    {
        var (host, editor) = Host();
        using (host)
        {
            var shown = ImageView(editor).Bounds;
            // Half the picture's width and height, from its top-left corner (in the view's size).
            editor.Crop(new Microsoft.Maui.Graphics.Rect(0, 0, shown.Width / 2, shown.Height / 2));
            Pump(host);
            editor.SaveEdits();
            Pump(host);
            using (var cropped = Edited(editor))
            {
                cropped.Width.Should().BeInRange(98, 100);
                cropped.Height.Should().BeInRange(48, 50);
                cropped.GetPixel(cropped.Width / 2, cropped.Height / 2).Should().Be(SKColors.Red);
            }

            editor.Undo();
            Pump(host);
            using var restored = Edited(editor);
            restored.Width.Should().Be(200);
            restored.GetPixel(190, 90).Should().Be(SKColors.Blue);
        }
    }

    [Fact]
    public void A_brightness_effect_brightens_the_saved_image()
    {
        var (host, editor) = Host();
        using (host)
        {
            editor.ImageEffect(ImageEffect.Brightness, 0.5);
            Pump(host);
            editor.SaveEdits();
            Pump(host);
            using var bright = Edited(editor);
            bright.GetPixel(50, 75).Red.Should().BeGreaterThan(200, "Win2D's brightness at 0.5 maps half grey towards white");
            bright.GetPixel(150, 25).Should().Be(SKColors.White);
        }
    }

    [Fact]
    public void Save_writes_the_edited_image_to_the_given_folder()
    {
        var (host, editor) = Host();
        using (host)
        {
            var folder = Path.Combine(Path.GetTempPath(), "openmaui-imageeditor-" + Guid.NewGuid().ToString("N"));
            var saved = new List<string>();
            editor.ImageSaved += (_, e) => saved.Add(e.Location);
            try
            {
                editor.Rotate();
                Pump(host);
                editor.Save(ImageFileType.Png, folder, "edited");
                for (int i = 0; i < 20 && saved.Count == 0; i++)
                    Pump(host, 2);

                saved.Should().ContainSingle();
                saved[0].Should().Be(Path.Combine(folder, "edited.png"));
                using (var file = SKBitmap.Decode(saved[0]))
                {
                    file.Width.Should().Be(100);
                    file.Height.Should().Be(200);
                }

                // The editor hides its "saved" notice 3 s later; the app must still be running then.
                var waited = System.Diagnostics.Stopwatch.StartNew();
                while (waited.Elapsed < TimeSpan.FromSeconds(3.6))
                    Pump(host, 1);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void GetStreamAsync_renders_a_view()
    {
        var box = new BoxView { Color = Microsoft.Maui.Graphics.Colors.Red, WidthRequest = 40, HeightRequest = 30 };
        using var host = new CompatHost(new ContentPage { Content = new VerticalStackLayout { Children = { box } } }, b => b.UseLinuxSyncfusion(), 200, 200);
        host.Render();
        using var stream = box.GetStreamAsync(Syncfusion.Maui.Core.ImageFileFormat.Png).GetAwaiter().GetResult();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        using var image = SKBitmap.Decode(copy.ToArray());
        image.Should().NotBeNull();
        image.GetPixel(image.Width / 2, image.Height / 2).Should().Be(SKColors.Red);
    }

    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }
}
