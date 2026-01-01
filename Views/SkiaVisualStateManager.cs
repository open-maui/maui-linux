using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Platform;

public static class SkiaVisualStateManager
{
	public static class CommonStates
	{
		public const string Normal = "Normal";

		public const string Disabled = "Disabled";

		public const string Focused = "Focused";

		public const string PointerOver = "PointerOver";

		public const string Pressed = "Pressed";

		public const string Selected = "Selected";

		public const string Checked = "Checked";

		public const string Unchecked = "Unchecked";

		public const string On = "On";

		public const string Off = "Off";
	}

	public static readonly BindableProperty VisualStateGroupsProperty = BindableProperty.CreateAttached("VisualStateGroups", typeof(SkiaVisualStateGroupList), typeof(SkiaVisualStateManager), (object)null, (BindingMode)2, (ValidateValueDelegate)null, new BindingPropertyChangedDelegate(OnVisualStateGroupsChanged), (BindingPropertyChangingDelegate)null, (CoerceValueDelegate)null, (CreateDefaultValueDelegate)null);

	public static SkiaVisualStateGroupList? GetVisualStateGroups(SkiaView view)
	{
		return (SkiaVisualStateGroupList)((BindableObject)view).GetValue(VisualStateGroupsProperty);
	}

	public static void SetVisualStateGroups(SkiaView view, SkiaVisualStateGroupList? value)
	{
		((BindableObject)view).SetValue(VisualStateGroupsProperty, (object)value);
	}

	private static void OnVisualStateGroupsChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		if (bindable is SkiaView view && newValue is SkiaVisualStateGroupList)
		{
			GoToState(view, "Normal");
		}
	}

	public static bool GoToState(SkiaView view, string stateName)
	{
		SkiaVisualStateGroupList visualStateGroups = GetVisualStateGroups(view);
		if (visualStateGroups == null || visualStateGroups.Count == 0)
		{
			return false;
		}
		bool result = false;
		foreach (SkiaVisualStateGroup item in visualStateGroups)
		{
			SkiaVisualState skiaVisualState = null;
			foreach (SkiaVisualState state in item.States)
			{
				if (state.Name == stateName)
				{
					skiaVisualState = state;
					break;
				}
			}
			if (skiaVisualState != null)
			{
				if (item.CurrentState != null && item.CurrentState != skiaVisualState)
				{
					UnapplyState(view, item.CurrentState);
				}
				ApplyState(view, skiaVisualState);
				item.CurrentState = skiaVisualState;
				result = true;
			}
		}
		return result;
	}

	private static void ApplyState(SkiaView view, SkiaVisualState state)
	{
		foreach (SkiaVisualStateSetter setter in state.Setters)
		{
			setter.Apply(view);
		}
	}

	private static void UnapplyState(SkiaView view, SkiaVisualState state)
	{
		foreach (SkiaVisualStateSetter setter in state.Setters)
		{
			setter.Unapply(view);
		}
	}
}
