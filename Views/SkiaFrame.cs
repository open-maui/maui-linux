using SkiaSharp;

namespace Microsoft.Maui.Platform;

public class SkiaFrame : SkiaBorder
{
	public SkiaFrame()
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		base.HasShadow = true;
		base.CornerRadius = 4f;
		SetPadding(10f);
		base.BackgroundColor = SKColors.White;
		base.Stroke = SKColors.Transparent;
		base.StrokeThickness = 0f;
	}
}
