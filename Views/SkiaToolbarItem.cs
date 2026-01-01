using System.Windows.Input;
using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaToolbarItem
{
	public string Text { get; set; } = "";

	public SKBitmap? Icon { get; set; }

	public SkiaToolbarItemOrder Order { get; set; }

	public ICommand? Command { get; set; }

	public SKRect HitBounds { get; set; }
}
