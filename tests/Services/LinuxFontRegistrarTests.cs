// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Rendering;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// Font registrar tests use real font files shipped with the OS. Each test
/// picks whichever family is installed (Noto Sans, then Liberation Sans, then
/// DejaVu Sans) and skips its assertions cleanly when none is present.
/// </summary>
public class LinuxFontRegistrarTests
{
    private static readonly (string Regular, string Bold, string Italic)[] Candidates =
    {
        ("/usr/share/fonts/google-noto/NotoSans-Regular.ttf", "/usr/share/fonts/google-noto/NotoSans-Bold.ttf", "/usr/share/fonts/google-noto/NotoSans-Italic.ttf"),
        ("/usr/share/fonts/liberation-sans-fonts/LiberationSans-Regular.ttf", "/usr/share/fonts/liberation-sans-fonts/LiberationSans-Bold.ttf", "/usr/share/fonts/liberation-sans-fonts/LiberationSans-Italic.ttf"),
        ("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf"),
        ("/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf", "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Oblique.ttf"),
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf"),
    };

    private static (string Regular, string Bold, string Italic)? Family =>
        Candidates.FirstOrDefault(c => File.Exists(c.Regular) && File.Exists(c.Bold) && File.Exists(c.Italic)) is var f && f.Regular != null ? f : null;

    private static string ExpectedFamilyName(string path)
    {
        using var face = SKTypeface.FromFile(path);
        return face.FamilyName;
    }

