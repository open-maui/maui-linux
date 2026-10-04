// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

/// <summary>
/// The icons of a tab bar, loaded through their image-source services (file, font, URI, stream,
/// or an app's own source) at the tab icon size and the screen's density, as the other platforms
/// load tab icons. A tab bar asks for an icon while drawing; null while it loads, and the owner
/// repaints when it arrives. Icons no longer asked for are released by <see cref="Retain"/>.
/// </summary>
internal sealed class TabIconCache
{
    private sealed class Entry
    {
        public IImageSourceServiceResult<SKBitmap>? Result;
        public CancellationTokenSource? Load;

        public void Release()
        {
            Load?.Cancel();
            Load = null;
            Result?.Dispose();
            Result = null;
        }
    }

    /// <summary>The logical size tab icons are loaded and drawn at.</summary>
    public const float IconSize = 24f;

    private readonly SkiaView _owner;
    private readonly Dictionary<IImageSource, Entry> _entries = new();

    public TabIconCache(SkiaView owner) => _owner = owner;

    /// <summary>The loaded icon of <paramref name="source"/>; starts loading it the first time.</summary>
    public SKBitmap? Get(IImageSource? source)
    {
        if (source == null || source.IsEmpty)
            return null;
        if (_entries.TryGetValue(source, out var entry))
            return entry.Result?.Value;
        entry = new Entry();
        _entries[source] = entry;
        _ = LoadAsync(source, entry);
        return null;
    }

    /// <summary>Releases the icons whose source is not in <paramref name="sources"/>.</summary>
    public void Retain(IEnumerable<IImageSource?> sources)
    {
        var keep = new HashSet<IImageSource>(sources.Where(s => s != null)!);
        foreach (var gone in _entries.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            _entries[gone].Release();
            _entries.Remove(gone);
        }
    }

    /// <summary>Releases every icon.</summary>
    public void Clear()
    {
        foreach (var entry in _entries.Values)
            entry.Release();
        _entries.Clear();
    }

    private async Task LoadAsync(IImageSource source, Entry entry)
    {
        var load = entry.Load = new CancellationTokenSource();
        try
        {
            var result = await LinuxImageSourceServices.LoadAsync(
                LinuxImageSourceServices.ServicesFor(_owner), source, Math.Max(1f, _owner.DeviceScale), new Size(IconSize, IconSize), load.Token);
            if (load.IsCancellationRequested)
            {
                result?.Dispose();
                return;
            }
            entry.Result = result;
            entry.Load = null;
            if (result == null)
                DiagnosticLog.Warn(_owner.GetType().Name, $"Tab icon not found: {source}");
            _owner.Invalidate();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error(_owner.GetType().Name, $"Loading a tab icon failed: {source}", ex);
        }
    }

    /// <summary>
    /// Draws <paramref name="icon"/> fitted into <paramref name="dest"/>, tinted with
    /// <paramref name="tint"/> (its alpha kept), as every platform tints tab icons with the tab's
    /// selected or unselected colour.
    /// </summary>
    public static void DrawTinted(SKCanvas canvas, SKBitmap icon, SKRect dest, SKColor tint)
    {
        float scale = Math.Min(dest.Width / icon.Width, dest.Height / icon.Height);
        float w = icon.Width * scale, h = icon.Height * scale;
        var rect = SKRect.Create(dest.MidX - w / 2, dest.MidY - h / 2, w, h);
        using var filter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.SrcIn);
        using var paint = new SKPaint { IsAntialias = true, ColorFilter = filter };
        using var image = SKImage.FromBitmap(icon);
        canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
    }
}
