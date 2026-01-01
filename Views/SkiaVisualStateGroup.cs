using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

public class SkiaVisualStateGroup
{
	public string Name { get; set; } = "";

	public List<SkiaVisualState> States { get; } = new List<SkiaVisualState>();

	public SkiaVisualState? CurrentState { get; set; }
}
