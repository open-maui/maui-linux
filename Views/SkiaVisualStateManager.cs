// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Maui.Platform;

/// <summary>
/// Visual State Manager for Skia-rendered controls.
/// Provides state-based styling through XAML VisualStateGroups.
/// </summary>
public static class SkiaVisualStateManager
{
    /// <summary>
    /// Common visual state names.
    /// </summary>
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

    /// <summary>
    /// Attached property for VisualStateGroups.
    /// </summary>
    public static readonly BindableProperty VisualStateGroupsProperty =
        BindableProperty.CreateAttached(
            "VisualStateGroups",
            typeof(SkiaVisualStateGroupList),
            typeof(SkiaVisualStateManager),
            null,
            propertyChanged: OnVisualStateGroupsChanged);

    /// <summary>
    /// Gets the visual state groups for the specified view.
    /// </summary>
    public static SkiaVisualStateGroupList? GetVisualStateGroups(SkiaView view)
    {
        return (SkiaVisualStateGroupList?)view.GetValue(VisualStateGroupsProperty);
    }

    /// <summary>
    /// Sets the visual state groups for the specified view.
    /// </summary>
    public static void SetVisualStateGroups(SkiaView view, SkiaVisualStateGroupList? value)
    {
        view.SetValue(VisualStateGroupsProperty, value);
    }

    private static void OnVisualStateGroupsChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is SkiaView view && newValue is SkiaVisualStateGroupList groups)
        {
            // Initialize to default state
            GoToState(view, CommonStates.Normal);
        }
    }

    /// <summary>
    /// Transitions the view to the specified visual state.
    /// </summary>
    /// <param name="view">The view to transition.</param>
    /// <param name="stateName">The name of the state to transition to.</param>
    /// <returns>True if the state was found and applied, false otherwise.</returns>
    public static bool GoToState(SkiaView view, string stateName)
    {
        var groups = GetVisualStateGroups(view);
        if (groups == null || groups.Count == 0)
            return false;

        bool stateFound = false;

        foreach (var group in groups)
        {
            // Find the state in this group
            SkiaVisualState? targetState = null;
            foreach (var state in group.States)
            {
                if (state.Name == stateName)
                {
                    targetState = state;
                    break;
                }
            }

            if (targetState != null)
            {
                // Unapply current state if different
                if (group.CurrentState != null && group.CurrentState != targetState)
                {
                    UnapplyState(view, group.CurrentState);
                }

                // Apply new state
                ApplyState(view, targetState);
                group.CurrentState = targetState;
                stateFound = true;
            }
        }

        return stateFound;
    }

    private static void ApplyState(SkiaView view, SkiaVisualState state)
    {
        foreach (var setter in state.Setters)
        {
            setter.Apply(view);
        }
    }

    private static void UnapplyState(SkiaView view, SkiaVisualState state)
    {
        foreach (var setter in state.Setters)
        {
            setter.Unapply(view);
        }
    }
}

/// <summary>
/// A list of visual state groups.
/// </summary>
public class SkiaVisualStateGroupList : List<SkiaVisualStateGroup>
{
}

/// <summary>
/// A group of mutually exclusive visual states.
/// </summary>
public class SkiaVisualStateGroup
{
    /// <summary>
    /// Gets or sets the name of this group.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets the collection of states in this group.
    /// </summary>
    public List<SkiaVisualState> States { get; } = new();

    /// <summary>
    /// Gets or sets the currently active state.
    /// </summary>
    public SkiaVisualState? CurrentState { get; set; }
}

/// <summary>
/// Represents a single visual state with its setters.
/// </summary>
public class SkiaVisualState
{
    /// <summary>
    /// Gets or sets the name of this state.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets the collection of setters for this state.
    /// </summary>
    public List<SkiaVisualStateSetter> Setters { get; } = new();
}

/// <summary>
/// Sets a property value when a visual state is active.
/// </summary>
public class SkiaVisualStateSetter
{
    /// <summary>
    /// Gets or sets the property to set.
    /// </summary>
    public BindableProperty? Property { get; set; }

    /// <summary>
    /// Gets or sets the value to set.
    /// </summary>
    public object? Value { get; set; }

    // Store original value for unapply
    private object? _originalValue;
    private bool _hasOriginalValue;

    /// <summary>
    /// Applies this setter to the target view.
    /// </summary>
    public void Apply(SkiaView view)
    {
        if (Property == null) return;

        // Store original value if not already stored
        if (!_hasOriginalValue)
        {
            _originalValue = view.GetValue(Property);
            _hasOriginalValue = true;
        }

        view.SetValue(Property, Value);
    }

    /// <summary>
    /// Unapplies this setter, restoring the original value.
    /// </summary>
    public void Unapply(SkiaView view)
    {
        if (Property == null || !_hasOriginalValue) return;

        view.SetValue(Property, _originalValue);
    }
}
