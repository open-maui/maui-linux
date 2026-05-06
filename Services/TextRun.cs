// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Services;

public class TextRun
{
    public string Text { get; }

    public SKTypeface Typeface { get; }

    public int StartIndex { get; }

    public TextRun(string text, SKTypeface typeface, int startIndex)
    {
        Text = text;
        Typeface = typeface;
        StartIndex = startIndex;
    }
}
