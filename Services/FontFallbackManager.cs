using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

public class FontFallbackManager
{
	private static FontFallbackManager? _instance;

	private static readonly object _lock = new object();

	private readonly string[] _fallbackFonts = new string[27]
	{
		"Noto Sans", "DejaVu Sans", "Liberation Sans", "FreeSans", "Noto Color Emoji", "Noto Emoji", "Symbola", "Segoe UI Emoji", "Noto Sans CJK SC", "Noto Sans CJK TC",
		"Noto Sans CJK JP", "Noto Sans CJK KR", "WenQuanYi Micro Hei", "WenQuanYi Zen Hei", "Droid Sans Fallback", "Noto Sans Arabic", "Noto Naskh Arabic", "DejaVu Sans", "Noto Sans Devanagari", "Noto Sans Tamil",
		"Noto Sans Bengali", "Noto Sans Telugu", "Noto Sans Thai", "Loma", "Noto Sans Hebrew", "Sans", "sans-serif"
	};

	private readonly Dictionary<string, SKTypeface?> _typefaceCache = new Dictionary<string, SKTypeface>();

	private readonly Dictionary<(int codepoint, string preferredFont), SKTypeface?> _glyphCache = new Dictionary<(int, string), SKTypeface>();

	public static FontFallbackManager Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_lock)
				{
					if (_instance == null)
					{
						_instance = new FontFallbackManager();
					}
				}
			}
			return _instance;
		}
	}

	private FontFallbackManager()
	{
		foreach (string item in _fallbackFonts.Take(10))
		{
			GetCachedTypeface(item);
		}
	}

	public SKTypeface GetTypefaceForCodepoint(int codepoint, SKTypeface preferred)
	{
		(int, string) key = (codepoint, preferred.FamilyName);
		if (_glyphCache.TryGetValue(key, out SKTypeface value))
		{
			return value ?? preferred;
		}
		if (TypefaceContainsGlyph(preferred, codepoint))
		{
			_glyphCache[key] = preferred;
			return preferred;
		}
		string[] fallbackFonts = _fallbackFonts;
		foreach (string fontFamily in fallbackFonts)
		{
			SKTypeface cachedTypeface = GetCachedTypeface(fontFamily);
			if (cachedTypeface != null && TypefaceContainsGlyph(cachedTypeface, codepoint))
			{
				_glyphCache[key] = cachedTypeface;
				return cachedTypeface;
			}
		}
		_glyphCache[key] = null;
		return preferred;
	}

	public SKTypeface GetTypefaceForText(string text, SKTypeface preferred)
	{
		if (string.IsNullOrEmpty(text))
		{
			return preferred;
		}
		foreach (Rune item in text.EnumerateRunes())
		{
			if (item.Value > 127)
			{
				return GetTypefaceForCodepoint(item.Value, preferred);
			}
		}
		return preferred;
	}

	public List<TextRun> ShapeTextWithFallback(string text, SKTypeface preferred)
	{
		List<TextRun> list = new List<TextRun>();
		if (string.IsNullOrEmpty(text))
		{
			return list;
		}
		_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder _003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2 = new _003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder();
		SKTypeface val = null;
		int startIndex = 0;
		int num = 0;
		foreach (Rune item in text.EnumerateRunes())
		{
			SKTypeface typefaceForCodepoint = GetTypefaceForCodepoint(item.Value, preferred);
			if (val == null)
			{
				val = typefaceForCodepoint;
			}
			else if (typefaceForCodepoint.FamilyName != val.FamilyName)
			{
				if (_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2.Length > 0)
				{
					list.Add(new TextRun(_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2.ToString(), val, startIndex));
				}
				_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2.Clear();
				val = typefaceForCodepoint;
				startIndex = num;
			}
			_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2.Append(item.ToString());
			num += item.Utf16SequenceLength;
		}
		if (_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2.Length > 0 && val != null)
		{
			list.Add(new TextRun(_003CFontFallbackManager_003EFAC9D2911A2850E174CCA7662C668F37C2FBBA325CAF5C11AFE3FA59C16CC64ED__StringBuilder2.ToString(), val, startIndex));
		}
		return list;
	}

	public bool IsFontAvailable(string fontFamily)
	{
		SKTypeface cachedTypeface = GetCachedTypeface(fontFamily);
		if (cachedTypeface != null)
		{
			return cachedTypeface.FamilyName.Equals(fontFamily, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public IEnumerable<string> GetAvailableFallbackFonts()
	{
		string[] fallbackFonts = _fallbackFonts;
		foreach (string text in fallbackFonts)
		{
			if (IsFontAvailable(text))
			{
				yield return text;
			}
		}
	}

	private SKTypeface? GetCachedTypeface(string fontFamily)
	{
		if (_typefaceCache.TryGetValue(fontFamily, out SKTypeface value))
		{
			return value;
		}
		SKTypeface val = SKTypeface.FromFamilyName(fontFamily);
		if (val != null && !val.FamilyName.Equals(fontFamily, StringComparison.OrdinalIgnoreCase))
		{
			val = null;
		}
		_typefaceCache[fontFamily] = val;
		return val;
	}

	private bool TypefaceContainsGlyph(SKTypeface typeface, int codepoint)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		SKFont val = new SKFont(typeface, 12f, 1f, 0f);
		try
		{
			ushort[] array = new ushort[1];
			string text = char.ConvertFromUtf32(codepoint);
			val.GetGlyphs(text, (Span<ushort>)array);
			return array[0] != 0;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}
}
