// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// A built-in service that also takes the logical size the view asks for: an SVG or a
/// font glyph is rendered at the size it is shown at, which the public load method
/// (like MAUI's on every platform) does not carry.
/// </summary>
internal interface ISizedImageSourceService
{
    Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(
        IImageSource imageSource, float scale, Size requestedSize, CancellationToken cancellationToken);
}

/// <summary>
/// Loads <see cref="IFileImageSource"/> on Linux: an image embedded in a loaded assembly,
/// else a file found by <see cref="ImageFileResolver"/> (next to the app, under
/// Resources/Images, and an SVG for a <c>.png</c> reference, since Linux ships the SVG
/// MAUI converts to PNG on the other platforms). Raster images (animated GIFs included)
/// are decoded once and shared through the image cache; SVGs are rendered at the scale
/// asked for.
/// </summary>
public class LinuxFileImageSourceService : FileImageSourceService, ILinuxImageSourceService, ISizedImageSourceService
{
    /// <summary>Creates the service.</summary>
    public LinuxFileImageSourceService(ILogger<LinuxFileImageSourceService>? logger = null)
        : base()
    {
        _logger = logger;
    }

    private readonly ILogger? _logger;

    /// <inheritdoc/>
    public virtual Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default) =>
        ((ISizedImageSourceService)this).GetImageAsync(imageSource, scale, Size.Zero, cancellationToken);

    async Task<IImageSourceServiceResult<SKBitmap>?> ISizedImageSourceService.GetImageAsync(
        IImageSource imageSource, float scale, Size requestedSize, CancellationToken cancellationToken)
    {
        if (imageSource is not IFileImageSource fileSource || string.IsNullOrEmpty(fileSource.File))
            return null;

        try
        {
            return await LoadFileAsync(fileSource.File, scale, requestedSize, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Unable to load image file '{File}'.", fileSource.File);
            throw;
        }
    }

    internal static async Task<LinuxImageSourceServiceResult> LoadFileAsync(
        string file, float scale, Size requestedSize, CancellationToken cancellationToken)
    {
        // Embedded resources first: the MAUI convention for app images.
        var (embedded, embeddedIsSvg) = LinuxImageLoader.OpenEmbeddedResource(file);
        if (embedded != null)
        {
            using (embedded)
            {
                if (embeddedIsSvg)
                {
                    var svg = await Task.Run(() => LinuxImageLoader.RenderSvgStream(embedded, requestedSize.Width, requestedSize.Height, scale), cancellationToken)
                        ?? throw new InvalidOperationException($"Unable to render the SVG resource for '{file}'.");
                    return new LinuxImageSourceServiceResult(svg, true, svg.Dispose);
                }

                var key = "embedded:" + file;
                return LinuxImageCache.TryGet(key)
                    ?? LinuxImageCache.Add(key, await DecodeAsync(embedded, file, cancellationToken));
            }
        }

        var path = ImageFileResolver.ResolvePath(file);
        if (path == null)
        {
            DiagnosticLog.Warn("ImageSources", $"File not found: {file}");
            throw new FileNotFoundException("Unable to find the image file.", file);
        }

        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var svg = await Task.Run(() => LinuxImageLoader.RenderSvgFile(path, requestedSize.Width, requestedSize.Height, scale), cancellationToken)
                ?? throw new InvalidOperationException($"Unable to render the SVG file '{path}'.");
            return new LinuxImageSourceServiceResult(svg, true, svg.Dispose) { SvgPath = path };
        }

        var cached = LinuxImageCache.TryGet(path);
        if (cached != null)
            return cached;

        var decoded = await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            return LinuxImageLoader.Decode(stream);
        }, cancellationToken);
        return LinuxImageCache.Add(path, decoded ?? throw new InvalidOperationException($"Unable to decode the image file '{path}'."));
    }

    private static async Task<DecodedImage> DecodeAsync(Stream stream, string name, CancellationToken cancellationToken) =>
        await Task.Run(() => LinuxImageLoader.Decode(stream), cancellationToken)
        ?? throw new InvalidOperationException($"Unable to decode the image '{name}'.");
}

/// <summary>
/// Loads <see cref="IUriImageSource"/> on Linux: downloads and decodes the image
/// (animated GIFs included) and, unless the source turns caching off, shares it through
/// the image cache keyed by URI.
/// </summary>
public class LinuxUriImageSourceService : UriImageSourceService, ILinuxImageSourceService
{
    private static readonly Lazy<HttpClient> s_http = new(() => new HttpClient());
    private readonly ILogger? _logger;

