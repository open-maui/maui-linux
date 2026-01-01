using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform;

public class SkiaVisualStateSetter
{
	private object? _originalValue;

	private bool _hasOriginalValue;

	public BindableProperty? Property { get; set; }

	public object? Value { get; set; }

	public void Apply(SkiaView view)
	{
		if (Property != null)
		{
			if (!_hasOriginalValue)
			{
				_originalValue = ((BindableObject)view).GetValue(Property);
				_hasOriginalValue = true;
			}
			((BindableObject)view).SetValue(Property, Value);
		}
	}

	public void Unapply(SkiaView view)
	{
		if (Property != null && _hasOriginalValue)
		{
			((BindableObject)view).SetValue(Property, _originalValue);
		}
	}
}
