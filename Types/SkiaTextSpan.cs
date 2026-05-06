using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaTextSpan
{
    public string Text { get; set; } = "";

    public SKColor? TextColor { get; set; }

    public SKColor? BackgroundColor { get; set; }

    public string? FontFamily { get; set; }

    public float FontSize { get; set; }

    public bool IsBold { get; set; }

    public bool IsItalic { get; set; }

    public bool IsUnderline { get; set; }

    public bool IsStrikethrough { get; set; }

    public float CharacterSpacing { get; set; }

    public float LineHeight { get; set; } = 1f;
}