    /// <summary>Creates the service.</summary>
    public LinuxUriImageSourceService(ILogger<LinuxUriImageSourceService>? logger = null)
        : base()
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
    {
        if (imageSource is not IUriImageSource uriSource || uriSource.Uri is null)
            return null;

        try
        {
            return await LoadUriAsync(uriSource.Uri, uriSource.CachingEnabled, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Unable to load image URI '{Uri}'.", uriSource.Uri);
            throw;
        }
    }

    internal static async Task<LinuxImageSourceServiceResult> LoadUriAsync(Uri uri, bool cachingEnabled, CancellationToken cancellationToken)
    {
        var key = uri.ToString();
        if (cachingEnabled && LinuxImageCache.TryGet(key) is { } cached)
            return cached;

        var data = await s_http.Value.GetByteArrayAsync(uri, cancellationToken);
        var decoded = await Task.Run(() =>
        {
            using var stream = new MemoryStream(data);
            return LinuxImageLoader.Decode(stream);
        }, cancellationToken) ?? throw new InvalidOperationException($"Unable to decode the image at '{uri}'.");

        return cachingEnabled ? LinuxImageCache.Add(key, decoded) : LinuxImageSourceServices.Owned(decoded);
    }
}

/// <summary>
/// Loads <see cref="IStreamImageSource"/> on Linux: decodes the stream (animated GIFs
/// included). Streams are not cached; the bitmap belongs to the result.
/// </summary>
public class LinuxStreamImageSourceService : StreamImageSourceService, ILinuxImageSourceService
{
    private readonly ILogger? _logger;

    /// <summary>Creates the service.</summary>
    public LinuxStreamImageSourceService(ILogger<LinuxStreamImageSourceService>? logger = null)
        : base()
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
    {
        if (imageSource is not IStreamImageSource streamSource || streamSource.IsEmpty)
            return null;

        try
        {
            using var stream = await streamSource.GetStreamAsync(cancellationToken)
                ?? throw new InvalidOperationException("The stream image source returned no stream.");
            cancellationToken.ThrowIfCancellationRequested();
            return await LoadStreamAsync(stream, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Unable to load image stream.");
            throw;
        }
    }

    internal static async Task<LinuxImageSourceServiceResult> LoadStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        var decoded = await Task.Run(() => LinuxImageLoader.Decode(stream), cancellationToken)
            ?? throw new InvalidOperationException("Unable to decode the image stream.");
        return LinuxImageSourceServices.Owned(decoded);
    }
}

/// <summary>
/// Loads <see cref="IFontImageSource"/> on Linux: draws the glyph, in the source's colour,
/// into a square bitmap at the scale asked for. The font family resolves through the fonts
/// the app registered with ConfigureFonts (alias or family name), then installed fonts.
/// </summary>
public class LinuxFontImageSourceService : FontImageSourceService, ILinuxImageSourceService, ISizedImageSourceService
{
    private const double DefaultSize = 24.0;
    private const int MinimumSize = 16;

    /// <summary>Creates the service.</summary>
    public LinuxFontImageSourceService(IFontManager fontManager, ILogger<LinuxFontImageSourceService>? logger = null)
        : base(fontManager)
    {
    }

    /// <inheritdoc/>
    public virtual Task<IImageSourceServiceResult<SKBitmap>?> GetImageAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default) =>
        ((ISizedImageSourceService)this).GetImageAsync(imageSource, scale, Size.Zero, cancellationToken);

    Task<IImageSourceServiceResult<SKBitmap>?> ISizedImageSourceService.GetImageAsync(
        IImageSource imageSource, float scale, Size requestedSize, CancellationToken cancellationToken)
    {
        // Rendered inline: a glyph is cheap, and the picture is in place when the load returns.
        if (imageSource is not IFontImageSource fontSource)
            return Task.FromResult<IImageSourceServiceResult<SKBitmap>?>(null);
        cancellationToken.ThrowIfCancellationRequested();

        var bitmap = RenderGlyph(fontSource, requestedSize.Width, requestedSize.Height, scale);
        IImageSourceServiceResult<SKBitmap>? result = bitmap == null
            ? null
            : new LinuxImageSourceServiceResult(bitmap, true, bitmap.Dispose);
        return Task.FromResult(result);
    }

