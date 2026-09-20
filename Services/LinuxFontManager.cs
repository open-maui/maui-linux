// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Linux implementation of <see cref="IFontManager"/>. On the generic TFM the
/// interface only exposes <see cref="DefaultFontSize"/>; this class adds the
/// platform-side resolution of a MAUI <see cref="Font"/> (family + weight +
/// slant) to an <see cref="SKTypeface"/>, consulting the
/// <see cref="LinuxFontRegistrar"/> before fontconfig.
/// </summary>
public sealed class LinuxFontManager : IFontManager
{
    /// <summary>
    /// Default label font size in device-independent points; matches
    /// <see cref="LinuxFontNamedSizeService"/>' <c>NamedSize.Default</c>.
    /// </summary>
    public const double PlatformDefaultFontSize = 14;

    /// <summary>
    /// Family name used when a <see cref="Font"/> has no family: fontconfig's
    /// generic sans-serif alias.
    /// </summary>
    public const string DefaultFontFamily = "Sans";

    private readonly LinuxFontRegistrar _registrar;

    /// <summary>
    /// Creates a manager backed by the process-wide <see cref="LinuxFontRegistrar.Instance"/>.
    /// </summary>
    public LinuxFontManager()
        : this(null)
    {
    }

    /// <summary>
    /// Creates a manager backed by <paramref name="registrar"/>. MAUI's DI hands
    /// in the <see cref="IFontRegistrar"/> singleton; anything that is not a
    /// <see cref="LinuxFontRegistrar"/> falls back to the process-wide instance.
    /// </summary>
    public LinuxFontManager(IFontRegistrar? registrar)
    {
        _registrar = registrar as LinuxFontRegistrar ?? LinuxFontRegistrar.Instance;
    }

    /// <inheritdoc />
    public double DefaultFontSize => PlatformDefaultFontSize;

    /// <summary>The registrar this manager resolves against.</summary>
    public LinuxFontRegistrar Registrar => _registrar;

    /// <summary>
    /// Resolves <paramref name="font"/> to a typeface: registered alias/family
    /// first, then fontconfig by family name and style, then the Skia default.
    /// </summary>
    public SKTypeface GetTypeface(Font font)
    {
        var family = string.IsNullOrWhiteSpace(font.Family) ? DefaultFontFamily : font.Family!;
        return GetTypeface(family, ToSKFontStyle(font));
    }

    /// <summary>
    /// Resolves a family name and Skia style to a typeface with the same
    /// precedence as <see cref="GetTypeface(Font)"/>.
    /// </summary>
    public SKTypeface GetTypeface(string? fontFamily, SKFontStyle style)
    {
        var family = string.IsNullOrWhiteSpace(fontFamily) ? DefaultFontFamily : fontFamily!;
        return _registrar.TryGetTypeface(family, style)
            ?? SKTypeface.FromFamilyName(family, style)
            ?? SKTypeface.Default;
    }

    /// <summary>
    /// Effective font size for <paramref name="font"/>: its own size when
    /// positive, otherwise <see cref="DefaultFontSize"/>.
    /// </summary>
    public double GetFontSize(Font font)
        => font.Size > 0 ? font.Size : DefaultFontSize;

    /// <summary>
    /// Maps a MAUI <see cref="Font"/>'s weight and slant to an <see cref="SKFontStyle"/>.
    /// <see cref="FontWeight"/> values are the CSS numeric weights (100-900)
    /// and map one-to-one onto <see cref="SKFontStyleWeight"/>.
    /// </summary>
    public static SKFontStyle ToSKFontStyle(Font font)
        => ToSKFontStyle(font.Weight, font.Slant);

    /// <summary>
    /// Maps a weight and slant pair to an <see cref="SKFontStyle"/>.
    /// </summary>
    public static SKFontStyle ToSKFontStyle(FontWeight weight, FontSlant slant)
    {
        int numericWeight = (int)weight;
        if (numericWeight <= 0)
            numericWeight = (int)SKFontStyleWeight.Normal;

        var skSlant = slant switch
        {
            FontSlant.Italic => SKFontStyleSlant.Italic,
            FontSlant.Oblique => SKFontStyleSlant.Oblique,
            _ => SKFontStyleSlant.Upright,
        };

        return new SKFontStyle((SKFontStyleWeight)numericWeight, SKFontStyleWidth.Normal, skSlant);
    }
}
