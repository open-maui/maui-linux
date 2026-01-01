using System.Collections.Generic;

namespace Microsoft.Maui.Platform;

public class SkiaVisualState
{
	public string Name { get; set; } = "";

	public List<SkiaVisualStateSetter> Setters { get; } = new List<SkiaVisualStateSetter>();
}