    /// <summary>
    /// The glyph in a square of the larger requested dimension (24 when none, 16 at
    /// least), times <paramref name="scale"/> pixels; null for an empty glyph.
    /// </summary>
    internal static SKBitmap? RenderGlyph(IFontImageSource fontSource, double requestedWidth, double requestedHeight, float scale = 1)
    {
        string glyph = fontSource.Glyph;
        if (string.IsNullOrEmpty(glyph))
            return null;

        int logical = (int)Math.Max(requestedWidth > 0 ? requestedWidth : DefaultSize, requestedHeight > 0 ? requestedHeight : DefaultSize);
        logical = Math.Max(logical, MinimumSize);
        int size = Math.Max(1, (int)Math.Round(logical * (scale > 0 ? scale : 1f)));

        SKColor color = fontSource.Color?.ToSKColor() ?? SKColors.Black;
        var bitmap = new SKBitmap(size, size, false);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var family = fontSource.Font.Family;
        SKTypeface? typeface = null;
        if (!string.IsNullOrEmpty(family))
        {
            // Icon fonts registered through ConfigureFonts (AddFont("fa-solid.otf",
            // "FontAwesome")) resolve by alias or family name through the same
            // registrar the label renderer uses.
            typeface = LinuxFontRegistrar.Instance.TryGetTypeface(family, SKFontStyle.Normal);

            if (typeface == null)
            {
                string[] fontPaths =
                {
                    "/usr/share/fonts/truetype/" + family + ".ttf",
                    "/usr/share/fonts/opentype/" + family + ".otf",
                    "/usr/local/share/fonts/" + family + ".ttf",
                    Path.Combine(AppContext.BaseDirectory, family + ".ttf"),
                };

                foreach (string path in fontPaths)
                {
                    if (File.Exists(path))
                    {
                        typeface = SKTypeface.FromFile(path, 0);
                        if (typeface != null)
                            break;
                    }
                }

                typeface ??= SKTypeface.FromFamilyName(family);
            }
        }

        typeface ??= SKTypeface.Default;

        float fontSize = size * 0.8f;
        using SKFont font = Rendering.SkiaFontFactory.Create(typeface, fontSize);
        using SKPaint paint = new SKPaint
        {
            Color = color,
            IsAntialias = true
        };

        // symbol: ink-centering intentional (FontImageSource icon glyph is optically centered by its ink bounds)
        font.MeasureText(glyph, out SKRect bounds, paint);
        float x = size / 2f;
        float y = (size - bounds.Top - bounds.Bottom) / 2f;
        canvas.DrawText(glyph, x, y, SKTextAlign.Center, font, paint);

        return bitmap;
    }
}

/// <summary>
/// The built-in services: registered with ConfigureImageSources by UseLinux, and the
/// fallback for the four MAUI source kinds when a handler has no Linux service from DI
/// (no MauiContext, or MAUI's own platform-neutral service registered after UseLinux).
/// </summary>
internal static class LinuxImageSourceServices
{
    private static readonly Lazy<LinuxFileImageSourceService> s_file = new(() => new LinuxFileImageSourceService());
    private static readonly Lazy<LinuxUriImageSourceService> s_uri = new(() => new LinuxUriImageSourceService());
    private static readonly Lazy<LinuxStreamImageSourceService> s_stream = new(() => new LinuxStreamImageSourceService());
    private static readonly Lazy<LinuxFontImageSourceService> s_font = new(() =>
        new LinuxFontImageSourceService(new LinuxFontManager(LinuxFontRegistrar.Instance)));

    public static LinuxFileImageSourceService File => s_file.Value;

    /// <summary>The built-in service for one of MAUI's source kinds, or null.</summary>
    public static ILinuxImageSourceService? ForSource(IImageSource source) => source switch
    {
        IFileImageSource => s_file.Value,
        IUriImageSource => s_uri.Value,
        IStreamImageSource => s_stream.Value,
        IFontImageSource => s_font.Value,
        _ => null,
    };

