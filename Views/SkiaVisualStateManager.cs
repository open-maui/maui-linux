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
        if (bindable is SkiaView view)
        {
            // Detach old triggers
            if (oldValue is SkiaVisualStateGroupList oldGroups)
            {
                foreach (var group in oldGroups)
                {
                    foreach (var state in group.States)
                    {
                        state.DetachTriggers();
                    }
                }
            }

            // Attach new triggers
            if (newValue is SkiaVisualStateGroupList groups)
            {
                foreach (var group in groups)
                {
                    foreach (var state in group.States)
                    {
                        state.AttachTriggers(view);
                    }
                }

                // Initialize to default state
                GoToState(view, CommonStates.Normal);
            }
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
