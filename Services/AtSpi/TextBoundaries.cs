// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform.Linux.Services.AtSpi;

/// <summary>
/// The text ranges AT-SPI's Text interface asks for: a character, word, sentence or line around
/// an offset (GetStringAtOffset's granularities 0 to 4), and the older boundary types of
/// GetTextAt/Before/AfterOffset (0 character, 1 word start, 2 word end, 3 sentence start,
/// 4 sentence end, 5 line start, 6 line end). Lines are the text's own line breaks.
/// </summary>
internal static class TextBoundaries
{
    private enum Unit { Character, Word, Sentence, Line }

    private static Unit UnitOfGranularity(uint granularity) => granularity switch
    {
        0 => Unit.Character,
        1 => Unit.Word,
        2 => Unit.Sentence,
        _ => Unit.Line, // 3 line, 4 paragraph
    };

    private static Unit UnitOfBoundary(uint type) => type switch
    {
        0 => Unit.Character,
        1 or 2 => Unit.Word,
        3 or 4 => Unit.Sentence,
        _ => Unit.Line,
    };

    public static (string, int, int) AtGranularity(string text, int offset, uint granularity) => Range(text, offset, UnitOfGranularity(granularity));

    public static (string, int, int) At(string text, int offset, uint type) => Range(text, offset, UnitOfBoundary(type));

    public static (string, int, int) Before(string text, int offset, uint type)
    {
        var (_, start, _) = Range(text, offset, UnitOfBoundary(type));
        return start <= 0 ? (string.Empty, 0, 0) : Range(text, start - 1, UnitOfBoundary(type));
    }

    public static (string, int, int) After(string text, int offset, uint type)
    {
        var (_, _, end) = Range(text, offset, UnitOfBoundary(type));
        return end >= text.Length ? (string.Empty, text.Length, text.Length) : Range(text, end, UnitOfBoundary(type));
    }

    private static (string, int, int) Range(string text, int offset, Unit unit)
    {
        if (text.Length == 0)
            return (string.Empty, 0, 0);
        offset = Math.Clamp(offset, 0, text.Length - 1);
        int start, end;
        switch (unit)
        {
            case Unit.Character:
                start = offset;
                end = offset + (char.IsHighSurrogate(text[offset]) && offset + 1 < text.Length ? 2 : 1);
                break;
            case Unit.Word:
                // A word and the spaces after it, as ATK's word-start boundary.
                start = offset;
                while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
                    start--;
                end = offset;
                while (end < text.Length && !char.IsWhiteSpace(text[end]))
                    end++;
                while (end < text.Length && char.IsWhiteSpace(text[end]) && text[end] != '\n')
                    end++;
                break;
            case Unit.Sentence:
                start = offset;
                while (start > 0 && !IsSentenceEnd(text, start - 1))
                    start--;
                end = offset;
                while (end < text.Length && !IsSentenceEnd(text, end))
                    end++;
                if (end < text.Length)
                    end++;
                while (end < text.Length && text[end] == ' ')
                    end++;
                break;
            default:
                // The line holding the offset, with its line break.
                start = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
                end = text.IndexOf('\n', offset);
                end = end < 0 ? text.Length : end + 1;
                break;
        }
        return (text[start..end], start, end);
    }

    private static bool IsSentenceEnd(string text, int index) => text[index] is '.' or '!' or '?' or '\n';
}