    [Fact]
    public void Register_AbsolutePath_ResolvesAliasToThatFace()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Italic, "TestItalicAlias");

        registrar.GetFont("TestItalicAlias").Should().Be(fam.Italic);

        var face = registrar.TryGetTypeface("TestItalicAlias");
        face.Should().NotBeNull();
        face!.FamilyName.Should().Be(ExpectedFamilyName(fam.Italic));
        face.FontStyle.Slant.Should().Be(SKFontStyleSlant.Italic);
    }

    [Fact]
    public void Register_FileNameOnly_ResolvesFromSearchDirectory()
    {
        if (Family is not { } fam) return;

        var dir = Path.GetDirectoryName(fam.Regular)!;
        var registrar = new LinuxFontRegistrar(new[] { dir });
        registrar.Register(Path.GetFileName(fam.Regular), "SearchDirAlias");

        registrar.GetFont("SearchDirAlias").Should().Be(fam.Regular);
        registrar.TryGetTypeface("SearchDirAlias").Should().NotBeNull();
    }

    [Fact]
    public void Register_FileNameWithoutExtension_ProbesKnownExtensions()
    {
        if (Family is not { } fam) return;

        var dir = Path.GetDirectoryName(fam.Regular)!;
        var registrar = new LinuxFontRegistrar(new[] { dir });
        registrar.Register(Path.GetFileNameWithoutExtension(fam.Regular), "NoExtAlias");

        registrar.GetFont("NoExtAlias").Should().Be(fam.Regular);
    }

    [Fact]
    public void Register_ResourcesFontsSubfolder_IsProbed()
    {
        if (Family is not { } fam) return;

        var root = Path.Combine(Path.GetTempPath(), "openmaui-fonts-" + Guid.NewGuid().ToString("N"));
        var fontsDir = Path.Combine(root, "Resources", "Fonts");
        Directory.CreateDirectory(fontsDir);
        var copied = Path.Combine(fontsDir, "MyApp-Regular.ttf");
        File.Copy(fam.Regular, copied);
        try
        {
            var registrar = new LinuxFontRegistrar(new[] { root, fontsDir });
            registrar.Register("MyApp-Regular.ttf", "MyAppRegular");

            registrar.GetFont("MyAppRegular").Should().Be(copied);
            registrar.TryGetTypeface("MyAppRegular").Should().NotBeNull();
            registrar.Clear();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetFont_MissingFile_ReturnsNull()
    {
        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register("DoesNotExist-Regular.ttf", "Missing");

        registrar.GetFont("Missing").Should().BeNull();
        registrar.TryGetTypeface("Missing").Should().BeNull();
        registrar.IsRegistered("Missing").Should().BeFalse();
    }

    [Fact]
    public void TryGetTypeface_UnknownName_ReturnsNull()
    {
        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.TryGetTypeface("Nope").Should().BeNull();
        registrar.TryGetTypeface(null).Should().BeNull();
        registrar.TryGetTypeface("").Should().BeNull();
    }

    [Fact]
    public void TryGetTypeface_ByFileName_Works()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Regular, "RegularAlias");

        registrar.TryGetTypeface(Path.GetFileName(fam.Regular)).Should().NotBeNull();
        registrar.TryGetTypeface(Path.GetFileNameWithoutExtension(fam.Regular)).Should().NotBeNull();
    }

    [Fact]
    public void TryGetTypeface_ByFamilyName_PicksClosestStyle()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Regular, "FamRegular");
        registrar.Register(fam.Bold, "FamBold");
        registrar.Register(fam.Italic, "FamItalic");
        var familyName = ExpectedFamilyName(fam.Regular);

        var bold = registrar.TryGetTypeface(familyName, SKFontStyle.Bold);
        bold.Should().NotBeNull();
        bold!.FontStyle.Weight.Should().Be((int)SKFontStyleWeight.Bold);

        var italic = registrar.TryGetTypeface(familyName, SKFontStyle.Italic);
        italic.Should().NotBeNull();
        italic!.FontStyle.Slant.Should().NotBe(SKFontStyleSlant.Upright);

        var regular = registrar.TryGetTypeface(familyName, SKFontStyle.Normal);
        regular.Should().NotBeNull();
        regular!.FontStyle.Weight.Should().Be((int)SKFontStyleWeight.Normal);
        regular.FontStyle.Slant.Should().Be(SKFontStyleSlant.Upright);
    }

    [Fact]
    public void TryGetTypeface_AliasWithBoldStyle_PrefersBoldSibling()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Regular, "SibRegular");
        registrar.Register(fam.Bold, "SibBold");

        var face = registrar.TryGetTypeface("SibRegular", SKFontStyle.Bold);
        face.Should().NotBeNull();
        face!.FontStyle.Weight.Should().Be((int)SKFontStyleWeight.Bold);

        // Without a bold sibling the alias face itself is returned.
        var lone = new LinuxFontRegistrar(Array.Empty<string>());
        lone.Register(fam.Regular, "LoneRegular");
        lone.TryGetTypeface("LoneRegular", SKFontStyle.Bold).Should().BeSameAs(lone.TryGetTypeface("LoneRegular"));
    }

    [Fact]
    public void TryGetTypeface_HashSuffixedFamily_UsesAliasPart()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Regular, "HashAlias");

        registrar.TryGetTypeface("Whatever.ttf#HashAlias").Should().NotBeNull();
    }

    [Fact]
    public void RegisteredFonts_ListsRegistrationsInOrder()
    {
        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register("a.ttf", "A");
        registrar.Register("b.ttf", "B");

        registrar.RegisteredFonts.Select(f => f.Alias).Should().Equal("A", "B");
        registrar.RegisteredFonts.Select(f => f.FileName).Should().Equal("a.ttf", "b.ttf");
    }

    [Fact]
    public void Register_EmptyFileName_Throws()
    {
        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        var act = () => registrar.Register("", "X");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResourceCache_ConsultsRegistrarBeforeFontconfig()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Italic, "CacheItalic");
        using var cache = new ResourceCache(registrar);

        var face = cache.GetTypeface("CacheItalic", SKFontStyle.Normal);
        face.Should().BeSameAs(registrar.TryGetTypeface("CacheItalic"));
        face.FamilyName.Should().Be(ExpectedFamilyName(fam.Italic));

        // Cached: same instance on the second call.
        cache.GetTypeface("CacheItalic", SKFontStyle.Normal).Should().BeSameAs(face);
    }

    [Fact]
    public void ResourceCache_Clear_DoesNotDisposeRegistrarFaces()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Regular, "KeepAlive");
        var cache = new ResourceCache(registrar);
        var face = cache.GetTypeface("KeepAlive", SKFontStyle.Normal);

        cache.Clear();
        cache.Dispose();

        // Still usable after the cache released it.
        face.Handle.Should().NotBe(IntPtr.Zero);
        face.FamilyName.Should().Be(ExpectedFamilyName(fam.Regular));
        registrar.TryGetTypeface("KeepAlive").Should().BeSameAs(face);
    }

    [Fact]
    public void ResourceCache_UnregisteredFamily_FallsBackToFontconfig()
    {
        using var cache = new ResourceCache(new LinuxFontRegistrar(Array.Empty<string>()));
        var face = cache.GetTypeface("Sans", SKFontStyle.Normal);
        face.Should().NotBeNull();

        using var noRegistrar = new ResourceCache(null);
        noRegistrar.GetTypeface("Sans", SKFontStyle.Bold).Should().NotBeNull();
    }

    [Fact]
    public void ResourceCache_Default_UsesProcessWideRegistrar()
    {
        if (Family is not { } fam) return;

        var alias = "ProcessWide_" + Guid.NewGuid().ToString("N");
        LinuxFontRegistrar.Instance.Register(fam.Bold, alias);
        using var cache = new ResourceCache();

        cache.GetTypeface(alias, SKFontStyle.Normal).Should().BeSameAs(LinuxFontRegistrar.Instance.TryGetTypeface(alias));
    }

    [Fact]
    public void FontManager_DefaultFontSize_Is14()
    {
        new LinuxFontManager().DefaultFontSize.Should().Be(14);
        new LinuxFontManager(null).DefaultFontSize.Should().Be(14);
    }

    [Theory]
    [InlineData(FontWeight.Thin, FontSlant.Default, 100, SKFontStyleSlant.Upright)]
    [InlineData(FontWeight.Regular, FontSlant.Default, 400, SKFontStyleSlant.Upright)]
    [InlineData(FontWeight.Bold, FontSlant.Italic, 700, SKFontStyleSlant.Italic)]
    [InlineData(FontWeight.Black, FontSlant.Oblique, 900, SKFontStyleSlant.Oblique)]
    public void FontManager_ToSKFontStyle_MapsWeightAndSlant(FontWeight weight, FontSlant slant, int expectedWeight, SKFontStyleSlant expectedSlant)
    {
        var style = LinuxFontManager.ToSKFontStyle(weight, slant);
        style.Weight.Should().Be(expectedWeight);
        style.Slant.Should().Be(expectedSlant);

        var font = Font.OfSize("X", 12, weight, slant);
        var viaFont = LinuxFontManager.ToSKFontStyle(font);
        viaFont.Weight.Should().Be(expectedWeight);
        viaFont.Slant.Should().Be(expectedSlant);
    }

    [Fact]
    public void FontManager_GetTypeface_ResolvesRegisteredFontObject()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(Array.Empty<string>());
        registrar.Register(fam.Regular, "MgrRegular");
        registrar.Register(fam.Bold, "MgrBold");
        var manager = new LinuxFontManager(registrar);
        manager.Registrar.Should().BeSameAs(registrar);

        var regular = manager.GetTypeface(Font.OfSize("MgrRegular", 12));
        regular.Should().BeSameAs(registrar.TryGetTypeface("MgrRegular"));

        var bold = manager.GetTypeface(Font.OfSize("MgrRegular", 12, FontWeight.Bold));
        bold.FontStyle.Weight.Should().Be((int)SKFontStyleWeight.Bold);

        manager.GetFontSize(Font.OfSize("MgrRegular", 12)).Should().Be(12);
        manager.GetFontSize(Font.Default).Should().Be(14);
    }

    [Fact]
    public void FontManager_GetTypeface_UnregisteredFamily_NeverReturnsNull()
    {
        var manager = new LinuxFontManager(new LinuxFontRegistrar(Array.Empty<string>()));
        manager.GetTypeface(Font.Default).Should().NotBeNull();
        manager.GetTypeface("Definitely Not A Font 123", SKFontStyle.Normal).Should().NotBeNull();
        manager.GetTypeface(null, SKFontStyle.Normal).Should().NotBeNull();
    }

    [Fact]
    public void ConfigureFonts_AddFont_FlowsThroughLinuxRegistrar()
    {
        if (Family is not { } fam) return;

        var registrar = new LinuxFontRegistrar(new[] { Path.GetDirectoryName(fam.Italic)! });
        var builder = MauiApp.CreateBuilder(useDefaults: true);

        // The exact registration lines the Linux host uses (MauiApp.CreateBuilder
        // has already TryAdd'ed MAUI's portable registrar, so Replace is required).
        builder.Services.Replace(ServiceDescriptor.Singleton<IFontRegistrar>(registrar));
        builder.Services.Replace(ServiceDescriptor.Singleton<IFontManager>(sp => new LinuxFontManager(sp.GetRequiredService<IFontRegistrar>())));

        builder.ConfigureFonts(fonts => fonts.AddFont(Path.GetFileName(fam.Italic), "ConfiguredItalic"));

        using var app = builder.Build();
        app.Services.GetRequiredService<IFontRegistrar>().Should().BeSameAs(registrar);
        app.Services.GetRequiredService<IFontManager>().Should().BeOfType<LinuxFontManager>();

        registrar.GetFont("ConfiguredItalic").Should().Be(fam.Italic);
        var face = registrar.TryGetTypeface("ConfiguredItalic");
        face.Should().NotBeNull();
        face!.FontStyle.Slant.Should().Be(SKFontStyleSlant.Italic);
    }
}
