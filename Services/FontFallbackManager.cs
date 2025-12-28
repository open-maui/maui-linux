// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

/// <summary>
/// Manages font fallback for text rendering when the primary font
/// doesn't contain glyphs for certain characters (emoji, CJK, etc.).
/// </summary>
public class FontFallbackManager
{
    private static FontFallbackManager? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of the font fallback manager.
    /// </summary>
    public static FontFallbackManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new FontFallbackManager();
                }
            }
            return _instance;
        }
    }

    // Fallback font chain ordered by priority
    private readonly string[] _fallbackFonts = new[]
    {
        // Primary sans-serif fonts
        "Noto Sans",
        "DejaVu Sans",
        "Liberation Sans",
        "FreeSans",

        // Emoji fonts
        "Noto Color Emoji",
        "Noto Emoji",
        "Symbola",
        "Segoe UI Emoji",

        // CJK fonts (Chinese, Japanese, Korean)
        "Noto Sans CJK SC",
        "Noto Sans CJK TC",
        "Noto Sans CJK JP",
        "Noto Sans CJK KR",
        "WenQuanYi Micro Hei",
        "WenQuanYi Zen Hei",
        "Droid Sans Fallback",

        // Arabic and RTL scripts
        "Noto Sans Arabic",
        "Noto Naskh Arabic",
        "DejaVu Sans",

        // Indic scripts
        "Noto Sans Devanagari",
        "Noto Sans Tamil",
        "Noto Sans Bengali",
        "Noto Sans Telugu",

        // Thai
        "Noto Sans Thai",
        "Loma",

        // Hebrew
        "Noto Sans Hebrew",

        // System fallbacks
        "Sans",
        "sans-serif"
    };

    // Cache for typeface lookups
    private readonly Dictionary<string, SKTypeface?> _typefaceCache = new();
    private readonly Dictionary<(int codepoint, string preferredFont), SKTypeface?> _glyphCache = new();

    private FontFallbackManager()
    {
        // Pre-cache common fallback fonts
        foreach (var fontName in _fallbackFonts.Take(10))
        {
            GetCachedTypeface(fontName);
        }
    }

    /// <summary>
    /// Gets a typeface that can render the specified codepoint.
    /// Falls back through the font chain if the preferred font doesn't support it.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint to render.</param>
    /// <param name="preferred">The preferred typeface to use.</param>
    /// <returns>A typeface that can render the codepoint, or the preferred typeface as fallback.</returns>
    public SKTypeface GetTypefaceForCodepoint(int codepoint, SKTypeface preferred)
    {
        // Check cache first
        var cacheKey = (codepoint, preferred.FamilyName);
        if (_glyphCache.TryGetValue(cacheKey, out var cached))
        {
            return cached ?? preferred;
        }

        // Check if preferred font has the glyph
        if (TypefaceContainsGlyph(preferred, codepoint))
        {
            _glyphCache[cacheKey] = preferred;
            return preferred;
        }

        // Search fallback fonts
        foreach (var fontName in _fallbackFonts)
        {
            var fallback = GetCachedTypeface(fontName);
            if (fallback != null && TypefaceContainsGlyph(fallback, codepoint))
            {
                _glyphCache[cacheKey] = fallback;
                return fallback;
            }
        }

        // No fallback found, return preferred (will show tofu)
        _glyphCache[cacheKey] = null;
        return preferred;
    }

    /// <summary>
    /// Gets a typeface that can render all codepoints in the text.
    /// For mixed scripts, use ShapeTextWithFallback instead.
    /// </summary>
    public SKTypeface GetTypefaceForText(string text, SKTypeface preferred)
    {
        if (string.IsNullOrEmpty(text))
            return preferred;

        // Check first non-ASCII character
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value > 127)
            {
                return GetTypefaceForCodepoint(rune.Value, preferred);
            }
        }

        return preferred;
    }

    /// <summary>
    /// Shapes text with automatic font fallback for mixed scripts.
    /// Returns a list of text runs, each with its own typeface.
    /// </summary>
    public List<TextRun> ShapeTextWithFallback(string text, SKTypeface preferred)
    {
        var runs = new List<TextRun>();
        if (string.IsNullOrEmpty(text))
            return runs;

        var currentRun = new StringBuilder();
        SKTypeface? currentTypeface = null;
        int runStart = 0;

        int charIndex = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var typeface = GetTypefaceForCodepoint(rune.Value, preferred);

            if (currentTypeface == null)
            {
                currentTypeface = typeface;
            }
            else if (typeface.FamilyName != currentTypeface.FamilyName)
            {
                // Typeface changed - save current run
                if (currentRun.Length > 0)
                {
                    runs.Add(new TextRun(currentRun.ToString(), currentTypeface, runStart));
                }
                currentRun.Clear();
                currentTypeface = typeface;
                runStart = charIndex;
            }

            currentRun.Append(rune.ToString());
            charIndex += rune.Utf16SequenceLength;
        }

        // Add final run
        if (currentRun.Length > 0 && currentTypeface != null)
        {
            runs.Add(new TextRun(currentRun.ToString(), currentTypeface, runStart));
        }

        return runs;
    }

    /// <summary>
    /// Checks if a typeface is available on the system.
    /// </summary>
    public bool IsFontAvailable(string fontFamily)
    {
        var typeface = GetCachedTypeface(fontFamily);
        return typeface != null && typeface.FamilyName.Equals(fontFamily, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a list of available fallback fonts on this system.
    /// </summary>
    public IEnumerable<string> GetAvailableFallbackFonts()
    {
        foreach (var fontName in _fallbackFonts)
        {
            if (IsFontAvailable(fontName))
            {
                yield return fontName;
            }
        }
    }

    private SKTypeface? GetCachedTypeface(string fontFamily)
    {
        if (_typefaceCache.TryGetValue(fontFamily, out var cached))
        {
            return cached;
        }

        var typeface = SKTypeface.FromFamilyName(fontFamily);

        // Check if we actually got the requested font or a substitution
        if (typeface != null && !typeface.FamilyName.Equals(fontFamily, StringComparison.OrdinalIgnoreCase))
        {
            // Got a substitution, don't cache it as the requested font
            typeface = null;
        }

        _typefaceCache[fontFamily] = typeface;
        return typeface;
    }

    private bool TypefaceContainsGlyph(SKTypeface typeface, int codepoint)
    {
        // Use SKFont to check glyph coverage
        using var font = new SKFont(typeface, 12);
        var glyphs = new ushort[1];
        var chars = char.ConvertFromUtf32(codepoint);
        font.GetGlyphs(chars, glyphs);

        // Glyph ID 0 is the "missing glyph" (tofu)
        return glyphs[0] != 0;
    }
}

/// <summary>
/// Represents a run of text with a specific typeface.
/// </summary>
public class TextRun
{
    /// <summary>
    /// The text content of this run.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The typeface to use for this run.
    /// </summary>
    public SKTypeface Typeface { get; }

    /// <summary>
    /// The starting character index in the original string.
    /// </summary>
    public int StartIndex { get; }

    public TextRun(string text, SKTypeface typeface, int startIndex)
    {
        Text = text;
        Typeface = typeface;
        StartIndex = startIndex;
    }
}

/// <summary>
/// StringBuilder for internal use.
/// </summary>
file class StringBuilder
{
    private readonly List<char> _chars = new();

    public int Length => _chars.Count;

    public void Append(string s)
    {
        _chars.AddRange(s);
    }

    public void Clear()
    {
        _chars.Clear();
    }

    public override string ToString()
    {
        return new string(_chars.ToArray());
    }
}
