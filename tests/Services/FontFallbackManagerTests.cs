// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;
using Xunit;

namespace Microsoft.Maui.Controls.Linux.Tests.Services;

/// <summary>
/// The fallback manager decides which typeface renders each codepoint. Its
/// cache used to be keyed by family name only, so after a regular label had
/// been drawn, italic and bold labels of the same family were handed the
/// regular face (italic text rendered upright). Found by the golden tests.
/// </summary>
public class FontFallbackManagerTests
{
    [Fact]
    public void Style_variants_of_one_family_are_not_conflated_by_the_glyph_cache()
    {
        var manager = FontFallbackManager.Instance;
        using var regular = SKTypeface.FromFamilyName("Sans", SKFontStyle.Normal);
        using var italic = SKTypeface.FromFamilyName("Sans", SKFontStyle.Italic);
        using var bold = SKTypeface.FromFamilyName("Sans", SKFontStyle.Bold);
        regular.FamilyName.Should().Be(italic.FamilyName, "the test needs two styles of the same family");

        // Prime the cache with the regular face first (the common app order).
        manager.GetTypefaceForCodepoint('a', regular).Should().BeSameAs(regular);

        manager.GetTypefaceForCodepoint('a', italic).Should().BeSameAs(italic);
        manager.GetTypefaceForCodepoint('a', bold).Should().BeSameAs(bold);
        manager.GetTypefaceForCodepoint('a', regular).Should().BeSameAs(regular);
    }

    [Fact]
    public void Shaping_keeps_the_preferred_style_for_covered_text()
    {
        var manager = FontFallbackManager.Instance;
        using var regular = SKTypeface.FromFamilyName("Sans", SKFontStyle.Normal);
        using var italic = SKTypeface.FromFamilyName("Sans", SKFontStyle.Italic);

        manager.ShapeTextWithFallback("Hello", regular);
        var runs = manager.ShapeTextWithFallback("Hello", italic);

        runs.Should().ContainSingle();
        runs[0].Typeface.Should().BeSameAs(italic);
        runs[0].Typeface.IsItalic.Should().BeTrue();
    }
}
