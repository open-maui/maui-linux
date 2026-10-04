// Screenshot comparison for ParityHarness.Diff: each page's reference and candidate
// screenshots are brought to the page's size in device-independent units, compared pixel
// by pixel, and written side by side with a difference panel, so a reviewer sees what a
// layout dump cannot (colours, borders, fonts, clipping, missing drawing).

using System.Globalization;
using System.Text;
using SkiaSharp;

namespace ParityHarness.Diff;

internal static class Visual
{
    // A channel difference above this is a visible change, not antialiasing noise.
    private const int Threshold = 48;

    public static string Compare(List<string> names, Dictionary<string, PageDump> refPages,
        Dictionary<string, PageDump> candPages, Options o, string refName, string candName)
    {
        Directory.CreateDirectory(o.Images!);
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("# Visual comparison");
        sb.AppendLine();
        sb.AppendLine($"Screenshots at the page size, compared pixel by pixel (a pixel differs when a colour channel moves by more than {Threshold}). Each image is {refName} | {candName} | the differing pixels in red. Text is drawn by different rasterisers on each platform, so some difference is expected on every text page; look at the images, not only the numbers.");
        sb.AppendLine();
        sb.AppendLine("| Page | Differing pixels | Image |");
        sb.AppendLine("|---|---:|---|");
        string relDir = o.Out != null
            ? Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(o.Out))!, Path.GetFullPath(o.Images!))
            : o.Images!;
        foreach (var name in names)
        {
            refPages.TryGetValue(name, out var r);
            candPages.TryGetValue(name, out var c);
            string? refShot = Shot(o.Reference, r);
            string? candShot = Shot(o.Candidate, c);
            if (refShot == null || candShot == null)
            {
                sb.AppendLine($"| {name} | no screenshot from {(refShot == null ? refName : candName)} | |");
                continue;
            }
            var size = r?.PageSize ?? c?.PageSize ?? r?.Requested;
            int w = Math.Max(1, (int)Math.Round(size?.W ?? 800)), h = Math.Max(1, (int)Math.Round(size?.H ?? 600));
            try
            {
                double ratio = CompareOne(refShot, candShot, w, h, Path.Combine(o.Images!, name + ".png"));
                string link = Path.Combine(relDir, name + ".png").Replace('\\', '/');
                sb.AppendLine($"| {name} | {(ratio * 100).ToString("0.00", CultureInfo.InvariantCulture)}% | [{name}.png]({link}) |");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"| {name} | comparison failed: {ex.Message} | |");
            }
        }
        return sb.ToString();
    }

    private static string? Shot(string dir, PageDump? dump)
    {
        if (dump?.Screenshot == null)
            return null;
        string path = Path.Combine(dir, dump.Screenshot);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Writes reference | candidate | difference at w x h and returns the share of differing pixels.</summary>
    private static double CompareOne(string refPath, string candPath, int w, int h, string outPath)
    {
        using var a = Load(refPath, w, h);
        using var b = Load(candPath, w, h);
        using var diff = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        long differing = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = a.GetPixel(x, y);
                var q = b.GetPixel(x, y);
                int d = Math.Max(Math.Abs(p.Red - q.Red), Math.Max(Math.Abs(p.Green - q.Green), Math.Abs(p.Blue - q.Blue)));
                if (d > Threshold)
                {
                    differing++;
                    diff.SetPixel(x, y, new SKColor(230, 30, 30));
                }
                else
                {
                    // The reference, faded, so the red reads against the page.
                    byte g = (byte)(255 - (255 - (p.Red + p.Green + p.Blue) / 3) / 4);
                    diff.SetPixel(x, y, new SKColor(g, g, g));
                }
            }
        }

        const int gap = 12;
        using var surface = SKSurface.Create(new SKImageInfo(w * 3 + gap * 2, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(128, 128, 128));
        canvas.DrawBitmap(a, 0, 0);
        canvas.DrawBitmap(b, w + gap, 0);
        canvas.DrawBitmap(diff, (w + gap) * 2, 0);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(outPath);
        data.SaveTo(file);
        return (double)differing / ((long)w * h);
    }

    /// <summary>The screenshot scaled to the page size in device-independent units, on white.</summary>
    private static SKBitmap Load(string path, int w, int h)
    {
        using var source = SKBitmap.Decode(path) ?? throw new InvalidDataException($"cannot decode {Path.GetFileName(path)}");
        var target = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);
        using var img = SKImage.FromBitmap(source);
        canvas.DrawImage(img, new SKRect(0, 0, w, h), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return target;
    }
}