    /// <summary>
    /// The service that loads <paramref name="source"/>: the one registered for its type
    /// when it has a Linux load method, else the built-in one for MAUI's source kinds.
    /// </summary>
    /// <exception cref="NotSupportedException">No service can load the source on Linux.</exception>
    public static ILinuxImageSourceService Resolve(IServiceProvider? services, IImageSource source)
    {
        IImageSourceService? registered = null;
        var provider = services?.GetService<IImageSourceServiceProvider>();
        if (provider != null)
        {
            try
            {
                registered = provider.GetImageSourceService(source.GetType());
            }
            catch (InvalidOperationException)
            {
                // No mapping for this source type: MAUI's provider throws rather than return null.
            }
        }

        if (registered is ILinuxImageSourceService linux)
            return linux;

        return ForSource(source) ?? throw new NotSupportedException(registered is null
            ? $"No image source service is registered for {source.GetType().Name}. Register one with ConfigureImageSources(services => services.AddService<TSource, TService>())."
            : $"{registered.GetType().Name} cannot load images on Linux: it does not implement {nameof(ILinuxImageSourceService)}.");
    }

    /// <summary>
    /// Loads <paramref name="source"/> through its service: the one path every consumer takes
    /// (Image and ImageButton, Button images, toolbar and flyout icons). The requested size only
    /// reaches the built-in file and font services, so SVGs and glyphs render at the size they are
    /// shown; a subclass that overrides GetImageAsync gets its override called.
    /// </summary>
    internal static async Task<IImageSourceServiceResult<SKBitmap>?> LoadAsync(
        IServiceProvider? services, IImageSource source, float scale, Size requestedSize, CancellationToken token)
    {
        var service = Resolve(services, source);
        return service is ISizedImageSourceService sized
            && (service.GetType() == typeof(LinuxFileImageSourceService) || service.GetType() == typeof(LinuxFontImageSourceService))
            ? await sized.GetImageAsync(source, scale, requestedSize, token).ConfigureAwait(true)
            : await service.GetImageAsync(source, scale, token).ConfigureAwait(true);
    }

    /// <summary>The app's services, the fallback for a consumer without a handler of its own.</summary>
    internal static IServiceProvider? AppServices =>
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services;

    /// <summary>The services of the MAUI view <paramref name="view"/> renders, else the app's.</summary>
    internal static IServiceProvider? ServicesFor(SkiaView view) =>
        view.MauiView?.Handler?.MauiContext?.Services ?? AppServices;

    /// <summary>A result that owns a freshly decoded image.</summary>
    public static LinuxImageSourceServiceResult Owned(DecodedImage decoded)
    {
        // Frames of an animation are left to the finalizer: an animation timer tick may
        // still be reading one when the view lets the result go.
        Action? dispose = decoded.Frames == null ? decoded.Bitmap.Dispose : null;
        return new LinuxImageSourceServiceResult(decoded.Bitmap, false, dispose) { Frames = decoded.Frames };
    }

    /// <summary>Registers the built-in services for MAUI's source interfaces and Controls' source types.</summary>
    public static void Register(Microsoft.Maui.Hosting.IImageSourceServiceCollection services)
    {
        // The Controls types too: UseMauiApp maps them to MAUI's own (platform-neutral,
        // load-less) services, and an exact type mapping wins over an interface one.
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<IFileImageSource>(services, sp => new LinuxFileImageSourceService(Logger<LinuxFileImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<Microsoft.Maui.Controls.FileImageSource>(services, sp => new LinuxFileImageSourceService(Logger<LinuxFileImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<IUriImageSource>(services, sp => new LinuxUriImageSourceService(Logger<LinuxUriImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<Microsoft.Maui.Controls.UriImageSource>(services, sp => new LinuxUriImageSourceService(Logger<LinuxUriImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<IStreamImageSource>(services, sp => new LinuxStreamImageSourceService(Logger<LinuxStreamImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<Microsoft.Maui.Controls.StreamImageSource>(services, sp => new LinuxStreamImageSourceService(Logger<LinuxStreamImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<IFontImageSource>(services, sp => new LinuxFontImageSourceService(FontManager(sp), Logger<LinuxFontImageSourceService>(sp)));
        Microsoft.Maui.Hosting.ImageSourceServiceCollectionExtensions.AddService<Microsoft.Maui.Controls.FontImageSource>(services, sp => new LinuxFontImageSourceService(FontManager(sp), Logger<LinuxFontImageSourceService>(sp)));
    }

    private static ILogger<T>? Logger<T>(IServiceProvider services) =>
        services.GetService<ILoggerFactory>()?.CreateLogger<T>();

    private static IFontManager FontManager(IServiceProvider services) =>
        services.GetService<IFontManager>() ?? new LinuxFontManager(LinuxFontRegistrar.Instance);
}
